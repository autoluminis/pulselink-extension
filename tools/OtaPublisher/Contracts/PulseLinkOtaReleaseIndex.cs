using System.Text.Json.Serialization;

namespace PulseLink.SharedKernel;

/// <summary>供客户端在下载前检查最新 OTA 发布物的、独立签名的在线索引。</summary>
public sealed record PulseLinkOtaReleaseIndex
{
    [JsonPropertyName("schemaVersion")] public int SchemaVersion { get; init; }
    [JsonPropertyName("product")] public string? Product { get; init; }
    [JsonPropertyName("channel")] public string? Channel { get; init; }
    [JsonPropertyName("version")] public string? Version { get; init; }
    [JsonPropertyName("minimumVersion")] public string? MinimumVersion { get; init; }
    [JsonPropertyName("runtimeIdentifier")] public string? RuntimeIdentifier { get; init; }
    [JsonPropertyName("packagePath")] public string? PackagePath { get; init; }
    [JsonPropertyName("packageSize")] public long PackageSize { get; init; }
    [JsonPropertyName("packageSha256")] public string? PackageSha256 { get; init; }
    [JsonPropertyName("publishedAt")] public DateTimeOffset PublishedAt { get; init; }
    [JsonPropertyName("releaseNotes")] public string? ReleaseNotes { get; init; }
    [JsonPropertyName("keyId")] public string? KeyId { get; init; }
    [JsonPropertyName("signatureAlgorithm")] public string? SignatureAlgorithm { get; init; }
    [JsonPropertyName("signature")] public string? Signature { get; init; }
}
