# build-release.ps1 - builds release\OverItTogether-v<version>.zip
#  1) downloads BepInEx 5 (first run only, into build\)
#  2) builds the mod (needs Getting Over It installed: it compiles against the game's own DLLs)
#  3) builds OverItTogether.exe with the C# compiler that ships with Windows
#  4) packs: OverItTogether.exe, HOW TO PLAY, mod\ (BepInEx + the plugin)
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$build = Join-Path $root "build"
$bep = Join-Path $build "bepinex"
$bepVersion = "5.4.23.5"

if (-not (Test-Path (Join-Path $bep "winhttp.dll"))) {
    Write-Host "Downloading BepInEx $bepVersion..."
    New-Item -ItemType Directory -Force $build | Out-Null
    $zip = Join-Path $build "bepinex.zip"
    Invoke-WebRequest "https://github.com/BepInEx/BepInEx/releases/download/v$bepVersion/BepInEx_win_x64_$bepVersion.zip" -OutFile $zip
    Expand-Archive $zip $bep -Force
}

Write-Host "Building the mod..."
dotnet build (Join-Path $root "src\OverItTogether\OverItTogether.csproj") -c Release -p:NoCopy=true -p:BepInExCore="$bep\BepInEx\core" | Out-Host
if ($LASTEXITCODE -ne 0) { throw "mod build failed" }
$dll = Join-Path $root "src\OverItTogether\bin\Release\net472\OverItTogether.dll"

Write-Host "Building the launcher..."
& (Join-Path $root "launcher\build.bat")

$ver = ([regex]::Match((Get-Content (Join-Path $root "src\OverItTogether\Plugin.cs") -Raw), 'Version = "([^"]+)"')).Groups[1].Value
$stage = Join-Path $env:TEMP "oit-release\OverItTogether"
if (Test-Path (Split-Path $stage)) { Remove-Item (Split-Path $stage) -Recurse -Force }
$mod = Join-Path $stage "mod"
New-Item -ItemType Directory -Force "$mod\BepInEx\plugins\OverItTogether", "$mod\BepInEx\config" | Out-Null
Copy-Item (Join-Path $bep "BepInEx\core") "$mod\BepInEx\core" -Recurse
Copy-Item (Join-Path $bep "winhttp.dll"), (Join-Path $bep "doorstop_config.ini"), (Join-Path $bep ".doorstop_version") $mod
Copy-Item $dll "$mod\BepInEx\plugins\OverItTogether\"
Set-Content "$mod\BepInEx\config\BepInEx.cfg" "[Logging.Console]`nEnabled = false`n`n[Logging.Disk]`nEnabled = true"
Copy-Item (Join-Path $root "launcher\OverItTogether.exe") $stage
Copy-Item (Join-Path $root "docs\HOW_TO_PLAY.md") (Join-Path $stage "HOW TO PLAY.md")

$out = Join-Path $root "release"; New-Item -ItemType Directory -Force $out | Out-Null
$zipOut = Join-Path $out "OverItTogether-v$ver.zip"
if (Test-Path $zipOut) { Remove-Item $zipOut }
Compress-Archive -Path $stage -DestinationPath $zipOut
Write-Host "Built $zipOut"
