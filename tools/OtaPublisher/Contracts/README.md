# 客户端协议快照

此目录来自 PulseLink 的 SharedKernel：原始基线 `7c39de4f`，发布地址包含配套 `codex/github-ota-source` 修改。签名原文生成代码及 DTO 与客户端完全一致，避免 PowerShell JSON 转义、字段顺序及 UTC 时间格式差异导致验签失败。

更新 OTA 协议或公钥时，必须同时更新此快照和客户端，并运行发布回归测试。这里只保存公钥；CI 不需要 OTA 私钥。客户端先内置新公钥，再用新私钥发布更新。
