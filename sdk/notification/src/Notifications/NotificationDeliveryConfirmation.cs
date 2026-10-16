namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>提供方确认通知投递的状态。</summary>
public enum NotificationDeliveryConfirmation
{
    /// <summary>提供方已接受投递，但尚未确认最终送达。</summary>
    Accepted,
    /// <summary>提供方或设备已明确确认送达或执行。</summary>
    Delivered,
    /// <summary>提供方或设备明确拒绝投递，仅适用于后续回执。</summary>
    Rejected
}
