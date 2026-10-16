# 通知插件开发指导书

适用 SDK / 宿主：26.10.15.003；通知 API：2；开发环境：.NET 10 SDK。修订日期：2026-10-16。
本次完善示例、交付模板和验证工具，公共契约版本保持不变；其他宿主版本必须重新验证，不能仅凭 apiVersion 相同推断二进制兼容。

## 1. 下载、校验与构建

从官网同时下载 SDK ZIP 与同名 `.sha256` 文件。在 PowerShell 中执行：

```powershell
$zip = 'PulseLink-Notification-SDK-26.10.15.003.zip'
$expected = (Get-Content "$zip.sha256").Split(' ')[0]
if ((Get-FileHash $zip -Algorithm SHA256).Hash -ne $expected) { throw 'SDK 文件校验失败' }
Expand-Archive $zip -DestinationPath ./notification-sdk
Set-Location ./notification-sdk/PulseLink-Notification-SDK
dotnet --version
dotnet build src/PulseLink.Plugin.Abstractions.csproj -c Release
dotnet build examples/GenericWebhook.csproj -c Release
dotnet run --project tests/GenericWebhook.Tests.csproj -c Release
```

最后一个命令使用内存 HTTP 替身验证示例，无第三方测试框架和外部服务依赖，失败返回非零退出码。它覆盖配置、JSON 负载、幂等标识、HTTP 错误、取消和停止释放；不等于真实网络或宿主安装验收。

契约源码及 `contract-manifest.json` 是兼容性基线，不要重命名程序集、修改命名空间或更改契约版本。引用项目时设置 `Private="false" ExcludeAssets="runtime"`，使运行包使用宿主契约；独立测试程序需要自己引用契约项目。

## 2. 从示例创建自己的插件

复制 `examples/` 并保留对 SDK 的正确相对引用。修改以下对应项：

| 内容 | 示例值 | 修改要求 |
| --- | --- | --- |
| 插件 ID | `example.generic-webhook` | 入口 `Id` 与 plugin.json 的 pluginId 一致且长期稳定 |
| 插件版本 | `1.0.0` | 入口 Version、项目 Version、清单 version 一致，正式修复递增三段版本 |
| 程序集 | `PulseLink.NotificationPlugins.GenericWebhook.dll` | csproj AssemblyName 与清单 entryAssembly 一致 |
| 入口类型 | `PulseLink.NotificationPlugins.GenericWebhook.GenericWebhookPlugin` | public、实现接口、有 public 无参构造器，与 entryType 一致 |
| 渠道 Key | `webhook` | 现有通道实例引用它，升级不可随意更改 |
| 最低宿主 | `26.10.15.003` | 必须以实际安装及运行验证为依据 |

示例 `plugin.json` 是未签名的打包输入；空 sha256 由发行工具计算。`marketplace.json` 是项目发行资料，不进入运行时 ZIP。发布前将所有 `example.invalid`、发行方、许可证、帮助、支持和隐私链接换成你实际维护的资料。示例不授予官方发行身份。

## 3. 通道配置和发送

`INotificationChannelPlugin.CreateChannels()` 返回渠道，`StopAsync()` 释放自身创建的客户端、连接、定时器和后台任务。示例客户端属于插件实例；停止后不能继续发送。

`INotificationChannel` 声明显示名、默认 JSON、广播模式和联系人类型。示例为广播通道，不要求联系人；定向渠道应设置 IsBroadcast=false，并声明及读取匹配的 ContactTypes / Recipients，不要重新扩大宿主已经筛选的接收人范围。

配置示例：

```json
{"url":"https://your-server.example/pulselink/webhook"}
```

ValidateConfigurationAsync 和 SendAsync 使用相同校验规则：JSON 对象中的 url 必须是绝对 HTTP(S) 地址，不允许 URL 内账号密码或片段。HTTP 仅用于受控网络调试，公网部署使用 HTTPS。取消令牌必须传递到底层 I/O；示例每次发送最多 10 秒，不跟随重定向，不读取或保存服务端响应正文。

发送 POST application/json，包含 deliveryId、eventType、severity、subject、body 和 attributes；不发送完整 ConfigurationJson。请求头 `Idempotency-Key` 与负载 deliveryId 相同。接收端应持久化去重，同一 DeliveryId 重试时返回一致结果。

HTTP 2xx 只返回 Accepted；4xx、429、5xx、重定向、网络异常会抛出，交由宿主记录失败和重试。示例不自行循环重试。取消/超时可能发生在远端已经受理之后，因此不能据此假定消息未发送。生产接入应按服务方契约增加鉴权，避免将令牌、联系人、消息正文和完整敏感 URL 写入日志。

## 4. 后台服务、健康、日志与回执

需要持续连接或异步回执时，插件额外实现 `INotificationPluginRuntimeAware.StartAsync(runtime, token)`。保存受限 runtime，由 StopAsync 取消并等待后台任务结束。

