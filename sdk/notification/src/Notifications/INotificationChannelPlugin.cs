namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>通知渠道插件入口；实现必须具有无参构造函数。</summary>
public interface INotificationChannelPlugin : IPulseLinkPlugin
{
    /// <summary>创建该插件公开的一个或多个通知渠道。</summary>
    IEnumerable<INotificationChannel> CreateChannels();

    /// <summary>停止插件并释放其创建的资源。</summary>
    ValueTask StopAsync(CancellationToken cancellationToken = default);
}
