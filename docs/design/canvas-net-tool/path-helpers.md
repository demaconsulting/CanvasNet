## PathHelpers

<!-- cspell:ignore Postconditions -->

![CanvasNetTool Structure](CanvasNetToolView.svg)

### Purpose

`PathHelpers` is a static utility class that provides a safe path-combination method. Its single
responsibility is to combine two path segments while verifying that the result does not escape the base
directory, protecting callers from string-level path-traversal attacks.

### Data Model

`PathHelpers` holds no instance state. The class is `internal static` with no fields or properties.

### Key Methods

**SafePathCombine**: Safely combines a base path and a relative path.

- *Parameters*: `string basePath` - the base directory path; `string relativePath` - the relative path to append.
- *Returns*: `string` - the combined path (preserves the caller's relative or absolute style).
- *Preconditions*: both parameters are non-null.
- *Postconditions*: the returned path, resolved to absolute form, is contained within `basePath`.

Validation steps: (1) reject null inputs with `ArgumentNullException.ThrowIfNull`; (2) call
`Path.Combine(basePath, relativePath)` to produce the candidate; (3) resolve both `basePath` and the candidate
to absolute form with `Path.GetFullPath`; (4) compute `Path.GetRelativePath(absoluteBase, absoluteCombined)`
and reject if the result equals `".."`, starts with `".."` followed by a directory separator, or is itself
rooted; (5) return the unresolved candidate from step 2.

The containment check uses `Path.GetRelativePath` rather than string inspection so that root paths, platform
case sensitivity, and separator normalization are handled natively. A double-dot segment counts as escaping
only when it is the entire relative result or is followed by a separator, so names such as `..data` are valid.
The rule is escape-based, not segment-based: a relative path containing `..` segments that stay under the base
directory (such as `a/../b`) is allowed, because the normalized result is still contained.

### Error Handling

`SafePathCombine` throws `ArgumentNullException` for null inputs and `ArgumentException`
("Invalid path component: {relativePath}") when the combined path escapes the base directory.
`NotSupportedException` and `PathTooLongException` may propagate from the underlying `Path` operations. No
logging or error accumulation is performed; callers receive exceptions directly.

### Dependencies

- **.NET BCL** - `Path` and `ArgumentNullException` are the only dependencies.

### Callers

- **Validation** - builds log file paths and temporary directory paths during self-validation.
