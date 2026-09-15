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

## 发布分支

每次发布使用短期分支，且分支名必须与待发布 ZIP 内清单、`release.json` 和 `incoming/` 文件名中的插件 ID、版本一致：

```text
publish/notification-{pluginId}-{version}
publish/extension-{pluginId}-{version}
```

每个发布分支只能包含一个插件的一个版本；身份或版本不一致时，CI 拒绝发布。发布分支合并后应删除。

## 发布安全

发布包和商城目录均经过签名校验。私钥及发布授权配置由受保护的维护流程管理，不会提交到本仓库。
