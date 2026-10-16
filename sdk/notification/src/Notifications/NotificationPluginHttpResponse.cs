namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>宿主托管 HTTP 客户端返回的安全响应。</summary>
public sealed record NotificationPluginHttpResponse(int StatusCode, string? ReasonPhrase, string Body, IReadOnlyDictionary<string, string> Headers);
