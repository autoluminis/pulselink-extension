using System.Text.Json;
using PulseLink.OtaPublisher;
using PulseLink.SharedKernel;

// CLI 始终使用内置官方公钥，测试注入的临时密钥不会进入发布命令。
try
{
    if (args.Length != 5 || args[4] is not ("validate" or "write-index")) throw new ArgumentException("用法: OtaPublisher <assets> <repository-root> <release-tag> <metadata-output> <validate|write-index>");
    var keys = PulseLinkTrustRegistry.OtaSigningKeys.ToDictionary(key => key.KeyId, key => key.PublicKeyPem, StringComparer.Ordinal);
    var index = await new OtaPublicationValidator(keys).ValidateAsync(args[0], args[1], args[2]);
    if (args[4] == "write-index") await OtaPublicationValidator.WriteIndexAsync(args[0], args[1], index);
    await File.WriteAllTextAsync(args[3], JsonSerializer.Serialize(new
    {
        releaseTag = args[2],
        indexPath = PulseLinkReleaseDistribution.GetOtaIndexPath(index.Channel!, index.RuntimeIdentifier!),
        packageFileName = Path.GetFileName(index.PackagePath!),
        index.Version,
        index.Channel
    }));
    Console.WriteLine($"OTA 发布物验证通过：{index.Version} / {index.Channel} / {index.RuntimeIdentifier}");
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
return 0;
