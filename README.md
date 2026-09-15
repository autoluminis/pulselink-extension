# PulseLink 插件商城发布仓库

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

## 密钥与分支

仓库维护者在 `providers/{providerId}.json` 登记发行者公钥标识；实际公钥由 CI 从受保护的 `PULSELINK_PROVIDER_KEYS_JSON` Secret 读取。目录索引由 `PULSELINK_CATALOG_PRIVATE_KEY_PEM` Secret 签名，私钥不会进入仓库。保护 `release` 分支，仅允许 GitHub Actions 的发布身份写入。
