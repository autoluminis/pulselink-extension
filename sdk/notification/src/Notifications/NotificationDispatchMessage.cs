namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>宿主传递给通知渠道的不可变投递消息。</summary>
/// <param name="DeliveryId">投递记录唯一标识，用于渠道端幂等处理。</param>
/// <param name="EventType">报警生命周期事件类型。</param>
/// <param name="Severity">报警严重级别。</param>
/// <param name="Subject">渲染完成的通知主题。</param>
/// <param name="Body">渲染完成的通知正文。</param>
/// <param name="Recipients">已按渠道能力筛选的收件人端点。</param>
/// <param name="Attributes">报警上下文扩展属性。</param>
/// <param name="ConfigurationJson">当前渠道实例 JSON 配置。</param>
public sealed record NotificationDispatchMessage(string DeliveryId, string EventType, string Severity, string Subject, string Body, IReadOnlyList<NotificationRecipient> Recipients, IReadOnlyDictionary<string, string> Attributes, string ConfigurationJson);
