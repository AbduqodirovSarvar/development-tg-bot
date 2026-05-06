using DevelopmentTgBot.Authentication;
using DevelopmentTgBot.Configuration;
using DevelopmentTgBot.Endpoints;
using DevelopmentTgBot.Notifications;
using DevelopmentTgBot.Telegram;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Options ─────────────────────────────────────────────────────────
// Bound up front (with validation) so a malformed config fails the
// process at startup rather than at the first request.
builder.Services
    .AddOptions<TelegramOptions>()
    .Bind(builder.Configuration.GetSection(TelegramOptions.SectionName))
    .ValidateOnStart();

builder.Services
    .AddOptions<GatewayOptions>()
    .Bind(builder.Configuration.GetSection(GatewayOptions.SectionName))
    .Validate(o => o.QueueCapacity > 0, "Gateway:QueueCapacity must be > 0.")
    .ValidateOnStart();

// ── Authentication ─────────────────────────────────────────────────
// API-key scheme is the only auth method — the gateway has no human
// users, only service-to-service callers. We register it as the
// default scheme so a bare [Authorize] picks it up without ceremony.
builder.Services
    .AddAuthentication(ApiKeyAuthenticationOptions.Scheme)
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationOptions.Scheme, _ => { });
builder.Services.AddAuthorization();

// ── Application services ───────────────────────────────────────────
builder.Services.AddSingleton<NotificationQueue>();
builder.Services.AddSingleton<INotificationService, NotificationService>();
builder.Services.AddHostedService<NotificationDispatcher>();

// Typed HttpClient for the Bot API. The resilience handler from
// Microsoft.Extensions.Http.Resilience adds transient-error retries
// (network blips, 5xx) before the call returns to the dispatcher's
// own retry loop, so two layers of protection cover both transport-
// level and Bot-API-level transience without overlap.
builder.Services.AddHttpClient<ITelegramSender, TelegramSender>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<TelegramOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = options.RequestTimeout;
})
.AddStandardResilienceHandler();

// ── OpenAPI / Swagger ─────────────────────────────────────────────
// `AddOpenApi` (built-in .NET 9) generates the JSON spec at
// /openapi/v1.json. `AddSwaggerGen` + `UseSwaggerUI` add the
// interactive HTML explorer — only mounted in Development to match
// the FamilyTree backend pattern. The Bearer security scheme lets
// you paste an API key into the "Authorize" dialog and have every
// subsequent request carry the right header.
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "Development Telegram Bot — Notification Gateway",
        Description = "API key bilan himoyalangan notification gateway. Klientlar nomli destination ga xabar yuboradi; gateway uni Telegram chat/topicga yo'naltiradi."
    });

    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        Description = "API key. \"Authorize\" tugmasiga bosib, faqat key qiymatini kiriting (\"Bearer \" prefiksi avtomatik qo'shiladi)."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Id = "ApiKey",
                    Type = ReferenceType.SecurityScheme
                }
            },
            new List<string>()
        }
    });
});

builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Development Tg Bot API V1");
        // Mount at the root so http://localhost:5201/ opens Swagger UI directly —
        // avoids the extra "/swagger" hop and matches the FamilyTree backend.
        c.RoutePrefix = string.Empty;
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapNotificationEndpoints();

app.Run();
