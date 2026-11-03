[CmdletBinding()]
param(
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [ValidateSet('android-arm64','android-arm','android-x64')][string]$Runtime = 'android-arm64',
    [string]$AndroidSdkDirectory = $env:ANDROID_HOME,
    [string]$JavaSdkDirectory = $env:JAVA_HOME,
    [string]$KeyStore,
    [string]$KeyAlias,
    [switch]$InstallWorkload
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$project = Join-Path $PSScriptRoot 'src\WUZHIQI.Android\WUZHIQI.Android.csproj'
$output = Join-Path $PSScriptRoot "artifacts\apk\$Configuration\$Runtime"
try {
    $localConfig = Join-Path $PSScriptRoot 'android-build.local.json'
    if (Test-Path -LiteralPath $localConfig) {
        $local = Get-Content -LiteralPath $localConfig -Raw -Encoding UTF8 | ConvertFrom-Json
        if (-not $AndroidSdkDirectory -and $local.PSObject.Properties['AndroidSdkDirectory']) { $AndroidSdkDirectory = $local.AndroidSdkDirectory }
        if (-not $JavaSdkDirectory -and $local.PSObject.Properties['JavaSdkDirectory']) { $JavaSdkDirectory = $local.JavaSdkDirectory }
        if (-not $env:NUGET_PACKAGES -and $local.PSObject.Properties['NuGetPackagesDirectory']) { $env:NUGET_PACKAGES = $local.NuGetPackagesDirectory }
        if (-not $env:NUGET_HTTP_CACHE_PATH -and $local.PSObject.Properties['NuGetHttpCacheDirectory']) { $env:NUGET_HTTP_CACHE_PATH = $local.NuGetHttpCacheDirectory }
    }
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw '请先安装 .NET 10 SDK。' }
    if ($InstallWorkload) {
        & dotnet workload install android
        if ($LASTEXITCODE -ne 0) { throw 'Android 工作负载安装失败，请以管理员身份安装后重试。' }
    }
    if (-not $AndroidSdkDirectory) { $AndroidSdkDirectory = $env:ANDROID_SDK_ROOT }
    if (-not $AndroidSdkDirectory) { $AndroidSdkDirectory = Join-Path $env:LOCALAPPDATA 'Android\Sdk' }
    if (-not (Test-Path -LiteralPath $AndroidSdkDirectory -PathType Container)) { throw '未找到 Android SDK，请用 -AndroidSdkDirectory 指定路径（需要 API 36 平台和 build-tools）。' }
    if (-not $JavaSdkDirectory -or -not (Test-Path -LiteralPath (Join-Path $JavaSdkDirectory 'bin\javac.exe'))) { throw '未找到 JDK，请设置 JAVA_HOME，或使用 -JavaSdkDirectory 指向 JDK 21。' }
    $arguments = @('publish', $project, '-c', $Configuration, '-f', 'net10.0-android', '-r', $Runtime,
        '-m:1', '-nodeReuse:false',
        '-p:AndroidPackageFormats=apk', '-p:RunAOTCompilation=false', '-p:EmbedAssembliesIntoApk=true',
        "-p:AndroidSdkDirectory=$AndroidSdkDirectory", "-p:JavaSdkDirectory=$JavaSdkDirectory", '-o', $output)
    if ($Configuration -eq 'Debug') {
        $debugKeys = Join-Path $PSScriptRoot 'artifacts\keys'
        New-Item -ItemType Directory -Force -Path $debugKeys | Out-Null
        $arguments += "-p:_ApkDebugKeyStore=$(Join-Path $debugKeys 'debug.keystore')"
    }
    if ($Configuration -eq 'Release') {
        if (-not $KeyStore -or -not (Test-Path -LiteralPath $KeyStore -PathType Leaf) -or -not $KeyAlias) { throw 'Release 构建需要 -KeyStore 和 -KeyAlias。调试安装直接运行本脚本即可。' }
        if (-not $env:WUZHIQI_KEY_PASSWORD -or -not $env:WUZHIQI_STORE_PASSWORD) { throw '请设置 WUZHIQI_KEY_PASSWORD 和 WUZHIQI_STORE_PASSWORD 环境变量。' }
        $keyPath = (Resolve-Path -LiteralPath $KeyStore).Path
        $arguments += @('-p:AndroidKeyStore=true', "-p:AndroidSigningKeyStore=$keyPath", "-p:AndroidSigningKeyAlias=$KeyAlias",
            '-p:AndroidSigningKeyPass=env:WUZHIQI_KEY_PASSWORD', '-p:AndroidSigningStorePass=env:WUZHIQI_STORE_PASSWORD')
    }
    # Run from the project root so global.json chooses the intended SDK.
    Push-Location $PSScriptRoot
    try {
        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) { throw 'APK 构建失败。请检查上面的 .NET/Android 错误。' }
    } finally { Pop-Location }
    $packages = @(Get-ChildItem -LiteralPath $output -Filter '*-Signed.apk' -File)
    if ($packages.Count -eq 0) { throw "没有找到已签名 APK：$output" }
    Write-Host '可安装 APK 已生成：' -ForegroundColor Green
    $packages | ForEach-Object { Write-Host $_.FullName }
} catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
