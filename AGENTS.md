# Project Overview

- **project-name**: `CanvasNet`
- **organization**: `DEMA Consulting`
- **project-tagline**: `Canvas rendering library for .NET`
- **description**: CanvasNet is a family of .NET libraries and a command-line tool built around a span-based 32-bit
  RGBA canvas: image codecs (PNG, JPEG, JPEG 2000, TIFF, BMP, GIF), TrueType text, and vector drawing in the core,
  with packages that render SVG, PDF, PowerPoint (with charts), Visio, OpenSCAD 3D models, Vega-Lite charts, and
  Mermaid and PlantUML diagrams onto it, a viewer that loads any of them, and the `canvasnet` tool that drives them all.
- **languages**: `C#`
- **technologies**: `.NET` (libraries: net8.0, net9.0, net10.0, net11.0; tool: net10.0, net11.0),
  System.Numerics.Tensors

# Project Structure

```text
├── docs/
│   ├── build_notes/
│   ├── code_quality/
│   ├── code_review_plan/
│   ├── code_review_report/
│   ├── design/
│   ├── requirements_doc/
│   ├── requirements_report/
│   ├── reqstream/
│   ├── sysml2/
│   ├── user_guide/
│   └── verification/
├── scripts/
├── src/
│   ├── DemaConsulting.CanvasNet/
│   ├── DemaConsulting.CanvasNet.Charts/
│   ├── DemaConsulting.CanvasNet.Layout/
│   ├── DemaConsulting.CanvasNet.Mermaid/
│   ├── DemaConsulting.CanvasNet.Pdf/
│   ├── DemaConsulting.CanvasNet.PlantUml/
│   ├── DemaConsulting.CanvasNet.Pptx/
│   ├── DemaConsulting.CanvasNet.Render3D/
│   ├── DemaConsulting.CanvasNet.Scad/
│   ├── DemaConsulting.CanvasNet.Svg/
│   ├── DemaConsulting.CanvasNet.Tool/
│   ├── DemaConsulting.CanvasNet.VegaLite/
│   ├── DemaConsulting.CanvasNet.Viewer/
│   └── DemaConsulting.CanvasNet.Vsdx/
├── test/
│   └── DemaConsulting.CanvasNet.{Name}.Tests/ (one per source project; the core is DemaConsulting.CanvasNet.Tests)
└── tools/
    └── DemaConsulting.CanvasNet.VegaLite.Benchmarks/
```

# Language and Spelling (ALL Agents)

Always use **US English** spelling in all output (code, comments, documentation,
commit messages, and reports).

# Reference Template

This repository follows a reference template for structure and file conventions.

- **template-url**: `https://github.com/demaconsulting/Agents/raw/refs/heads/template`
- **Repository map**: `{template-url}/repository-map.md`
- **Template files**: `{template-url}/{file-path}` for files described in the map

# Codebase Navigation (ALL Agents)

When working with source code, design, or requirements artifacts, query the SysML2
architecture model under `docs/sysml2/` first (see the `sysml2tools-query` skill) to
understand software structure, purpose, and relationships. Fall back to
`docs/design/introduction.md` for the human-facing narrative, folder layout, and
companion artifact locations, and use it as the primary map when the model doesn't
yet cover something.

# Key Configuration Files

- **`.config/dotnet-tools.json`** - Local tool manifest for Continuous Compliance tools
- **`.editorconfig`** - Code formatting rules
- **`.clang-format`** - C/C++ formatting (if applicable)
- **`.textlint.yaml`** - Spell-check configuration and technical term dictionary (TextLint)
- **`.lintmark.yaml`** - Markdown formatting rules (LintMark)
- **`.yamllint.yaml`** - YAML formatting configuration
- **`.reviewmark.yaml`** - File review definitions and tracking
- **`nuget.config`** - NuGet package sources (if .NET)
- **`requirements.yaml`** - Root requirements file with includes
- **`pip-requirements.txt`** - Python dependencies for yamllint and yamlfix
- **`docs/sysml2/`** - SysML2 architecture model; authoritative source for software structure
- **`fix.ps1`** - Applies the auto-fixers (dotnet format, LintMark, YAML); a fixer run always exits 0.
  Requires a scope; see `pwsh ./fix.ps1 -Help` and *Scoped Validation*.
- **`build.ps1`** - Builds the solution and runs the tests. Requires a scope (`-All` is the CI build); see
  `pwsh ./build.ps1 -Help` and *Scoped Validation*.
- **`lint.ps1`** - Runs the lint checks (`-All` is the CI merge gate). Requires a scope; see
  `pwsh ./lint.ps1 -Help` and *Scoped Validation*.
- **`scripts/ProjectScripts.psm1`** - PowerShell module with the scoping logic the three scripts share.
  Shared verbatim across repositories: never edit it here (see *Project Scripts*).

