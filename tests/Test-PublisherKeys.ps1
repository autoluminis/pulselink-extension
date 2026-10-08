Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/../scripts/Publisher-Keys.ps1"
$keyId = 'PULSELINK-RELEASE-20260909-114323-C4E0322DCA104E619C21B71FE5B0207C'
$key = [Security.Cryptography.ECDsa]::Create([Security.Cryptography.ECCurve+NamedCurves]::nistP256)
$passed = 0
function Assert([bool] $Condition, [string] $Message) { if (-not $Condition) { throw $Message } }
function Assert-Rejected([scriptblock] $Action) { $rejected = $false; try { & $Action | Out-Null } catch { $rejected = $true }; Assert $rejected '错误配置应当被拒绝。' }
try {
    $pem = $key.ExportSubjectPublicKeyInfoPem()
    $keys = Get-PublisherKeys -LegacyKeysJson '{"legacy":"keep"}'
    Assert ($keys.legacy -eq 'keep' -and $keys.Count -eq 1) '未新增官方配置时必须保留旧集合。'; $passed++
    $keys = Get-PublisherKeys -LegacyKeysJson '{"legacy":"keep"}' -OfficialKeyId $keyId -OfficialPublicKeyPem $pem
    Assert ($keys.legacy -eq 'keep' -and $keys[$keyId] -eq $pem) '新增公钥不得删除其他发行方。'; $passed++
    $keys = Get-PublisherKeys -OfficialKeyId $keyId -OfficialPublicKeyPem $pem
    Assert ($keys.Count -eq 1 -and $keys[$keyId] -eq $pem) '允许初始化官方公钥。'; $passed++
    $legacy = @{ legacy = 'keep'; $keyId = 'old-key' } | ConvertTo-Json -Compress
    $keys = Get-PublisherKeys -LegacyKeysJson $legacy -OfficialKeyId $keyId -OfficialPublicKeyPem $pem
    Assert ($keys.legacy -eq 'keep' -and $keys[$keyId] -eq $pem) '仅替换同 KeyId 条目。'; $passed++
    Assert-Rejected { Get-PublisherKeys -LegacyKeysJson '[]' }; $passed++
    Assert-Rejected { Get-PublisherKeys -OfficialKeyId 'wrong-id' -OfficialPublicKeyPem $pem }; $passed++
    Assert-Rejected { Get-PublisherKeys -OfficialKeyId $keyId -OfficialPublicKeyPem $key.ExportPkcs8PrivateKeyPem() }; $passed++
    Assert-Rejected { Get-PublisherKeys -OfficialKeyId $keyId -OfficialPublicKeyPem 'invalid' }; $passed++
    $other = [Security.Cryptography.ECDsa]::Create([Security.Cryptography.ECCurve+NamedCurves]::nistP384)
    try { Assert-Rejected { Get-PublisherKeys -OfficialKeyId $keyId -OfficialPublicKeyPem $other.ExportSubjectPublicKeyInfoPem() }; $passed++ } finally { $other.Dispose() }
    Write-Host "$passed 项发行方公钥合并回归通过。"
} finally { $key.Dispose() }

