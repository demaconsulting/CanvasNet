# build.ps1
#
# PURPOSE:
#   Unified cross-platform build script (replaces build.bat and build.sh).
#   Builds the solution in Release configuration and runs the unit tests, either all of them
#   (-All, the full local build and test run) or only the test projects a change affects (-Changed / -Project).
#
# USAGE:
#   pwsh ./build.ps1 -Help                     Print the usage text and exit 0
#   pwsh ./build.ps1 -All                      Full local build and all tests
#   pwsh ./build.ps1 -Changed [-Base <ref>]    Only the test projects affected by the changes since <ref>
#                                              (default origin/main, else main) plus uncommitted changes
#   pwsh ./build.ps1 -Project Svg,Mermaid      Only the test projects affected by the named projects
#   Add -DryRun to any scope to print the selection without building anything.
#   With no arguments the usage text is printed and the script exits 1 (a scope must be chosen).
#
#   A changed project selects every project that references it, directly or transitively, from the
#   ProjectReference graph, and files linked into another project (Compile/None/EmbeddedResource items
#   that point outside the project folder) select that project too. Changes that affect the whole
#   build (solution, project, props/targets, global.json, .editorconfig, nuget.config, tool manifest,
#   this script, scripts/ProjectScripts.psm1) fall back to the full build; repository files tests read
#   at run time select their test projects through $RuntimeReads; other changes the build does not
#   read select nothing.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points and add optional parameters that focus a run. The reusable scoping
#   logic lives in scripts/ProjectScripts.psm1, which is shared across repositories and must not
#   be edited here. Slow or environment-dependent test tiers (GUI automation, integration) may be
#   opt-in switches.
#   With no arguments the script prints the usage text and exits 1; -All must still build and run
#   the standard test suite (the agents rely on that). Never remove or weaken a test step to make it pass.

param(
    # Full local build and all tests.
    [switch]$All,

    # Build and test only the projects affected by the changed files.
    [switch]$Changed,

    # Git reference to compare against for -Changed (default: origin/main, else main).
    [string]$Base,

    # Build and test only the projects affected by these projects (full or short names, e.g. Svg, Core).
    [string[]]$Project,

    # Print the selection and exit without building.
    [switch]$DryRun,

    # Print the usage text and exit 0.
    [switch]$Help
)

$ErrorActionPreference = 'Stop'
Import-Module (Join-Path $PSScriptRoot 'scripts/ProjectScripts.psm1') -Force

# [PROJECT-SPECIFIC] Repository files that tests read at run time (through the repository root, not through a
# project item), and the test projects that read them. Keep in step with the tests.
$RuntimeReads = [ordered]@{
    '^docs/user_guide/canvas-net-tool\.md$'                = 'DemaConsulting.CanvasNet.Tool.Tests'
    '^docs/user_guide/canvas-net-(mermaid|plant-uml)\.md$' = 'DemaConsulting.CanvasNet.Viewer.Tests'
    '^docs/filters/canvasnet\.lua$'                        = 'DemaConsulting.CanvasNet.Tool.Tests'
    '^docs/user_guide/canvas-net-viewer\.md$'              = 'DemaConsulting.CanvasNet.Viewer.Tests'
}

# ==============================================================================
# USAGE AND SCOPE VALIDATION
# ==============================================================================

