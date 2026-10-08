namespace PulseLink.SharedKernel;

/// <summary>正式发布物内置的信任注册表。所有端点及验签公钥均在此集中维护，不能由运行时配置替换。</summary>
public static class PulseLinkTrustRegistry
{
    /// <summary>受信任的在线许可证服务 HTTPS 地址。新增官方节点时仅在此列表中追加。</summary>
    public static IReadOnlyList<Uri> LicenseServiceEndpoints { get; } = [new Uri("https://plicense.clarilume.cn:9443/")];

    /// <summary>受信任的许可证签名公钥；保留历史密钥可让旧许可证在密钥轮换后继续验证。</summary>
    public static IReadOnlyList<PulseLinkTrustedSigningKey> LicenseSigningKeys { get; } =
    [
        new("PULSELINK-LICENSE-20260909-113149-FD9DA55B83BC4328AFBA5C37BBBE66E1", """
            -----BEGIN PUBLIC KEY-----
            MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEu8xoENfomakRbgE+HOMHEWC5a5mn
            5wJsphxLT0AIayFY8Bz9BEvASviCH2Knumy8ZiN/QzOwQ0q0VmTEdmDITw==
            -----END PUBLIC KEY-----
            """)
    ];

    /// <summary>受信任的 OTA 发布公钥。</summary>
    public static IReadOnlyList<PulseLinkTrustedSigningKey> OtaSigningKeys { get; } =
    [
        new("PULSELINK-RELEASE-20260909-114323-C4E0322DCA104E619C21B71FE5B0207C", """
            -----BEGIN PUBLIC KEY-----
            MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE5zfuxSeIDY1/0Fks0f8S6HnoCJog
            yvqdTrV+H4tXmxzGvOe08Cfcyqk4AMXWl04FokWynHyUy9XheSXg4uWfKw==
            -----END PUBLIC KEY-----
            """)
    ];

    /// <summary>受信任的插件发布公钥。</summary>
    public static IReadOnlyList<PulseLinkTrustedSigningKey> PluginSigningKeys { get; } = OtaSigningKeys;

    /// <summary>受信任的商城目录签名公钥。目录与官方 OTA 发布目录由同一发布信任根签署。</summary>
    public static IReadOnlyList<PulseLinkTrustedSigningKey> MarketplaceCatalogSigningKeys { get; } = OtaSigningKeys;

    /// <summary>按密钥标识查找许可证验签公钥。</summary>
    public static string? GetLicensePublicKeyPem(string? keyId) => GetPublicKeyPem(LicenseSigningKeys, keyId);

    /// <summary>按密钥标识查找 OTA 验签公钥。</summary>
    public static string? GetOtaPublicKeyPem(string? keyId) => GetPublicKeyPem(OtaSigningKeys, keyId);

    /// <summary>按密钥标识查找插件验签公钥。</summary>
    public static string? GetPluginPublicKeyPem(string? keyId) => GetPublicKeyPem(PluginSigningKeys, keyId);

    /// <summary>按密钥标识查找商城目录验签公钥。</summary>
    public static string? GetMarketplaceCatalogPublicKeyPem(string? keyId) => GetPublicKeyPem(MarketplaceCatalogSigningKeys, keyId);

    private static string? GetPublicKeyPem(IReadOnlyList<PulseLinkTrustedSigningKey> keys, string? keyId) => keys.FirstOrDefault(item => string.Equals(item.KeyId, keyId, StringComparison.Ordinal))?.PublicKeyPem;
}
