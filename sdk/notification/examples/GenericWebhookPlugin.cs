using PulseLink.Plugin.Abstractions.Notifications;

namespace PulseLink.NotificationPlugins.GenericWebhook;

/// <summary>通知 SDK Webhook 示例。</summary>
public sealed class GenericWebhookPlugin : INotificationChannelPlugin
{
    private readonly HttpClient client = new(new HttpClientHandler { AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    public string Id => "example.generic-webhook";
    public string DisplayName => "Generic Webhook";
    public Version Version => new(1, 0, 0);
    public IEnumerable<INotificationChannel> CreateChannels() => [new GenericWebhookChannel(client)];
    public ValueTask StopAsync(CancellationToken cancellationToken = default)
    {
        client.Dispose();
        return ValueTask.CompletedTask;
    }
}
