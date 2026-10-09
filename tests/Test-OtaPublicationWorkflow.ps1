# 仅验证编排顺序与失败边界；密码学和 ZIP 完整性由 OtaPublisher.Tests 验证。
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$publisher = Join-Path (Split-Path -Parent $PSScriptRoot) 'scripts/Publish-Ota.ps1'
$global:OtaMockSteps = [Collections.Generic.List[string]]::new()
$global:OtaMockFailure = ''
$global:OtaMockRoot = ''
$global:OtaMockPushes = 0

function global:git {
    $global:LASTEXITCODE = 0
    if ($args -contains 'branch') { return 'release' }
    if ($args -contains 'status') { return }
    if ($args -contains 'fetch') { $global:OtaMockSteps.Add('fetch'); return }
    if ($args -contains 'worktree') {
        if ($args -contains 'add') { New-Item -ItemType Directory -Path $args[-2] | Out-Null }
        return
    }
    if ($args -contains 'diff') { $global:LASTEXITCODE = 1; return }
    if ($args -contains 'push') {
        $global:OtaMockSteps.Add('push')
        $global:OtaMockPushes++
        if ($global:OtaMockFailure -eq 'push' -or ($global:OtaMockFailure -in @('race', 'newer') -and $global:OtaMockPushes -eq 1)) { $global:LASTEXITCODE = 1 }
    }
}
function global:gh {
    $global:LASTEXITCODE = 0
    if ($args -contains 'download') { $global:OtaMockSteps.Add('download'); return }
    if ($args -contains 'edit') {
        $global:OtaMockSteps.Add('publish')
        if ($global:OtaMockFailure -eq 'publish') { $global:LASTEXITCODE = 1 }
        return
    }
    throw '测试遇到未知 gh 调用。'
}
function global:dotnet {
    $global:LASTEXITCODE = 0
    $mode = $args[-1]
    $global:OtaMockSteps.Add($mode)
    if ($global:OtaMockFailure -eq $mode) { $global:LASTEXITCODE = 1; return }
    if ($mode -eq 'validate' -and $global:OtaMockFailure -eq 'newer' -and $global:OtaMockPushes -gt 0) { $global:LASTEXITCODE = 1; return }
    if ($mode -eq 'validate') {
        $metadata = @{ indexPath = 'ota/stable/win-x64/latest.release.json'; Version = '26.10.08.001'; Channel = 'stable' }
        $metadata | ConvertTo-Json | Set-Content -LiteralPath $args[-2] -Encoding utf8
        return
    }
    if ($mode -eq 'write-index') {
        Set-Content -LiteralPath (Join-Path $global:OtaMockRoot 'index-advanced') -Value 'published' -Encoding utf8
        return
    }
    throw '测试遇到未知 dotnet 调用。'
}

foreach ($failure in @('', 'validate', 'publish', 'write-index', 'push', 'race', 'newer')) {
    $global:OtaMockSteps.Clear()
    $global:OtaMockFailure = $failure
    $global:OtaMockPushes = 0
    $global:OtaMockRoot = Join-Path ([IO.Path]::GetTempPath()) ('pulselink-ota-orchestration-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $global:OtaMockRoot | Out-Null
    try {
        $thrown = $false
        try { & $publisher -ReleaseTag 'ota-stable-win-x64-26.10.08.001' -Repository 'autoluminis/pulselink-extension' -RepositoryRoot $global:OtaMockRoot }
        catch { $thrown = $true }
        if ($thrown -ne ($failure -notin @('', 'race'))) { throw "失败传播不正确：$failure" }
        $expected = switch ($failure) {
            'validate' { 'download,validate' }
            'publish' { 'download,validate,fetch,validate,publish' }
            'write-index' { 'download,validate,fetch,validate,publish,write-index' }
            'push' { 'download,validate,fetch,validate,publish,write-index,push,fetch,validate,publish,write-index,push,fetch,validate,publish,write-index,push' }
            'race' { 'download,validate,fetch,validate,publish,write-index,push,fetch,validate,publish,write-index,push' }
            'newer' { 'download,validate,fetch,validate,publish,write-index,push,fetch,validate' }
            default { 'download,validate,fetch,validate,publish,write-index,push' }
        }
        if (($global:OtaMockSteps -join ',') -ne $expected) { throw "发布顺序错误：$($global:OtaMockSteps -join ',')" }
        $hasIndex = Test-Path -LiteralPath (Join-Path $global:OtaMockRoot 'index-advanced')
        if ($hasIndex -ne ($failure -in @('', 'push', 'race', 'newer'))) { throw "未上线资产提前推进了索引：$failure" }
        Write-Host "PASS 发布编排 $failure"
    }
    finally {
        # 此目录在本测试中直接创建，绝对路径不来自发布制品或外部输入。
        $cleanup = [IO.Path]::GetFullPath($global:OtaMockRoot)
        $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
        if (-not $cleanup.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($cleanup) -notmatch '^pulselink-ota-orchestration-[a-f0-9]{32}$') { throw '测试清理目录越界。' }
        Remove-Item -LiteralPath $cleanup -Recurse -Force
    }
}
Write-Host 'OTA orchestration: 7/7 passed'
