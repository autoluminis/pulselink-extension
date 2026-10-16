namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>渠道可使用的、已筛选后的收件人端点。</summary>
/// <param name="EndpointType">端点类型，例如 <c>Email</c>。</param>
/// <param name="Address">端点目标地址。</param>
/// <param name="DisplayName">可选的收件人显示名称。</param>
public sealed record NotificationRecipient(string EndpointType, string Address, string? DisplayName = null);
