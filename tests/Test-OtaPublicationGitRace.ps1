# 使用真实本地 Git 远端复现旧 checkout 与推送期间竞争；签名边界另由 OtaPublisher.Tests 验证。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$global:OtaGitExecutable = (Get-Command git.exe).Source
$publisher = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts/Publish-Ota.ps1'

function Invoke-RealGit([string[]] $Arguments) {
    & $global:OtaGitExecutable @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "本地 Git 测试失败：$Arguments" }
}
function Add-RivalCommit([bool] $Newer) {
    if ($Newer) {
        $path = Join-Path $global:OtaRival 'ota/stable/win-x64'
        New-Item -ItemType Directory -Force -Path $path | Out-Null
        '{"version":"26.10.10.001"}' | Set-Content (Join-Path $path 'latest.release.json')
    }
    '保留插件发布变更' | Set-Content (Join-Path $global:OtaRival 'plugin.txt')
    Invoke-RealGit @('-C', $global:OtaRival, 'add', '.')
    Invoke-RealGit @('-C', $global:OtaRival, 'commit', '-m', '新增：模拟并发插件发布')
    Invoke-RealGit @('-C', $global:OtaRival, 'push', 'origin', 'HEAD:release')
}
function global:git {
    if ($args -contains 'push') {
        $global:OtaPushCount++
        if ($global:OtaPushCount -eq 1 -and $global:OtaScenario -in @('race', 'newer')) { Add-RivalCommit ($global:OtaScenario -eq 'newer') }
    }
    & $global:OtaGitExecutable @args
}
function global:gh { $global:LASTEXITCODE = 0 }
function global:dotnet {
    $global:LASTEXITCODE = 0
    $root = $args[-4]
    $path = Join-Path $root 'ota/stable/win-x64/latest.release.json'
    if (Test-Path $path) {
        $current = Get-Content $path -Raw | ConvertFrom-Json
        if ([Version]$current.version -gt [Version]'26.10.09.001') { $global:LASTEXITCODE = 1; return }
    }
    @{ indexPath = 'ota/stable/win-x64/latest.release.json'; Version = '26.10.09.001'; Channel = 'stable' } | ConvertTo-Json | Set-Content $args[-2]
    if ($args[-1] -eq 'write-index') {
        New-Item -ItemType Directory -Force -Path (Split-Path $path -Parent) | Out-Null
        '{"version":"26.10.09.001"}' | Set-Content $path
    }
}

foreach ($scenario in @('stale', 'race', 'newer')) {
    $global:OtaScenario = $scenario
    $global:OtaPushCount = 0
    $taskRoot = Join-Path ([IO.Path]::GetTempPath()) ('pulselink-ota-git-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $taskRoot | Out-Null
    try {
        $remote = Join-Path $taskRoot 'remote.git'
        $root = Join-Path $taskRoot 'checkout'
        $global:OtaRival = Join-Path $taskRoot 'rival'
        Invoke-RealGit @('init', '--bare', '--initial-branch=release', $remote)
        Invoke-RealGit @('clone', $remote, $root)
        Invoke-RealGit @('-C', $root, 'config', 'user.name', 'test')
        Invoke-RealGit @('-C', $root, 'config', 'user.email', 'test@example.com')
        '.publisher-output/' | Set-Content (Join-Path $root '.gitignore')
        Invoke-RealGit @('-C', $root, 'add', '.')
        Invoke-RealGit @('-C', $root, 'commit', '-m', '新增：初始化隔离发布测试')
        Invoke-RealGit @('-C', $root, 'push', 'origin', 'release')
        Invoke-RealGit @('clone', $remote, $global:OtaRival)
        Invoke-RealGit @('-C', $global:OtaRival, 'config', 'user.name', 'test')
        Invoke-RealGit @('-C', $global:OtaRival, 'config', 'user.email', 'test@example.com')
        $original = & $global:OtaGitExecutable -C $root rev-parse HEAD
        if ($scenario -eq 'stale') { Add-RivalCommit $false }
        $thrown = $false
        try { & $publisher -ReleaseTag 'ota-stable-win-x64-26.10.09.001' -Repository 'autoluminis/pulselink-extension' -RepositoryRoot $root }
        catch { $thrown = $true }
        if ($thrown -ne ($scenario -eq 'newer')) { throw "失败传播错误：$scenario" }
        $index = (& $global:OtaGitExecutable --git-dir=$remote show release:ota/stable/win-x64/latest.release.json) | ConvertFrom-Json
        $expected = if ($scenario -eq 'newer') { '26.10.10.001' } else { '26.10.09.001' }
        if ($index.version -ne $expected) { throw '索引被错误推进或降级。' }
        & $global:OtaGitExecutable --git-dir=$remote show release:plugin.txt | Out-Null
        if ($LASTEXITCODE -ne 0) { throw '并发插件提交被丢弃。' }
        if ((& $global:OtaGitExecutable -C $root rev-parse HEAD) -ne $original) { throw '调用方 checkout 被重置。' }
        if (@(& $global:OtaGitExecutable -C $root worktree list).Count -ne 1) { throw '隔离工作树未清理。' }
        if ($scenario -eq 'stale') {
            $before = & $global:OtaGitExecutable --git-dir=$remote rev-parse release
            & $publisher -ReleaseTag 'ota-stable-win-x64-26.10.09.001' -Repository 'autoluminis/pulselink-extension' -RepositoryRoot $root
            if ((& $global:OtaGitExecutable --git-dir=$remote rev-parse release) -ne $before) { throw '相同发布重试产生了重复提交。' }
        }
        Write-Host "PASS 真实 Git 发布竞争：$scenario"
    }
    finally {
        $cleanup = [IO.Path]::GetFullPath($taskRoot)
        $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
        if (-not $cleanup.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($cleanup) -notmatch '^pulselink-ota-git-[a-f0-9]{32}$') { throw '测试清理目录越界。' }
        Remove-Item -LiteralPath $cleanup -Recurse -Force
    }
}
Write-Host 'OTA Git race: 3/3 passed'
