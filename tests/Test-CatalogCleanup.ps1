# 使用临时目录和临时密钥，验证删除测试发布物后能重新生成空扩展目录和保留正式插件目录。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$publisher = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts/Publish-Package.ps1'
$temporaryRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) ('pulselink-catalog-cleanup-' + [Guid]::NewGuid().ToString('N'))))
$temporaryParent = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
if (-not $temporaryRoot.StartsWith($temporaryParent, [StringComparison]::OrdinalIgnoreCase)) { throw '测试目录不在临时目录内。' }
$key = [Security.Cryptography.ECDsa]::Create([Security.Cryptography.ECCurve+NamedCurves]::nistP256)
$originalPrivateKey = $env:PULSELINK_CATALOG_PRIVATE_KEY_PEM
$originalPassword = $env:PULSELINK_CATALOG_PRIVATE_KEY_PASSWORD
$originalKeyId = $env:PULSELINK_CATALOG_KEY_ID
function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
try {
    New-Item -ItemType Directory -Force "$temporaryRoot/incoming", "$temporaryRoot/providers", "$temporaryRoot/extensions/providers/autoluminis", "$temporaryRoot/notifications/providers/autoluminis/plugins/pulselink.wxpusher/1.0.1" | Out-Null
    # 扩展 plugins/ 不存在，模拟 Git 检出删除唯一测试插件后的实际目录结构。
    [IO.File]::WriteAllText("$temporaryRoot/extensions/providers/autoluminis/index.json", '{"plugins":[{"pluginId":"example.demo"}]}')
    [IO.File]::WriteAllText("$temporaryRoot/providers/autoluminis.json", '{"schemaVersion":1,"providerId":"autoluminis","displayName":"Autoluminis","publisherKeyIds":["cleanup-test-key"]}')
    $archivePath = "$temporaryRoot/notifications/providers/autoluminis/plugins/pulselink.wxpusher/1.0.1/package.zip"
    [IO.File]::WriteAllBytes($archivePath, [Text.Encoding]::UTF8.GetBytes('signed-archive-fixture'))
    $archiveSignature = $key.SignData([IO.File]::ReadAllBytes($archivePath), [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
    $release = [ordered]@{
        schemaVersion = 1
        packageKind = 'notification-plugin'
        pluginId = 'pulselink.wxpusher'
        name = 'WxPusher 通知'
        description = '商城目录测试插件'
        version = '1.0.1'
        releasedAt = '2026-10-08T00:00:00Z'
        releaseNotes = '测试'
        isPrerelease = $false
        hostCompatibility = @{ minimumVersion = '26.09.14.002'; maximumVersionExclusive = $null }
        package = @{
            sizeBytes = (Get-Item $archivePath).Length
            sha256 = (Get-FileHash $archivePath -Algorithm SHA256).Hash.ToLowerInvariant()
            signatureKeyId = 'cleanup-test-key'
            signatureAlgorithm = 'ECDSA-P256-SHA256'
            signature = [Convert]::ToBase64String($archiveSignature)
        }
        marketplace = @{
            providerId = 'autoluminis'
            providerName = 'Autoluminis'
            summary = '通知插件'
            tags = @('notification', 'wxpusher')
            developers = @(@{ id = 'autoluminis'; displayName = 'Autoluminis'; url = 'https://example.com' })
            maintainers = @(@{ id = 'autoluminis'; displayName = 'Autoluminis'; url = 'https://example.com' })
            sourceUrl = 'https://example.com/source'
        }
    }
    [IO.File]::WriteAllText("$temporaryRoot/notifications/providers/autoluminis/plugins/pulselink.wxpusher/1.0.1/release.json", ($release | ConvertTo-Json -Depth 20))
    [IO.File]::WriteAllText("$temporaryRoot/notifications/providers/autoluminis/plugins/pulselink.wxpusher/index.json", '{"versions":[{"version":"1.0.0"}]}')
    $env:PULSELINK_CATALOG_PRIVATE_KEY_PEM = $key.ExportPkcs8PrivateKeyPem()
    $env:PULSELINK_CATALOG_KEY_ID = 'cleanup-test-key'
    & pwsh -NoProfile -File $publisher -IncomingDirectory incoming -RepositoryRoot $temporaryRoot -Publish -AllowEmpty
    Assert ($LASTEXITCODE -eq 0) '目录重建失败。'
    # 使用临时密码加密临时密钥，验证与正式加密私钥相同的发布路径。
    $env:PULSELINK_CATALOG_PRIVATE_KEY_PASSWORD = [Guid]::NewGuid().ToString('N')
    $parameters = [Security.Cryptography.PbeParameters]::new([Security.Cryptography.PbeEncryptionAlgorithm]::Aes256Cbc, [Security.Cryptography.HashAlgorithmName]::SHA256, 10000)
    $env:PULSELINK_CATALOG_PRIVATE_KEY_PEM = $key.ExportEncryptedPkcs8PrivateKeyPem($env:PULSELINK_CATALOG_PRIVATE_KEY_PASSWORD, $parameters)
    & pwsh -NoProfile -File $publisher -IncomingDirectory incoming -RepositoryRoot $temporaryRoot -Publish -AllowEmpty
    Assert ($LASTEXITCODE -eq 0) '加密私钥目录重建失败。'
    $extension = Get-Content "$temporaryRoot/extensions/providers/autoluminis/index.json" -Raw | ConvertFrom-Json -AsHashtable
    Assert ($extension.plugins.Count -eq 0) '已移除的测试扩展不能继续出现在提供方索引。'
    $notification = Get-Content "$temporaryRoot/notifications/providers/autoluminis/plugins/pulselink.wxpusher/index.json" -Raw | ConvertFrom-Json -AsHashtable
    Assert ($notification.versions.Count -eq 1 -and $notification.versions[0].version -eq '1.0.1') '只保留正式版本，不保留测试 1.0.0。'
    $category = Get-Content "$temporaryRoot/notifications/index.json" -Raw | ConvertFrom-Json -AsHashtable
    $provider = Get-Content "$temporaryRoot/notifications/providers/autoluminis/index.json" -Raw | ConvertFrom-Json -AsHashtable
    Assert ($category.catalogId -eq 'notifications' -and $category.packageKind -eq 'notification-plugin') '分类目录必须带有宿主要求的 catalogId。'
    Assert ($provider.publisherKeyIds -contains 'cleanup-test-key') '发行方目录必须携带受信任的发布密钥列表。'
    Assert ($notification.providerId -eq 'autoluminis' -and $notification.providerName -eq 'Autoluminis' -and $notification.name -eq 'WxPusher 通知') '插件目录缺少发行方或插件展示信息。'
    Assert ($notification.developers.Count -eq 1 -and $notification.tags.Count -eq 2) '插件目录应保留 marketplace 元数据。'
    $version = $notification.versions[0]
    Assert ($version.hostCompatibility.minimumVersion -eq '26.09.14.002') '版本目录必须包含宿主兼容范围。'
    Assert ($version.package.path -eq 'notifications/providers/autoluminis/plugins/pulselink.wxpusher/1.0.1/package.zip') '版本目录包路径错误。'
    Assert ($version.package.archiveSha256 -eq $release.package.sha256 -and $version.package.sizeBytes -eq $release.package.sizeBytes) '版本目录必须使用真实包摘要和大小。'
    Assert ($version.package.signatureKeyId -eq 'cleanup-test-key' -and $version.package.signature -eq $release.package.signature) '版本目录必须保留 ZIP 签名信息。'
    Assert (-not $version.ContainsKey('releasePath')) '版本目录不能继续采用旧版 releasePath 格式。'
    $indexFiles = @(Get-ChildItem -LiteralPath $temporaryRoot -Filter index.json -File -Recurse)
    Assert ($indexFiles.Count -eq 5) '两个分类、两个发行方及一个正式插件的索引应完整生成。'
    foreach ($indexFile in $indexFiles) {
        Assert (-not ([IO.File]::ReadAllBytes($indexFile.FullName) -contains 13)) '发布索引必须使用 LF 换行，避免 Git 规范化后摘要与签名失效。'
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
    Write-Host '清理目录回归通过：商城 Schema、5 份索引签名、包摘要、引用摘要一致。'
} finally {
    $env:PULSELINK_CATALOG_PRIVATE_KEY_PEM = $originalPrivateKey
    $env:PULSELINK_CATALOG_PRIVATE_KEY_PASSWORD = $originalPassword
    $env:PULSELINK_CATALOG_KEY_ID = $originalKeyId
    $key.Dispose()
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
}
