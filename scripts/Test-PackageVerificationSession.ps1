# Dependency-free regression checks for the actual package verifier helpers. No build/publish or UI.
[CmdletBinding()]
param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'Production-compatible Windows lease checks require PowerShell 7 on Windows.' }
. (Join-Path $PSScriptRoot 'PackageVerification.Session.ps1')

$testRoot = Join-Path ([IO.Path]::GetTempPath()) "GlyphFrame.PackageSessionTests/$([Guid]::NewGuid().ToString('N'))"
$testProcessId = $PID
$passed = 0
[IO.Directory]::CreateDirectory($testRoot) | Out-Null

function Assert-Check([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "Assertion failed: $Message" }
}

function Assert-Throws([scriptblock]$Action, [string]$Message) {
    $thrown = $false
    try { & $Action | Out-Null } catch { $thrown = $true }
    Assert-Check $thrown $Message
}

function Run-Check([string]$Name, [scriptblock]$Action) {
    & $Action
    $script:passed++
    Write-Host "PASS: $Name"
}

function New-TestSession([string]$Name) {
    $data = Join-Path $testRoot $Name
    $cache = Get-PackageArtifactCacheRoot $data
    $session = Join-Path $cache "session-$testProcessId-$([Guid]::NewGuid().ToString('N'))"
    [IO.Directory]::CreateDirectory($session) | Out-Null
    return [pscustomobject]@{ DataRoot = $data; CacheRoot = $cache; SessionDirectory = $session; LeasePath = Join-Path $session '.session.lease' }
}

