# 公钥集合采用追加配置，避免更新官方公钥时覆盖不可读回的其他发行方 Secret。
function Get-PublisherKeys {
    param([string] $LegacyKeysJson, [string] $OfficialKeyId, [string] $OfficialPublicKeyPem)
    $keys = if ([string]::IsNullOrWhiteSpace($LegacyKeysJson)) { @{} } else { $LegacyKeysJson | ConvertFrom-Json -AsHashtable }
    if ($keys -isnot [hashtable]) { throw '发行方公钥集合必须为 JSON 对象。' }
    if ([string]::IsNullOrWhiteSpace($OfficialPublicKeyPem)) { return $keys }
    if ($OfficialKeyId -ne 'PULSELINK-RELEASE-20260909-114323-C4E0322DCA104E619C21B71FE5B0207C') { throw '官方发布公钥的 KeyId 未被当前客户端信任。' }
    if ($OfficialPublicKeyPem -notmatch '-----BEGIN PUBLIC KEY-----' -or $OfficialPublicKeyPem -match 'PRIVATE KEY') { throw '官方公钥配置必须只包含公开 PEM 公钥。' }
    $key = [Security.Cryptography.ECDsa]::Create()
    try {
        $key.ImportFromPem($OfficialPublicKeyPem)
        if ($key.ExportParameters($false).Curve.Oid.Value -ne '1.2.840.10045.3.1.7') { throw '官方发布公钥必须使用 ECDSA P-256。' }
        $keys[$OfficialKeyId] = $key.ExportSubjectPublicKeyInfoPem()
    } finally { $key.Dispose() }
    return $keys
}
