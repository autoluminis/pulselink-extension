namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>通知渠道配置变化时可选的回调接口。</summary>
public interface INotificationPluginConfigurationAware
{
    /// <summary>处理当前插件渠道配置的更新。</summary>
    ValueTask OnChannelConfigurationsChangedAsync(INotificationPluginRuntime runtime, CancellationToken cancellationToken = default);
}
