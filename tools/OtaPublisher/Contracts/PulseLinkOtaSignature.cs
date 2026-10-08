using System.Buffers;
using System.Text.Json;

namespace PulseLink.SharedKernel;

/// <summary>
/// 为 OTA 发布端和运行时验证端生成一致、确定性的清单签名载荷。
/// </summary>
public static class PulseLinkOtaSignature
{
    private const string SigningContext = "PulseLink:SystemOta:Manifest:v1";

    /// <summary>当前清单使用的 ECDSA P-256 签名算法标识。</summary>
    public const string Algorithm = "ECDSA-P256-SHA256";

    /// <summary>将不含签名值的清单字段编码为确定性 UTF-8 JSON。</summary>
    /// <param name="manifest">需要签名或验签的 OTA 清单。</param>
    /// <returns>确定性的签名载荷。</returns>
    public static byte[] CreateSigningPayload(PulseLinkOtaManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var buffer = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(buffer);
        writer.WriteStartObject();
        writer.WriteString("context", SigningContext);
        writer.WriteNumber("schemaVersion", manifest.SchemaVersion);
        writer.WriteString("product", manifest.Product);
        writer.WriteString("version", manifest.Version);
        writer.WriteString("minimumVersion", manifest.MinimumVersion);
        writer.WriteString("runtimeIdentifier", manifest.RuntimeIdentifier);
        writer.WriteString("payload", manifest.Payload);
        writer.WriteNumber("payloadSize", manifest.PayloadSize);
        writer.WriteString("payloadSha256", manifest.PayloadSha256);
        writer.WriteString("publishedAt", manifest.PublishedAt.ToUniversalTime());
        writer.WriteString("releaseNotes", manifest.ReleaseNotes);
        writer.WriteString("keyId", manifest.KeyId);
        writer.WriteString("signatureAlgorithm", manifest.SignatureAlgorithm);
        writer.WriteEndObject();
        writer.Flush();
        return buffer.WrittenSpan.ToArray();
    }
}
