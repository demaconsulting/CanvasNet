# build.ps1
#
# PURPOSE:
#   Unified cross-platform build script (replaces build.bat and build.sh).
#   Builds the solution in Release configuration and runs all unit tests.
#
# USAGE:
#   pwsh ./build.ps1                          Full build and all tests (unchanged template behavior)
#   pwsh ./build.ps1 -Affected [-Base <ref>]  Only the test projects affected by the changes since <ref>
#                                             (default origin/main, else main) plus uncommitted changes
#   pwsh ./build.ps1 -Project Svg,Mermaid     Only the test projects affected by the named projects
#   Add -DryRun to print the selection without building anything.
#
#   A changed project selects every project that references it, directly or transitively, from the
#   ProjectReference graph, and files linked into another project (Compile/None/EmbeddedResource items
#   that point outside the project folder) select that project too. Changes that affect the whole
#   build (solution, project, props/targets, global.json, .editorconfig, nuget.config, tool manifest,
#   this script) fall back to the full build; repository files tests read at run time select their test
#   projects through $RuntimeReads; other changes the build does not read select nothing.
#
# CUSTOMIZATION:
#   This script belongs to the project: extend it as the project grows. Add steps at the
#   [PROJECT-SPECIFIC] points and add optional parameters that focus a run (here the scoped
#   -Affected/-Project/-DryRun modes). Slow or environment-dependent test tiers (GUI automation,
#   integration) may be opt-in switches.
#   Run with no arguments it must still build and run the standard test suite (agents rely
#   on that; here it is unchanged from the template), and never remove or weaken a test step
#   to make it pass.

param(
    # Build and test only the projects affected by the changed files.
    [switch]$Affected,

    # Git reference to compare against for -Affected (default: origin/main, else main).
    [string]$Base,

    # Build and test only the projects affected by these projects (full or short names, e.g. Svg, Core).
    [string[]]$Project,

    # Print the selection and exit without building.
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

# ==============================================================================
# HELPER FUNCTIONS (scoped modes only)
# ==============================================================================

function Get-ChangedFiles {
    param([string]$Base)

    # Default to origin/main, falling back to the local main branch
    if (-not $Base) {
        $Base = 'origin/main'
        git rev-parse --verify --quiet "$Base^{commit}" > $null 2>&1
        if ($LASTEXITCODE -ne 0) { $Base = 'main' }
    }
    git rev-parse --verify --quiet "$Base^{commit}" > $null 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Cannot resolve base reference '$Base'."
        return $null
    }
    Write-Host "Changes since: $Base (plus uncommitted and untracked files)"

    # Committed changes since the merge-base, then staged, unstaged, and untracked files
    $committed = @(git -c core.quotePath=false diff --name-only --no-renames "$Base...HEAD")
    if ($LASTEXITCODE -ne 0) { return $null }
    $working = @(git -c core.quotePath=false status --porcelain --untracked-files=all --no-renames)
    if ($LASTEXITCODE -ne 0) { return $null }

    $files = $committed + @($working | ForEach-Object { $_.Substring(3).Trim('"') })
    return , @($files | Where-Object { $_ } | ForEach-Object { $_ -replace '\\', '/' } | Sort-Object -Unique)
}

function Resolve-RepoPath {
    param([string]$Directory, [string]$RelativePath)

    # Combine a repository-relative directory with a relative path, collapsing '.' and '..'
    $segments = [System.Collections.Generic.List[string]]::new()
    foreach ($segment in "$Directory/$RelativePath" -replace '\\', '/' -split '/') {
        if ($segment -eq '..') { if ($segments.Count -gt 0) { $segments.RemoveAt($segments.Count - 1) } }
        elseif ($segment -and $segment -ne '.') { $segments.Add($segment) }
    }
    return $segments -join '/'
}

function Convert-GlobToRegex {
    param([string]$Glob)

    # MSBuild wildcards: '**' spans folders, '*' and '?' stay within one folder
    $regex = [regex]::Escape($Glob)
    $regex = $regex -replace '\\\*\\\*/', '(.*/)?' -replace '\\\*\\\*', '.*' -replace '\\\*', '[^/]*' -replace '\\\?', '[^/]'
    return "^$regex$"
}

function Get-ProjectGraph {
    # Reads every project under src/, test/, and tools/: its folder, its project references,
    # and the files outside its folder that it compiles, embeds, or copies (linked files).
    $graph = [ordered]@{}
    $files = Get-ChildItem -Path src, test, tools -Recurse -Filter *.csproj -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '[/\\](bin|obj)[/\\]' } |
        Sort-Object FullName
    foreach ($file in $files) {
        $path = [System.IO.Path]::GetRelativePath((Get-Location).Path, $file.FullName) -replace '\\', '/'
        $directory = Split-Path $path -Parent
        $directory = $directory -replace '\\', '/'
        [xml]$xml = Get-Content -Raw $file.FullName

        $references = @()
        $links = @()
        foreach ($item in $xml.SelectNodes('/Project/ItemGroup/*')) {
            $include = $item.GetAttribute('Include')
            if (-not $include) { continue }

            if ($item.LocalName -eq 'ProjectReference') {
                $references += [System.IO.Path]::GetFileNameWithoutExtension(($include -replace '\\', '/'))
                continue
            }
            if ($item.LocalName -notin @('Compile', 'None', 'Content', 'EmbeddedResource')) { continue }

            # Files only packed into the NuGet package (README.md, Icon.png, licenses) do not affect tests
            $pack = $item.GetAttribute('Pack') + $item.SelectSingleNode('Pack')?.InnerText
            $copy = $item.GetAttribute('CopyToOutputDirectory') + $item.SelectSingleNode('CopyToOutputDirectory')?.InnerText
            if ($item.LocalName -eq 'None' -and $pack -eq 'true' -and -not $copy) { continue }

            foreach ($part in $include -split ';') {
                $resolved = Resolve-RepoPath $directory $part.Trim()
                if (-not $resolved.StartsWith("$directory/", [System.StringComparison]::OrdinalIgnoreCase)) {
                    $links += Convert-GlobToRegex $resolved
                }
            }
        }

        $name = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)
        $graph[$name] = [pscustomobject]@{
            Name       = $name
            Path       = $path
            Directory  = $directory
            References = $references
            Links      = $links
        }
    }
    return $graph
}

