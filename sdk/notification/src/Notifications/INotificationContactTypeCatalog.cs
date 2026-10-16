namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>提供已由通知插件声明的用户联系方式类型目录。</summary>
public interface INotificationContactTypeCatalog
{
    /// <summary>返回所有已注册且可用于用户资料的联系方式类型定义。</summary>
    IReadOnlyList<NotificationContactTypeDefinition> ListContactTypes();
}
