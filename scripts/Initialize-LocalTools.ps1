# Explicit developer action; retrieves pinned archive data, never runs an installer/tool.
[CmdletBinding(DefaultParameterSetName='Archive')]
param(
    [Parameter(Mandatory, ParameterSetName='Archive')][string]$ArchivePath,
    [Parameter(Mandatory, ParameterSetName='Verify')][switch]$VerifyOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows -or $PSVersionTable.PSVersion.Major -lt 7) { throw 'PowerShell 7 on Windows is required.' }
. (Join-Path $PSScriptRoot 'LocalTools.Helpers.ps1')
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$pins = Get-LocalToolPins (Join-Path $repo 'Tools/ffmpeg/SHA256SUMS.txt')
$destination = Join-Path $repo 'artifacts/local-runtime/controlled-9.0-glyphframe.2/Tools/ffmpeg'
if (Test-LocalTools $destination $pins) {
    Write-Host "PASS: existing local FFmpeg/ffprobe match reviewed hashes: $destination"
    return
}
if ($VerifyOnly) { throw 'Controlled video tools are missing. Initialize with the exact reviewed local -ArchivePath, then rebuild. No public download is configured.' }
if (Test-Path -LiteralPath $destination) { throw 'Incomplete local tool cache; inspect/preserve it before retrying. No files were overwritten.' }
$run = New-PortableRunDirectory (Join-Path $repo 'artifacts/tool-acquisition') ([Guid]::NewGuid().ToString('N'))
$archive = [IO.Path]::GetFullPath($ArchivePath)
Install-LocalToolArchive $archive $destination $run $pins
Write-Host "PASS: pinned local tools prepared: $destination"
Write-Host 'No downloaded executable was run. Rebuild before launching video workflows. Downloads/staging retained under ignored artifacts; this does not approve binary redistribution.'
