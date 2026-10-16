namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>调用方插件拥有的通知渠道配置。</summary>
public sealed record NotificationPluginChannelConfiguration(string ChannelKey, string Name, bool IsEnabled, string ConfigurationJson);
