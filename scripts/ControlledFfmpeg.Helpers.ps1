# Offline experimental build support. No acquisition, activation, history rewrite or cleanup.
. (Join-Path $PSScriptRoot 'PortablePackage.Helpers.ps1')

function Get-ControlledSourceLock([string]$Path) {
    $lock = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($lock.SchemaVersion -ne 2 -or $lock.Profile -cne 'windows-x64-static-gpl3-experimental-2' -or
        $lock.ToolVersion -cne '9.0-glyphframe.2' -or $lock.SourceDateEpoch -le 0 -or
        $lock.PublicDistributionApproved -isnot [bool] -or $lock.PublicDistributionApproved -or
        $lock.WarningReviewComplete -isnot [bool] -or $lock.WarningReviewComplete -or
        $lock.CorrespondingSourceApproved -isnot [bool] -or $lock.CorrespondingSourceApproved -or @($lock.Patches).Count -ne 1) {
        throw 'Expected the unapproved version-2 experimental profile with one explicit downstream patch.'
    }
    $patch=$lock.Patches[0]
    if ($patch.Component -cne 'ffmpeg' -or $patch.File -cne 'patches/0001-vvc-weighted-scaled-contract.patch' -or
        $patch.SHA256 -cnotmatch '^[0-9a-f]{64}$' -or @($patch.Files).Count -ne 1 -or
        $patch.Files[0].Path -cne 'libavcodec/vvc/inter_template.c' -or
        $patch.Files[0].BeforeSHA256 -cnotmatch '^[0-9a-f]{64}$' -or $patch.Files[0].AfterSHA256 -cnotmatch '^[0-9a-f]{64}$' -or
        $patch.Files[0].BeforeSHA256 -ceq $patch.Files[0].AfterSHA256) { throw 'Unsupported or incomplete downstream patch identity.' }
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $lock.Artifacts) {
        if ($entry.Name -cnotin @('ffmpeg','x264','zlib','x264-history') -or -not $names.Add($entry.Name) -or
            $entry.Revision -cnotmatch '^[0-9a-f]{40}$' -or $entry.SHA256 -cnotmatch '^[0-9a-f]{64}$' -or
            $entry.File -cnotmatch '^[a-z0-9][a-z0-9.-]+\.(tar\.gz|bundle)$' -or $entry.File.Contains('..') -or
            -not $files.Add($entry.File) -or
            (($entry.Name -ceq 'x264-history') -ne $entry.File.EndsWith('.bundle',[StringComparison]::Ordinal))) { throw 'Unsafe, duplicate or invalid source lock entry.' }
    }
    if ($names.Count -ne 4 -or ($lock.Artifacts | Where-Object Name -eq 'x264').Revision -cne
        ($lock.Artifacts | Where-Object Name -eq 'x264-history').Revision) { throw 'Incomplete or inconsistent source set.' }
    return $lock
}

