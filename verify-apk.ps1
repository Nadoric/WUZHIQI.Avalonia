[CmdletBinding()]
param(
    [string]$ApkPath,
    [string]$AndroidSdkDirectory = $env:ANDROID_HOME,
    [string]$JavaSdkDirectory = $env:JAVA_HOME
)
$ErrorActionPreference = 'Stop'
try {
    if (-not $ApkPath) { $ApkPath = Join-Path $PSScriptRoot 'artifacts\apk\Debug\android-arm64\com.wuzhiqi.gomoku-Signed.apk' }
    $configPath = Join-Path $PSScriptRoot 'android-build.local.json'
    if (Test-Path -LiteralPath $configPath) {
        $local = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $AndroidSdkDirectory) { $AndroidSdkDirectory = $local.AndroidSdkDirectory }
        if (-not $JavaSdkDirectory) { $JavaSdkDirectory = $local.JavaSdkDirectory }
    }
    if (-not (Test-Path -LiteralPath $ApkPath -PathType Leaf)) { throw "找不到 APK：$ApkPath" }
    if (-not $JavaSdkDirectory -or -not $AndroidSdkDirectory) { throw '请指定 Android SDK 和 JDK 路径。' }
    $env:JAVA_HOME = $JavaSdkDirectory
    $tools = Join-Path $AndroidSdkDirectory 'build-tools\36.0.0'
    & (Join-Path $tools 'apksigner.bat') verify --verbose --print-certs $ApkPath
    if ($LASTEXITCODE -ne 0) { throw 'APK 签名校验失败。' }
    & (Join-Path $tools 'zipalign.exe') -c -P 16 4 $ApkPath
    if ($LASTEXITCODE -ne 0) { throw 'APK 对齐校验失败。' }
    & (Join-Path $tools 'aapt2.exe') dump badging $ApkPath
    if ($LASTEXITCODE -ne 0) { throw 'APK Manifest 读取失败。' }
    Write-Host "APK 签名、16 KB ZIP 对齐和 Manifest 校验通过：$ApkPath" -ForegroundColor Green
    Get-FileHash -LiteralPath $ApkPath -Algorithm SHA256
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