| 可选接口或宿主能力 | 使用场景 |
| --- | --- |
| INotificationPluginConfigurationAware | 渠道配置变化时刷新连接或缓存 |
| INotificationPluginHealthReporter | 标记持续上报健康的有状态插件，通过 runtime.ReportHealthAsync 上报；无状态渠道不应实现，频率须高于宿主过期频率 |
| INotificationPluginOperations | 声明操作白名单并处理自定义管理 UI 请求 |
| INotificationChannel.ContactTypes | 由渠道声明联系人类型；INotificationContactTypeCatalog 是宿主汇总读取接口，不是插件生命周期入口 |
| runtime.ListChannelConfigurationsAsync | 读取当前插件的渠道实例配置 |
| runtime.GetPluginConfigurationAsync / UpdatePluginConfigurationAsync | 保存独立于渠道实例的插件服务配置 |
| runtime.SendHttpAsync | 使用宿主管理的受限 HTTP 通路 |
| runtime.ReportHealthAsync / WriteLogAsync | 报告健康、记录脱敏运行日志 |
| runtime.ReportReceiptAsync | 将服务方异步回执关联到原 DeliveryId |

只在确认真实送达后报告 Delivered；无法确认时保留 Accepted。回执携带原 DeliveryId、提供方消息 ID 和状态；不要根据 HTTP 200 自动判定实际送达。配置整体替换操作会影响所有当前插件渠道实例，更新前保留仍需使用的实例。插件不能获取宿主数据库或内部模块服务。

## 5. 构建、签名与交付

`examples/bin/Release/net10.0/` 产生入口 DLL、依赖描述与未签名清单。运行包中不应包含 PulseLink.Plugin.Abstractions.dll、测试程序集、密钥或项目源码。第三方依赖必须随包交付并核对许可证及目标平台。

SDK 只提供开发源码，不附带完整 ReleasePackager。取得授权发行工具和自己的 ECDSA 发布密钥后，在发行工具所在项目中使用以下命令形式（替换路径与密钥编号）：

```powershell
dotnet run --project path/to/PulseLink.ReleasePackager -- plugin-pack `
  --project path/to/GenericWebhook.csproj `
  --private-key D:/plugin-signing/private.pem `
  --signing-key-id your-publisher-key `
  --allow-external-signing-key `
  --output artifacts/your-plugins
```

工具校验入口身份、版本和商城资料，计算入口及文件摘要并签名，输出插件 ZIP 与同版本 release.json。不要手写假签名或将 SDK ZIP 当作安装包；修改 DLL 或包内容后必须重新打包签名。公钥、密钥编号及指纹交管理员核对，私钥始终由发行方保管且保持在包外。

私有交付不要求进入公共目录，但仍需完成管理员的插件信任和安装授权。公开发行另需发行方审核、公开文档、支持及隐私资料，按仓库 README 的 incoming/ 流程提交一对发布物。

## 6. 宿主安装与验收

1. 备份现有插件和配置，核对目标宿主、平台、发布公钥指纹与安装授权。
2. 在“系统管理 → 插件信任”授权适当范围；在通知中心上传签名插件 ZIP 并启用。
3. 创建 webhook 渠道实例，填入自建接收端地址。广播模式无需收件人联系人。
4. 将渠道关联通知策略，产生真实测试事件，检查投递记录、远端记录及 DeliveryId 一致性。
5. 测试成功、配置错误、HTTP 失败、超时、发送中取消、同 ID 重复投递、插件停用和宿主重启。
6. 升级前验证旧配置及渠道 Key 兼容，升级后再验证发送；按宿主版本管理验证回滚与资源释放。

签名与宿主装载成功、真实服务发送成功、异步回执确认应分别记录证据。验收表至少保存宿主版本、插件版本、配置脱敏样本、场景、预期/实际结果和投递 ID。

## 7. 常见问题

| 现象 | 检查 |
| --- | --- |
| 找不到 .NET 10 / NETSDK1045 | 安装 .NET 10 SDK，检查 dotnet --list-sdks 及上级 global.json |
| 编译通过但无法加载 | entryAssembly、entryType、无参构造器、宿主契约版本、依赖及平台 |
| 身份或版本不一致 | 同步入口 Id/Version、csproj、plugin.json 后重新构建 |
| 签名/摘要失败 | 是否修改了签名后的包、是否使用正确公钥与授权范围 |
| 已受理但未送达 | 核对服务方结果与回执，不将 Accepted 当作 Delivered |
| 更新后配置不兼容 | 保留旧渠道 Key 和配置迁移兼容性，查看宿主更新失败原因 |
| 重复消息 | 检查接收端是否按 DeliveryId 持久化去重 |

在线指导：https://autoluminis.github.io/pulselink-extension/developer.html
发行流程：https://github.com/autoluminis/pulselink-extension/blob/release/README.md