function Show-Usage {
    Write-ScriptUsage -Script 'build.ps1' -IterateWith '-Changed (or -Project)' `
        -Purpose 'build the solution (Release) and run the unit tests.' `
        -Options ([ordered]@{
            '-All'            = 'Full local build and all tests; cannot be combined with another scope.'
            '-Changed'        = 'Only the test projects affected by files changed since the base, plus uncommitted files.'
            '-Base <ref>'     = 'Base reference for -Changed (default origin/main, else main).'
            '-Project <names>' = 'Only the test projects affected by these projects (full or short names, e.g. Svg,Core).'
            '-DryRun'         = 'Print what would be built, tested, and skipped, then exit 0 without running anything.'
            '-Help'           = 'Print this text and exit 0.'
        }) `
        -Examples @('-Changed', '-Project Svg,Mermaid', '-Changed -DryRun', '-All')
}

if ($Help) { Show-Usage; exit 0 }

$scopeProblems = Test-ScopeArguments -Base $Base -Scopes ([ordered]@{
        All     = $All.IsPresent
        Changed = $Changed.IsPresent
        Project = [bool]$Project
    })
if ($scopeProblems.Count -gt 0) {
    $scopeProblems | ForEach-Object { Write-Host "Error: $_" }
    Write-Host ""
    Show-Usage
    exit 1
}

# ==============================================================================
# SCOPED BUILD (-Changed / -Project)
# Selects the affected test projects; falls back to the full build when needed.
# ==============================================================================

if (-not $All) {
    $graph = Get-ProjectGraph
    $selected = [System.Collections.Generic.HashSet[string]]::new()
    $fullReason = $null

    foreach ($single in @($Project | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        $resolved = Resolve-ProjectName $graph $single
        if (-not $resolved) {
            Write-Host "Unknown project '$single'. Known projects: $($graph.Keys -join ', ')"
            exit 1
        }
        [void]$selected.Add($resolved)
    }

    if ($Changed) {
        $changedFiles = Get-ChangedFiles -Base $Base
        if ($null -eq $changedFiles) {
            $fullReason = 'the changed files could not be determined'
        } else {
            Write-Host "Changed files: $($changedFiles.Count)"
            $fullReason = Add-ChangedProjects -Graph $graph -Files $changedFiles -Selected $selected -RuntimeReads $RuntimeReads
        }
    }

    if ($fullReason) {
        Write-Host "Full build required: $fullReason."
        if ($DryRun) { Write-Host "Dry run: would run the full build and all tests."; exit 0 }
    } else {
        Expand-ReferencingProjects -Graph $graph -Selected $selected

        # Test projects are built and tested; other leaf projects (tools/) are built only
        $tests = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'test/*' })
        $tools = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'tools/*' })
        $skipped = @($graph.Values | Where-Object { $_.Directory -like 'test/*' -and -not $selected.Contains($_.Name) })
        if ($tests.Count -eq 0 -and $tools.Count -eq 0) {
            Write-Host "Nothing to build: no changed file is read by the build."
            exit 0
        }

        Write-Host "Affected test projects (built and tested):"
        $tests | ForEach-Object { Write-Host "  $($_.Name)" }
        if ($tools.Count -gt 0) {
            Write-Host "Affected tool projects (build only):"
            $tools | ForEach-Object { Write-Host "  $($_.Name)" }
        }
        if ($DryRun) {
            Write-Host "Skipped test projects ($($skipped.Count)): $(($skipped | ForEach-Object { $_.Name }) -join ', ')"
            Write-Host "Dry run: nothing was built or tested."
            exit 0
        }

        foreach ($entry in $tests + $tools) {
            Write-Host "Building $($entry.Name)..."
            dotnet build $entry.Path --configuration Release
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }

        foreach ($entry in $tests) {
            Write-Host "Running unit tests: $($entry.Name)..."
            dotnet test --project $entry.Path --configuration Release --report-trx
            if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        }

        Write-Host "Scoped build and tests completed successfully!"
        exit 0
    }
}

if ($DryRun) { Write-Host "Dry run: would run the full build and all tests."; exit 0 }

# ==============================================================================
# FULL BUILD (-All, or a scoped run that needs everything)
# ==============================================================================

Write-Host "Building project..."
dotnet build --configuration Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# [PROJECT-SPECIFIC] Add additional build steps here.

Write-Host "Running unit tests..."
dotnet test --configuration Release --report-trx
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# [PROJECT-SPECIFIC] Add additional test or post-build steps here.

Write-Host "Build and tests completed successfully!"
