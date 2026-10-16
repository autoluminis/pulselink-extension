namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>拥有后台资源的通知插件可选生命周期接口。</summary>
public interface INotificationPluginRuntimeAware
{
    /// <summary>在渠道注册后启动后台资源。</summary>
    ValueTask StartAsync(INotificationPluginRuntime runtime, CancellationToken cancellationToken = default);
}
