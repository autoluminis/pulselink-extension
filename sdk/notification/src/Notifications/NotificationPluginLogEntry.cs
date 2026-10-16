namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>插件产生的结构化运行日志。</summary>
public sealed record NotificationPluginLogEntry(string Level, string Event, string Message, string? DeliveryId = null, string? Detail = null);
