namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>通知投递完成后由提供方返回的确认结果。</summary>
public sealed record NotificationDeliveryResult(NotificationDeliveryConfirmation Confirmation, string? ProviderMessageId = null, string? ProviderStatus = null, string? ResponseSummary = null)
{
    /// <summary>创建已受理但尚未确认送达的结果。</summary>
    public static NotificationDeliveryResult Accepted(string? providerMessageId = null, string? providerStatus = null, string? responseSummary = null)
        => new(NotificationDeliveryConfirmation.Accepted, providerMessageId, providerStatus, responseSummary);

    /// <summary>创建已确认送达的结果。</summary>
    public static NotificationDeliveryResult Delivered(string? providerMessageId = null, string? providerStatus = null, string? responseSummary = null)
        => new(NotificationDeliveryConfirmation.Delivered, providerMessageId, providerStatus, responseSummary);
}
