<#
.SYNOPSIS
    Project tasks for ScrumPulse: build, test, verify, git setup and wiki publishing.

.DESCRIPTION
    Replaces init-repo.ps1, push-wiki.ps1 and inspect.ps1, which were three scripts with
    overlapping assumptions and no shared guard rails.

    Run from the repository root.

.PARAMETER Task
    build    Restore and compile the backend and the Angular app.
    test     Run backend, frontend and end-to-end suites.
    verify   Static checks: truncated files, secrets, stray build output. No compiler needed.
    init     Create the git repository and commit the tree in logical units.
    wiki     Publish wiki\*.md to the GitHub wiki repository.
    inspect  Reflect over the Microsoft.Agents.AI assembly, for diagnosing SDK shape changes.
    all      verify, then build, then test.

.EXAMPLE
    .\scrumpulse.ps1 verify
    .\scrumpulse.ps1 build
    .\scrumpulse.ps1 init
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('build', 'test', 'verify', 'init', 'wiki', 'inspect', 'all', 'help')]
    [string] $Task = 'help',

    # Used by 'init'. Matches the GitHub account that owns the repository and the wiki.
    [string] $AuthorName  = 'Ranjitha Savandaiah',
    [string] $AuthorEmail = 'lsranjitha@gmail.com',

    [string] $WikiRemote  = 'https://github.com/RanjithaSavandaiah/ScrumPulse.wiki.git',

    # Skips the Playwright run, which needs a built app and a free port.
    [switch] $SkipE2E
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$RepoRoot = $PSScriptRoot
$UiPath   = Join-Path $RepoRoot 'src\ScrumPulse.UI'
$Solution = Join-Path $RepoRoot 'ScrumPulse.slnx'

# ---------------------------------------------------------------------------
# Output helpers
# ---------------------------------------------------------------------------

function Write-Step { param([string] $Text) Write-Host "`n==> $Text" -ForegroundColor Cyan }
function Write-Good { param([string] $Text) Write-Host "    $Text" -ForegroundColor Green }
function Write-Warn { param([string] $Text) Write-Host "    $Text" -ForegroundColor Yellow }
function Write-Bad  { param([string] $Text) Write-Host "    $Text" -ForegroundColor Red }

function Assert-RepoRoot {
    if (-not (Test-Path $Solution)) {
        throw "Run this from the repository root (the folder containing ScrumPulse.slnx)."
    }
}

# Fails loudly rather than letting a later step run against a half-built tree.
function Invoke-Checked {
    param(
        [Parameter(Mandatory)] [string]   $What,
        [Parameter(Mandatory)] [string]   $Exe,
        [Parameter(Mandatory)] [string[]] $Arguments,
        [string] $WorkingDirectory = $RepoRoot
    )

    Push-Location $WorkingDirectory
    try {
        & $Exe @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$What failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }
}

function Test-CommandExists {
    param([string] $Name)
    return [bool](Get-Command $Name -ErrorAction SilentlyContinue)
}

# ---------------------------------------------------------------------------
# verify - static checks that need no toolchain
# ---------------------------------------------------------------------------