function Test-BuildWideFile {
    param([string]$File)

    # Files that configure the whole build: any of them changing needs the full build
    $leaf = Split-Path $File -Leaf
    return ($leaf -match '^(Directory\.Build\.(props|targets|rsp)|Directory\.Packages\.props|global\.json|nuget\.config|\.editorconfig|\.globalconfig)$') -or
        ($File -match '\.(sln|slnx|slnf|csproj|props|targets)$') -or
        ($File -in @('.config/dotnet-tools.json', 'build.ps1'))
}

# [PROJECT-SPECIFIC] Repository files that tests read at run time (through the repository root, not through a
# project item), and the test projects that read them. Keep in step with the tests.
$RuntimeReads = [ordered]@{
    '^docs/user_guide/canvas-net-tool\.md$'                = 'DemaConsulting.CanvasNet.Tool.Tests'
    '^docs/user_guide/canvas-net-(mermaid|plant-uml)\.md$' = 'DemaConsulting.CanvasNet.Viewer.Tests'
    '^docs/filters/canvasnet\.lua$'                        = 'DemaConsulting.CanvasNet.Tool.Tests'
    '^docs/user_guide/canvas-net-viewer\.md$'              = 'DemaConsulting.CanvasNet.Viewer.Tests'
}

function Resolve-ProjectName {
    param($Graph, [string]$Name)

    # Accept full names, names without the core prefix (Svg, Svg.Tests), or Core for the core library
    $core = $Graph.Keys | Where-Object { $Graph[$_].Directory -like 'src/*' } |
        Sort-Object Length | Select-Object -First 1
    if ($Name -eq 'Core') { return $core }
    foreach ($candidate in @($Name, "$core.$Name")) {
        $match = $Graph.Keys | Where-Object { $_ -eq $candidate }
        if ($match) { return $match }
    }
    return $null
}

# ==============================================================================
# SCOPED BUILD (-Affected / -Project)
# Selects the affected test projects; falls back to the full build when needed.
# ==============================================================================

if ($Affected -or $Project) {
    $graph = Get-ProjectGraph
    $selected = [System.Collections.Generic.HashSet[string]]::new()
    $fullReason = $null

    foreach ($name in $Project) {
        foreach ($single in $name -split ',') {
            $resolved = Resolve-ProjectName $graph $single.Trim()
            if (-not $resolved) {
                Write-Host "Unknown project '$single'. Known projects: $($graph.Keys -join ', ')"
                exit 1
            }
            [void]$selected.Add($resolved)
        }
    }

    if ($Affected) {
        $changed = Get-ChangedFiles -Base $Base
        if ($null -eq $changed) {
            $fullReason = 'the changed files could not be determined'
        } else {
            Write-Host "Changed files: $($changed.Count)"
            foreach ($file in $changed) {
                if (Test-BuildWideFile $file) { $fullReason = "$file affects the whole build"; break }
                foreach ($pattern in $RuntimeReads.Keys) {
                    if ($file -match $pattern -and $graph.Contains($RuntimeReads[$pattern])) { [void]$selected.Add($RuntimeReads[$pattern]) }
                }
                foreach ($entry in $graph.Values) {
                    $owned = $file.StartsWith("$($entry.Directory)/", [System.StringComparison]::OrdinalIgnoreCase)
                    $linked = @($entry.Links | Where-Object { $file -match $_ }).Count -gt 0
                    if ($owned -or $linked) { [void]$selected.Add($entry.Name) }
                }
            }
        }
    }

    if ($fullReason) {
        Write-Host "Full build required: $fullReason."
        if ($DryRun) { Write-Host "Dry run: would run the full build and all tests."; exit 0 }
    } else {
        # Add every project that references a selected project, directly or transitively
        $queue = [System.Collections.Generic.Queue[string]]::new([string[]]@($selected))
        while ($queue.Count -gt 0) {
            $current = $queue.Dequeue()
            foreach ($entry in $graph.Values) {
                if ($entry.References -contains $current -and $selected.Add($entry.Name)) { $queue.Enqueue($entry.Name) }
            }
        }

        # Test projects are built and tested; other leaf projects (tools/) are built only
        $tests = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'test/*' })
        $tools = @($graph.Values | Where-Object { $selected.Contains($_.Name) -and $_.Directory -like 'tools/*' })
        if ($tests.Count -eq 0 -and $tools.Count -eq 0) {
            Write-Host "Nothing to build: no changed file is read by the build."
            exit 0
        }

        Write-Host "Affected test projects:"
        $tests | ForEach-Object { Write-Host "  $($_.Name)" }
        if ($tools.Count -gt 0) {
            Write-Host "Affected tool projects (build only):"
            $tools | ForEach-Object { Write-Host "  $($_.Name)" }
        }
        if ($DryRun) { exit 0 }

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

        Write-Host "Affected build and tests completed successfully!"
        exit 0
    }
}

if ($DryRun) { Write-Host "Dry run: would run the full build and all tests."; exit 0 }

# ==============================================================================
# FULL BUILD
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
