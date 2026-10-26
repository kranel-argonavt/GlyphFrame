# Explicit developer action; retrieves pinned archive data, never runs an installer/tool.
[CmdletBinding(DefaultParameterSetName='Archive')]
param(
    [Parameter(Mandatory, ParameterSetName='Archive')][string]$ArchivePath,
    [Parameter(Mandatory, ParameterSetName='Download')][switch]$Download,
    [Parameter(Mandatory, ParameterSetName='Verify')][switch]$VerifyOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 on Windows is required.' }
. (Join-Path $PSScriptRoot 'LocalTools.Helpers.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pins = Get-LocalToolPins (Join-Path $repo 'Tools/ffmpeg/SHA256SUMS.txt')
$destination = Join-Path $repo 'artifacts/local-runtime/Tools/ffmpeg'
if (Test-LocalTools $destination $pins) {
    Write-Host "PASS: existing local FFmpeg/ffprobe match reviewed hashes: $destination"
    return
}
if ($VerifyOnly) { throw 'Local video tools are missing. Initialize with -Download or -ArchivePath, then rebuild.' }
if (Test-Path -LiteralPath $destination) { throw 'Incomplete local tool cache; inspect/preserve it before retrying. No files were overwritten.' }
$run = New-PortableRunDirectory (Join-Path $repo 'artifacts/tool-acquisition') ([Guid]::NewGuid().ToString('N'))
$archive = if ($Download) {
    $path = Join-Path $run 'ffmpeg-9.0-essentials_build.zip'
    Invoke-WebRequest -Uri 'https://github.com/GyanD/codexffmpeg/releases/download/9.0/ffmpeg-9.0-essentials_build.zip' -OutFile $path -TimeoutSec 180
    $path
} else { [IO.Path]::GetFullPath($ArchivePath) }
Install-LocalToolArchive $archive $destination $run $pins
Write-Host "PASS: pinned local tools prepared: $destination"
Write-Host 'No downloaded executable was run. Rebuild before launching video workflows. Downloads/staging retained under ignored artifacts; this does not approve binary redistribution.'
