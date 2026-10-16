namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>标记会通过宿主运行时持续报告健康状态的插件。</summary>
/// <remarks>无状态通知渠道不应实现此接口；实现方上报频率必须高于宿主配置的健康状态过期频率。</remarks>
public interface INotificationPluginHealthReporter;
