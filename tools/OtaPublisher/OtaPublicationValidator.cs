using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using PulseLink.SharedKernel;

namespace PulseLink.OtaPublisher;

/// <summary>离线核验已签署 OTA 发布物；发布服务仅持有公钥，不重新签署发行方的索引。</summary>
public sealed class OtaPublicationValidator(IReadOnlyDictionary<string, string> trustedKeys)
{
    private const long MaximumPackageBytes = 524_288_000;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>验证候选包及已有索引；同版本仅允许重试完全相同的发布物，禁止降级或覆盖。</summary>
    public async Task<PulseLinkOtaReleaseIndex> ValidateAsync(string assetsDirectory, string repositoryRoot, string releaseTag)
    {
        var indexPath = Path.Combine(assetsDirectory, PulseLinkReleaseDistribution.OtaIndexFileName);
        var index = await ReadIndexAsync(indexPath);
        ValidateIndex(index);
        var expectedTag = PulseLinkReleaseDistribution.GetOtaReleaseTag(index.Channel!, index.RuntimeIdentifier!, index.Version!);
        Require(releaseTag == expectedTag, "Release 标签与已签名索引不一致。");
        var packageName = Path.GetFileName(index.PackagePath!);
        var packages = Directory.GetFiles(assetsDirectory, "*.ota.zip");
        Require(packages.Length == 1 && Path.GetFileName(packages[0]) == packageName, "发布必须包含唯一的同名 OTA 包。");
        var packagePath = Path.Combine(assetsDirectory, packageName);
        Require(new FileInfo(packagePath).Length == index.PackageSize, "OTA 包大小与已签名索引不一致。");
        Require(await HashFileAsync(packagePath) == index.PackageSha256!.ToLowerInvariant(), "OTA 包 SHA-256 与索引不一致。");
        await ValidateArchiveAsync(packagePath, index);

        var currentPath = Path.Combine(repositoryRoot, PulseLinkReleaseDistribution.GetOtaIndexPath(index.Channel!, index.RuntimeIdentifier!));
        if (File.Exists(currentPath))
        {
            var current = await ReadIndexAsync(currentPath);
            ValidateIndex(current);
            Require(current.Channel == index.Channel && current.RuntimeIdentifier == index.RuntimeIdentifier, "已有索引目录与通道或平台不一致。");
            var order = Version.Parse(index.Version!).CompareTo(Version.Parse(current.Version!));
            Require(order >= 0, "OTA 通道不允许版本降级。");
            if (order == 0)
                Require(PulseLinkOtaReleaseIndexSignature.CreateSigningPayload(index).SequenceEqual(PulseLinkOtaReleaseIndexSignature.CreateSigningPayload(current)), "已发布版本不可更改，重试必须使用同一发布物。");
        }
        return index;
    }

