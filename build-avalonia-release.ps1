$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'src\ClawJump.Avalonia\ClawJump.Avalonia.csproj'
$output = Join-Path $PSScriptRoot 'publish\avalonia-win-x64'

Write-Host 'Cleaning old Avalonia publish directory...' -ForegroundColor Cyan

if (Test-Path $output) {
    Remove-Item -LiteralPath $output -Recurse -Force
}

Write-Host 'Publishing Claw Jump Avalonia...' -ForegroundColor Cyan

dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:EnableCompressionInSingleFile=true -o $output

Write-Host ''
Write-Host 'Publish completed:' -ForegroundColor Green
Write-Host (Join-Path $output 'ClawJump.exe')
