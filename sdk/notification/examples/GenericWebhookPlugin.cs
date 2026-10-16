using PulseLink.Plugin.Abstractions.Notifications;

namespace PulseLink.NotificationPlugins.GenericWebhook;

/// <summary>通知 SDK Webhook 示例。</summary>
public sealed class GenericWebhookPlugin : INotificationChannelPlugin
{
    public string Id => "example.generic-webhook";
    public string DisplayName => "Generic Webhook";
    public Version Version => new(1, 0, 0);
    public IEnumerable<INotificationChannel> CreateChannels() => [new GenericWebhookChannel()];
    public ValueTask StopAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
}
