# 使用临时目录和临时密钥，验证删除测试发布物后能重新生成空扩展目录和保留正式插件目录。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$publisher = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts/Publish-Package.ps1'
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('pulselink-catalog-cleanup-' + [Guid]::NewGuid().ToString('N'))))
$temporaryParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if (-not $temporaryRoot.StartsWith($temporaryParent, [StringComparison]::OrdinalIgnoreCase)) { throw '测试目录不在临时目录内。' }
$key = [Security.Cryptography.ECDsa]::Create([Security.Cryptography.ECCurve+NamedCurves]::nistP256)
$originalPrivateKey = $env:PULSELINK_CATALOG_PRIVATE_KEY_PEM
$originalKeyId = $env:PULSELINK_CATALOG_KEY_ID
function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
try {
    New-Item -ItemType Directory -Force "$temporaryRoot/incoming", "$temporaryRoot/extensions/providers/autoluminis", "$temporaryRoot/notifications/providers/autoluminis/plugins/pulselink.wxpusher/1.0.1" | Out-Null
    # 扩展 plugins/ 不存在，模拟 Git 检出删除唯一测试插件后的实际目录结构。
    [IO.File]::WriteAllText("$temporaryRoot/extensions/providers/autoluminis/index.json", '{"plugins":[{"pluginId":"example.demo"}]}')
    [IO.File]::WriteAllText("$temporaryRoot/notifications/providers/autoluminis/plugins/pulselink.wxpusher/1.0.1/release.json", '{"version":"1.0.1","package":{"sha256":"retained-package-hash"}}')
    [IO.File]::WriteAllText("$temporaryRoot/notifications/providers/autoluminis/plugins/pulselink.wxpusher/index.json", '{"versions":[{"version":"1.0.0"}]}')
    $env:PULSELINK_CATALOG_PRIVATE_KEY_PEM = $key.ExportPkcs8PrivateKeyPem()
    $env:PULSELINK_CATALOG_KEY_ID = 'cleanup-test-key'
    & pwsh -NoProfile -File $publisher -IncomingDirectory incoming -RepositoryRoot $temporaryRoot -Publish -AllowEmpty
    Assert ($LASTEXITCODE -eq 0) '目录重建失败。'
    $extension = Get-Content "$temporaryRoot/extensions/providers/autoluminis/index.json" -Raw | ConvertFrom-Json -AsHashtable
    Assert ($extension.plugins.Count -eq 0) '已移除的测试扩展不能继续出现在提供方索引。'
    $notification = Get-Content "$temporaryRoot/notifications/providers/autoluminis/plugins/pulselink.wxpusher/index.json" -Raw | ConvertFrom-Json -AsHashtable
    Assert ($notification.versions.Count -eq 1 -and $notification.versions[0].version -eq '1.0.1') '只保留正式版本，不保留测试 1.0.0。'
    $indexFiles = @(Get-ChildItem -LiteralPath $temporaryRoot -Filter index.json -File -Recurse)
    Assert ($indexFiles.Count -eq 5) '两个分类、两个发行方及一个正式插件的索引应完整生成。'
    foreach ($indexFile in $indexFiles) {
        $signature = Get-Content "$($indexFile.FullName).sig" -Raw | ConvertFrom-Json -AsHashtable
        Assert ($signature.keyId -eq 'cleanup-test-key') '目录必须使用当前签名密钥。'
        $verified = $key.VerifyData([IO.File]::ReadAllBytes($indexFile.FullName), [Convert]::FromBase64String($signature.signature), [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
        Assert $verified '目录签名与重建后的实际字节不一致。'
        $index = Get-Content $indexFile.FullName -Raw | ConvertFrom-Json -AsHashtable
        foreach ($field in @('providers', 'plugins')) {
            if (-not $index.ContainsKey($field)) { continue }
            foreach ($row in $index[$field]) {
                $actual = (Get-FileHash -LiteralPath (Join-Path $temporaryRoot $row.indexPath) -Algorithm SHA256).Hash.ToLowerInvariant()
                Assert ($actual -eq $row.indexSha256) '目录引用摘要必须与下级索引一致。'
            }
        }
    }
    Write-Host '清理目录回归通过：空扩展目录、正式版本保留、5 份索引签名和引用摘要一致。'
} finally {
    $env:PULSELINK_CATALOG_PRIVATE_KEY_PEM = $originalPrivateKey
    $env:PULSELINK_CATALOG_KEY_ID = $originalKeyId
    $key.Dispose()
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
}
