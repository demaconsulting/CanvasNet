### FilterPaths

![PandocFilter Structure](PandocFilterView.svg)

#### Purpose

`FilterPaths` holds the path rules of the Pandoc filter: a literal port of the path helpers of the Lua filter (version
16) and of the `pandoc.path` functions it used, so that a picture's source is found, and the files of its dependencies
are named in the cache key, exactly as the Lua filter did. It also ports the tool's own rules for `file:` URIs
(`FileReferenceResolver.ParseFileUri`) and remote URLs (`FeatureCatalog.IsRemoteDataUrl`), so that the filter hashes
the files the tool reads. The unit is the internal static class `FilterPaths` in
`src/DemaConsulting.CanvasNet.Tool/PandocFilter/FilterPaths.cs`.

`pandoc.path` is Haskell's `System.FilePath` for the platform Pandoc runs on: on Windows both `/` and `\` separate, a
drive is `C:`, `C:\`, a share `\\server\`, or a device path `\\?\C:\` or `\\?\UNC\server\`, and elsewhere only `/`
separates. `Path.GetFullPath` is never used, because it trims trailing dots and spaces on Windows and would change the
names a key is made of.

#### Data Model

The class is stateless. **Windows** (`bool`) is fixed at start-up from `OperatingSystem.IsWindows()` and selects the
path rules; **Separator** is `\` on Windows and `/` elsewhere. **LuaSpaces** holds the characters of Lua's `%s` class in
the C locale (space, tab, line feed, vertical tab, form feed, carriage return).

#### Key Methods

**Lua text helpers**: `IsLuaSpace`, `HasNonSpace`, `TrimLuaSpace`, `TrimLuaSpaceStart`, and `TrimLuaSpaceEnd` use the
`%s` class; `AsciiLower` lower-cases A to Z only (`string.lower`); `CompareBytes` and `SortBytes` order texts by their
UTF-8 bytes (`table.sort`); `DecodePercent` replaces every `%XX` escape on the UTF-8 bytes, so `%C3%A9` is one
character, a `%` without two hexadecimal digits stays, and bytes that are not valid UTF-8 become U+FFFD.

**pandoc.path ports**: `Split` (`splitDirectories`: the drive, then each segment without trailing separators), `Join`
(`joinPath`: a segment that starts with a separator or has a drive replaces everything before it), `IsAbsolute` (on
Windows a drive with a separator or a share, so `\x` and `C:x` are relative; elsewhere a leading `/`), `Directory`
(`takeDirectory`; `.` for a bare name), `FileName` (`takeFileName`), `Stem` (the path without the last extension of its
file name), `SplitSearchPath` (`;` on Windows, where an empty entry is dropped and a double-quoted entry unquoted; `:`
elsewhere, where an empty entry is `.`), and `Normalize` (`normalise`: platform separators, no repeated separators or
`.` segments, `..` kept, a Windows drive letter upper-cased, one trailing separator for a folder path, `.` for an empty
path).

**Collapse(path)**: normalizes the path and removes each `..` with the segment before it; a `..` above the root of a
rooted path stays at the root, and a leading `..` of a relative path is kept. **Absolute(path, workingDirectory)**
collapses an absolute path, or the path joined to the given working directory (an explicit input, so no process state
is read).

**RelativePath(from, target)**: the path of the target seen from the folder, comparing whole segments (with their case),
joined with `/` (a backslash also becomes `/`); `null` when the two share no first segment (another drive).
**Label(root, path)** is the relative path, or the full path when there is none.

**FileUriPath(text)**: for a `file:` URI (scheme in any case) of the form `file:///x` or `file:/x`, the percent-decoded
path, with `/C:/x` read as `C:/x`; `null` for a URI with a host (`localhost` included), a decoded path that starts with
two separators, or another form. **IsRemoteUrl(url)**: after leading white space, `true` for two leading slashes or
backslashes, a scheme (`SchemeOf`, the Lua pattern `^(%a[%w+.-]*):`) of two or more characters other than `file`, or a
`file:` URI with a host; a single letter such as a drive is not a scheme.

**LocalDataFile(folder, url)**: `null` for a blank or remote URL; otherwise the `file:` URI path or the URL with `\`
read as `/`, then an absolute path collapsed, a path rooted without a drive placed on the drive of the folder, or the
path joined to the folder and collapsed. Percent signs of a plain path are kept. **LocalImageFile(folder, link)** does
the same after `CutFragment`, which removes everything from the first `#` or `?`.

**IsSameOrUnder(path, folder)**: compares whole segments after removing trailing separators, with `/` read as `\` and
the ASCII letter case ignored on Windows only.

**LocateSource(src, resourceDirectories)**: tries the name as written and then its percent-decoded form when that
differs; an absolute name is collapsed and used when it opens, and a relative name is joined to each resource folder in
order; the first file that opens is returned, otherwise `null`. There is no containment check: the filter is not a
security boundary.

**FileExists(path)** opens the file for reading with `FileShare.ReadWrite | FileShare.Delete` (Lua `io.open(path,
"rb")`); **ReadFile(path)** reads its bytes. These two are the only members that touch the file system.

#### Error Handling

No member throws for a path. `FileExists` returns `false`, and `ReadFile` returns `null`, for an `IOException`,
`UnauthorizedAccessException`, `ArgumentException`, or `NotSupportedException` (a folder, a missing or unreadable file,
or a malformed path). The other members are pure functions that return `null` (or the text unchanged) when a rule does
not apply. All members are safe to call from several threads at once.

#### Dependencies

- **.NET base class library** - `System.Text.Encoding` for the UTF-8 bytes, `FileStream` and `File` for the two file
  system reads.

#### Callers

- **PandocFilterEngine** - finds a picture's source with `LocateSource`, and takes folders and relative paths.
- **DependencyHasher** - resolves and labels the files a picture reads (`LocalDataFile`, `LocalImageFile`, `Label`,
  `ReadFile`, `SortBytes`, and the Lua text helpers).
- **FilterSettings** - reads the resource path (`SplitSearchPath`, `Absolute`) and the environment variables, and
  places folders (`IsSameOrUnder`, `RelativePath`).
- **PictureAttributes** - lower-cases and trims attribute values and sorts options (`SchemeOf`, `SortBytes`).
- **PictureCache** - builds the names of the cache files (`Join`, `FileName`, `Stem`, `FileExists`, `ReadFile`).
