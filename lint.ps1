# lint.ps1
#
# PURPOSE:
#   Runs all lint checks and reports failures. Exits 1 on error.
#   Used by CI/CD as the merge gate and by the lint-fix agent
#   during pre-PR cleanup.
#
#   To auto-fix formatting issues, run fix.ps1 instead.
#
# EXTENSION POINTS:
#   Search for "[PROJECT-SPECIFIC]" comments to find the designated locations
#   for adding project-specific lint checks.
#
# MODIFICATION POLICY:
#   Only modify this file to add project-specific operations at the designated
#   [PROJECT-SPECIFIC] extension points, or to update tool versions as needed.

# ==============================================================================
# HELPER FUNCTIONS
# ==============================================================================

function Get-VenvActivateScript {
    if (Test-Path ".venv/Scripts/Activate.ps1") { return ".venv/Scripts/Activate.ps1" }  # Windows
    if (Test-Path ".venv/bin/Activate.ps1") { return ".venv/bin/Activate.ps1" }          # Linux/macOS
    return $null
}

function Initialize-PythonVenv {
    if (-not (Test-Path ".venv")) {
        python -m venv .venv
        if ($LASTEXITCODE -ne 0) { return $false }
    }

    $activateScript = Get-VenvActivateScript
    if (-not $activateScript) { return $false }
    & $activateScript
    if (-not (Get-Command deactivate -ErrorAction SilentlyContinue)) { return $false }

    $installSucceeded = $false
    try {
        pip install -r pip-requirements.txt --quiet --disable-pip-version-check
        $installSucceeded = $LASTEXITCODE -eq 0
        return $installSucceeded
    }
    finally {
        if (-not $installSucceeded -and (Get-Command deactivate -ErrorAction SilentlyContinue)) {
            deactivate 2>$null
        }
    }
}

# ==============================================================================
# LINT CHECKS
# Runs all lint checks. Exits 1 if any check fails.
# ==============================================================================

$lintError = $false

# --- PYTHON SECTION ---
# Sets up a virtual environment and runs yamllint.
Write-Host "Linting: YAML..."
$skipPython = -not (Initialize-PythonVenv)
if ($skipPython) { $lintError = $true }

if (-not $skipPython) {
    yamllint .
    if ($LASTEXITCODE -ne 0) { $lintError = $true }
    deactivate
}

# [PROJECT-SPECIFIC] Add additional Python-based lint checks here.
# Example:
#   if (-not $skipPython) {
#       flake8 src/
#       if ($LASTEXITCODE -ne 0) { $lintError = $true }
#   }

# --- NPM SECTION ---
# Installs npm dependencies and runs cspell and markdownlint-cli2.
Write-Host "Linting: spelling and markdown..."
$skipNpm = $false
$env:PUPPETEER_SKIP_DOWNLOAD = "true"
npm install --silent
if ($LASTEXITCODE -ne 0) { $lintError = $true; $skipNpm = $true }

if (-not $skipNpm) {
    npx cspell --no-progress --no-color --quiet "**/*.{md,yaml,yml,json,cs,cpp,hpp,h,txt}"
    if ($LASTEXITCODE -ne 0) { $lintError = $true }

    npx markdownlint-cli2 "**/*.md"
    if ($LASTEXITCODE -ne 0) { $lintError = $true }
}

# [PROJECT-SPECIFIC] Add additional npm-based lint checks here.
# Example (ESLint for TypeScript):
#   if (-not $skipNpm) {
#       npx eslint "src/**/*.ts"
#       if ($LASTEXITCODE -ne 0) { $lintError = $true }
#   }

# --- DOTNET LINTING SECTION ---
# Runs compliance tools: reqstream, versionmark, reviewmark, sysml2tools.
Write-Host "Linting: compliance tools..."
$skipDotnetTools = $false
dotnet tool restore > $null
if ($LASTEXITCODE -ne 0) { $lintError = $true; $skipDotnetTools = $true }

