# PulseLink 通知插件开发、测试与发布指导书

- 适用 SDK：`PulseLink.Plugin.Abstractions` 26.10.17.001
- 通知插件 API：v2
- 目标框架：.NET 10 (`net10.0`)
- 最低宿主版本：26.09.14.002

本指导书与同版 SDK NuGet、项目模板、独立打包器和 `compatibility-matrix.json` 一起发布。不得混用不同 SDK 套件版本中的文件。

## 1. 支持范围和安全边界

通知插件在 PulseLink 宿主进程内运行，属于受信代码，不是隔离沙箱。插件只能使用 `PulseLink.Plugin.Abstractions` 暴露的消息和受限运行时能力，不得引用 PulseLink 模块、数据库、依赖注入容器或宿主内部类型。

SDK 支持：渠道声明、配置校验、投递、异步回执、结构化日志、健康状态、宿主管理的 HTTP 请求、插件级配置和受控 UI 操作。不提供任意文件、数据库、服务容器或宿主凭据访问。

## 2. 安装套件并创建项目

从同一个 SDK 发布目录执行：

```powershell
dotnet new install .\templates\PulseLink.NotificationPlugin.Template.<version>.nupkg
dotnet new pulselink-notification -n Contoso.PulseLink.Notifications `
  --pluginId contoso.notifications --displayName "Contoso Notifications" `
  --providerId contoso --sdkVersion <version>
dotnet restore .\Contoso.PulseLink.Notifications `
  --source .\packages --source https://api.nuget.org/v3/index.json
dotnet test .\Contoso.PulseLink.Notifications --configuration Release --no-restore
```

`pluginId` 一经发布不得更改。建议使用反向域名或组织前缀的小写稳定标识。项目、帮助和发行方 URL 必须替换模板占位值。

## 3. 公共契约和生命周期

入口实现 `INotificationChannelPlugin`，必须有公开无参构造函数；`Id`、`Version` 必须与 `plugin.json` 一致。`CreateChannels` 返回稳定的渠道集合，渠道 `Key` 在插件内唯一。

短连接插件只需实现 `INotificationChannelPlugin`。拥有监听器、连接池或后台循环的插件再实现 `INotificationPluginRuntimeAware`；`StartAsync` 启动资源，`StopAsync` 必须在取消预算内停止接收、终止后台任务并释放套接字、计时器和句柄。停止流程应可重复调用，且不得遗留后台异常。

配置变化需要热重载时实现 `INotificationPluginConfigurationAware`。无状态插件不要实现 `INotificationPluginHealthReporter`；长期运行插件实现该标记后，必须以高于宿主过期阈值的频率上报健康状态。

## 4. 配置和敏感信息

`ValidateConfigurationAsync` 必须拒绝无效 JSON、缺失必填项、非法范围以及不安全 URL。升级配置时保持旧字段兼容，新增字段提供安全默认值；必须删除或重命名字段时，在插件版本说明中提供迁移步骤。

日志、健康摘要、异常响应和 UI 返回值不得包含密码、令牌、私钥、完整电话号码或原始敏感报文。插件调用 HTTP 服务时使用宿主的 `SendHttpAsync`，遵守 1–180 秒超时、1 MB 请求体及 64 KB 响应体限制。

## 5. 投递、幂等和重试

PulseLink 使用至少一次投递尝试语义，不承诺 exactly-once。外部服务已经受理但宿主尚未提交结果时，进程退出或租约丢失可能造成相同 `DeliveryId` 再次发送。

插件必须：

1. 把 `DeliveryId` 作为幂等键传递或持久化；
2. 区分提供方受理 `Accepted` 与明确送达 `Delivered`；
3. 对取消令牌和超时作出有界响应；
4. 不在结果不确定时自行无限重试；
5. 对部分收件人成功、迟到回执和进程重启建立测试。

明确失败可抛出异常交由宿主 Outbox 重试。异步确认通过 `ReportReceiptAsync` 回报，且必须关联原 `DeliveryId`。

## 6. 自定义 UI

插件 UI 只能调用宿主提供的插件 UI 网关和 `INotificationPluginOperations` 白名单操作。资源使用相对路径，不依赖外部脚本；遵守宿主 CSP。操作名称稳定、不区分大小写，服务端仍需验证载荷、权限和当前状态。UI 不得读取宿主令牌或绕过审计接口。

## 7. 必需测试

发布前至少覆盖：

- 入口无参构造、标识和版本与清单一致；
- 渠道键唯一，默认配置可通过校验；
- 非法 JSON、缺失字段、范围和 URL 校验；
- 成功、拒绝、超时、取消及提供方错误；
- 相同 `DeliveryId` 重复调用和重启恢复；
- 部分成功和迟到回执；
- 配置热更新并发；
- `StopAsync` 有界、重复且无资源泄漏；
- 日志和健康数据无敏感信息；
- 包内清单、程序集身份、摘要和签名验证。

## 8. 清单与版本

`plugin.json` 使用 `manifestVersion: 2`、`apiVersion: 2`。版本为三个不超过 65534 的数字段，入口程序集版本、入口 `Version`、清单版本和发布元数据必须一致。`minimumHostVersion` 不能低于所用 API 首次支持的宿主版本；需要上限时在发布元数据中设置 `maximumHostVersionExclusive`。

兼容更新递增 patch，新增向后兼容功能递增 minor，破坏插件行为或配置契约递增 major。SDK 的破坏性公共契约变化必须进入新的 `apiVersion`。

## 9. 开发签名与验包

开发、CI、正式发布使用不同密钥。私钥不得进入源码、模板、构建日志或插件 ZIP。开发密钥只用于本地/隔离环境，并通过显式测试信任配置授权；正式包必须使用已经登记并获插件范围授权的发行方密钥。

```powershell
pulselink-packager plugin-pack `
  --project .\src\Contoso.PulseLink.Notifications\Contoso.PulseLink.Notifications.csproj `
  --private-key C:\secure\contoso-release.pem `
  --signing-key-id contoso-release-2026 `
  --output .\artifacts\plugins `
  --version 1.0.0

pulselink-packager verify `
  --package .\artifacts\plugins\contoso.notifications-1.0.0.plugin.zip `
  --public-key C:\secure\contoso-release-public.pem
```

