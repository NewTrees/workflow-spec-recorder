param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishDir = Join-Path $repoRoot "dist\ApaFlowRecorderSelfContained"
$installerScript = Join-Path $repoRoot "installer\ApaFlowRecorder.iss"
$installerOutput = Join-Path $repoRoot "dist\installer"
$dotnet = "C:\Program Files\dotnet\dotnet.exe"
$isccCandidates = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$iscc = $isccCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not (Test-Path -LiteralPath $dotnet)) {
    throw "未找到 dotnet：$dotnet"
}

if (-not $iscc) {
    throw "未找到 Inno Setup 编译器 ISCC.exe。请先安装 Inno Setup 6，或运行：winget install --id JRSoftware.InnoSetup -e"
}

Push-Location $repoRoot
try {
    $repoRootFull = [System.IO.Path]::GetFullPath($repoRoot)
    $publishDirFull = [System.IO.Path]::GetFullPath($publishDir)
    if (-not $publishDirFull.StartsWith($repoRootFull, [StringComparison]::OrdinalIgnoreCase)) {
        throw "发布目录不在仓库内，拒绝清理：$publishDirFull"
    }

    if (Test-Path -LiteralPath $publishDir) {
        Remove-Item -LiteralPath $publishDir -Recurse -Force
    }

    & $dotnet publish "src\ApaFlowRecorder.Desktop\ApaFlowRecorder.Desktop.csproj" `
        -c $Configuration `
        -r $Runtime `
        --self-contained true `
        -p:PublishSingleFile=false `
        -o $publishDir

    Copy-Item -LiteralPath (Join-Path $repoRoot "extension") -Destination (Join-Path $publishDir "extension") -Recurse -Force
    Copy-Item -LiteralPath (Join-Path $repoRoot "README.md") -Destination (Join-Path $publishDir "README.md") -Force

    $rootReadme = Join-Path (Split-Path $repoRoot -Parent) "先看我-WorkflowSpecRecorder使用说明.txt"
    if (Test-Path -LiteralPath $rootReadme) {
        Copy-Item -LiteralPath $rootReadme -Destination (Join-Path $publishDir "先看我-WorkflowSpecRecorder使用说明.txt") -Force
    }

    New-Item -ItemType Directory -Path $installerOutput -Force | Out-Null
    & $iscc $installerScript
}
finally {
    Pop-Location
}
