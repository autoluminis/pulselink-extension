namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>通知插件可使用的受限宿主功能。</summary>
public interface INotificationPluginRuntime
{
    /// <summary>返回仅属于当前插件的渠道配置。</summary>
    ValueTask<IReadOnlyList<NotificationPluginChannelConfiguration>> ListChannelConfigurationsAsync(CancellationToken cancellationToken = default);

    /// <summary>原子替换当前插件拥有的所有渠道实例配置。</summary>
    ValueTask UpdateChannelConfigurationsAsync(string configurationJson, CancellationToken cancellationToken = default);

    /// <summary>读取独立于通知渠道实例存在的插件服务配置。</summary>
    ValueTask<string?> GetPluginConfigurationAsync(CancellationToken cancellationToken = default);

    /// <summary>持久化无需依附通知渠道实例的插件服务配置。</summary>
    ValueTask UpdatePluginConfigurationAsync(string configurationJson, CancellationToken cancellationToken = default);

    /// <summary>记录已提交投递的后续提供方或设备回执。</summary>
    ValueTask ReportReceiptAsync(NotificationDeliveryReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>持久化可查询且数量受限的插件运行日志。</summary>
    ValueTask WriteLogAsync(NotificationPluginLogEntry entry, CancellationToken cancellationToken = default);

    /// <summary>更新插件当前的运行健康状态。</summary>
    ValueTask ReportHealthAsync(NotificationPluginHealth health, CancellationToken cancellationToken = default);

    /// <summary>通过宿主托管的客户端执行受限的出站 HTTP 请求。</summary>
    ValueTask<NotificationPluginHttpResponse> SendHttpAsync(NotificationPluginHttpRequest request, CancellationToken cancellationToken = default);
}