# Standards Application (ALL Agents Must Follow)

Before performing any work, agents must read and apply the relevant standards
from `.github/standards/`. Use this matrix to determine which to load:

- **Any code**: `coding-principles.md`
- **C# code**: `coding-principles.md`, `csharp-language.md`
- **C++ code**: `coding-principles.md`, `cpp-language.md`
- **Any tests**: `testing-principles.md`
- **C# tests**: `testing-principles.md`, `csharp-testing.md`
- **C++ tests**: `testing-principles.md`, `cpp-testing.md`
- **Requirements**: `requirements-principles.md`, `software-items.md`, `reqstream-usage.md`
- **Design docs**: `software-items.md`, `design-documentation.md`, `technical-documentation.md`
- **Verification docs**: `software-items.md`, `verification-documentation.md`, `technical-documentation.md`
- **Review configuration**: `software-items.md`, `reviewmark-usage.md`
- **Software structure**: `sysml2-modeling.md`
- **Any documentation**: `technical-documentation.md`
- **Diagrams in documentation**: `diagram-documentation.md` (and the `canvasnet` skill for tooling)
- **Reading PDF or Office documents**: the `docdown` skill

Load only the standards relevant to your specific task scope.

# Repository-Specific Standards Exceptions

The files in `.github/standards/` and `.github/skills/` are shared verbatim across repositories. This repository
applies these accepted exceptions and additions on top of them:

## No Wall-Clock Dependence in Tests (`testing-principles.md`, `csharp-testing.md`)

Tests run on shared machines that are often heavily loaded (many agents build and test at once), so a test must
pass or fail the same way however fast or busy the machine is:

- Do not assert on elapsed time (`Stopwatch`, `DateTime.UtcNow` differences, "generous" limits, hang guards) and do
  not set per-test timeouts. A genuine hang is caught by the CI job, not by a per-test clock.
- Do not use timers, sleeps, or delays (`CancelAfter`, `Thread.Sleep`, `Task.Delay`, timed `Wait`/`Join`) to make
  something happen at the right moment. Trigger it deterministically from inside the work (for example cancel when
  a known callback, echo, or step is reached), or use a pre-canceled token.
- Prove bounds with deterministic measures instead: the specific limit error, steps or items charged, allocated
  bytes, or the absence of further work after a cancellation point. Asserting these resource measures (allocated
  bytes, step, item, or work counts, limit errors) is allowed even where the shared rules favor observable behavior.
- Parity tests that compare checked-in documentation (the user guides) with the help cards, and byte-identity
  checks of generated checked-in files (the Pandoc shim), are allowed: they keep documentation and code from
  drifting apart.
- Performance is measured by profiling and benchmarks (`tools/`), never by unit tests.

# Agent Delegation Guidelines

The default agent should handle simple, straightforward tasks directly.
Delegate to specialized agents only for specific scenarios:

- **Pre-PR lint cleanup** (fix all lint issues before pull request) → Call the lint-fix agent
- **Scoped fixes with no new user-visible behavior** (PR review comments, doc
  corrections, known bug fixes with defined root cause) → Call the developer agent
- **Light quality checking** (basic validation) → Call the quality agent
- **Any change introducing new user-visible behavior** (features, enhancements,
  new commands or options) → Call the implementation agent
- **Formal bug resolution** (complex debugging, unknown root cause) → Call the implementation agent
- **Formal reviews** (compliance verification, detailed analysis) → Call the formal-review agent
- **Structural audit**: (repository layout vs. template) → Call the template-sync agent
- **Implementation planning only** (review a plan before committing to implementation) → Call the planning agent

# Agent Reporting (Specialized Agents Must Follow)

Specialized agents MUST generate a completion report:

1. Save to `.agent-logs/{agent-name}-{subject}-{unique-id}.md`
   where `{subject}` is a kebab-case task summary (max 5 words) and
   `{unique-id}` is a short unique suffix (e.g., 8-char hex or timestamp)
2. Start with `**Result**: (SUCCEEDED|FAILED|INCOMPLETE)` as the first metadata field
3. Include the agent-specific report sections defined in each agent's prompt
4. Return the summary to the caller

Result semantics for orchestrator decision-making:

- **SUCCEEDED**: Work completed and all quality gates applicable to that agent's scope met
- **FAILED**: Work could not be completed or quality gates not met
- **INCOMPLETE**: Work cannot proceed without information only the user can
  provide (implementation, planning, and template-sync agents)

# Formatting (After Making Changes)

After making changes, run the auto-fix pass. A fixer run applies the fixers
silently and **always exits 0** - agents do not need to respond to its output.
A scope is required (no arguments prints the usage text and exits 1):

```pwsh
pwsh ./fix.ps1 -Changed   # during development: only the fixers for the changed file types
pwsh ./fix.ps1 -All       # everything
```

