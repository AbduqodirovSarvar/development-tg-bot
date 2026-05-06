using DevelopmentTgBot.Authentication;
using DevelopmentTgBot.Configuration;
using DevelopmentTgBot.Endpoints;
using DevelopmentTgBot.Notifications;
using DevelopmentTgBot.Telegram;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

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

// ── Misc ──────────────────────────────────────────────────────────
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapNotificationEndpoints();

app.Run();
