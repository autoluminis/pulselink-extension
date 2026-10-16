using System.Net.Http.Json;
using System.Text.Json;
using PulseLink.Plugin.Abstractions.Notifications;

namespace PulseLink.NotificationPlugins.GenericWebhook;

/// <summary>SDK 示例：将标准化通知负载发送到配置地址的广播渠道。</summary>
public sealed class GenericWebhookChannel : INotificationChannel
{
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(10) };
    private static readonly JsonSerializerOptions ConfigurationJsonOptions = new() { PropertyNameCaseInsensitive = true };

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
        try
        {
            var configuration = JsonSerializer.Deserialize<WebhookConfiguration>(configurationJson, ConfigurationJsonOptions);
            var isValid = configuration is not null && Uri.TryCreate(configuration.Url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
            return ValueTask.FromResult(isValid
                ? NotificationChannelValidationResult.Valid
                : new NotificationChannelValidationResult(false, ["配置必须包含 http 或 https URL 字段。"]));
        }
        catch (JsonException)
        {
            return ValueTask.FromResult(new NotificationChannelValidationResult(false, ["配置不是有效 JSON。"]));
        }
    }

    public async ValueTask<NotificationDeliveryResult> SendAsync(NotificationDispatchMessage message, CancellationToken cancellationToken = default)
    {
        var configuration = JsonSerializer.Deserialize<WebhookConfiguration>(message.ConfigurationJson, ConfigurationJsonOptions)
            ?? throw new InvalidOperationException("Webhook 配置无效。");
        using var response = await Client.PostAsJsonAsync(configuration.Url, new
        {
            message.DeliveryId,
            message.EventType,
            message.Severity,
            message.Subject,
            message.Body,
            message.Attributes
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        return NotificationDeliveryResult.Accepted(providerStatus: $"HTTP {(int)response.StatusCode}", responseSummary: response.ReasonPhrase);
    }

    private sealed class WebhookConfiguration
    {
        public string Url { get; init; } = string.Empty;
    }
}