    /// <summary>只写入原始签名索引字节；应在 Release 资产公开后调用，避免指向未上线的大包。</summary>
    public static async Task WriteIndexAsync(string assetsDirectory, string repositoryRoot, PulseLinkOtaReleaseIndex index)
    {
        var destination = Path.Combine(repositoryRoot, PulseLinkReleaseDistribution.GetOtaIndexPath(index.Channel!, index.RuntimeIdentifier!));
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(assetsDirectory, PulseLinkReleaseDistribution.OtaIndexFileName));
        var temporary = destination + ".pending";
        await File.WriteAllBytesAsync(temporary, bytes);
        File.Move(temporary, destination, overwrite: true);
    }

    private void ValidateIndex(PulseLinkOtaReleaseIndex index)
    {
        Require(index.SchemaVersion == 1 && index.Product == "PulseLink", "OTA 索引结构或产品无效。");
        Require(index.Channel is "stable" or "beta" or "internal", "OTA 通道无效。");
        Require(index.RuntimeIdentifier is "win-x64" or "win-arm64" or "win-x86", "OTA 平台无效。");
        ValidateVersion(index.Version);
        ValidateVersion(index.MinimumVersion);
        Require(Version.Parse(index.MinimumVersion!) <= Version.Parse(index.Version!), "最低版本高于目标版本。");
        var expectedPath = PulseLinkReleaseDistribution.GetOtaPackagePath(index.Channel!, index.RuntimeIdentifier!, $"PulseLink-{index.Version}-{index.RuntimeIdentifier}.ota.zip");
        Require(index.PackagePath == expectedPath, "OTA 包路径或文件名不符合官方发布规则。");
        Require(index.PackageSize is > 0 and <= MaximumPackageBytes, "OTA 包超出 500 MB 限制或为空。");
        Require(IsHash(index.PackageSha256), "OTA 包摘要格式无效。");
        Require(index.PublishedAt != default && index.PublishedAt <= DateTimeOffset.UtcNow.AddMinutes(5), "OTA 发布时间无效。");
        Require(index.ReleaseNotes?.Length is not > 8_000, "更新说明超过 8000 字符。");
        Verify(PulseLinkOtaReleaseIndexSignature.CreateSigningPayload(index), index.KeyId, index.SignatureAlgorithm, index.Signature);
    }

    private async Task ValidateArchiveAsync(string path, PulseLinkOtaReleaseIndex index)
    {
        using var archive = ZipFile.OpenRead(path);
        Require(archive.Entries.Count == 2 && archive.Entries.Select(entry => entry.FullName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "OTA 外层 ZIP 必须仅有清单和载荷，不能含重复条目。");
        var manifestEntry = archive.GetEntry("pulselink-ota.json") ?? throw new InvalidOperationException("OTA 包缺少 pulselink-ota.json。");
        Require(manifestEntry.Length is > 0 and <= 65_536, "OTA 清单超出 64 KB 限制或为空。");
        await using var manifestStream = manifestEntry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<PulseLinkOtaManifest>(manifestStream, JsonOptions) ?? throw new InvalidOperationException("OTA 清单无效。");
        Require(manifest.SchemaVersion == 1 && manifest.Product == index.Product && manifest.Version == index.Version && manifest.MinimumVersion == index.MinimumVersion && manifest.RuntimeIdentifier == index.RuntimeIdentifier && manifest.KeyId == index.KeyId && manifest.PublishedAt == index.PublishedAt && manifest.ReleaseNotes == index.ReleaseNotes, "OTA 清单与签名索引的发布身份或元数据不一致。");
        Require(!string.IsNullOrWhiteSpace(manifest.Payload) && manifest.Payload.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_') && manifest.Payload is not "." and not "..", "OTA 载荷文件名不安全。");
        Verify(PulseLinkOtaSignature.CreateSigningPayload(manifest), manifest.KeyId, manifest.SignatureAlgorithm, manifest.Signature);
        var payload = archive.GetEntry(manifest.Payload!) ?? throw new InvalidOperationException("OTA 载荷不存在。");
        Require(payload.Length is > 0 and <= MaximumPackageBytes && payload.Length == manifest.PayloadSize && IsHash(manifest.PayloadSha256), "OTA 载荷大小或摘要无效。");
        await using var payloadStream = payload.Open();
        Require(await HashStreamAsync(payloadStream, manifest.PayloadSize) == manifest.PayloadSha256!.ToLowerInvariant(), "OTA 载荷摘要不一致。");
    }

    private void Verify(byte[] signingPayload, string? keyId, string? algorithm, string? signature)
    {
        Require(keyId is not null && trustedKeys.ContainsKey(keyId), "OTA 发布密钥不受信任。");
        Require(algorithm == PulseLinkOtaSignature.Algorithm && !string.IsNullOrWhiteSpace(signature), "OTA 签名元数据无效。");
        using var key = ECDsa.Create();
        key.ImportFromPem(trustedKeys[keyId!]);
        Require(key.KeySize == 256 && key.VerifyData(signingPayload, Convert.FromBase64String(signature!), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation), "OTA 签名无效。");
    }

    private static async Task<PulseLinkOtaReleaseIndex> ReadIndexAsync(string path)
    {
        Require(new FileInfo(path).Length is > 0 and <= 65_536, "OTA 索引超出 64 KB 限制或为空。");
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<PulseLinkOtaReleaseIndex>(stream, JsonOptions) ?? throw new InvalidOperationException("OTA 索引无效。");
    }

    private static void ValidateVersion(string? value) => Require(value is not null && value.All(character => char.IsAsciiDigit(character) || character == '.') && Version.TryParse(value, out var version) && version.Build >= 0, "OTA 版本格式无效。");
    private static bool IsHash(string? value) => value is { Length: 64 } && value.All(Uri.IsHexDigit);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    private static async Task<string> HashFileAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        return await HashStreamAsync(stream, MaximumPackageBytes);
    }

    private static async Task<string> HashStreamAsync(Stream stream, long limit)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81_920];
        long length = 0;
        int count;
        while ((count = await stream.ReadAsync(buffer)) != 0)
        {
            length += count;
            Require(length <= limit, "解压或读取的制品超过签名大小限制。");
            hash.AppendData(buffer, 0, count);
        }
        Require(length > 0, "制品不能为空。");
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
}