function Invoke-Verify {
    Assert-RepoRoot
    $problems = 0

    # A file truncated mid-token is the failure mode that cost the most time on this project:
    # it looks fine in a listing and only surfaces as a confusing compiler error.
    Write-Step 'Checking for truncated or corrupted source files'

    $sourceFiles = Get-ChildItem -Path (Join-Path $RepoRoot 'src'), (Join-Path $RepoRoot 'tests') `
        -Include *.cs, *.ts, *.html, *.csproj -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\(node_modules|obj|bin|dist|\.angular)\\' }

    foreach ($file in $sourceFiles) {
        $bytes = [System.IO.File]::ReadAllBytes($file.FullName)

        if ($bytes -contains 0) {
            Write-Bad "NUL bytes: $($file.FullName.Substring($RepoRoot.Length + 1))"
            $problems++
            continue
        }

        $text = [System.Text.Encoding]::UTF8.GetString($bytes).TrimEnd()
        if ($text.Length -eq 0) { continue }

        if ($text[-1] -notin '}', ']', ')', '>', ';', '*', '/') {
            Write-Bad "Ends mid-token: $($file.FullName.Substring($RepoRoot.Length + 1))"
            $problems++
        }
    }

    if ($problems -eq 0) { Write-Good "$($sourceFiles.Count) source files look complete." }

    # Every project file must parse, or restore fails before the compiler runs.
    Write-Step 'Checking project files parse as XML'
    $projectFiles = Get-ChildItem -Path $RepoRoot -Include *.csproj, *.slnx -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(node_modules|obj|bin)\\' }

    foreach ($project in $projectFiles) {
        try {
            [xml](Get-Content $project.FullName -Raw) | Out-Null
        }
        catch {
            Write-Bad "Malformed XML: $($project.Name)"
            $problems++
        }
    }
    if ($problems -eq 0) { Write-Good "$($projectFiles.Count) project files parse." }

    # A public repository makes a committed secret a disclosure, not a mistake to fix later.
    Write-Step 'Scanning for committed secrets'
    $secretPatterns = @(
        'ghp_[A-Za-z0-9]{20,}',
        'xox[baprs]-[A-Za-z0-9-]{10,}',
        'AKIA[0-9A-Z]{16}',
        '-----BEGIN (RSA |OPENSSH |EC )?PRIVATE KEY-----',
        'gsk_[A-Za-z0-9]{20,}',
        'AIza[0-9A-Za-z_-]{30,}'
    )

    $scanTargets = Get-ChildItem -Path $RepoRoot -Include *.cs, *.ts, *.json, *.yaml, *.yml, *.md, *.ps1 -Recurse -File |
        Where-Object { $_.FullName -notmatch '\\(node_modules|obj|bin|dist|\.angular|\.git)\\' }

    $secretHits = 0
    foreach ($pattern in $secretPatterns) {
        $matches = Select-String -Path $scanTargets.FullName -Pattern $pattern -CaseSensitive -ErrorAction SilentlyContinue
        foreach ($hit in $matches) {
            Write-Bad "Possible secret in $($hit.Filename):$($hit.LineNumber)"
            $secretHits++
        }
    }
    if ($secretHits -eq 0) { Write-Good 'No credential patterns found.' } else { $problems += $secretHits }

    # Committing these bloats the repository and leaks local paths through source maps.
    Write-Step 'Checking build output is not present'
    foreach ($unwanted in @('src\ScrumPulse.UI\dist', 'src\ScrumPulse.UI\node_modules')) {
        if (Test-Path (Join-Path $RepoRoot $unwanted)) {
            Write-Warn "$unwanted exists. Confirm .gitignore covers it before committing."
        }
    }

    Write-Host ''
    if ($problems -gt 0) {
        Write-Bad "$problems problem(s) found."
        exit 1
    }

    Write-Good 'All static checks passed.'
}

# ---------------------------------------------------------------------------
# build
# ---------------------------------------------------------------------------

function Invoke-Build {
    Assert-RepoRoot

    if (-not (Test-CommandExists 'dotnet')) { throw 'dotnet SDK not found on PATH.' }
    if (-not (Test-CommandExists 'npm'))    { throw 'npm not found on PATH.' }

    Write-Step 'Restoring and building the backend'
    Invoke-Checked -What 'dotnet build' -Exe 'dotnet' -Arguments @('build', $Solution, '-warnaserror')

    Write-Step 'Installing frontend packages'
    # Angular 22 pre-release peers are stricter than the published ranges allow.
    Invoke-Checked -What 'npm install' -Exe 'npm' `
        -Arguments @('install', '--legacy-peer-deps') -WorkingDirectory $UiPath

    Write-Step 'Building the Angular app'
    Invoke-Checked -What 'ng build' -Exe 'npm' `
        -Arguments @('run', 'build') -WorkingDirectory $UiPath

    $UiDist = Join-Path $UiPath 'dist\scrum-pulse.ui\browser'
    $ApiWwwRoot = Join-Path $RepoRoot 'src\ScrumPulse.Api\wwwroot'
    if (Test-Path $UiDist) {
        Write-Step 'Deploying built UI to API wwwroot'
        if (-not (Test-Path $ApiWwwRoot)) { New-Item -ItemType Directory -Force -Path $ApiWwwRoot | Out-Null }
        Copy-Item -Path (Join-Path $UiDist '*') -Destination $ApiWwwRoot -Recurse -Force
    }

    Write-Good 'Build complete.'
}

# ---------------------------------------------------------------------------
# test
# ---------------------------------------------------------------------------

function Invoke-Test {
    Assert-RepoRoot

    Write-Step 'Backend tests'
    Invoke-Checked -What 'dotnet test' -Exe 'dotnet' -Arguments @('test', $Solution, '--nologo')

    Write-Step 'Frontend unit tests'
    if (-not (Test-Path (Join-Path $UiPath 'node_modules'))) {
        Write-Warn 'node_modules missing; installing first.'
        Invoke-Checked -What 'npm install' -Exe 'npm' `
            -Arguments @('install', '--legacy-peer-deps') -WorkingDirectory $UiPath
    }

    Invoke-Checked -What 'ng test' -Exe 'npm' `
        -Arguments @('run', 'test:ci') -WorkingDirectory $UiPath

    if ($SkipE2E) {
        Write-Warn 'Skipping end-to-end tests (-SkipE2E).'
        return
    }

    Write-Step 'End-to-end tests'
    Write-Warn 'Playwright starts the API itself on port 8080.'
    Invoke-Checked -What 'playwright test' -Exe 'npm' `
        -Arguments @('run', 'test:e2e') -WorkingDirectory $UiPath

    Write-Good 'All suites passed.'
}

