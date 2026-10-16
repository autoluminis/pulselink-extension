using System.Text.Json;

namespace PulseLink.Plugin.Abstractions.Notifications;

/// <summary>通知插件服务向自定义 UI 暴露、且由宿主治理的可选操作。</summary>
public interface INotificationPluginOperations
{
    /// <summary>可经宿主代理调用的操作名称，名称不区分大小写。</summary>
    IReadOnlyList<string> OperationNames { get; }

    /// <summary>执行白名单操作；宿主会在调用前完成认证、审计和请求限制。</summary>
    ValueTask<JsonElement> ExecuteOperationAsync(string operation, JsonElement payload, CancellationToken cancellationToken = default);
}
