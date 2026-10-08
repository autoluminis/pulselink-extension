namespace PulseLink.SharedKernel;

/// <summary>受信任签名密钥的稳定标识及 PEM 公钥。</summary>
public sealed record PulseLinkTrustedSigningKey(string KeyId, string PublicKeyPem);
