using System.Text.Json.Serialization;

namespace PulseLink.SharedKernel;

/// <summary>
/// 定义 PulseLink 系统 OTA 发布包的版本、平台、载荷和签名元数据。
/// </summary>
public sealed record PulseLinkOtaManifest
{
    /// <summary>OTA 清单结构版本。</summary>
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; init; }

    /// <summary>目标产品标识。</summary>
    [JsonPropertyName("product")]
    public string? Product { get; init; }

    /// <summary>目标产品版本。</summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>允许安装该更新包的最低当前版本。</summary>
    [JsonPropertyName("minimumVersion")]
    public string? MinimumVersion { get; init; }

    /// <summary>目标 .NET 运行时标识，例如 win-x64。</summary>
    [JsonPropertyName("runtimeIdentifier")]
    public string? RuntimeIdentifier { get; init; }

    /// <summary>外层 OTA ZIP 内的载荷文件名。</summary>
    [JsonPropertyName("payload")]
    public string? Payload { get; init; }

    /// <summary>载荷文件的精确字节数。</summary>
    [JsonPropertyName("payloadSize")]
    public long PayloadSize { get; init; }

    /// <summary>载荷文件的 SHA-256 十六进制摘要。</summary>
    [JsonPropertyName("payloadSha256")]
    public string? PayloadSha256 { get; init; }

    /// <summary>发布包生成的 UTC 时间。</summary>
    [JsonPropertyName("publishedAt")]
    public DateTimeOffset PublishedAt { get; init; }

    /// <summary>面向管理员的更新说明。</summary>
    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; init; }

    /// <summary>用于选择受信发布公钥的稳定标识。</summary>
    [JsonPropertyName("keyId")]
    public string? KeyId { get; init; }

    /// <summary>清单签名算法。</summary>
    [JsonPropertyName("signatureAlgorithm")]
    public string? SignatureAlgorithm { get; init; }

    /// <summary>清单签名的 Base64 编码。</summary>
    [JsonPropertyName("signature")]
    public string? Signature { get; init; }
}
