# 一键生成 Android 版本（Release APK）。
# 用法：./build-android.ps1 [-Configuration Release] [-OutputDir dist\android]
param(
	[string]$Configuration = "Release",
	[string]$OutputDir = "dist/android"
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$project = Join-Path $root "CharacterSkinsMC.App/CharacterSkinsMC.App.csproj"
$androidTfm = "net11.0-android"

Write-Host "== 发布 Android 版本 配置=$Configuration 目标框架=$androidTfm =="

# 多目标工程 publish 必须显式 -f；apk 格式由 csproj 默认值提供，可被 -p: 覆盖。
# 未提供正式 keystore 时自动用 debug 密钥签名，仅限侧载自用。
dotnet publish $project -f $androidTfm -c $Configuration --nologo `
	-p:AndroidPackageFormat=apk

if ($LASTEXITCODE -ne 0) {
	Write-Error "publish 失败，退出码 $LASTEXITCODE"
	exit $LASTEXITCODE
}

# 产物在 App 的 bin/<配置>/<TFM>/publish 下，统一收集到输出目录便于分发。
$publishDir = Join-Path (Split-Path $project) "bin/$Configuration/$androidTfm/publish"
$dest = Join-Path $root $OutputDir
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Get-ChildItem $publishDir -Filter *.apk | ForEach-Object {
	Copy-Item $_.FullName (Join-Path $dest $_.Name) -Force
	Write-Host "已收集：$($_.Name)"
}

Write-Host "== 完成，APK 输出目录：$dest =="