if (-not $skipDotnetTools) {
    dotnet reqstream --lint --requirements requirements.yaml
    if ($LASTEXITCODE -ne 0) { $lintError = $true }

    dotnet versionmark --lint
    if ($LASTEXITCODE -ne 0) { $lintError = $true }

    dotnet reviewmark --lint
    if ($LASTEXITCODE -ne 0) { $lintError = $true }

    if (Test-Path docs/sysml2) {
        dotnet sysml2tools lint 'docs/sysml2/**/*.sysml'
        if ($LASTEXITCODE -ne 0) { $lintError = $true }
    }
}

# [PROJECT-SPECIFIC] Add additional dotnet tool lint checks here.
# Example:
#   if (-not $skipDotnetTools) {
#       dotnet custom-tool --lint
#       if ($LASTEXITCODE -ne 0) { $lintError = $true }
#   }

# --- DOTNET FORMATTING SECTION ---
# Verifies C# code formatting matches .editorconfig rules.
Write-Host "Linting: dotnet format..."
$skipDotnetFormat = $false
dotnet restore > $null
if ($LASTEXITCODE -ne 0) { $lintError = $true; $skipDotnetFormat = $true }

if (-not $skipDotnetFormat) {
    # NOTE: `dotnet format --verify-no-changes` has a known cross-platform exit-code
    # bug (e.g. dotnet/sdk#41422) where it can report a non-zero exit code even when
    # zero files require formatting, inconsistently between operating systems. To get
    # a reliable check, format in place and use `git diff` as the source of truth.
    #
    # A plain `git diff` after formatting would also include any pre-existing
    # uncommitted *.cs changes unrelated to formatting (e.g. a developer's
    # in-progress edits), causing false failures. Stash those away first so the
    # formatter runs against a clean tree and the diff reflects only its own
    # changes, then restore the original working tree afterward regardless of
    # outcome, leaving no side effects from running this check.
    #
    # `git status --porcelain` (rather than `git diff`) is used to detect
    # pre-existing changes so that untracked *.cs files are also caught and
    # stashed - otherwise an untracked file reformatted by `dotnet format`
    # would be left modified with no diff ever having been checked against it.
    # `--index` on the pop restores the original staged/unstaged split rather
    # than flattening everything into the working tree.
    $statusBeforeFormat = git status --porcelain -- '*.cs'
    $hasPreexistingCsChanges = [bool]$statusBeforeFormat
    if ($hasPreexistingCsChanges) {
        git stash push --include-untracked --quiet --message "lint.ps1: pre-existing *.cs changes" -- '*.cs'
        if ($LASTEXITCODE -ne 0) {
            $lintError = $true
            $skipDotnetFormat = $true
            Write-Host "Failed to stash pre-existing *.cs changes; skipping dotnet format check."
            $hasPreexistingCsChanges = $false
        }
    }
}

if (-not $skipDotnetFormat) {
    dotnet format --no-restore
    if ($LASTEXITCODE -ne 0) {
        $lintError = $true
    }
    else {
        git diff --exit-code -- '*.cs'
        if ($LASTEXITCODE -ne 0) {
            $lintError = $true
            Write-Host "dotnet format made changes; run fix.ps1 locally and commit the results."
        }
    }

    # Restore the working tree to its original state: discard any in-place
    # formatting changes made purely for this check, then reapply the
    # developer's original uncommitted *.cs changes (if any were stashed).
    # `--index` restores the original staged/unstaged split rather than
    # dropping everything into the working tree unstaged.
    git checkout -- '*.cs' 2>$null
    if ($hasPreexistingCsChanges) {
        git stash pop --index --quiet
        if ($LASTEXITCODE -ne 0) {
            $lintError = $true
            Write-Host "Failed to restore stashed *.cs changes; check 'git stash list' and resolve manually."
        }
    }
}

# [PROJECT-SPECIFIC] Add additional format verification checks here.
# Example (clang-format check for C/C++):
#   Get-ChildItem -Recurse -Include "*.cpp","*.hpp","*.h" | ForEach-Object {
#       $result = clang-format --dry-run --Werror $_.FullName 2>&1
#       if ($LASTEXITCODE -ne 0) { Write-Output $result; $lintError = $true }
#   }

exit ($lintError ? 1 : 0)
