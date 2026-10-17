# PulseLink 通知插件 API v2 公共契约

本文件冻结通知插件 API v2 的公共面。权威程序集为同版 `PulseLink.Plugin.Abstractions` NuGet 包；宿主内部类型不属于 SDK。

## 必需入口

| 类型 | 稳定成员 | 约束 |
|---|---|---|
| `IPulseLinkPlugin` | `Id`、`DisplayName`、`Version` | `Id` 和 `Version` 与清单一致 |
| `INotificationChannelPlugin` | `CreateChannels()`、`StopAsync()` | 公开无参构造；停止有界且可重复 |
| `INotificationChannel` | `Key`、`DisplayName`、`IsBroadcast`、`ContactTypes`、`DefaultConfigurationJson`、`MaximumInstances`、`ValidateConfigurationAsync()`、`SendAsync()` | 渠道键稳定且在插件内唯一 |

## 可选能力接口

| 类型 | 用途 |
|---|---|
| `INotificationPluginRuntimeAware` | 启动监听器、连接池或后台资源 |
| `INotificationPluginConfigurationAware` | 渠道配置保存后的热重载 |
| `INotificationPluginHealthReporter` | 声明插件持续上报健康状态 |
| `INotificationPluginOperations` | 向受控插件 UI 公开白名单操作 |

宿主通过 `INotificationPluginRuntime` 提供以下受限能力：读取/更新本插件渠道配置、读取/更新插件级配置、报告异步回执、写结构化日志、报告健康状态和发送受治理 HTTP 请求。插件不得假定运行时实现类型或把它转换为宿主服务。

## 数据契约

- `NotificationDispatchMessage`：不可变投递快照；`DeliveryId` 是幂等键。
- `NotificationRecipient`：已由宿主筛选的端点。
- `NotificationDeliveryResult`：同步返回 `Accepted` 或 `Delivered`。
- `NotificationDeliveryReceipt`：异步返回 `Accepted`、`Delivered` 或 `Rejected`。
- `NotificationChannelValidationResult`：配置验证结果和安全的错误列表。
- `NotificationPluginChannelConfiguration`：仅属于当前插件的渠道配置。
- `NotificationPluginHttpRequest` / `NotificationPluginHttpResponse`：宿主管理的 HTTP 边界。
- `NotificationPluginLogEntry`：受限结构化日志。
- `NotificationPluginHealth`：`Healthy`、`Degraded` 或 `Unhealthy` 健康快照。
- `NotificationContactTypeDefinition`：插件声明的稳定联系方式类型。

## 版本与废弃策略

1. API v2 内优先通过新可选接口或带默认实现的接口成员扩展能力。
2. 已发布成员至少跨一个正式宿主发布周期标记废弃，文档同时给出替代项。
3. 删除成员、改变现有成员语义或修改清单的不兼容结构，需要新的 `apiVersion`。
4. 插件安装兼容范围由 `plugin.json.minimumHostVersion` 与发布元数据的 `maximumHostVersionExclusive` 共同约束。
5. 安全修复若必须收紧输入、网络或敏感数据边界，可在相同 API 版本中实施；发布说明必须明确行为变化。

## 非公共面

`PulseLink.Modules.*`、`PulseLink.Infrastructure`、数据库实体、控制器、后台工作器、宿主 DI、商城内部 DTO 和打包器内部类型均不是插件 SDK。第三方插件对这些程序集的编译或反射依赖不受兼容保证，并会在发布检查中被拒绝。
