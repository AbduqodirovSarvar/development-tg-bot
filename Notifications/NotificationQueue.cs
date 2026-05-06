using System.Threading.Channels;
using DevelopmentTgBot.Configuration;
using Microsoft.Extensions.Options;

namespace DevelopmentTgBot.Notifications;

/// <summary>
/// Bounded in-memory queue between the endpoint and the dispatcher. Uses
/// <see cref="Channel{T}"/> with <see cref="BoundedChannelFullMode.Wait"/>
/// so we apply back-pressure rather than dropping messages — the
/// endpoint surfaces a full queue as HTTP 503 via
/// <see cref="TryEnqueue(NotificationJob)"/>.
///
/// <para><b>Persistence note.</b> This queue is in-memory; a process
/// crash drops in-flight jobs. That's an acceptable trade-off for the
/// notification-gateway use case (operational alerts, not financial
/// transactions). If you need durability, swap the implementation for
/// Redis Streams or a database-backed outbox.</para>
/// </summary>
public sealed class NotificationQueue
{
    private readonly Channel<NotificationJob> _channel;

    public NotificationQueue(IOptions<GatewayOptions> options)
    {
        var capacity = Math.Max(1, options.Value.QueueCapacity);
        _channel = Channel.CreateBounded<NotificationJob>(new BoundedChannelOptions(capacity)
        {
            // Wait when the queue is full so the endpoint can decide what
            // to do (return 503). Drop-newest would silently swallow alerts.
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// <summary>
    /// Non-blocking enqueue. Returns false when the queue is at capacity,
    /// which the endpoint translates into HTTP 503 so the caller can
    /// retry or alert.
    /// </summary>
    public bool TryEnqueue(NotificationJob job) => _channel.Writer.TryWrite(job);

    public ChannelReader<NotificationJob> Reader => _channel.Reader;
}
