# Inert validation regressions. No acquisition, compilation, input execution or Git writes.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'ControlledFfmpeg.Helpers.ps1')
$parent=Join-Path ([IO.Path]::GetTempPath()) 'GlyphFrame.ControlledFfmpegTests'
$id=[Guid]::NewGuid().ToString('N'); $root=New-PortableRunDirectory $parent $id
$passed=0
function Assert-Check([bool]$Condition,[string]$Message) { if (-not $Condition) { throw $Message } }
function Assert-Throws([scriptblock]$Action) { $caught=$false; try { & $Action | Out-Null } catch { $caught=$true }; Assert-Check $caught 'Expected fail-closed rejection.' }
function Run-Check([string]$Name,[scriptblock]$Action) { & $Action; $script:passed++; Write-Host "PASS: $Name" }
function Fresh-Lock { return Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ffmpeg/sources.lock.json') -Raw | ConvertFrom-Json }
function Check-Lock($Lock) { $path=Join-Path $root 'lock.json'; $Lock | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $path -Encoding utf8NoBOM; Get-ControlledSourceLock $path | Out-Null }
try {
    Run-Check 'Standalone build-material helpers load without application-native terms tooling' {
        $standalone=Join-Path $root 'standalone-helpers'
        [void][IO.Directory]::CreateDirectory($standalone)
        foreach ($name in @('ControlledFfmpeg.Helpers.ps1','PortablePackage.Helpers.ps1','PackageLegal.Helpers.ps1')) {
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot $name) -Destination (Join-Path $standalone $name)
        }
        Push-Location -LiteralPath $standalone
        try {
            & pwsh -NoProfile -Command '$ErrorActionPreference="Stop"; . ./ControlledFfmpeg.Helpers.ps1; foreach($name in @("Get-ControlledSourceLock","Assert-PortableRelativePath","Get-LegalTextHash")){if(-not(Get-Command $name -ErrorAction SilentlyContinue)){throw "Standalone helper missing"}}; if(Get-Command New-IsolatedNativeTermsPreviewFixture -ErrorAction SilentlyContinue){throw "Unrelated application-native fixture leaked into tool sources"}'
            Assert-Check ($LASTEXITCODE -eq 0) 'Standalone source helper dependency closure failed.'
        } finally { Pop-Location }
    }
    Run-Check 'Reviewed profile contains four pinned inputs, one explicit patch and no approval' {
        $lock=Get-ControlledSourceLock (Join-Path $PSScriptRoot 'ffmpeg/sources.lock.json')
        Assert-Check ($lock.Artifacts.Count -eq 4 -and $lock.Patches.Count -eq 1 -and -not $lock.PublicDistributionApproved -and -not $lock.CorrespondingSourceApproved) 'Wrong source-lock state.'
    }
    Run-Check 'Public, source and warning approvals cannot be silently enabled or stringified' {
        foreach ($field in @('PublicDistributionApproved','CorrespondingSourceApproved','WarningReviewComplete')) {
            foreach ($value in @($true,'false')) { $lock=Fresh-Lock; $lock.$field=$value; Assert-Throws {Check-Lock $lock} }
        }
    }
    Run-Check 'Unsupported schema, profile, version and epoch rejected' {
        foreach ($field in @('SchemaVersion','Profile','ToolVersion','SourceDateEpoch')) { $lock=Fresh-Lock; $lock.$field=if($field -in @('SchemaVersion','SourceDateEpoch')){0}else{'unsupported'}; Assert-Throws {Check-Lock $lock} }
    }
    Run-Check 'Missing, duplicate and unknown components rejected' {
        $lock=Fresh-Lock; $lock.Artifacts=@($lock.Artifacts | Select-Object -First 3); Assert-Throws {Check-Lock $lock}
        $lock=Fresh-Lock; $lock.Artifacts[1].Name='ffmpeg'; Assert-Throws {Check-Lock $lock}
        $lock=Fresh-Lock; $lock.Artifacts[1].Name='unknown'; Assert-Throws {Check-Lock $lock}
    }
    Run-Check 'Unsafe artifact names, invalid hashes and revisions rejected' {
        foreach ($name in @('../outside.tar.gz','C:outside.tar.gz','nested/source.tar.gz','source..tar.gz')) { $lock=Fresh-Lock; $lock.Artifacts[0].File=$name; Assert-Throws {Check-Lock $lock} }
        $lock=Fresh-Lock; $lock.Artifacts[0].SHA256='not-a-hash'; Assert-Throws {Check-Lock $lock}
        $lock=Fresh-Lock; $lock.Artifacts[0].Revision='short'; Assert-Throws {Check-Lock $lock}
    }
    Run-Check 'Version-history mismatch, wrong archive kind and unreviewed patches rejected' {
        $lock=Fresh-Lock; $lock.Artifacts[3].Revision='a'*40; Assert-Throws {Check-Lock $lock}
        $lock=Fresh-Lock; $lock.Artifacts[0].File='unexpected.bundle'; Assert-Throws {Check-Lock $lock}
        $lock=Fresh-Lock; $lock.Patches=@('unreviewed.patch'); Assert-Throws {Check-Lock $lock}
    }
    Run-Check 'Patch removal, duplication, component/path changes and invalid pre/postimages rejected' {
        $copy=Fresh-Lock; $copy.Patches=@(); Assert-Throws {Check-Lock $copy}
        $copy=Fresh-Lock; $copy.Patches+=@($copy.Patches[0]); Assert-Throws {Check-Lock $copy}
        foreach($field in @('Component','File','SHA256')) { $copy=Fresh-Lock; $copy.Patches[0].$field='unsupported'; Assert-Throws {Check-Lock $copy} }
        foreach($field in @('Path','BeforeSHA256','AfterSHA256')) { $copy=Fresh-Lock; $copy.Patches[0].Files[0].$field='unsupported'; Assert-Throws {Check-Lock $copy} }
        $copy=Fresh-Lock; $copy.Patches[0].Files[0].AfterSHA256=$copy.Patches[0].Files[0].BeforeSHA256; Assert-Throws {Check-Lock $copy}
        $copy=Fresh-Lock; $copy.Patches[0].Files=@(); Assert-Throws {Check-Lock $copy}
    }
    Run-Check 'Exact downstream patch bytes accepted as data only' { Assert-ControlledPatchFiles (Join-Path $PSScriptRoot 'ffmpeg') (Fresh-Lock) }
    $patchRoot=Join-Path $root 'patch-fixture'; [void][IO.Directory]::CreateDirectory((Join-Path $patchRoot 'patches'))
    $patchLock=Fresh-Lock; $patchPath=Join-Path $patchRoot $patchLock.Patches[0].File
    [IO.File]::WriteAllText($patchPath,"inert patch data`n")
    $patchLock.Patches[0].SHA256=(Get-FileHash $patchPath).Hash.ToLowerInvariant()
    Run-Check 'Changed/missing/CRLF/NUL/oversized patch rejected before application' {
        Assert-ControlledPatchFiles $patchRoot $patchLock
        $saved=[IO.File]::ReadAllBytes($patchPath)
        [IO.File]::Move($patchPath,"$patchPath.saved"); Assert-Throws {Assert-ControlledPatchFiles $patchRoot $patchLock}; [IO.File]::Move("$patchPath.saved",$patchPath)
        foreach($text in @("altered`n","inert`r`n","inert$([char]0)`n",'no newline')) {
            [IO.File]::WriteAllText($patchPath,$text)
            $copy=Fresh-Lock; $copy.Patches[0].SHA256=(Get-FileHash $patchPath).Hash.ToLowerInvariant()
            if($text -eq "altered`n") {Assert-Throws {Assert-ControlledPatchFiles $patchRoot $patchLock}}
            else {Assert-Throws {Assert-ControlledPatchFiles $patchRoot $copy}}
        }
        $stream=[IO.File]::OpenWrite($patchPath);try{$stream.SetLength(64KB+1)}finally{$stream.Dispose()}
        Assert-Throws {Assert-ControlledPatchFiles $patchRoot $patchLock}; [IO.File]::WriteAllBytes($patchPath,$saved)
    }
    Run-Check 'Only locked pre/postimage may change; full path/hash set preserved' {
        $patch=(Fresh-Lock).Patches[0]; $path=$patch.Files[0].Path
        $before=[Collections.Generic.Dictionary[string,string]]::new([StringComparer]::Ordinal)
        $before.Add($path,$patch.Files[0].BeforeSHA256);$before.Add('unrelated.c','same')
        $after=[Collections.Generic.Dictionary[string,string]]::new($before,[StringComparer]::Ordinal);$after[$path]=$patch.Files[0].AfterSHA256
        Assert-ControlledPatchedTree $before $after $patch
        foreach($kind in @('unlisted','before','after','add','remove')) {
            $b=[Collections.Generic.Dictionary[string,string]]::new($before,[StringComparer]::Ordinal)
            $a=[Collections.Generic.Dictionary[string,string]]::new($after,[StringComparer]::Ordinal)
            switch($kind){'unlisted'{$a['unrelated.c']='changed'};'before'{$b[$path]='wrong'};'after'{$a[$path]='wrong'};'add'{$a.Add('added.c','x')};'remove'{[void]$a.Remove('unrelated.c')}}
            Assert-Throws {Assert-ControlledPatchedTree $b $a $patch}
        }
    }
    $inputs=Join-Path $root 'inputs'; [void][IO.Directory]::CreateDirectory($inputs)
    $lock=Fresh-Lock
    foreach ($entry in $lock.Artifacts) {
        [IO.File]::WriteAllText((Join-Path $inputs $entry.File),"inert source data $($entry.Name)")
        $entry.SHA256=(Get-FileHash -LiteralPath (Join-Path $inputs $entry.File)).Hash.ToLowerInvariant()
    }
    Run-Check 'Exact local input bytes accepted without extraction or execution' { Assert-ControlledInputFiles $inputs $lock }
    Run-Check 'Missing or changed inputs rejected' {
        $file=Join-Path $inputs $lock.Artifacts[0].File; $original=[IO.File]::ReadAllBytes($file)
        [IO.File]::Move($file,"$file.saved"); Assert-Throws {Assert-ControlledInputFiles $inputs $lock}; [IO.File]::Move("$file.saved",$file)
        [IO.File]::AppendAllText($file,'changed'); Assert-Throws {Assert-ControlledInputFiles $inputs $lock}; [IO.File]::WriteAllBytes($file,$original)
    }
    Run-Check 'Oversized inputs rejected before hashing/extraction' {
        $file=Join-Path $inputs $lock.Artifacts[0].File; $original=[IO.File]::ReadAllBytes($file)
        $stream=[IO.File]::OpenWrite($file); try {$stream.SetLength(200MB+1)} finally {$stream.Dispose()}
        Assert-Throws {Assert-ControlledInputFiles $inputs $lock}; [IO.File]::WriteAllBytes($file,$original)
    }
    Run-Check 'Ordinary archive tree and dotfiles accepted' { Assert-ControlledArchiveEntries @('source-abc/','source-abc/.gitignore','source-abc/src/file.c') }
    Run-Check 'Traversal, absolute paths, ADS, backslashes and embedded Git metadata rejected' {
        foreach ($path in @('source-abc/../escape','/outside','C:/outside','source-abc/file:stream','source-abc\file','source-abc/.git/config')) {Assert-Throws {Assert-ControlledArchiveEntries @('source-abc/',$path)}}
    }
    Run-Check 'Empty, multi-root and case-conflicting archives rejected' {
        Assert-Throws {Assert-ControlledArchiveEntries @()}
        Assert-Throws {Assert-ControlledArchiveEntries @('source-abc/','other/file')}
        Assert-Throws {Assert-ControlledArchiveEntries @('source-abc/','source-abc/file','source-abc/FILE')}
    }
    Run-Check 'File/directory prefix conflicts rejected in either order' {
        Assert-Throws {Assert-ControlledArchiveEntries @('source-abc/','source-abc/parent','source-abc/parent/child.c')}
        Assert-Throws {Assert-ControlledArchiveEntries @('source-abc/','source-abc/parent/child.c','source-abc/parent')}
    }
    Run-Check 'Only regular tar files and directories accepted' {
        Assert-ControlledArchiveTypes @('drwxrwxr-x  0 root root 0 Jan 1 source/','-rw-rw-r--  0 root root 1 Jan 1 source/file')
        foreach ($type in @('l','h','b','c','p')) {Assert-Throws {Assert-ControlledArchiveTypes @("${type}rwxrwxrwx 0 root root 0 Jan 1 source/link")}}
    }
    $fresh=Join-Path $root 'fresh-run'
    Run-Check 'New short build root accepted without creating it' { $resolved=Assert-ControlledBuildDestination $fresh @($inputs); Assert-Check ($resolved -ceq $fresh -and -not (Test-Path -LiteralPath $fresh)) 'Unexpected destination mutation.' }
    Run-Check 'Existing, overlapping, relative, whitespace and overly long build roots rejected' {
        foreach ($path in @($root,$inputs,(Join-Path $inputs 'child'),(Join-Path $root 'with space'),'relative-run',(Join-Path $root ('x'*170)),(Join-Path $root 'NUL'))) { Assert-Throws {Assert-ControlledBuildDestination $path @($inputs)} }
    }
    $toolRoot=Join-Path $root 'toolchain'; [void][IO.Directory]::CreateDirectory($toolRoot)
    $toolPaths=@('usr/bin/bash.exe','usr/bin/make.exe','usr/bin/git.exe','usr/bin/tar.exe','usr/bin/msys-2.0.dll','ucrt64/bin/gcc.exe','ucrt64/bin/nasm.exe','ucrt64/bin/ld.exe','ucrt64/bin/ar.exe','ucrt64/bin/ranlib.exe')
    $toolFiles=@(foreach ($path in $toolPaths) { $file=Join-Path $toolRoot $path; [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file)); [IO.File]::WriteAllText($file,'inert, never execute'); [pscustomobject]@{Path=$path;SHA256=(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()} })
    $tools=[pscustomobject]@{SchemaVersion=1;Environment='UCRT64';Files=$toolFiles;Packages=@(1..20 | ForEach-Object {[pscustomobject]@{Name="fixture-$_";Version='1.0'}})}
    Run-Check 'Exact critical tool file identities accepted as data only' { Assert-ControlledToolchainFiles $toolRoot $tools }
    Run-Check 'Changed tool executable rejected before invocation' { $file=Join-Path $toolRoot 'usr/bin/bash.exe'; [IO.File]::AppendAllText($file,'changed'); Assert-Throws {Assert-ControlledToolchainFiles $toolRoot $tools} }
    Run-Check 'Unsupported or incomplete host inventory rejected' {
        $copy=$tools | ConvertTo-Json -Depth 6 | ConvertFrom-Json; $copy.Environment='MINGW64'; Assert-Throws {Assert-ControlledToolchainFiles $toolRoot $copy}
        $copy=$tools | ConvertTo-Json -Depth 6 | ConvertFrom-Json; $copy.Files=@(); Assert-Throws {Assert-ControlledToolchainFiles $toolRoot $copy}
    }
    $inventory=@($tools.Packages | ForEach-Object {"$($_.Name) $($_.Version)"})
    Run-Check 'Pinned package inventory permits ordering and final empty lines' { Assert-ControlledPackageInventory @($inventory[19..0]+@('')) $tools }
    Run-Check 'Missing, added, altered or duplicate package inventory rejected' {
        foreach ($actual in @(@($inventory[0..18]),@($inventory+@('extra 1.0')),@($inventory[0..18]+@('fixture-20 2.0')))) { Assert-Throws {Assert-ControlledPackageInventory $actual $tools} }
        $copy=$tools | ConvertTo-Json -Depth 6 | ConvertFrom-Json; $copy.Packages+=@($copy.Packages[0]); Assert-Throws {Assert-ControlledPackageInventory $inventory $copy}
    }
    Run-Check 'Unapproved input cannot create a build root or execute inert tools' {
        Assert-Throws {& (Join-Path $PSScriptRoot 'Build-ControlledFfmpeg.ps1') -SourceDirectory $inputs -MsysRoot $toolRoot -BuildRoot $fresh}
        Assert-Check (-not (Test-Path -LiteralPath $fresh)) 'Rejected inputs created a build root.'
    }
    Run-Check 'Recipe remains LF-only with offline Git and no nonfree flags' {
        $recipe=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'ffmpeg/build.sh'))
        Assert-Check (-not $recipe.Contains("`r") -and $recipe.Contains('protocol.allow=never') -and -not $recipe.Contains('--enable-nonfree')) 'Unexpected recipe policy.'
    }
    Run-Check 'Owned process ignores ambient compiler and startup overrides' {
        $names=@('CFLAGS','ASFLAGS','GCC_EXEC_PREFIX','PKG_CONFIG','MAKEFILES','BASH_ENV'); $saved=@{}
        try {
            foreach ($name in $names) { $saved[$name]=[Environment]::GetEnvironmentVariable($name,'Process'); [Environment]::SetEnvironmentVariable($name,'inert-poison','Process') }
            $command='[pscustomobject]@{ CFLAGS=$env:CFLAGS; ASFLAGS=$env:ASFLAGS; GCC_EXEC_PREFIX=$env:GCC_EXEC_PREFIX; PKG_CONFIG=$env:PKG_CONFIG; MAKEFILES=$env:MAKEFILES; BASH_ENV=$env:BASH_ENV; Path=$env:PATH; Root=$env:SystemRoot } | ConvertTo-Json -Compress'
            $output=Invoke-ControlledProcess (Join-Path $PSHOME 'pwsh.exe') @('-NoProfile','-Command',$command) $root (Join-Path $root 'environment-probe') | ConvertFrom-Json
            foreach ($name in $names) { Assert-Check ([string]::IsNullOrEmpty($output.$name)) 'Ambient override leaked.' }
            # pwsh may prepend its own installation directory after process startup.
            $allowedPaths=@($PSHOME,"$env:SystemRoot/System32",$env:SystemRoot) | ForEach-Object {[IO.Path]::GetFullPath($_).TrimEnd('\')}
            $actualPaths=@($output.Path -split ';' | Where-Object {$_} | ForEach-Object {[IO.Path]::GetFullPath($_).TrimEnd('\')})
            Assert-Check ($actualPaths.Count -in @(2,3) -and @($actualPaths | Where-Object {$_ -notin $allowedPaths}).Count -eq 0 -and
                $output.Root -ceq $env:SystemRoot) "Unexpected native child environment: $($output.Path)"
        } finally { foreach ($name in $names) { [Environment]::SetEnvironmentVariable($name,$saved[$name],'Process') } }
    }
    Run-Check 'Owned process nonzero exit cannot be reported as success' {
        Assert-Throws {Invoke-ControlledProcess (Join-Path $PSHOME 'pwsh.exe') @('-NoProfile','-Command','exit 7') $root (Join-Path $root 'failed-process')}
    }
    $runtimePath=Join-Path $PSScriptRoot 'ffmpeg/runtime.lock.json'
    $hostLock=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ffmpeg/toolchain.lock.json') -Raw | ConvertFrom-Json
    $runtime=Get-ControlledRuntimeLock $runtimePath $hostLock
    function Fresh-Runtime { Get-Content -LiteralPath $runtimePath -Raw | ConvertFrom-Json }
    function Check-Runtime($Value) { $file=Join-Path $root 'runtime.json'; $Value | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $file -Encoding utf8NoBOM; Get-ControlledRuntimeLock $file $hostLock | Out-Null }
    Run-Check 'Runtime collection retains six exact installed notices and no approval' {
        Assert-ControlledRuntimeFiles (Join-Path $PSScriptRoot 'ffmpeg') $runtime
    }
    Run-Check 'Runtime schema, profile, approval and incomplete sets rejected' {
        foreach ($field in @('SchemaVersion','Profile','PublicDistributionApproved','Notices','Objects','Components')) {
            $copy=Fresh-Runtime
            $copy.$field=switch($field){'SchemaVersion'{0};'Profile'{'unknown'};'PublicDistributionApproved'{$true};default{@()}}
            Assert-Throws {Check-Runtime $copy}
        }
        $copy=Fresh-Runtime; $copy.PublicDistributionApproved='false'; Assert-Throws {Check-Runtime $copy}
    }
    Run-Check 'Runtime package-version mismatch and duplicate component rejected' {
        $copy=Fresh-Runtime; $copy.Components[0].Version='changed'; Assert-Throws {Check-Runtime $copy}
        $copy=Fresh-Runtime; $copy.Components[1]=$copy.Components[0]; Assert-Throws {Check-Runtime $copy}
    }
    Run-Check 'Runtime paths, components, hashes and duplicates fail closed' {
        foreach ($field in @('Path','HostPath','Component','SHA256')) {
            $copy=Fresh-Runtime; $copy.Notices[0].$field=if($field -in @('Path','HostPath')){'../escape'}else{'invalid'}; Assert-Throws {Check-Runtime $copy}
        }
        $copy=Fresh-Runtime; $copy.Notices[1]=$copy.Notices[0]; Assert-Throws {Check-Runtime $copy}
        $copy=Fresh-Runtime; $copy.Objects[1]=$copy.Objects[0]; Assert-Throws {Check-Runtime $copy}
        $copy=Fresh-Runtime; $copy.Objects[0].Path='ucrt64/lib/unreviewed.a'; Assert-Throws {Check-Runtime $copy}
    }
    $fakeNotices=Join-Path $root 'runtime-notices'; $fakeHost=Join-Path $root 'runtime-host'; $inert=Fresh-Runtime
    foreach ($entry in $inert.Notices) {
        foreach ($file in @((Join-Path $fakeNotices $entry.Path),(Join-Path $fakeHost $entry.HostPath))) {
            [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file)); [IO.File]::WriteAllText($file,'inert notice fixture')
        }
        $entry.SHA256=(Get-FileHash -LiteralPath (Join-Path $fakeNotices $entry.Path)).Hash.ToLowerInvariant()
    }
    foreach ($entry in $inert.Objects) {
        $file=Join-Path $fakeHost $entry.Path; [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file)); [IO.File]::WriteAllText($file,'inert object fixture')
        $entry.SHA256=(Get-FileHash -LiteralPath $file).Hash.ToLowerInvariant()
    }
    Run-Check 'Runtime host/retained notices and static objects verified without executing inputs' { Assert-ControlledRuntimeFiles $fakeNotices $inert $fakeHost }
    Run-Check 'Changed host object cannot inherit retained runtime review' {
        $file=Join-Path $fakeHost $inert.Objects[0].Path; $saved=[IO.File]::ReadAllBytes($file)
        [IO.File]::AppendAllText($file,'changed'); Assert-Throws {Assert-ControlledRuntimeFiles $fakeNotices $inert $fakeHost}; [IO.File]::WriteAllBytes($file,$saved)
    }
    Run-Check 'Missing or changed notice rejected independently of objects' {
        $file=Join-Path $fakeNotices $inert.Notices[0].Path; $saved=[IO.File]::ReadAllBytes($file)
        [IO.File]::Move($file,"$file.saved"); Assert-Throws {Assert-ControlledRuntimeFiles $fakeNotices $inert}; [IO.File]::Move("$file.saved",$file)
        [IO.File]::AppendAllText($file,'changed'); Assert-Throws {Assert-ControlledRuntimeFiles $fakeNotices $inert $fakeHost}; [IO.File]::WriteAllBytes($file,$saved)
        $file=Join-Path $fakeHost $inert.Notices[0].HostPath
        [IO.File]::AppendAllText($file,'changed'); Assert-Throws {Assert-ControlledRuntimeFiles $fakeNotices $inert $fakeHost}
    }
    Write-Host "Controlled FFmpeg helper checks passed: $passed; failed: 0. No network, compilation, input execution or Git writes."
} finally {
    $resolved=[IO.Path]::GetFullPath($root)
    Assert-PortableDirectoryChain $resolved
    if ([IO.Path]::GetDirectoryName($resolved) -cne [IO.Path]::GetFullPath($parent) -or [IO.Path]::GetFileName($resolved) -cne $id -or
        [IO.File]::ReadAllText((Join-Path $resolved '.glyphframe-portable-package-run')) -cne $id) { throw 'Unsafe owned-fixture cleanup target.' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
