using System.Buffers;
using System.Text.Json;

namespace PulseLink.SharedKernel;

/// <summary>为在线 OTA 索引提供与包内清单用途隔离的确定性签名载荷。</summary>
public static class PulseLinkOtaReleaseIndexSignature
{
    private const string SigningContext = "PulseLink:SystemOta:ReleaseIndex:v1";
    /// <summary>在线发布索引采用的签名算法标识。</summary>
    public const string Algorithm = "ECDSA-P256-SHA256";

    /// <summary>生成字段顺序固定的索引签名原文。</summary>
    public static byte[] CreateSigningPayload(PulseLinkOtaReleaseIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("context", SigningContext);
        writer.WriteNumber("schemaVersion", index.SchemaVersion);
        writer.WriteString("product", index.Product);
        writer.WriteString("channel", index.Channel);
        writer.WriteString("version", index.Version);
        writer.WriteString("minimumVersion", index.MinimumVersion);
        writer.WriteString("runtimeIdentifier", index.RuntimeIdentifier);
        writer.WriteString("packagePath", index.PackagePath);
        writer.WriteNumber("packageSize", index.PackageSize);
        writer.WriteString("packageSha256", index.PackageSha256);
        writer.WriteString("publishedAt", index.PublishedAt.ToUniversalTime());
        writer.WriteString("releaseNotes", index.ReleaseNotes);
        writer.WriteString("keyId", index.KeyId);
        writer.WriteString("signatureAlgorithm", index.SignatureAlgorithm);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}
