namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>由通知插件声明的用户联系方式类型及其填写引导信息。</summary>
/// <param name="Code">全局稳定且唯一的类型代码，作为用户联系方式的唯一键。</param>
/// <param name="Name">供管理、日志与程序识别的名称。</param>
/// <param name="DisplayName">用户中心展示给用户的中文或本地化名称。</param>
/// <param name="Placeholder">用户填写地址时显示的示例或格式提示。</param>
/// <param name="Description">解释此联系方式用途的简短说明。</param>
public sealed record NotificationContactTypeDefinition(string Code, string Name, string DisplayName, string? Placeholder = null, string? Description = null);
