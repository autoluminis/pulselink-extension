namespace PulseLink.Plugin.Abstractions;

/// <summary>
/// PulseLink 第三方插件的基础契约。
/// </summary>
/// <remarks>
/// 插件标识和版本用于宿主的加载、治理与兼容性检查。
/// </remarks>
public interface IPulseLinkPlugin
{
    /// <summary>插件的全局唯一标识。</summary>
    string Id { get; }

    /// <summary>面向管理界面展示的插件名称。</summary>
    string DisplayName { get; }

    /// <summary>插件自身的语义化版本。</summary>
    Version Version { get; }
}