try {
    Run-Check 'Short temporary data root budgets both production paths and escaped file URIs' {
        $run = [Guid]::NewGuid().ToString('N')
        $plan = Get-PackageVerificationPathPlan ([IO.Path]::GetTempPath()) $run
        Assert-PackageVerificationPathBudget $plan
        Assert-Check ($plan.DataRoot -eq [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "GFpkg/$run"))) 'must use short unique temporary root'
        Assert-Check ($plan.ProspectiveArtifacts.Count -eq 2) 'completed and pending production names checked'
        foreach ($artifact in $plan.ProspectiveArtifacts) {
            Assert-Check ($artifact.PathLength -eq $artifact.Path.Length -and $artifact.FileUriLength -eq $artifact.FileUri.Length) 'actual lengths retained'
            Assert-Check (([Uri]$artifact.FileUri).IsFile) 'absolute file URI checked'
        }
        Assert-Check ($plan.ProspectiveArtifacts[1].Path.EndsWith('.pending.mp4')) 'pending path includes longer suffix'
        Assert-Check (-not [IO.Directory]::Exists($plan.DataRoot)) 'planning must not create evidence'
    }
    Run-Check 'Unsuitable deeply nested temporary base is rejected without fallback' {
        $plan = Get-PackageVerificationPathPlan (Join-Path $testRoot ('nested-' * 20)) ([Guid]::NewGuid().ToString('N'))
        Assert-Check (-not $plan.WithinBudget) 'long base must exceed budget'
        Assert-Throws { Assert-PackageVerificationPathBudget $plan } 'must reject long base before launch'
    }
    Run-Check 'Escaped Unicode URI can exceed budget independently of filesystem path' {
        $plan = Get-PackageVerificationPathPlan (Join-Path 'C:\T' ('Ж' * 20)) ([Guid]::NewGuid().ToString('N'))
        Assert-Check ($plan.ProspectiveArtifacts[1].PathLength -le $plan.MaximumPathLength) 'filesystem path fits'
        Assert-Check ($plan.ProspectiveArtifacts[1].FileUriLength -gt $plan.MaximumFileUriLength) 'escaped URI exceeds budget'
        Assert-Throws { Assert-PackageVerificationPathBudget $plan } 'must reject oversized URI'
    }
    Run-Check 'Path planning rejects relative bases and noncanonical run identities' {
        Assert-Throws { Get-PackageVerificationPathPlan 'relative' ('0' * 32) } 'absolute base required'
        Assert-Throws { Get-PackageVerificationPathPlan ([IO.Path]::GetTempPath()) '../other' } 'run-owned GUID required'
    }
    Run-Check 'Missing cache fails bounded observation without creating evidence' {
        $data = Join-Path $testRoot 'missing'
        Assert-Throws { Wait-PackageActiveSession $data $testProcessId -TimeoutMilliseconds 0 } 'missing cache must fail'
        Assert-Check (-not [IO.Directory]::Exists($data)) 'observer must not create data/cache'
    }
    Run-Check 'Empty cache fails bounded observation' {
        $data = Join-Path $testRoot 'empty'
        [IO.Directory]::CreateDirectory((Get-PackageArtifactCacheRoot $data)) | Out-Null
        Assert-Throws { Wait-PackageActiveSession $data $testProcessId -TimeoutMilliseconds 0 } 'empty cache must fail'
    }
    Run-Check 'Missing lease fails and is never created by observation' {
        $fixture = New-TestSession 'missing-lease'
        Assert-Throws { Wait-PackageActiveSession $fixture.DataRoot $testProcessId -TimeoutMilliseconds 0 } 'missing lease must fail'
        Assert-Check (-not [IO.File]::Exists($fixture.LeasePath)) 'observer must not create lease'
    }
    Run-Check 'Unlocked lease fails active-session observation' {
        $fixture = New-TestSession 'unlocked'
        [IO.File]::WriteAllText($fixture.LeasePath, 'test lease')
        Assert-Throws { Wait-PackageActiveSession $fixture.DataRoot $testProcessId -TimeoutMilliseconds 0 } 'unlocked lease must fail'
    }
    Run-Check 'Actively held production-compatible exclusive lease is accepted' {
        $fixture = New-TestSession 'active'
        $lease = [IO.FileStream]::new($fixture.LeasePath, [IO.FileMode]::OpenOrCreate,
            [IO.FileAccess]::ReadWrite, [IO.FileShare]::None, 256, [IO.FileOptions]::WriteThrough)
        try {
            $evidence = Wait-PackageActiveSession $fixture.DataRoot $testProcessId -TimeoutMilliseconds 0
            Assert-Check ($evidence.SessionDirectory -eq $fixture.SessionDirectory) 'exact session captured'
            Assert-Check ($evidence.LeasePath -eq $fixture.LeasePath) 'production lease filename captured'
            Assert-Check ($evidence.ApplicationProcessId -eq $testProcessId) 'expected process bound'
            Assert-Check ($evidence.LeaseEvidence.Contains('0x80070020')) 'specific sharing violation evidence'
            [DateTimeOffset]::Parse($evidence.ObservedUtc) | Out-Null
        }
        finally { $lease.Dispose() }
        Assert-Check (-not (Test-PackageActiveLease $fixture.LeasePath)) 'released lease is not active'
    }
    Run-Check 'Access denial and unrelated I/O failures are not active-lease evidence' {
        foreach ($exception in @(
            [UnauthorizedAccessException]::new('denied'),
            [IO.IOException]::new('arbitrary I/O'),
            [IO.IOException]::new('missing', -2147024894),
            [IO.IOException]::new('access denied', -2147024891),
            [IO.IOException]::new('byte-range lock (not production sharing)', -2147024863)
        )) {
            Assert-Check (-not (Test-PackageLeaseSharingViolation $exception)) 'only production sharing violation accepted'
        }
        Assert-Check (Test-PackageLeaseSharingViolation ([IO.IOException]::new('sharing', -2147024864))) 'actual HRESULT classified'
    }
    Run-Check 'Foreign process session is not accepted despite an exclusive lease' {
        $fixture = New-TestSession 'foreign'
        $lease = [IO.FileStream]::new($fixture.LeasePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try {
            Assert-Throws { Wait-PackageActiveSession $fixture.DataRoot ($testProcessId + 1) -TimeoutMilliseconds 0 } 'wrong process session must fail'
        }
        finally { $lease.Dispose() }
    }
    Run-Check 'Retained observed session fails shutdown cleanup verification' {
        $fixture = New-TestSession 'retained'
        $lease = [IO.FileStream]::new($fixture.LeasePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try { $evidence = Wait-PackageActiveSession $fixture.DataRoot $testProcessId -TimeoutMilliseconds 0 }
        finally { $lease.Dispose() }
        $cleanup = Get-PackageSessionCleanupEvidence $fixture.DataRoot $evidence.SessionDirectory
        Assert-Check (-not $cleanup.Success) 'retained session must fail'
        Assert-Check (-not $cleanup.SessionRemoved) 'retained session reported accurately'
        Assert-Check ($cleanup.RemainingSessionDirectories.Count -eq 1) 'retained session counted'
    }
    Run-Check 'Removed observed session passes shutdown cleanup verification' {
        $fixture = New-TestSession 'removed'
        $lease = [IO.FileStream]::new($fixture.LeasePath, [IO.FileMode]::CreateNew, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
        try { $evidence = Wait-PackageActiveSession $fixture.DataRoot $testProcessId -TimeoutMilliseconds 0 }
        finally { $lease.Dispose() }
        [IO.File]::Delete($fixture.LeasePath)
        [IO.Directory]::Delete($fixture.SessionDirectory)
        $cleanup = Get-PackageSessionCleanupEvidence $fixture.DataRoot $evidence.SessionDirectory
        Assert-Check $cleanup.Success 'removed observed session must pass'
        Assert-Check $cleanup.SessionRemoved 'removal recorded'
        Assert-Check ($cleanup.RemainingSessionDirectories.Count -eq 0) 'no sessions remain'
    }
    Run-Check 'Another retained session prevents cleanup success after observed session removal' {
        $fixture = New-TestSession 'other-session'
        [IO.Directory]::Delete($fixture.SessionDirectory)
        [IO.Directory]::CreateDirectory((Join-Path $fixture.CacheRoot 'session-other')) | Out-Null
        $cleanup = Get-PackageSessionCleanupEvidence $fixture.DataRoot $fixture.SessionDirectory
        Assert-Check (-not $cleanup.Success) 'any remaining session must fail'
        Assert-Check $cleanup.SessionRemoved 'observed removal distinct from remaining sessions'
    }
    Run-Check 'Unsafe/outside cleanup target and vanished cache cannot pass' {
        $fixture = New-TestSession 'unsafe-cleanup'
        Assert-Throws { Get-PackageSessionCleanupEvidence $fixture.DataRoot (Join-Path $testRoot 'outside') } 'outside scope must fail'
        Assert-Throws { Get-PackageSessionCleanupEvidence (Join-Path $testRoot 'absent') (
            Join-Path (Get-PackageArtifactCacheRoot (Join-Path $testRoot 'absent')) "session-$testProcessId-$([Guid]::NewGuid().ToString('N'))") } 'missing cache cannot prove cleanup'
    }
    Write-Host "Verifier regression checks passed: $passed; failed: 0. No publish or application launch performed."
}
finally {
    # Generated GUID scope under the named temporary test root; never delete a computed broad directory.
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) 'GlyphFrame.PackageSessionTests'))
    $resolvedTestRoot = [IO.Path]::GetFullPath($testRoot)
    if ([IO.Path]::GetDirectoryName($resolvedTestRoot) -eq $allowedRoot -and
        [IO.Path]::GetFileName($resolvedTestRoot) -match '^[0-9a-f]{32}$') {
        Remove-Item -LiteralPath $resolvedTestRoot -Recurse -Force
    }
}
