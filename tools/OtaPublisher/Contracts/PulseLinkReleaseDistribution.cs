namespace PulseLink.SharedKernel;

/// <summary>编译到客户端内的官方制品发布路径；现场部署不可修改。</summary>
public static class PulseLinkReleaseDistribution
{
    /// <summary>当前随程序发布的发布源选择。发布工程可在代码评审后切换，现场配置不能覆盖。</summary>
    public const PulseLinkReleaseSource ActiveSource = PulseLinkReleaseSource.GitHubRaw;

    /// <summary>Nginx 静态托管根目录。</summary>
    public const string NginxBaseUrl = "https://release.veio.cn/";

    /// <summary>GitHub Raw 静态托管根目录，与 Nginx 保持完全相同的目录结构。</summary>
    public const string GitHubRawBaseUrl = "https://raw.githubusercontent.com/autoluminis/pulselink-extension/release/";

    /// <summary>官方 OTA 大文件使用 GitHub Releases，避免 Git 单文件限制及 LFS 指针进入下载链路。</summary>
    public const string GitHubReleaseBaseUrl = "https://github.com/autoluminis/pulselink-extension/releases/download/";

    /// <summary>当前内置发布源的根地址。</summary>
    public static string BaseUrl => ActiveSource switch
    {
        PulseLinkReleaseSource.NginxStatic => NginxBaseUrl,
        PulseLinkReleaseSource.GitHubRaw => GitHubRawBaseUrl,
        _ => throw new InvalidOperationException("未注册的官方发布源。")
    };

    /// <summary>稳定 OTA 通道；测试或灰度通道须在对应程序版本中显式内置。</summary>
    public const string OtaChannel = "stable";

    /// <summary>测试 OTA 通道。</summary>
    public const string BetaOtaChannel = "beta";

    /// <summary>内部验证 OTA 通道。</summary>
    public const string InternalOtaChannel = "internal";

    /// <summary>OTA 发布目录根路径。</summary>
    public const string OtaDirectory = "ota";

    /// <summary>每个通道和运行时目录内的当前发布索引文件名。</summary>
    public const string OtaIndexFileName = "latest.release.json";

    /// <summary>通知插件商城目录的索引相对路径。</summary>
    public const string NotificationMarketplaceIndexPath = "notifications/index.json";

    /// <summary>返回通道和运行时标识对应的 OTA 发布目录。</summary>
    public static string GetOtaDirectory(string channel, string runtimeIdentifier)
        => $"{OtaDirectory}/{ValidateOtaChannel(channel)}/{ValidateSegment(runtimeIdentifier, nameof(runtimeIdentifier))}";

    /// <summary>验证并规范化编译期注册的 OTA 发布通道。</summary>
    public static string ValidateOtaChannel(string? channel)
    {
        var normalized = string.IsNullOrWhiteSpace(channel) ? OtaChannel : channel.Trim();
        return normalized switch
        {
            OtaChannel or BetaOtaChannel or InternalOtaChannel => normalized,
            _ => throw new ArgumentException("OTA 发布通道只能是 stable、beta 或 internal。", nameof(channel))
        };
    }

    /// <summary>返回通道和运行时标识对应的 OTA 索引路径。</summary>
    public static string GetOtaIndexPath(string channel, string runtimeIdentifier) => $"{GetOtaDirectory(channel, runtimeIdentifier)}/{OtaIndexFileName}";

    /// <summary>返回通道和运行时标识对应的官方 OTA 索引地址。</summary>
    public static Uri GetOtaIndexUri(string channel, string runtimeIdentifier) => GetUri(GetOtaIndexPath(channel, runtimeIdentifier));

    /// <summary>返回版本化 OTA 包在官方发布源中的相对路径。</summary>
    public static string GetOtaPackagePath(string channel, string runtimeIdentifier, string packageFileName)
    {
        var fileName = Path.GetFileName(packageFileName);
        if (!string.Equals(fileName, packageFileName, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(fileName) || packageFileName.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-' and not '_') || fileName is "." or "..") throw new ArgumentException("OTA 包文件名无效。", nameof(packageFileName));
        return $"{GetOtaDirectory(channel, runtimeIdentifier)}/{fileName}";
    }

    /// <summary>从已验签的索引字段推导唯一官方 Release 标签，保留版本中的前导零。</summary>
    public static string GetOtaReleaseTag(string channel, string runtimeIdentifier, string version)
    {
        if (!Version.TryParse(version, out var parsed) || parsed.Build < 0 || version.Any(character => !char.IsAsciiDigit(character) && character != '.')) throw new ArgumentException("OTA 版本格式无效。", nameof(version));
        return $"ota-{ValidateOtaChannel(channel)}-{ValidateSegment(runtimeIdentifier, nameof(runtimeIdentifier))}-{version}";
    }

    /// <summary>索引仍签署受控相对路径；GitHub 源将大包定位至对应 Release 的同名资产。</summary>
    public static Uri GetOtaPackageUri(PulseLinkOtaReleaseIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);
        var fileName = Path.GetFileName(index.PackagePath ?? string.Empty);
        var expectedPath = GetOtaPackagePath(index.Channel!, index.RuntimeIdentifier!, fileName);
        if (!string.Equals(index.PackagePath, expectedPath, StringComparison.Ordinal)) throw new ArgumentException("OTA 包路径不符合官方目录规则。", nameof(index));
        var tag = GetOtaReleaseTag(index.Channel!, index.RuntimeIdentifier!, index.Version!);
        return ActiveSource == PulseLinkReleaseSource.GitHubRaw ? new Uri($"{GitHubReleaseBaseUrl}{tag}/{fileName}") : GetUri(expectedPath);
    }

    /// <summary>仅允许官方源和 GitHub Release 资产主机；签名摘要仍验证最终下载字节。</summary>
    public static IReadOnlySet<string> GetOtaPackageTrustedHosts() => ActiveSource == PulseLinkReleaseSource.GitHubRaw
        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com" }
        : new HashSet<string>(StringComparer.OrdinalIgnoreCase) { new Uri(BaseUrl).Host };

    /// <summary>基地址与相对发布路径拼接为唯一官方地址。</summary>
    public static Uri GetUri(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath.StartsWith('/') || relativePath.Contains('\\') || relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..")) throw new ArgumentException("发布路径无效。", nameof(relativePath));
        return new Uri(new Uri(BaseUrl), relativePath);
    }

    /// <summary>返回官方通知插件商城索引地址。</summary>
    public static Uri GetNotificationMarketplaceIndexUri() => GetUri(NotificationMarketplaceIndexPath);

    private static string ValidateSegment(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_')) throw new ArgumentException("发布路径段无效。", parameterName);
        return value;
    }
}
