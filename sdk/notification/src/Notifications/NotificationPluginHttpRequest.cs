namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>可信通知插件可发起的受限出站请求。</summary>
public sealed record NotificationPluginHttpRequest(string Method, string Url, string? Body = null, string? ContentType = "application/json", IReadOnlyDictionary<string, string>? Headers = null, int TimeoutSeconds = 30);