function Assert-ControlledInputFiles([string]$Directory, $Lock) {
    Assert-PortableDirectoryChain $Directory
    foreach ($entry in $Lock.Artifacts) {
        $path = Join-Path $Directory $entry.File
        if (-not [IO.File]::Exists($path) -or ([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            [IO.FileInfo]::new($path).Length -eq 0 -or [IO.FileInfo]::new($path).Length -gt 200MB -or
            (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -cne $entry.SHA256) { throw "Missing, linked, oversized or altered source input: $($entry.File)" }
    }
}

function Assert-ControlledPatchFiles([string]$Directory, $Lock) {
    foreach ($patch in $Lock.Patches) {
        $path=Join-Path $Directory $patch.File
        Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($path))
        if (-not [IO.File]::Exists($path) -or ([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            [IO.FileInfo]::new($path).Length -eq 0 -or [IO.FileInfo]::new($path).Length -gt 64KB -or
            (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -cne $patch.SHA256) { throw 'Missing, linked, oversized or altered downstream patch.' }
        $text=[IO.File]::ReadAllText($path)
        if ($text.Contains("`r") -or $text.Contains([char]0) -or -not $text.EndsWith("`n")) { throw 'Patch must be NUL-free canonical LF text with a final newline.' }
    }
}

function Get-ControlledTreeHashes([string]$Directory) {
    Assert-PortableDirectoryChain $Directory
    $hashes=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
    foreach ($entry in Get-ChildItem -LiteralPath $Directory -Force -Recurse) {
        if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'Linked source tree entry.' }
        if (-not $entry.PSIsContainer) {
            $relative=[IO.Path]::GetRelativePath($Directory,$entry.FullName).Replace('\','/')
            Assert-PortableRelativePath $relative
            $hashes.Add($relative,(Get-FileHash -LiteralPath $entry.FullName).Hash.ToLowerInvariant())
        }
    }
    return $hashes
}

function Assert-ControlledPatchedTree($Before, $After, $Patch) {
    if ($Before.Count -ne $After.Count) { throw 'Downstream patch added/removed a source file.' }
    foreach ($entry in $Patch.Files) {
        if (-not $Before.ContainsKey($entry.Path) -or -not $After.ContainsKey($entry.Path) -or
            $Before[$entry.Path] -cne $entry.BeforeSHA256 -or $After[$entry.Path] -cne $entry.AfterSHA256) { throw 'Patched source preimage/postimage mismatch.' }
    }
    foreach ($path in $Before.Keys) {
        if (-not $After.ContainsKey($path)) { throw 'Downstream patch changed the source path set.' }
        if (@($Patch.Files | Where-Object Path -CEQ $path).Count -eq 0 -and $Before[$path] -cne $After[$path]) { throw 'Downstream patch changed an unlisted source file.' }
    }
}

function Invoke-ControlledSourcePatches([string]$Directory,[string]$PatchRoot,$Lock,[string]$MsysRoot,[string]$LogDirectory) {
    Assert-ControlledPatchFiles $PatchRoot $Lock
    foreach ($patch in $Lock.Patches) {
        $before=Get-ControlledTreeHashes $Directory
        foreach ($entry in $patch.Files) {
            if (-not $before.ContainsKey($entry.Path) -or $before[$entry.Path] -cne $entry.BeforeSHA256) { throw 'Patch requires the exact pristine preimage; no fuzzy/repeated application.' }
        }
        $path=Join-Path $PatchRoot $patch.File
        $git=Join-Path $MsysRoot 'usr/bin/git.exe'
        # --no-index works outside Git; never uses this project's index or records a commit.
        $arguments=@('-c','core.autocrlf=false','apply','--no-index','--whitespace=error-all')
        $null=Invoke-ControlledProcess $git ($arguments+@('--check','--',$path)) $Directory (Join-Path $LogDirectory 'patch-check') $MsysRoot
        $null=Invoke-ControlledProcess $git ($arguments+@('--',$path)) $Directory (Join-Path $LogDirectory 'patch-apply') $MsysRoot
        $after=Get-ControlledTreeHashes $Directory
        Assert-ControlledPatchedTree $before $after $patch
        Assert-ControlledPatchFiles $PatchRoot $Lock
        [ordered]@{File=$patch.File;SHA256=$patch.SHA256;SourceFiles=$after.Count;Files=$patch.Files;Verified=$true} |
            ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $LogDirectory 'patch-result.json') -Encoding utf8NoBOM
    }
}

function Get-ControlledRuntimeLock([string]$Path, $ToolchainLock) {
    $lock=Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($lock.SchemaVersion -ne 1 -or $lock.Profile -cne 'windows-x64-static-gpl3-experimental-1' -or
        $lock.PublicDistributionApproved -isnot [bool] -or $lock.PublicDistributionApproved -or
        @($lock.Components).Count -ne 3 -or @($lock.Notices).Count -ne 6 -or @($lock.Objects).Count -ne 11) {
        throw 'Incomplete or approved runtime material cannot use the experimental workflow.'
    }
    $components=[Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($component in $lock.Components) {
        $package=@($ToolchainLock.Packages | Where-Object Name -CEQ $component.Package)
        if ($component.Name -cnotin @('gcc-runtime','mingw-runtime','winpthreads') -or -not $components.Add($component.Name) -or
            $package.Count -ne 1 -or $package[0].Version -cne $component.Version -or $component.Upstream -cnotmatch '^https://') {
            throw 'Runtime component does not match the pinned package identity.'
        }
    }
    $required=@('notices/gcc/COPYING3.txt','notices/gcc/COPYING.RUNTIME.txt','notices/mingw/COPYING.txt',
        'notices/mingw/COPYING.MinGW-w64-runtime.txt','notices/mingw/COPYING.MinGW-w64.txt','notices/winpthreads/COPYING.txt')
    $paths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $hostPaths=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $lock.Notices) {
        Assert-PortableRelativePath $entry.Path
        Assert-PortableRelativePath $entry.HostPath
        if ($entry.Path -cnotin $required -or -not $paths.Add($entry.Path) -or -not $hostPaths.Add($entry.HostPath) -or
            $entry.HostPath -cnotmatch '^ucrt64/share/licenses/(libgcc|crt|winpthreads)/[A-Za-z0-9.-]+$' -or
            -not $components.Contains($entry.Component) -or $entry.SHA256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Invalid runtime notice identity/path.' }
    }
    foreach ($entry in $lock.Objects) {
        Assert-PortableRelativePath $entry.Path
        if ($entry.Path -cnotmatch '^ucrt64/lib/(gcc/x86_64-w64-mingw32/[0-9.]+/)?[a-z0-9]+\.(a|o)$' -or
            -not $paths.Add($entry.Path) -or -not $components.Contains($entry.Component) -or $entry.SHA256 -cnotmatch '^[0-9a-f]{64}$') {
            throw 'Invalid/duplicate static runtime object identity.'
        }
    }
    foreach ($name in @('libgcc.a','crtbegin.o','crtend.o','crt2.o','libmingw32.a','libmingwex.a','libmsvcrt.a','libpthread.a','libbcrypt.a','libkernel32.a','libshell32.a')) {
        if (@($lock.Objects | Where-Object { [IO.Path]::GetFileName($_.Path) -ceq $name }).Count -ne 1) { throw 'Required static runtime material is absent.' }
    }
    return $lock
}

function Assert-ControlledRuntimeFiles([string]$NoticeRoot, $Lock, [string]$MsysRoot='') {
    $checks=@(foreach ($entry in $Lock.Notices) { @{Root=$NoticeRoot;Path=$entry.Path;SHA256=$entry.SHA256} })
    if ($MsysRoot) {
        $checks+=@(foreach ($entry in $Lock.Notices) { @{Root=$MsysRoot;Path=$entry.HostPath;SHA256=$entry.SHA256} })
        $checks+=@(foreach ($entry in $Lock.Objects) { @{Root=$MsysRoot;Path=$entry.Path;SHA256=$entry.SHA256} })
    }
    foreach ($entry in $checks) {
        Assert-PortableRelativePath $entry.Path
        $path=Join-Path $entry.Root $entry.Path
        Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($path))
        if (-not [IO.File]::Exists($path) -or ([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            [IO.FileInfo]::new($path).Length -eq 0 -or (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -cne $entry.SHA256) {
            throw "Missing, linked or changed runtime material: $($entry.Path)"
        }
    }
}

function Assert-ControlledArchiveEntries([string[]]$Entries) {
    if ($Entries.Count -lt 2) { throw 'Empty source archive.' }
    $prefix = ($Entries[0] -split '/')[0]
    if ($prefix -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._+-]+$') { throw 'Invalid archive root.' }
    $seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $files = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $directories = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $Entries) {
        if (-not $entry.StartsWith("$prefix/",[StringComparison]::Ordinal) -or $entry -match '\\|:|(^|/)\.\.(/|$)|(^|/)\.git(/|$)|[\x00-\x1f]' -or
            -not $seen.Add($entry.TrimEnd('/'))) { throw 'Unsafe or conflicting source archive path.' }
        $name=$entry.TrimEnd('/')
        $parts=$name.Split('/')
        for ($i=1;$i -lt $parts.Count;$i++) {
            $parent=$parts[0..($i-1)] -join '/'
            if ($files.Contains($parent)) { throw 'Archive file is used as a directory.' }
            [void]$directories.Add($parent)
        }
        if ($entry.EndsWith('/')) { [void]$directories.Add($name) }
        else {
            if ($directories.Contains($name)) { throw 'Archive directory is replaced by a file.' }
            [void]$files.Add($name)
        }
    }
}

function Assert-ControlledArchiveTypes([string[]]$Entries) {
    if ($Entries.Count -eq 0 -or @($Entries | Where-Object { $_ -notmatch '^[d-][rwxstST-]{9}\s' }).Count -ne 0) { throw 'Only regular source files/directories are allowed; links and special entries are rejected.' }
}

function Assert-ControlledBuildDestination([string]$Directory,[string[]]$ProtectedDirectories) {
    Assert-PortableDirectoryChain $Directory
    $absolute=[IO.Path]::GetFullPath($Directory).TrimEnd('\','/')
    if ($absolute -match '\s' -or $absolute.Length -gt 160 -or
        [IO.Path]::GetFileName($absolute) -cnotmatch '^[A-Za-z0-9][A-Za-z0-9._-]{2,63}$' -or
        [IO.Path]::GetFileName($absolute) -match '^(?i:con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\..*)?$' -or
        [IO.Directory]::Exists($absolute) -or [IO.File]::Exists($absolute)) { throw 'Build root must be a new, short, non-root directory without spaces.' }
    foreach ($protected in $ProtectedDirectories) {
        $path=[IO.Path]::GetFullPath($protected).TrimEnd('\','/')
        if ($absolute.Equals($path,[StringComparison]::OrdinalIgnoreCase) -or
            $absolute.StartsWith($path+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase) -or
            $path.StartsWith($absolute+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)) { throw 'Build root overlaps source inputs or the toolchain.' }
    }
    return $absolute
}

function Assert-ControlledToolchainFiles([string]$Root,$Lock) {
    Assert-PortableDirectoryChain $Root
    if ($Lock.SchemaVersion -ne 1 -or $Lock.Environment -cne 'UCRT64' -or @($Lock.Files).Count -lt 10 -or @($Lock.Packages).Count -lt 20) { throw 'Incomplete pinned toolchain inventory.' }
    $seen=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($entry in $Lock.Files) {
        Assert-PortableRelativePath $entry.Path
        if ($entry.Path -cnotmatch '^(usr|ucrt64)/(bin|lib)/' -or -not $seen.Add($entry.Path) -or $entry.SHA256 -cnotmatch '^[0-9a-f]{64}$') { throw 'Invalid toolchain file pin.' }
        $path=Join-Path $Root $entry.Path
        Assert-PortableDirectoryChain ([IO.Path]::GetDirectoryName($path))
        if (-not [IO.File]::Exists($path) -or ([IO.File]::GetAttributes($path) -band [IO.FileAttributes]::ReparsePoint) -ne 0 -or
            (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant() -cne $entry.SHA256) { throw "Toolchain file identity changed: $($entry.Path)" }
    }
    foreach ($required in @('usr/bin/bash.exe','usr/bin/make.exe','usr/bin/git.exe','usr/bin/tar.exe','usr/bin/msys-2.0.dll','ucrt64/bin/gcc.exe','ucrt64/bin/nasm.exe','ucrt64/bin/ld.exe')) {
        if (-not $seen.Contains($required)) { throw 'Toolchain lock omits a critical executable.' }
    }
}

function Assert-ControlledPackageInventory([string[]]$Actual,$Lock) {
    $expected=@($Lock.Packages | ForEach-Object { "$($_.Name) $($_.Version)" })
    if (@($expected | Select-Object -Unique).Count -ne $expected.Count -or
        @($Lock.Packages.Name | Select-Object -Unique).Count -ne $expected.Count -or
        @(Compare-Object ($expected | Sort-Object) @($Actual | Where-Object { $_ } | Sort-Object)).Count -ne 0) { throw 'Installed package inventory does not match the pinned environment; no auto-update/downgrade is performed.' }
}

function Invoke-ControlledProcess([string]$Executable,[string[]]$NativeArguments,[string]$WorkingDirectory,[string]$LogPrefix,[string]$MsysRoot='') {
    $start=[Diagnostics.ProcessStartInfo]::new($Executable)
    $start.UseShellExecute=$false; $start.CreateNoWindow=$true
    $start.WorkingDirectory=$WorkingDirectory
    $start.RedirectStandardOutput=$true; $start.RedirectStandardError=$true
    # An allowlist also excludes less common overrides (ASFLAGS, GCC_EXEC_PREFIX,
    # PKG_CONFIG, MAKEFILES, etc.) instead of chasing an incomplete denylist.
    $allowed=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($key in @('SystemRoot','WINDIR','ComSpec','TEMP','TMP','PATHEXT','OS','PROCESSOR_ARCHITECTURE',
        'NUMBER_OF_PROCESSORS','USERPROFILE','USERNAME','USERDOMAIN','LOCALAPPDATA','APPDATA')) { [void]$allowed.Add($key) }
    foreach ($key in @($start.Environment.Keys)) { if (-not $allowed.Contains($key)) { [void]$start.Environment.Remove($key) } }
    $start.Environment['PATH']="$env:SystemRoot/System32;$env:SystemRoot"
    if ($MsysRoot) {
        $start.Environment['PATH']="$MsysRoot/ucrt64/bin;$MsysRoot/usr/bin;$env:SystemRoot/System32;$env:SystemRoot"
        $start.Environment['MSYSTEM']='UCRT64'
        $start.Environment['CHOST']='x86_64-w64-mingw32'
        $start.Environment['MSYSTEM_CHOST']='x86_64-w64-mingw32'
        $start.Environment['GIT_CONFIG_NOSYSTEM']='1'
        $start.Environment['GIT_CONFIG_GLOBAL']='/dev/null'
    }
    foreach ($argument in $NativeArguments) { $start.ArgumentList.Add($argument) }
    $process=[Diagnostics.Process]::new(); $process.StartInfo=$start
    $started=$false
    try {
        $started=$process.Start()
        if (-not $started) { throw 'Owned build process could not start.' }
        $stdout=$process.StandardOutput.ReadToEndAsync(); $stderr=$process.StandardError.ReadToEndAsync()
        $process.WaitForExit()
        [IO.File]::WriteAllText("$LogPrefix.stdout.txt",$stdout.GetAwaiter().GetResult())
        [IO.File]::WriteAllText("$LogPrefix.stderr.txt",$stderr.GetAwaiter().GetResult())
        if ($process.ExitCode -ne 0) { throw "Owned build step exit $($process.ExitCode); inspect $LogPrefix logs." }
        return [IO.File]::ReadAllText("$LogPrefix.stdout.txt")
    } finally { if ($started -and -not $process.HasExited) { $process.Kill($true) }; $process.Dispose() }
}
