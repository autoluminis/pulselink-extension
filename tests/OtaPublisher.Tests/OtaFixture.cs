using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using PulseLink.OtaPublisher;
using PulseLink.SharedKernel;

namespace PulseLink.OtaPublisher.Tests;

/// <summary>使用进程内临时公钥生成真实签名包；不访问官方私钥或外部网络。</summary>
internal sealed class OtaFixture : IDisposable
{
    private readonly ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    /// <summary>隔离的候选制品目录。</summary>
    public string Assets { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    /// <summary>隔离的模拟 release 分支目录。</summary>
    public string Repository { get; } = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    /// <summary>只信任本测试临时公钥的验证器。</summary>
    public OtaPublicationValidator Validator => new(new Dictionary<string, string> { ["test-key"] = key.ExportSubjectPublicKeyInfoPem() });
    /// <summary>最后生成的真实签名索引。</summary>
    public PulseLinkOtaReleaseIndex Index { get; private set; } = null!;
    /// <summary>最后生成的包内签名清单。</summary>
    public PulseLinkOtaManifest Manifest { get; private set; } = null!;
    /// <summary>创建目录；每个用例独立拥有所有文件。</summary>
    public OtaFixture() { Directory.CreateDirectory(Assets); Directory.CreateDirectory(Repository); }

    /// <summary>生成实际 ZIP 与两层签名，允许构造确定的校验失败边界。</summary>
    public async Task CreateAsync(string version = "26.10.08.001", string channel = "stable", string runtime = "win-x64", Func<PulseLinkOtaReleaseIndex, PulseLinkOtaReleaseIndex>? changeIndex = null, Func<PulseLinkOtaManifest, PulseLinkOtaManifest>? changeManifest = null, bool corruptPayload = false, bool duplicateManifest = false)
    {
        var payload = new byte[] { 1, 2, 3, 4 };
        var timestamp = new DateTimeOffset(2026, 1, 1, 1, 2, 3, TimeSpan.Zero).AddTicks(1234567);
        Manifest = new PulseLinkOtaManifest { SchemaVersion = 1, Product = "PulseLink", Version = version, MinimumVersion = "1.0.0", RuntimeIdentifier = runtime, Payload = "payload.zip", PayloadSize = payload.Length, PayloadSha256 = Convert.ToHexString(SHA256.HashData(payload)), PublishedAt = timestamp, ReleaseNotes = "中文更新说明：修复\\路径与 <网页> \"引号\"\n第二行", KeyId = "test-key", SignatureAlgorithm = PulseLinkOtaSignature.Algorithm };
        if (changeManifest is not null) Manifest = changeManifest(Manifest);
        Manifest = Manifest with { Signature = Sign(PulseLinkOtaSignature.CreateSigningPayload(Manifest)) };
        var name = $"PulseLink-{version}-{runtime}.ota.zip";
        var path = Path.Combine(Assets, name);
        if (File.Exists(path)) File.Delete(path);
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            using (var stream = archive.CreateEntry("pulselink-ota.json", CompressionLevel.NoCompression).Open())
                JsonSerializer.Serialize(stream, Manifest);
            if (duplicateManifest)
            {
                using var duplicate = archive.CreateEntry("pulselink-ota.json").Open();
                JsonSerializer.Serialize(duplicate, Manifest);
            }
            using var body = archive.CreateEntry(Manifest.Payload!, CompressionLevel.NoCompression).Open();
            body.Write(corruptPayload ? new byte[] { 5, 6, 7, 8 } : payload);
        }
        Index = new PulseLinkOtaReleaseIndex { SchemaVersion = 1, Product = "PulseLink", Channel = channel, Version = version, MinimumVersion = "1.0.0", RuntimeIdentifier = runtime, PackagePath = $"ota/{channel}/{runtime}/{name}", PackageSize = new FileInfo(path).Length, PackageSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(path))), PublishedAt = timestamp, ReleaseNotes = Manifest.ReleaseNotes, KeyId = "test-key", SignatureAlgorithm = PulseLinkOtaReleaseIndexSignature.Algorithm };
        if (changeIndex is not null) Index = changeIndex(Index);
        Index = Index with { Signature = Sign(PulseLinkOtaReleaseIndexSignature.CreateSigningPayload(Index)) };
        await SaveIndexAsync(Index);
    }

    /// <summary>覆盖候选索引原始字节，以模拟签名后篡改。</summary>
    public Task SaveIndexAsync(PulseLinkOtaReleaseIndex index) => File.WriteAllTextAsync(Path.Combine(Assets, "latest.release.json"), JsonSerializer.Serialize(index));
    /// <summary>模拟索引已切换，并保留原始签名字节。</summary>
    public Task InstallIndexAsync() => OtaPublicationValidator.WriteIndexAsync(Assets, Repository, Index);
    /// <summary>运行候选制品完整校验。</summary>
    public Task<PulseLinkOtaReleaseIndex> ValidateAsync(string? tag = null) => Validator.ValidateAsync(Assets, Repository, tag ?? $"ota-{Index.Channel}-{Index.RuntimeIdentifier}-{Index.Version}");
    /// <summary>生成标准 P1363 格式的测试签名。</summary>
    public string Sign(byte[] bytes) => Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    /// <summary>仅清理本用例直接创建的两个绝对临时目录。</summary>
    public void Dispose() { key.Dispose(); Directory.Delete(Assets, true); Directory.Delete(Repository, true); }
}
