using System.Net.Http.Json;
using System.Text.Json;
using PulseLink.Plugin.Abstractions.Notifications;

namespace PulseLink.NotificationPlugins.GenericWebhook;

/// <summary>SDK 示例：将标准化通知负载发送到配置地址的广播渠道。</summary>
public sealed class GenericWebhookChannel : INotificationChannel
{
    private readonly HttpClient client;
    private static readonly JsonSerializerOptions ConfigurationJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public GenericWebhookChannel(HttpClient client) => this.client = client ?? throw new ArgumentNullException(nameof(client));

    public string Key => "webhook";
    public string DisplayName => "Generic Webhook";
    public bool IsBroadcast => true;
    public string DefaultConfigurationJson => """
        {
          "url": "https://example.invalid/pulselink/webhook"
        }
        """;
    public IReadOnlyList<NotificationContactTypeDefinition> ContactTypes => [];

    public ValueTask<NotificationChannelValidationResult> ValidateConfigurationAsync(string configurationJson, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            ParseConfiguration(configurationJson);
            return ValueTask.FromResult(NotificationChannelValidationResult.Valid);
        }
        catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException)
        {
            return ValueTask.FromResult(new NotificationChannelValidationResult(false, ["配置必须是 JSON 对象，包含无账号密码和片段的绝对 HTTP(S) url。"]));
        }
    }

    public async ValueTask<NotificationDeliveryResult> SendAsync(NotificationDispatchMessage message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var configuration = ParseConfiguration(message.ConfigurationJson);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.Url);
        request.Headers.Add("Idempotency-Key", message.DeliveryId);
        request.Content = JsonContent.Create(new
        {
            message.DeliveryId,
            message.EventType,
            message.Severity,
            message.Subject,
            message.Body,
            message.Attributes
        });
        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        return NotificationDeliveryResult.Accepted(providerStatus: $"HTTP {(int)response.StatusCode}");
    }

    private static WebhookConfiguration ParseConfiguration(string json)
    {
        var configuration = JsonSerializer.Deserialize<WebhookConfiguration>(json, ConfigurationJsonOptions);
        if (configuration is null || !Uri.TryCreate(configuration.Url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("Webhook 配置无效。");
        return configuration;
    }

    private sealed class WebhookConfiguration
    {
        public string Url { get; init; } = string.Empty;
    }
}