# ---------------------------------------------------------------------------
# init - git repository
# ---------------------------------------------------------------------------

function Invoke-Init {
    Assert-RepoRoot

    if (Test-Path (Join-Path $RepoRoot '.git')) {
        throw 'This folder already has a .git directory. Delete it first if you mean to start over.'
    }

    Write-Step 'Creating the repository'
    Invoke-Checked -What 'git init' -Exe 'git' -Arguments @('init', '--initial-branch=main')

    & git config user.name  $AuthorName
    & git config user.email $AuthorEmail

    # Without this, Windows rewrites every line ending on commit and the first diff claims
    # all 500 files changed, burying anything real.
    & git config core.autocrlf false

    # Grouped so that `git log` reads as a tour of the architecture. Every commit is dated
    # now, because that is when the repository was created.
    $commits = [ordered]@{
        'Add solution scaffolding, gitignore and editor settings' =
            @('ScrumPulse.slnx', '.gitignore', '.dockerignore', '.editorconfig')
        'Add domain entities, enums and working-hours calculation' =
            @('src\ScrumPulse.Domain')
        'Add EF Core persistence with tenant and soft-delete query filters' =
            @('src\ScrumPulse.Infrastructure\Persistence')
        'Add session security: signing key, JWT issuer, credential protection' =
            @('src\ScrumPulse.Infrastructure\Security', 'src\ScrumPulse.Application\Common\Security')
        'Add application layer: CQRS handlers, DTOs and services' =
            @('src\ScrumPulse.Application')
        'Add remaining infrastructure services and dependency registration' =
            @('src\ScrumPulse.Infrastructure')
        'Add AI layer on Microsoft Agent Framework with token budgeting' =
            @('src\ScrumPulse.AI')
        'Add API controllers, middleware and request pipeline' =
            @('src\ScrumPulse.Api')
        'Add Angular shell, routing, theming and core services' =
            @('src\ScrumPulse.UI\src\app\core', 'src\ScrumPulse.UI\src\app\app.routes.ts',
              'src\ScrumPulse.UI\src\app\app.config.ts', 'src\ScrumPulse.UI\src\styles.css')
        'Add feature screens: standup, sprint, blockers, capacity, retro, reviews' =
            @('src\ScrumPulse.UI\src\app\features')
        'Add public marketing pages and read-only demo mode' =
            @('src\ScrumPulse.UI\src\app\marketing', 'src\ScrumPulse.UI\src\app\demo')
        'Add Azure DevOps board sync and CSV import' =
            @('src\ScrumPulse.Application\Integration', 'src\ScrumPulse.Infrastructure\Integration')
        'Add remaining Angular application and build configuration' =
            @('src\ScrumPulse.UI')
        'Add backend, frontend and end-to-end test suites' =
            @('tests', 'src\ScrumPulse.UI\e2e')
        'Add CI workflow, Dockerfile and Render deployment configuration' =
            @('.github', 'Dockerfile', 'render.yaml')
        'Add README, architecture notes, wiki and contributing guide' =
            @('README.md', 'ARCHITECTURE.md', 'CONTRIBUTING.md', 'wiki')
    }

    Write-Step 'Committing'
    foreach ($message in $commits.Keys) {
        $paths = $commits[$message] | Where-Object { Test-Path (Join-Path $RepoRoot $_) }

        if (-not $paths) { continue }

        & git add -- $paths

        & git diff --cached --quiet
        if ($LASTEXITCODE -eq 0) { continue }   # already covered by an earlier group

        & git commit -q -m $message
        Write-Good $message
    }

    # Config files, assets and anything the groups above did not name.
    & git add -A
    & git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) {
        & git commit -q -m 'Add supporting configuration and assets'
        Write-Good 'Add supporting configuration and assets'
    }

    Write-Host ''
    & git --no-pager log --oneline

    Write-Host @"

Next:
  .\scrumpulse.ps1 verify
  git remote add origin https://github.com/RanjithaSavandaiah/ScrumPulse.git
  git push -u origin main
"@ -ForegroundColor Yellow
}

# ---------------------------------------------------------------------------
# wiki
# ---------------------------------------------------------------------------

