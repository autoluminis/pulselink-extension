[CmdletBinding(DefaultParameterSetName = 'Validate')]
param(
    [Parameter(Mandatory)] [string] $IncomingDirectory,
    [Parameter(Mandatory)] [string] $RepositoryRoot,
    [switch] $ValidateOnly,
    [switch] $Publish,
    [switch] $AllowEmpty
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Read-Json([string] $Path) { Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable }
function Write-Utf8Json([string] $Path, $Value) {
    $json = $Value | ConvertTo-Json -Depth 20
    [IO.File]::WriteAllText($Path, $json + "`n", [Text.UTF8Encoding]::new($false))
}
function Get-Sha256([string] $Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Assert-Id([string] $Value, [string] $Name) { Assert ($Value -match '^[a-z][a-z0-9-]{2,63}$') "$Name 无效。" }
function Assert-Version([string] $Value, [string] $Name) { Assert ($Value -match '^\d+\.\d+\.\d+$') "$Name 必须是三段数字版本。" }
function Get-ArchiveManifest([string] $ArchivePath) {
    $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
    try {
        $entries = @($archive.Entries | Where-Object { $_.Name })
        Assert ($entries.Count -gt 0) 'ZIP 不能为空。'
        $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($entry in $entries) {
            Assert ($entry.FullName -notmatch '(^/|\\|(^|/)\.\.(/|$)|(^|/)\./)') "ZIP 含不安全路径：$($entry.FullName)"
            Assert ($seen.Add($entry.FullName)) "ZIP 含重复路径：$($entry.FullName)"
        }
        $plugin = @($entries | Where-Object FullName -eq 'plugin.json')
        $extension = @($entries | Where-Object FullName -eq 'extension.json')
        Assert (($plugin.Count + $extension.Count) -eq 1) 'ZIP 必须且只能包含 plugin.json 或 extension.json 之一。'
        $entry = if ($plugin.Count -eq 1) { $plugin[0] } else { $extension[0] }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $manifest = $reader.ReadToEnd() | ConvertFrom-Json -AsHashtable } finally { $reader.Dispose() }
        return @{ Kind = if ($plugin.Count -eq 1) { 'notification-plugin' } else { 'extension' }; Manifest = $manifest; Entries = $entries }
    } finally { $archive.Dispose() }
}
function Test-ArchiveSignature([string] $ArchivePath, [hashtable] $Package, [hashtable] $AuthorizedKeys) {
    foreach ($field in @('signatureKeyId', 'signatureAlgorithm', 'signature')) { Assert (-not [string]::IsNullOrWhiteSpace([string]$Package[$field])) "release.json package.$field 缺失。" }
    Assert ($Package.signatureAlgorithm -eq 'ECDSA-P256-SHA256') '仅支持 ECDSA-P256-SHA256 ZIP 签名。'
    $pem = $AuthorizedKeys[$Package.signatureKeyId]
    Assert (-not [string]::IsNullOrWhiteSpace([string]$pem)) "未提供签名密钥 $($Package.signatureKeyId) 的受保护公钥。"
    $key = [Security.Cryptography.ECDsa]::Create()
    try {
        $key.ImportFromPem([string]$pem)
        $signature = [Convert]::FromBase64String([string]$Package.signature)
        $ok = $key.VerifyData([IO.File]::ReadAllBytes($ArchivePath), $signature, [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
        Assert $ok 'ZIP 原始字节签名无效。'
    } finally { $key.Dispose() }
}
function Sign-Index([string] $IndexPath) {
    $pem = $env:PULSELINK_CATALOG_PRIVATE_KEY_PEM
    if ([string]::IsNullOrWhiteSpace($pem)) { return }
    Assert (-not [string]::IsNullOrWhiteSpace($env:PULSELINK_CATALOG_KEY_ID)) '缺少 PULSELINK_CATALOG_KEY_ID。'
    $key = [Security.Cryptography.ECDsa]::Create()
    try {
        $key.ImportFromPem($pem)
        $signature = $key.SignData([IO.File]::ReadAllBytes($IndexPath), [Security.Cryptography.HashAlgorithmName]::SHA256, [Security.Cryptography.DSASignatureFormat]::IeeeP1363FixedFieldConcatenation)
        Write-Utf8Json "$IndexPath.sig" ([ordered]@{ schemaVersion = 1; algorithm = 'ECDSA-P256-SHA256'; keyId = $env:PULSELINK_CATALOG_KEY_ID; signature = [Convert]::ToBase64String($signature) })
    } finally { $key.Dispose() }
}
function Rebuild-Indexes([string] $Root, [string] $KindRoot, [string] $PackageKind) {
    $providerRows = @()
    Get-ChildItem -LiteralPath $KindRoot -Directory -ErrorAction SilentlyContinue | ForEach-Object {
        $provider = $_
        $pluginRows = @()
        Get-ChildItem -LiteralPath "$($provider.FullName)/plugins" -Directory -ErrorAction SilentlyContinue | ForEach-Object {
            $plugin = $_
            $versions = @()
            Get-ChildItem -LiteralPath $plugin.FullName -Directory | ForEach-Object {
                $release = Read-Json "$($_.FullName)/release.json"
                $versions += [ordered]@{ version = $release.version; releasePath = (($_.FullName.Substring($Root.Length + 1) -replace '\\','/') + '/release.json'); archiveSha256 = $release.package.sha256 }
            }
            $pluginIndex = [ordered]@{ schemaVersion = 1; pluginId = $plugin.Name; packageKind = $PackageKind; versions = @($versions | Sort-Object version) }
            $pluginPath = "$($plugin.FullName)/index.json"; Write-Utf8Json $pluginPath $pluginIndex; Sign-Index $pluginPath
            $pluginRows += [ordered]@{ pluginId = $plugin.Name; indexPath = ($pluginPath.Substring($Root.Length + 1) -replace '\\','/'); indexSha256 = Get-Sha256 $pluginPath }
        }
        $providerIndex = [ordered]@{ schemaVersion = 1; providerId = $provider.Name; packageKind = $PackageKind; plugins = @($pluginRows | Sort-Object pluginId) }
        $providerPath = "$($provider.FullName)/index.json"; Write-Utf8Json $providerPath $providerIndex; Sign-Index $providerPath
        $providerRows += [ordered]@{ providerId = $provider.Name; indexPath = ($providerPath.Substring($Root.Length + 1) -replace '\\','/'); indexSha256 = Get-Sha256 $providerPath }
    }
    $category = [ordered]@{ schemaVersion = 1; packageKind = $PackageKind; generatedAt = [DateTimeOffset]::UtcNow.ToString('O'); providers = @($providerRows | Sort-Object providerId) }
    $path = "$KindRoot/index.json"; Write-Utf8Json $path $category; Sign-Index $path
}

Assert ($ValidateOnly -xor $Publish) '必须二选一指定 -ValidateOnly 或 -Publish。'
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$incoming = [IO.Path]::GetFullPath($IncomingDirectory, $root)
$zips = @(Get-ChildItem -LiteralPath $incoming -Filter '*.zip' -File)
$releases = @(Get-ChildItem -LiteralPath $incoming -Filter '*.release.json' -File)
if ($zips.Count -eq 0 -and $AllowEmpty) { exit 0 }
Assert ($zips.Count -eq 1 -and $releases.Count -eq 1) '一次发布只能在 incoming 中包含一个 ZIP 与一个 .release.json。'
$zip = $zips[0]; $releasePath = $releases[0]; $release = Read-Json $releasePath
Assert ($release.schemaVersion -eq 1) 'release.json schemaVersion 必须为 1。'
Assert ($release.packageKind -in @('extension','notification-plugin')) 'release.json packageKind 无效。'
Assert-Id ([string]$release.pluginId) 'release.json pluginId'; Assert-Version ([string]$release.version) 'release.json version'
Assert ($release.package.fileName -eq $zip.Name) 'release.json package.fileName 与 ZIP 文件名不一致。'
Assert ($release.package.sizeBytes -eq $zip.Length) 'release.json package.sizeBytes 与 ZIP 实际大小不一致。'
Assert ($release.package.sha256 -eq (Get-Sha256 $zip.FullName)) 'release.json package.sha256 与 ZIP 原始字节不一致。'
$providerId = [string]$release.marketplace.providerId; Assert-Id $providerId 'marketplace.providerId'
$provider = Read-Json "$root/providers/$providerId.json"; Assert ($provider.providerId -eq $providerId) '发行方配置不匹配。'
$keys = if ($env:PULSELINK_PROVIDER_KEYS_JSON) { $env:PULSELINK_PROVIDER_KEYS_JSON | ConvertFrom-Json -AsHashtable } else { @{} }
Assert ($provider.publisherKeyIds -contains $release.package.signatureKeyId) '该密钥未获发行方授权。'
Test-ArchiveSignature $zip.FullName $release.package $keys
$archive = Get-ArchiveManifest $zip.FullName; Assert ($archive.Kind -eq $release.packageKind) 'ZIP 类型与 release.json 不一致。'
$manifestId = if ($archive.Kind -eq 'extension') { [string]$archive.Manifest.id } else { [string]$archive.Manifest.pluginId }
Assert ($manifestId -eq $release.pluginId -and $archive.Manifest.version -eq $release.version) 'ZIP 内清单与 release.json 的插件 ID 或版本不一致。'
Assert ($archive.Manifest.manifestVersion -eq 2) '商城只接受 manifestVersion 2 的插件包。'
$targetKind = if ($release.packageKind -eq 'extension') { 'extensions' } else { 'notifications' }
$target = "$root/$targetKind/providers/$providerId/plugins/$($release.pluginId)/$($release.version)"
Assert (-not (Test-Path -LiteralPath $target)) '该插件版本已发布，已发布目录不可覆盖。'
if ($ValidateOnly) { Write-Host "验证通过：$($release.pluginId) $($release.version)"; exit 0 }
Assert (-not [string]::IsNullOrWhiteSpace($env:PULSELINK_CATALOG_PRIVATE_KEY_PEM)) '发布必须配置 PULSELINK_CATALOG_PRIVATE_KEY_PEM。'
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -LiteralPath $zip.FullName -Destination "$target/package.zip"
Copy-Item -LiteralPath $releasePath -Destination "$target/release.json"
Remove-Item -LiteralPath $zip.FullName, $releasePath
Rebuild-Indexes $root "$root/$targetKind" $release.packageKind
Write-Host "已发布：$($release.pluginId) $($release.version)"
