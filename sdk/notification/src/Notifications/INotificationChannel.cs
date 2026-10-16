namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>由第三方插件提供的通知发送能力。</summary>
public interface INotificationChannel
{
    /// <summary>插件内唯一的渠道标识。</summary>
    string Key { get; }
    /// <summary>用于管理界面展示的渠道名称。</summary>
    string DisplayName { get; }
    /// <summary>渠道是否不依赖具体联系人端点而直接广播。</summary>
    bool IsBroadcast { get; }
    /// <summary>渠道能够消费的联系方式类型定义，包含唯一代码、展示名与用户填写提示。</summary>
    IReadOnlyList<NotificationContactTypeDefinition> ContactTypes { get; }
    /// <summary>新建渠道实例时预填的 JSON 配置模板。</summary>
    string DefaultConfigurationJson => "{}";
    /// <summary>同一插件渠道允许创建的配置实例数；空值表示不限制。</summary>
    int? MaximumInstances => null;
    /// <summary>验证该渠道实例的 JSON 配置。</summary>
    ValueTask<NotificationChannelValidationResult> ValidateConfigurationAsync(string configurationJson, CancellationToken cancellationToken = default);
    /// <summary>发送已由宿主完成策略、模板和收件人解析的通知。</summary>
    /// <summary>
    /// 发送通知并返回服务商确认结果。任务完成并不等同于送达：实现必须说明服务商仅接受请求，还是已明确确认送达。
    /// </summary>
    ValueTask<NotificationDeliveryResult> SendAsync(NotificationDispatchMessage message, CancellationToken cancellationToken = default);
}
