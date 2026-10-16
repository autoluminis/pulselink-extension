namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>渠道配置校验结果。</summary>
/// <param name="IsValid">配置是否可用。</param>
/// <param name="Errors">校验失败时的错误说明集合。</param>
public sealed record NotificationChannelValidationResult(bool IsValid, IReadOnlyList<string> Errors)
{
    /// <summary>表示校验通过的共享结果。</summary>
    public static NotificationChannelValidationResult Valid { get; } = new(true, []);
}
