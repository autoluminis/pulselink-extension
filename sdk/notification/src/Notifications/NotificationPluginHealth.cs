namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>插件长期运行资源的当前健康状态。</summary>
public sealed record NotificationPluginHealth(string Status, string? Summary = null, IReadOnlyDictionary<string, string>? Metrics = null);
