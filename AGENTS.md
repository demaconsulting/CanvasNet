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
- **`fix.ps1`** - Applies all auto-fixers silently (dotnet format, LintMark, YAML). Always exits 0.
  `-Changed` limits it to the changed file types (see *Scoped Validation*).
- **`build.ps1`** - Builds the solution and runs all tests; `-Affected` limits it to the affected test
  projects (see *Scoped Validation*).
- **`lint.ps1`** - Runs all lint checks (the CI merge gate); `-Changed` limits it to the checks whose
  inputs changed (see *Scoped Validation*).

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

## Artifact-Location Comments Instead of SoftwareInfo (`software-items.md`, `sysml2tools-query`)

The shared standards and the `sysml2tools-query` skill describe artifact locations recorded as
`metadata info : SoftwareInfo { ... }` annotations and queried as `Metadata SoftwareInfo.*` lines. This repository's
SysML2 model has not been migrated to that metadata yet (the migration is deferred). Until it is:

- The model records artifact locations as comments on each element: `comment sourceRef /* Source: ... */`,
  `comment testRef /* Test: ... */`, `comment designRef /* Design: ... */`,
  `comment verificationRef /* Verification: ... */`, and `comment reqRef /* Requirements: ... */`, plus a few
  custom-named comments (such as `photoTestRef`, `mermaidTestRef`, `selectionRef`, and `distributionRef`) for
  additional test files and notes.
- Agents read those comments (with `dotnet sysml2tools query` describe or text output, or directly in the `.sysml`
  files under `docs/sysml2/`) instead of looking for `Metadata SoftwareInfo.*` lines, and do not add
  `docs/sysml2/common/software-items.sysml` or `SoftwareInfo` annotations piecemeal.
- New model elements follow the existing comment convention, so the model stays consistent until it is migrated as
  a whole.

## No Wall-Clock Dependence in Tests (`testing-principles.md`, `csharp-testing.md`)

Tests run on shared machines that are often heavily loaded (many agents build and test at once), so a test must
pass or fail the same way however fast or busy the machine is:

- Do not assert on elapsed time (`Stopwatch`, `DateTime.UtcNow` differences, "generous" limits, hang guards) and do
  not set per-test timeouts. A genuine hang is caught by the CI job, not by a per-test clock.
- Do not use timers, sleeps, or delays (`CancelAfter`, `Thread.Sleep`, `Task.Delay`, timed `Wait`/`Join`) to make
  something happen at the right moment. Trigger it deterministically from inside the work (for example cancel when
  a known callback, echo, or step is reached), or use a pre-canceled token.
- Prove bounds with deterministic measures instead: the specific limit error, steps or items charged, allocated
  bytes, or the absence of further work after a cancellation point.
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

After making changes, run the auto-fix pass. This applies all available fixers
silently and **always exits 0** - agents do not need to respond to its output.

```pwsh
pwsh ./fix.ps1 -Changed   # during development: only the fixers for the changed file types
pwsh ./fix.ps1            # everything
```

This automatically handles: `dotnet format`, Markdown formatting (LintMark), and YAML
formatting. Full lint compliance is a **pre-PR responsibility**, not an agent
responsibility - invoke the lint-fix agent once before submitting a pull request.

## CI Quality Tools

CI runs `lint.ps1` which checks: lintmark, textlint, yamllint, dotnet format,
reqstream, versionmark, reviewmark, and sysml2tools.

# Scoped Validation (ALL Agents Must Follow)

The solution has fourteen source projects and fourteen test projects, and a full `pwsh ./build.ps1` builds and
tests all of them. While developing, build, test, fix, and lint **only what the change affects**; run the
full build and full lint once before a pull request. The scripts compute the scope from the files changed
since `origin/main` (or `-Base <ref>`) plus uncommitted and untracked files:

```pwsh
pwsh ./build.ps1 -Affected   # build and test only the affected test projects
pwsh ./fix.ps1 -Changed      # run only the fixers for the changed file types
pwsh ./lint.ps1 -Changed     # run only the checks whose inputs changed
```

- `build.ps1 -Affected` maps each changed file to its project, adds every project that references it
  (directly or transitively, from the `ProjectReference` graph) and every project that links the file in
  (`Compile`/`None`/`EmbeddedResource` items pointing outside the project folder), then builds and tests the
  selected test projects. A change to the solution, a project file, `*.props`/`*.targets`, `global.json`,
  `.editorconfig`, `nuget.config`, `.config/dotnet-tools.json`, or `build.ps1` falls back to the full build.
  Repository files that tests read at run time (the user guides the help-card parity tests compare, and the
  checked-in Pandoc shim) select those test projects through the `$RuntimeReads` map in `build.ps1`; keep it in
  step when a test starts reading another repository file. Any other change the build does not read
  (documentation, images) builds nothing.
