# 系统 OTA 发布

本仓库同时承载插件商城与系统 OTA。插件仍使用 incoming/ 的原有流程；系统 OTA 使用草稿 GitHub Release，不把大包提交到 Git，也不使用 LFS。

## 发布物与信任

由配套 PulseLink ReleasePackager 的 ota-pack 命令离线生成：

```text
ota/stable/win-x64/
  PulseLink-26.10.08.001-win-x64.ota.zip
  latest.release.json
```

索引必须由客户端已信任的正式 OTA 私钥签署，包内有签名 pulselink-ota.json 和对应载荷 ZIP。CI 只使用 Contracts 内客户端相同的公钥；不需要新增私钥 Secret，不把商城 index.json.sig 当成 OTA 索引签名。

允许 stable、beta、internal 与 win-x64、win-x86、win-arm64。每个 Release 标签包含通道、平台和原始版本：
`ota-stable-win-x64-26.10.08.001`。版本中的前导零必须与索引完全一致。

## 操作步骤

1. 在正式产品源码和签名机生成 OTA，并完成离线安装、健康确认及回滚验收。包内程序集、前端与 OTA 清单必须属于同一版本。
2. 创建同仓库的草稿 Release，上传 OTA ZIP 与原始 latest.release.json：

```powershell
gh release create ota-stable-win-x64-26.10.08.001 --repo autoluminis/pulselink-extension --draft --target release --title "PulseLink 26.10.08.001"
gh release upload ota-stable-win-x64-26.10.08.001 --repo autoluminis/pulselink-extension ./ota/stable/win-x64/PulseLink-26.10.08.001-win-x64.ota.zip ./ota/stable/win-x64/latest.release.json
```

3. 在 Actions 运行“Validate and publish system OTA”，选择 release 分支，填入该标签。也可执行：

```powershell
gh workflow run publish-ota.yml --repo autoluminis/pulselink-extension --ref release -f release_tag=ota-stable-win-x64-26.10.08.001
```

4. 流程验证签名索引、完整包的大小/摘要、包内清单身份/签名及载荷摘要，拒绝标签不一致、降级、路径逃逸、重复条目和同版本改包。验证通过后先公开 Release，再把索引原始字节提交到 release 分支的 ota/通道/平台/latest.release.json。
5. 查看本次 Actions 成功状态，再从客户端检查、下载、安装并观察回滚路径。仅上传草稿不代表客户端可用。

同版本的完全相同发布物可以重跑。公开 Release 后若 Git 索引提交失败，旧索引继续有效；重跑可补完索引提交。历史 Release 保留，避免已缓存的签名旧索引指向被删除的大包。产品版本不得复用。

插件与 OTA 发布共用 release 分支的并发锁。GitHub Token 需要 contents:write；无需访问 Nginx、SSH 或已下线的 release.veio.cn。

## 客户端下载设置

配套 PulseLink 版本改为 Raw 索引 + Release 资产。在系统设置中可启用 jsDelivr 仓库 CDN、完整 URL 下载加速或服务器 HTTP 代理。jsDelivr 仅支持仓库文件，不能下载 Release 附件，分支缓存可能延迟新索引出现。所有来源的签名与摘要校验保持开启。

旧客户端仍指向下线域名，必须先通过离线 OTA 或手动部署安装迁移版本。

## 开发验证

```powershell
dotnet run --project tests/OtaPublisher.Tests --configuration Release
pwsh -NoProfile -File tests/Test-OtaPublicationWorkflow.ps1
```

30 项回归使用临时 ECDSA 密钥与实际 ZIP；不依赖正式私钥。另有 5 项脚本编排回归，模拟外部命令并验证每个失败阶段停止后续操作。Contracts 是客户端确定性签名协议快照，修改时必须与客户端同步，并重新验证两端。

初始上线仍需上传首个正式签名 OTA 发布物。本 PR 不伪造签名索引，不公开测试密钥签名包，尚不代表生产升级或现场回滚验收。
