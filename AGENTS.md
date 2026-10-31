# Project Overview

- **project-name**: `CanvasNet`
- **organization**: `DEMA Consulting`
- **project-tagline**: `Canvas rendering library for .NET`
- **description**: CanvasNet is a .NET library providing a canvas-based drawing and rendering API.
- **languages**: `C#`
- **technologies**: `.NET`

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
│   └── DemaConsulting.CanvasNet/
└── test/
    └── DemaConsulting.CanvasNet.Tests/
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
- **`.cspell.yaml`** - Spell-check configuration and technical term dictionary
- **`.markdownlint-cli2.yaml`** - Markdown formatting rules
- **`.yamllint.yaml`** - YAML formatting configuration
- **`.reviewmark.yaml`** - File review definitions and tracking
- **`nuget.config`** - NuGet package sources (if .NET)
- **`package.json`** - Node.js dependencies for formatting tools
- **`requirements.yaml`** - Root requirements file with includes
- **`pip-requirements.txt`** - Python dependencies for yamllint and yamlfix
- **`docs/sysml2/`** - SysML2 architecture model; authoritative source for software structure
- **`fix.ps1`** - Applies all auto-fixers silently (dotnet format, markdown, YAML). Always exits 0.
- **`build.ps1`** - Builds the solution and runs all tests (see *Scoped Validation* for development use).

# Standards Application (ALL Agents Must Follow)

Before performing any work, agents must read and apply the relevant standards
from `.github/standards/`. Use this matrix to determine which to load:

- **Any code**: `coding-principles.md`
- **C# code**: `coding-principles.md`, `csharp-language.md`
- **Any tests**: `testing-principles.md`
- **C# tests**: `testing-principles.md`, `csharp-testing.md`
- **Requirements**: `requirements-principles.md`, `software-items.md`, `reqstream-usage.md`
- **Design docs**: `software-items.md`, `design-documentation.md`, `technical-documentation.md`
- **Verification docs**: `software-items.md`, `verification-documentation.md`, `technical-documentation.md`
- **Review configuration**: `software-items.md`, `reviewmark-usage.md`
- **Software structure**: `sysml2-modeling.md`
- **Any documentation**: `technical-documentation.md`

Load only the standards relevant to your specific task scope.

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
pwsh ./fix.ps1
```

This automatically handles: `dotnet format`, markdown formatting, and YAML
formatting. Full lint compliance is a **pre-PR responsibility**, not an agent
responsibility - invoke the lint-fix agent once before submitting a pull request.

## CI Quality Tools

CI runs `lint.ps1` which checks: markdownlint-cli2, cspell, yamllint, dotnet format,
reqstream, versionmark, reviewmark, and sysml2tools.

# Scoped Validation (ALL Agents Must Follow)

The solution has ten source projects and ten test projects, and a full `pwsh ./build.ps1` builds and
tests all of them. While developing, build and test **only the affected test projects**; run the full
build once before a pull request. `build.ps1` is a template-owned script and is deliberately not
modified for this - use plain `dotnet test` on the test project instead. This policy refines any
"run `pwsh ./build.ps1`" step in the agent definitions under `.github/agents/`, which are template-owned:
use the scoped commands during development and the full build before a pull request. For example:

```pwsh
dotnet test test/DemaConsulting.CanvasNet.Svg.Tests --configuration Release
```

`dotnet test` on a test project also builds the source projects it references.

## Which Test Projects to Run

A source project is a dependency of every project that references it, directly or transitively
(derived from the `ProjectReference` graph). Run the test projects listed for each changed source project:

| Changed project | Test projects to run (all under `test/`) |
| --- | --- |
| `DemaConsulting.CanvasNet` (core) | **All** test projects (every other project depends on it) |
| `DemaConsulting.CanvasNet.Svg` | `Svg.Tests`, `Pptx.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Charts` | `Charts.Tests`, `Pptx.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Pdf` | `Pdf.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Pptx` | `Pptx.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Vsdx` | `Vsdx.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Render3D` | `Render3D.Tests`, `Scad.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Scad` | `Scad.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.VegaLite` | `VegaLite.Tests`, `Tool.Tests` |
| `DemaConsulting.CanvasNet.Tool` | `Tool.Tests` |
| A test project only | That test project only |

Test project names are `DemaConsulting.CanvasNet.{Name}` as in the table (the core project is
`DemaConsulting.CanvasNet.Tests`).

## Rules

- **Documentation-only changes** (`docs/`, `README.md`, agent files) need no build or test run;
  run `pwsh ./fix.ps1` and the relevant lint checks instead.
- **Changes outside a single project** (solution file, `global.json`, `.editorconfig`, package or
  build configuration, `build.ps1`) affect everything - run the full `pwsh ./build.ps1`.
- **Before opening or updating a pull request**, run the full `pwsh ./build.ps1` and `pwsh ./lint.ps1`
  once (via the lint-fix agent for lint). Scoped runs are for the inner development loop only and are
  not a substitute for the full build.

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

- `.reviewmark.yaml`, `.cspell.yaml`, `.editorconfig`
- `.markdownlint-cli2.yaml`, `.yamllint.yaml`
- `requirements.yaml`, `fix.ps1`, `lint.ps1`

# Continuous Compliance Overview

This repository follows the [Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance)
approach. Tools: **ReqStream** (requirements traceability), **ReviewMark** (file review enforcement),
**BuildMark** (tool versions), **VersionMark** (version tracking).