- `build.ps1 -Project Svg,Mermaid` selects explicit projects by full or short name (`Core` for the core
  library, `Svg.Tests` for a test project) with the same rule.
- `lint.ps1 -Changed` runs `dotnet format` only for changed C# files (the whole solution when a project or
  build configuration file changed), TextLint and LintMark only for the file types they check, yamllint
  only for YAML, and each compliance tool only when its inputs changed. It prints each skipped check.
- Add `-DryRun` to `build.ps1` or `lint.ps1` to print the selection without running anything.

These scripts started from the reference template and are customized here; with no switches they run
the full build, all fixers, and all checks exactly as before (CI runs `lint.ps1` with no switches). This
policy refines any "run `pwsh ./build.ps1`" step in the agent definitions under `.github/agents/`, which
are template-owned: use the scoped commands during development and the full build before a pull request.

## Which Test Projects to Run

`build.ps1 -Affected` computes this selection; the table is the human-readable reference. A source project
is a dependency of every project that references it, directly or transitively. Each changed source project
selects these test projects:

| Changed project | Test projects to run (all under `test/`) |
| --- | --- |
| `DemaConsulting.CanvasNet` (core) | **All** test projects except `Layout.Tests` (Layout references no project) |
| `DemaConsulting.CanvasNet.Svg` | `Svg.Tests`, `Pptx.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Charts` | `Charts.Tests`, `Pptx.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Pdf` | `Pdf.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Pptx` | `Pptx.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Vsdx` | `Vsdx.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Render3D` | `Render3D.Tests`, `Scad.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Scad` | `Scad.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.VegaLite` | `VegaLite.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Layout` | `Layout.Tests`, `Mermaid.Tests`, `PlantUml.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Mermaid` | `Mermaid.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.PlantUml` | `PlantUml.Tests`, `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Viewer` | `Viewer.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Tool` | `Tool.Tests` |
| A test project only | That test project only (see the shared test support note below) |

Test project names are `DemaConsulting.CanvasNet.{Name}` as in the table (the core project is
`DemaConsulting.CanvasNet.Tests`). A change to the core library or `VegaLite` also builds the
`tools/DemaConsulting.CanvasNet.VegaLite.Benchmarks` project (build only, no tests).

Shared test support is linked between test projects, and `-Affected` follows those links:

- `FixtureFactory*.cs`, `SurfaceAssert.cs`, and `Fixtures/**` live in `Viewer.Tests` and are linked into
  `Tool.Tests`: a change runs **both** `Viewer.Tests` and `Tool.Tests`.
- `TestSupport/**` in the core `Tests` project is linked into `Charts.Tests`, `Pdf.Tests`, `Pptx.Tests`,
  `Svg.Tests`, and `Vsdx.Tests` (and `SyntheticFontBuilder.cs` into `Scad.Tests`).
- The user-guide examples under `docs/user_guide/examples/` are linked into `VegaLite.Tests` (`gallery/**`),
  `Mermaid.Tests` (`mermaid-*.mmd`), and `PlantUml.Tests` (`plantuml-*.puml`).

## Rules

- **Documentation-only changes** (`docs/`, `README.md`, agent files) need no build or test run
  (`build.ps1 -Affected` reports nothing to build); run `pwsh ./fix.ps1 -Changed` and
  `pwsh ./lint.ps1 -Changed` instead.
- **Changes outside a single project** (solution file, project files, `global.json`, `.editorconfig`, package
  or build configuration, `build.ps1`) affect everything - `build.ps1 -Affected` runs the full build.
- **Before opening or updating a pull request** that changes code or build configuration, run the full
  `pwsh ./build.ps1` and `pwsh ./lint.ps1` once (via the lint-fix agent for lint). A documentation-only
  pull request needs only `pwsh ./fix.ps1` and `pwsh ./lint.ps1`. Scoped runs are for the inner
  development loop only and are not a substitute for the full build.

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

- Run with no arguments, each script still does its full job (CI and the agents rely on that); slow or
  environment-dependent test tiers may be opt-in switches of `build.ps1`.
- Never remove or weaken a check to make lint or tests pass.

This repository's focus parameters (`-Affected`, `-Project`, `-Changed`, `-Base`, `-DryRun`) are described in
*Scoped Validation*.

# Continuous Compliance Overview

This repository follows the [Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance)
approach. Tools: **ReqStream** (requirements traceability), **ReviewMark** (file review enforcement),
**BuildMark** (tool versions), **VersionMark** (version tracking).
