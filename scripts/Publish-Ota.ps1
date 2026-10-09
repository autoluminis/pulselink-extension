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

Invoke-Checked git @('-C', $root, 'config', 'user.name', 'pulselink-ota[bot]')
Invoke-Checked git @('-C', $root, 'config', 'user.email', 'pulselink-ota[bot]@users.noreply.github.com')

# 共用流水线锁不能阻止人工合并，也不会刷新事件绑定的旧提交。
# 每次从远端最新版本创建隔离工作树，重新检查降级和同版本冲突，禁止强推或盲目 rebase 索引。
for ($attempt = 1; $attempt -le 3; $attempt++) {
    Invoke-Checked git @('-C', $root, 'fetch', 'origin', 'refs/heads/release:refs/remotes/origin/release')
    $publicationRoot = Join-Path $work ('index-' + [Guid]::NewGuid().ToString('N'))
    Invoke-Checked git @('-C', $root, 'worktree', 'add', '--detach', $publicationRoot, 'origin/release')
    try {
        Invoke-Checked dotnet @('run', '--project', $project, '--configuration', 'Release', '--no-build', '--', $assets, $publicationRoot, $ReleaseTag, $metadata, 'validate')
        # 更新包先公开，签名索引后切换；失败时已有索引继续指向可下载的旧 Release。
        $prerelease = if ($release.Channel -eq 'stable') { '--prerelease=false' } else { '--prerelease=true' }
        Invoke-Checked gh @('release', 'edit', $ReleaseTag, '--repo', $Repository, '--draft=false', '--latest=false', $prerelease)
        Invoke-Checked dotnet @('run', '--project', $project, '--configuration', 'Release', '--no-build', '--', $assets, $publicationRoot, $ReleaseTag, $metadata, 'write-index')
        Invoke-Checked git @('-C', $publicationRoot, 'add', '--', $release.indexPath)
        & git -C $publicationRoot diff --cached --quiet
        if ($LASTEXITCODE -eq 0) { Write-Host '相同 OTA 发布物已发布，无需重复提交。'; return }
        if ($LASTEXITCODE -ne 1) { throw '无法检查索引变更。' }
        Invoke-Checked git @('-C', $publicationRoot, 'commit', '-m', "新增：发布系统 OTA $($release.Version) ($($release.Channel))")
        & git -C $publicationRoot push origin HEAD:release
        if ($LASTEXITCODE -eq 0) { Write-Host "OTA 发布完成，签名索引已更新：$($release.Version)。"; return }
        if ($attempt -eq 3) { throw 'OTA 索引推送连续失败；Release 附件已保留，请重试正式发布并检查远端权限或分支保护。' }
        Write-Host '索引推送失败，重新读取远端并验签后重试。'
    }
    finally {
        # 仅移除本次随机创建的隔离工作树；不重置调用方 checkout 或其他发布提交。
        Invoke-Checked git @('-C', $root, 'worktree', 'remove', '--force', $publicationRoot)
    }
}
