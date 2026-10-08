# PulseLink 插件商城与系统 OTA 发布仓库

系统 OTA 使用草稿 GitHub Release 上传、签名校验和原始签名索引发布流程，详见 [系统 OTA 发布](docs/ota-publishing.md)。OTA 包不提交到 Git；下文 incoming/ 流程仅用于插件。

此仓库只接收已打包的插件发布物，不保存插件源码，也不会在此编译、测试插件项目。

发布者在一个 Pull Request 中仅提交一对文件：

```text
incoming/{pluginId}-{version}.zip
incoming/{pluginId}-{version}.release.json
```

`release.json` 必须由 PulseLink ReleasePackager 生成，且 `package` 必须包含 ZIP 原始字节的 `sha256`、`sizeBytes`、`signatureKeyId`、`signatureAlgorithm` 与 `signature`。CI 以 `providers/{providerId}.json` 中明确授权的公钥标识验证签名，拒绝未知发行方、未知密钥、重复版本和多包 PR。

合并到 `release` 分支后，CI 将唯一通过校验的成品迁移至：

```text
extensions/providers/{providerId}/plugins/{pluginId}/{version}/
notifications/providers/{providerId}/plugins/{pluginId}/{version}/
```

并重建各插件 `index.json` 和分类 `index.json`。已发布版本不可修改；修复必须发布新版本。

每个插件默认仅保留最新三个三段数字版本。发布第四个版本时，CI 会在同一事务中删除最旧版本目录并重建索引；删除仅作用于该插件的已发布版本目录。

## 发布分支

每次发布使用短期分支，且分支名必须与待发布 ZIP 内清单、`release.json` 和 `incoming/` 文件名中的插件 ID、版本一致：

```text
publish/notification-{pluginId}-{version}
publish/extension-{pluginId}-{version}
```

每个发布分支只能包含一个插件的一个版本；身份或版本不一致时，CI 拒绝发布。发布分支合并后应删除。

## 发布安全

官方插件验签公钥通过 Actions Secret `PULSELINK_OFFICIAL_PUBLISHER_PUBLIC_KEY_PEM` 配置，当前 KeyId 为 `PULSELINK-RELEASE-20260909-114323-C4E0322DCA104E619C21B71FE5B0207C`。工作流将该公钥合并到既有 `PULSELINK_PROVIDER_KEYS_JSON` 集合，只替换同 KeyId 条目，保留其他发行方密钥；发行方配置中的 `publisherKeyIds` 授权检查保持开启。此配置只保存公钥，不保存本地打包私钥，不改变 OTA 内置信任根或商城目录签名私钥。

发布包和商城目录均经过签名校验。私钥及发布授权配置由受保护的维护流程管理，不会提交到本仓库。
