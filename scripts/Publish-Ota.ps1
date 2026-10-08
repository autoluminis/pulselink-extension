[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $ReleaseTag,
    [Parameter(Mandatory)] [string] $Repository,
    [Parameter(Mandatory)] [string] $RepositoryRoot
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Checked([string] $Command, [string[]] $Arguments) {
    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Command 失败，退出码 $LASTEXITCODE。" }
}

if ($Repository -cne 'autoluminis/pulselink-extension') { throw '仅允许官方发布仓库。' }
if ($ReleaseTag -cnotmatch '^ota-(stable|beta|internal)-win-(x64|x86|arm64)-[0-9]+\.[0-9]+\.[0-9]+(\.[0-9]+)?$') { throw 'OTA Release 标签无效。' }
$root = [IO.Path]::GetFullPath($RepositoryRoot)
$branch = & git -C $root branch --show-current
if ($LASTEXITCODE -ne 0 -or $branch -cne 'release') { throw '只能从 release 分支发布。' }
$working = & git -C $root status --porcelain
if ($LASTEXITCODE -ne 0 -or $working) { throw '发布前工作区必须干净。' }
$work = Join-Path $root '.publisher-output/ota'
$assets = Join-Path $work ([Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $assets | Out-Null
$metadata = Join-Path $work 'metadata.json'
$project = Join-Path $root 'tools/OtaPublisher/OtaPublisher.csproj'
Invoke-Checked gh @('release', 'download', $ReleaseTag, '--repo', $Repository, '--pattern', '*.ota.zip', '--pattern', 'latest.release.json', '--dir', $assets)
Invoke-Checked dotnet @('run', '--project', $project, '--configuration', 'Release', '--', $assets, $root, $ReleaseTag, $metadata, 'validate')
$release = Get-Content -LiteralPath $metadata -Raw | ConvertFrom-Json

# 更新包先公开，签名索引后切换；失败时已有索引继续指向可下载的旧 Release。
# --latest=false 避免不同通道/平台竞争 GitHub 的全局 latest；客户端按固定 tag 下载。
$prerelease = if ($release.Channel -eq 'stable') { '--prerelease=false' } else { '--prerelease=true' }
Invoke-Checked gh @('release', 'edit', $ReleaseTag, '--repo', $Repository, '--draft=false', '--latest=false', $prerelease)
Invoke-Checked dotnet @('run', '--project', $project, '--configuration', 'Release', '--no-build', '--', $assets, $root, $ReleaseTag, $metadata, 'write-index')
Invoke-Checked git @('-C', $root, 'config', 'user.name', 'pulselink-ota[bot]')
Invoke-Checked git @('-C', $root, 'config', 'user.email', 'pulselink-ota[bot]@users.noreply.github.com')
Invoke-Checked git @('-C', $root, 'add', '--', $release.indexPath)
& git -C $root diff --cached --quiet
if ($LASTEXITCODE -eq 0) { Write-Host '相同 OTA 发布物已发布，无需重复提交。'; exit 0 }
if ($LASTEXITCODE -ne 1) { throw '无法检查索引变更。' }
Invoke-Checked git @('-C', $root, 'commit', '-m', "新增：发布系统 OTA $($release.Version) ($($release.Channel))")
Invoke-Checked git @('-C', $root, 'push', 'origin', 'HEAD:release')
