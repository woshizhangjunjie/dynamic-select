param(
    [string]$Configuration = "Release",
    [string]$Project = "DailyWords.Mac\DailyWords.Mac.csproj",
    [string[]]$Rids = @("osx-arm64", "osx-x64")
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$dotnet = Join-Path $root ".dotnet\dotnet.exe"
$artifactRoot = Join-Path $root "publish\macos"

if (-not (Test-Path $dotnet)) {
    throw "dotnet sdk not found"
}

if (Test-Path $artifactRoot) {
    Remove-Item -LiteralPath $artifactRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $artifactRoot | Out-Null

foreach ($rid in $Rids) {
    $publishDir = Join-Path $artifactRoot ($rid + "\publish")
    $bundleDir = Join-Path $artifactRoot ($rid + "\DailyWords.app")
    $contentsDir = Join-Path $bundleDir "Contents"
    $macOsDir = Join-Path $contentsDir "MacOS"
    $resourcesDir = Join-Path $contentsDir "Resources"

    & $dotnet publish $Project -c $Configuration -r $rid --self-contained true -p:UseAppHost=true -o $publishDir

    New-Item -ItemType Directory -Path $macOsDir -Force | Out-Null
    New-Item -ItemType Directory -Path $resourcesDir -Force | Out-Null

    Copy-Item -Path (Join-Path $publishDir "*") -Destination $macOsDir -Recurse -Force

    $infoPlist = @"
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>zh_CN</string>
  <key>CFBundleDisplayName</key>
  <string>DailyWords</string>
  <key>CFBundleExecutable</key>
  <string>DailyWordsMac</string>
  <key>CFBundleIdentifier</key>
  <string>com.dailywords.mac</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>DailyWords</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>1.0.0</string>
  <key>CFBundleVersion</key>
  <string>1.0.0</string>
  <key>LSMinimumSystemVersion</key>
  <string>10.14</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
"@

    Set-Content -LiteralPath (Join-Path $contentsDir "Info.plist") -Value $infoPlist -Encoding UTF8

    wsl sh -lc "chmod +x 'publish/macos/$rid/DailyWords.app/Contents/MacOS/DailyWordsMac'"
    wsl sh -lc "cd publish/macos && tar -czf 'DailyWords-$rid-app.tar.gz' '$rid/DailyWords.app'"
    wsl sh -lc "cd publish/macos && tar -a -cf 'DailyWords-$rid-app.zip' '$rid/DailyWords.app'"
}

Write-Host "mac artifacts: $artifactRoot"
