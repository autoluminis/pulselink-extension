# PulseLink 通知插件开发包

版本：26.10.17.001；目标宿主：26.10.17.001；开发环境：.NET 10 SDK。
这是可独立构建的 SDK 源码与 Webhook 示例，不是可直接安装的插件 ZIP，也不是完整 PulseLink 产品源码。

## 构建

```sh
dotnet build src/PulseLink.Plugin.Abstractions.csproj -c Release
dotnet build examples/GenericWebhook.csproj -c Release
dotnet run --project tests/GenericWebhook.Tests.csproj -c Release
```

引用 src 项目或构建产生的 PulseLink.Plugin.Abstractions.dll。部署插件时不要附带 SDK DLL，由宿主提供同名契约程序集。不要更改契约命名空间、程序集名称或版本。

示例入口：PulseLink.NotificationPlugins.GenericWebhook.GenericWebhookPlugin；插件 ID：example.generic-webhook；版本：1.0.0；广播渠道 Key：webhook。配置示例：`{"url":"https://example.invalid/pulselink/webhook"}`。请替换成自己的服务地址、插件 ID 和命名空间。

## 接口与发送行为

- 实现 INotificationChannelPlugin，提供无参构造器，CreateChannels 返回渠道。
- INotificationChannel 声明 Key、DisplayName、IsBroadcast、ContactTypes、DefaultConfigurationJson。
- ValidateConfigurationAsync 校验 JSON；SendAsync 使用平台已经筛选的消息与收件人。
- Accepted 仅表示受理；实际送达通过回执契约报告。失败、取消和超时应准确返回或抛出。
- 接收端按 DeliveryId 持久化去重。外部发送与宿主结果保存不是原子事务，可能重复投递。
- 示例提供配置校验、10 秒超时、取消传递、Idempotency-Key、拒绝重定向和停止资源释放；上线需按实际服务补充鉴权、敏感配置保护及接收端持久化去重。

完整步骤见 [通知插件开发指导书](DEVELOPING.md)，包含下载校验、身份字段对应、运行时接口、签名交付、宿主验收与故障排查。API v2 约束见 [通知插件 API](notification-plugin-api-v2.md)，版本兼容范围见 [兼容矩阵](notification-plugin-compatibility-matrix.json)。`examples/plugin.json` 和 `examples/marketplace.json` 为打包输入模板，发布前替换示例发行身份和 URL。

## 从构建到交付

编译成功不等于已经安装。安装包需要 plugin.json、入口程序集、依赖、文件摘要及发行方签名。通知插件 apiVersion 为 2。pluginId、entryType、版本必须与代码一致，更新 DLL 后重新计算 SHA-256。不要把本包 ZIP 上传为通知插件。

私有交付使用自己的发布密钥，管理员核对公钥并授权安装范围；不要向任何人提交私钥。公开目录另需 marketplace.json、版本兼容资料、公开文档和发行方审核。

此开发包不包含官方签名私钥或完整发行打包工具。正式签名包应通过经过授权的 PulseLink 发行工具链生成；公开发行的材料和审核流程见仓库 README。不要手工绕过宿主签名校验。

在线文档：https://autoluminis.github.io/pulselink-extension/developer.html
发行规范：https://github.com/autoluminis/pulselink-extension/blob/release/README.md

本包仅包含通知插件公共契约和示例，供开发 PulseLink 通知插件使用。未包含产品内部模块、数据库实现或任何凭据。兼容其他宿主版本前请重新验证契约和实际安装行为。

