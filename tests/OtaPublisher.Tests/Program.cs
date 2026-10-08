using PulseLink.OtaPublisher.Tests;
using PulseLink.SharedKernel;

var tests = new List<(string Name, Func<Task> Run)>();
void Test(string name, Func<OtaFixture, Task> run) => tests.Add((name, async () => { using var fixture = new OtaFixture(); await run(fixture); }));
async Task Reject(Func<Task> action)
{
    try { await action(); } catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or System.Security.Cryptography.CryptographicException or FormatException or FileNotFoundException) { return; }
    throw new Exception("期望拒绝发布，但校验成功。");
}
void Equal(string expected, string actual) { if (expected != actual) throw new Exception($"不一致：{expected} / {actual}"); }

foreach (var channel in new[] { "stable", "beta", "internal" })
    foreach (var runtime in new[] { "win-x64", "win-x86", "win-arm64" })
    {
        Test($"合法签名与中文说明 {channel}/{runtime}", async fixture =>
        {
            await fixture.CreateAsync(channel: channel, runtime: runtime);
            await fixture.ValidateAsync();
            await fixture.InstallIndexAsync();
            var expected = await File.ReadAllTextAsync(Path.Combine(fixture.Assets, "latest.release.json"));
            Equal(expected, await File.ReadAllTextAsync(Path.Combine(fixture.Repository, $"ota/{channel}/{runtime}/latest.release.json")));
        });
    }
Test("相同发布重试幂等", async fixture => { await fixture.CreateAsync(); await fixture.InstallIndexAsync(); await fixture.ValidateAsync(); });
Test("版本降级被拒绝且保留旧索引", async fixture =>
{
    await fixture.CreateAsync("26.10.09.001"); await fixture.InstallIndexAsync();
    var current = await File.ReadAllTextAsync(Path.Combine(fixture.Repository, "ota/stable/win-x64/latest.release.json"));
    await fixture.CreateAsync(); await Reject(async () => await fixture.ValidateAsync());
    Equal(current, await File.ReadAllTextAsync(Path.Combine(fixture.Repository, "ota/stable/win-x64/latest.release.json")));
});
Test("同版本不同内容不可覆盖", async fixture =>
{
    await fixture.CreateAsync(); await fixture.InstallIndexAsync();
    await fixture.CreateAsync(changeManifest: manifest => manifest with { ReleaseNotes = "不同更新说明" });
    await Reject(async () => await fixture.ValidateAsync());
});
Test("索引签名后篡改被拒绝", async fixture =>
{
    await fixture.CreateAsync(); await fixture.SaveIndexAsync(fixture.Index with { ReleaseNotes = "篡改" });
    await Reject(async () => await fixture.ValidateAsync());
});
Test("索引不能使用清单签名上下文", async fixture =>
{
    await fixture.CreateAsync(); await fixture.SaveIndexAsync(fixture.Index with { Signature = fixture.Sign(PulseLinkOtaSignature.CreateSigningPayload(fixture.Manifest)) });
    await Reject(async () => await fixture.ValidateAsync());
});
Test("未知密钥被拒绝", async fixture => { await fixture.CreateAsync(changeIndex: index => index with { KeyId = "unknown" }); await Reject(async () => await fixture.ValidateAsync()); });
Test("包原始字节被篡改", async fixture =>
{
    await fixture.CreateAsync(); var path = Directory.GetFiles(fixture.Assets, "*.ota.zip").Single();
    var bytes = await File.ReadAllBytesAsync(path); bytes[0] ^= 1; await File.WriteAllBytesAsync(path, bytes);
    await Reject(async () => await fixture.ValidateAsync());
});
Test("外层摘要合法但载荷被篡改", async fixture => { await fixture.CreateAsync(corruptPayload: true); await Reject(async () => await fixture.ValidateAsync()); });
Test("重复清单被拒绝", async fixture => { await fixture.CreateAsync(duplicateManifest: true); await Reject(async () => await fixture.ValidateAsync()); });
Test("清单与索引目标版本不一致", async fixture => { await fixture.CreateAsync(changeManifest: manifest => manifest with { Version = "26.10.09.001" }); await Reject(async () => await fixture.ValidateAsync()); });
Test("标签与签名通道不一致", async fixture => { await fixture.CreateAsync(); await Reject(async () => await fixture.ValidateAsync("ota-beta-win-x64-26.10.08.001")); });
foreach (var path in new[] { "../outside.zip", "ota/stable/win-x64/../outside.zip", "https://evil.example/a.zip", "ota/stable/win-x64/a.zip?evil" })
    Test($"路径逃逸被拒绝 {path}", async fixture => { await fixture.CreateAsync(changeIndex: index => index with { PackagePath = path }); await Reject(async () => await fixture.ValidateAsync()); });
Test("载荷路径逃逸被拒绝", async fixture => { await fixture.CreateAsync(changeManifest: manifest => manifest with { Payload = "../payload.zip" }); await Reject(async () => await fixture.ValidateAsync()); });
Test("未来发布时间被拒绝", async fixture => { await fixture.CreateAsync(changeIndex: index => index with { PublishedAt = DateTimeOffset.UtcNow.AddHours(1) }); await Reject(async () => await fixture.ValidateAsync()); });
Test("最低版本高于目标版本被拒绝", async fixture => { await fixture.CreateAsync(changeIndex: index => index with { MinimumVersion = "99.0.0" }); await Reject(async () => await fixture.ValidateAsync()); });
Test("不支持的平台被拒绝", async fixture => { await fixture.CreateAsync(runtime: "linux-x64"); await Reject(async () => await fixture.ValidateAsync()); });
Test("超大索引被拒绝", async fixture => { await fixture.CreateAsync(); await File.AppendAllTextAsync(Path.Combine(fixture.Assets, "latest.release.json"), new string(' ', 65_536)); await Reject(async () => await fixture.ValidateAsync()); });
Test("多个候选包被拒绝", async fixture => { await fixture.CreateAsync(); File.Copy(Directory.GetFiles(fixture.Assets, "*.ota.zip").Single(), Path.Combine(fixture.Assets, "extra.ota.zip")); await Reject(async () => await fixture.ValidateAsync()); });

var failed = 0;
foreach (var test in tests)
{
    try { await test.Run(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception exception) { failed++; Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}"); }
}
Console.WriteLine($"OTA publisher: {tests.Count - failed}/{tests.Count} passed");
return failed == 0 ? 0 : 1;
