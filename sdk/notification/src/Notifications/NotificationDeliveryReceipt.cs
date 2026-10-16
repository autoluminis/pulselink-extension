namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>异步收到的通知投递回执。</summary>
public sealed record NotificationDeliveryReceipt(string DeliveryId, NotificationDeliveryConfirmation Confirmation, string? ProviderMessageId = null, string? ProviderStatus = null, string? ResponseSummary = null);