发布前离线保存包 SHA-256、清单、签名 KeyId、构建提交和测试结果。密钥轮换先授权新公钥，再用新密钥发布；确认客户端覆盖后撤销旧授权。撤销不能由插件包自身完成。

## 10. 安装、升级与回滚测试

在非生产宿主依次验证：全新安装、启用、创建渠道、测试投递、停用、同版本拒绝、升级、失败回滚、旧版回滚和卸载。升级前保留当前包和配置备份；Pending 投递未处理完成时不要替换整体通知配置。

本地离线安装只证明该宿主环境的兼容性。商城发布还必须通过目录签名、哈希链、ZIP 分离签名、发行方授权以及宿主版本检查。

## 11. 商城发布

维护 `marketplace.json` 的真实发行方、开发者、说明、文档链接和发布说明。正式发布流程：

1. 从干净提交恢复依赖并运行全部测试；
2. 使用同版 SDK 套件打包器生成签名包；
3. 对原始 ZIP 执行离线验签并记录 SHA-256；
4. 提交发布 PR，不提交密钥或构建缓存；
5. 等待发布仓库验证及目录重建；
6. 在客户端刷新商城并进行安装、投递、升级与回滚验收。

PR 创建或流水线调度成功不等于发布成功；最终以远端签名索引一致、客户端可安装及验收记录为准。

SDK 套件本身的发布入口是 `.github/workflows/notification-plugin-sdk-release.yml`。先以 `plugin-sdk-v<productVersion>` tag 或手动运行执行验证；工作流完成模板、签名、验包、宿主安装/升级/回滚/卸载和 SHA-256 门禁后，才可在 `notification-plugin-sdk-release` 环境批准发布 GitHub Release 或 NuGet。正式发布不读取插件私钥，NuGet 发布需要环境密钥 `NUGET_API_KEY`。

## 12. 发布检查表

- [ ] SDK、模板、打包器和兼容矩阵版本一致
- [ ] 清单、入口和程序集版本一致
- [ ] 无主仓库 ProjectReference 或内部类型依赖
- [ ] 单元、故障、生命周期和幂等测试通过
- [ ] 私钥、令牌和现场数据扫描无发现
- [ ] 开发密钥与正式密钥隔离
- [ ] 签名包离线验签通过并记录 SHA-256
- [ ] 非生产宿主安装、升级和回滚通过
- [ ] 发布索引和客户端商城验证通过
- [ ] 发布说明列出兼容范围、迁移步骤和已知限制