This automatically handles: `dotnet format`, Markdown formatting (LintMark), and YAML
formatting. Full lint compliance is a **pre-PR responsibility**, not an agent
responsibility - invoke the lint-fix agent once before submitting a pull request.

## CI Quality Tools

CI runs `lint.ps1 -All` which checks: lintmark, textlint, yamllint, dotnet format,
reqstream, versionmark, reviewmark, and sysml2tools.

# Scoped Validation (ALL Agents Must Follow)

The solution has fourteen source projects and fourteen test projects. `build.ps1`, `lint.ps1`, and `fix.ps1`
require you to choose a scope: run with no arguments they print their usage text and **exit 1**. Read the
usage text before using a script; it lists the switches, examples, and (for lint and fix) the valid `-Check`
names:

```pwsh
pwsh ./build.ps1 -Help
pwsh ./lint.ps1 -Help
pwsh ./fix.ps1 -Help
```

- **Iterate scoped.** While developing, build, test, fix, and lint only what the change affects
  (`-Changed`, `-Project`, `-Check`). Add `-DryRun` to see the selection (what runs and what is skipped)
  without running anything; it also tells you which test projects a change affects.
- **Run `-All` before handing work back.** Before opening or updating a pull request, run
  `pwsh ./build.ps1 -All` and `pwsh ./lint.ps1 -All` once (via the lint-fix agent for lint). Scoped runs are
  for the inner development loop only and are not a substitute for the full build. CI runs `-All`.
- **Documentation-only changes** (`docs/`, `README.md`, agent files) need no build or test run
  (`build.ps1 -Changed` reports nothing to build); run `pwsh ./fix.ps1 -Changed` and `pwsh ./lint.ps1 -Changed`,
  then `pwsh ./lint.ps1 -All` before the pull request.
- **Changes that affect everything** (solution file, project files, `global.json`, `.editorconfig`, package
  or build configuration, `build.ps1`, `scripts/ProjectScripts.psm1`) make `build.ps1 -Changed` fall back to
  the full build.
- **Template-owned instructions** under `.github/agents/`, `.github/standards/`, and `.github/skills/` still
  say `pwsh ./build.ps1`, `pwsh ./lint.ps1`, or `pwsh ./fix.ps1` without a switch. Read each such command as
  the same script with a scope: `-Changed` (or `-Project`/`-Check`) while iterating and `-All` for the final
  run before a pull request. A command without a scope fails with exit 1.
- **Keep `$RuntimeReads` in `build.ps1` in step.** Repository files that tests read at run time (the user
  guides the help-card parity tests compare, and the checked-in Pandoc shim) select their test projects
  through that map; update it when a test starts reading another repository file.

# Scope Discipline (ALL Agents Must Follow)

- **No generated file access**: Files inside any `generated/` folder are build
  outputs - do not read, lint, or modify them
- **Minimum necessary changes**: Only modify files directly required by the task
- **No speculative refactoring**: Do not refactor code adjacent to the change
  unless the task explicitly requests it
- **No drive-by fixes**: If you discover pre-existing issues in files you are
  reading but not modifying, document them in the report but do not fix them
- **Declare scope upfront**: Before making changes, determine which files will be
  modified. Any file outside this scope requires explicit justification.

# Protected Configuration Files

These files contain carefully designed configuration with documented intent
in header comments. Agents MUST NOT modify them unless the task explicitly
requires it and the modification preserves the documented design intent:

- `.reviewmark.yaml`, `.textlint.yaml`, `.editorconfig`
- `.lintmark.yaml`, `.yamllint.yaml`
- `requirements.yaml`

# Project Scripts

`build.ps1`, `lint.ps1`, and `fix.ps1` belong to the project: extend them as it grows. Add project-specific
steps, update tool versions, and add optional parameters that focus a run (for example one library in a
multi-library repository, or headless tests without GUI automation). Two rules keep them trustworthy:

- With no arguments each script prints its usage text and exits 1, so an un-updated caller fails loudly;
  `-All` must still do the script's full job (CI and the agents rely on that). Slow or
  environment-dependent test tiers may be opt-in switches of `build.ps1`.
- Never remove or weaken a check to make lint or tests pass.

The reusable scoping logic (usage text, argument validation, changed-file discovery, the project graph) lives
in `scripts/ProjectScripts.psm1`, which is shared across DEMA repositories and synchronized verbatim from the
template: never edit it in this repository. Repository-specific behavior belongs in the `[PROJECT-SPECIFIC]`
blocks of the scripts (here the `$RuntimeReads` map in `build.ps1`). The switches are documented by
`-Help` of each script (see *Scoped Validation*).

# Continuous Compliance Overview

This repository follows the [Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance)
approach. Tools: **ReqStream** (requirements traceability), **ReviewMark** (file review enforcement),
**BuildMark** (tool versions), **VersionMark** (version tracking).