function Invoke-Wiki {
    Assert-RepoRoot

    $wikiSource = Join-Path $RepoRoot 'wiki'
    if (-not (Test-Path $wikiSource)) {
        throw "No wiki folder found at $wikiSource."
    }

    Write-Step 'Checking the wiki repository exists'
    & git ls-remote $WikiRemote 2>&1 | Out-Null

    if ($LASTEXITCODE -ne 0) {
        Write-Bad 'The GitHub wiki has not been initialised yet.'
        Write-Host '    Open the repository Wiki tab, click "Create the first page", save it,' -ForegroundColor White
        Write-Host '    then re-run: .\scrumpulse.ps1 wiki' -ForegroundColor White
        exit 1
    }

    $staging = Join-Path ([System.IO.Path]::GetTempPath()) "scrumpulse-wiki-$(Get-Random)"

    try {
        Write-Step 'Cloning the wiki'
        Invoke-Checked -What 'git clone' -Exe 'git' -Arguments @('clone', '--quiet', $WikiRemote, $staging)

        Write-Step 'Copying pages'
        foreach ($page in Get-ChildItem -Path $wikiSource -Filter *.md) {
            Copy-Item -Path $page.FullName -Destination $staging -Force
            Write-Host "    $($page.Name)" -ForegroundColor Gray
        }

        Push-Location $staging
        try {
            & git config user.name  $AuthorName
            & git config user.email $AuthorEmail
            & git add -A

            if (-not (& git status --porcelain)) {
                Write-Good 'Wiki already up to date.'
                return
            }

            & git commit -q -m 'docs(wiki): update ScrumPulse engineering wiki'

            # GitHub wikis still default to master, not main.
            & git push -q origin master
            Write-Good 'Wiki published.'
        }
        finally {
            Pop-Location
        }
    }
    finally {
        # Staged under TEMP rather than inside the repository, so a failed run cannot leave
        # a nested git checkout for `git add -A` to swallow.
        if (Test-Path $staging) { Remove-Item -Recurse -Force $staging }
    }
}

# ---------------------------------------------------------------------------
# inspect - diagnostic for the Agent Framework assembly
# ---------------------------------------------------------------------------

function Invoke-Inspect {
    # Kept because the Agent Framework SDK is pre-release and its type shapes move between
    # versions; this is how a build error about a missing property gets diagnosed.
    $packageRoot = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.agents.ai'

    if (-not (Test-Path $packageRoot)) {
        throw "Package not restored. Run '.\scrumpulse.ps1 build' first."
    }

    $assemblyPath = Get-ChildItem -Path $packageRoot -Filter 'Microsoft.Agents.AI.dll' -Recurse |
        Select-Object -First 1 -ExpandProperty FullName

    if (-not $assemblyPath) { throw "Microsoft.Agents.AI.dll not found under $packageRoot." }

    Write-Step "Reflecting over $assemblyPath"
    $assembly = [System.Reflection.Assembly]::LoadFrom($assemblyPath)

    foreach ($type in $assembly.GetExportedTypes() | Where-Object { $_.Name -like '*ChatClient*' }) {
        Write-Host "`nTYPE: $($type.FullName)" -ForegroundColor Cyan

        Write-Host '  PROPERTIES:'
        foreach ($property in $type.GetProperties()) {
            Write-Host "    $($property.PropertyType.Name) $($property.Name)"
        }

        Write-Host '  CONSTRUCTORS:'
        foreach ($constructor in $type.GetConstructors()) {
            Write-Host "    $constructor"
        }
    }
}

# ---------------------------------------------------------------------------

function Show-Help {
    Write-Host @'
ScrumPulse project tasks

  .\scrumpulse.ps1 verify    Static checks: truncation, secrets, malformed project files
  .\scrumpulse.ps1 build     Restore and compile backend + Angular
  .\scrumpulse.ps1 test      Backend, frontend and end-to-end suites
  .\scrumpulse.ps1 all       verify, build, test
  .\scrumpulse.ps1 init      Create the git repository and commit in logical units
  .\scrumpulse.ps1 wiki      Publish wiki\*.md to the GitHub wiki
  .\scrumpulse.ps1 inspect   Reflect over the Agent Framework assembly

Options
  -SkipE2E                   Skip Playwright during 'test'
  -AuthorName / -AuthorEmail Override the git identity used by 'init' and 'wiki'
'@ -ForegroundColor White
}

switch ($Task) {
    'verify'  { Invoke-Verify }
    'build'   { Invoke-Build }
    'test'    { Invoke-Test }
    'init'    { Invoke-Init }
    'wiki'    { Invoke-Wiki }
    'inspect' { Invoke-Inspect }
    'all'     { Invoke-Verify; Invoke-Build; Invoke-Test }
    default   { Show-Help }
}
