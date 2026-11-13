using Microsoft.Win32.SafeHandles;

namespace DemaConsulting.CanvasNet.Files;

/// <summary>
///     Test seams around the open of a referenced file, so tests can change the file system or the reported real path
///     between the open and the check, deterministically and without timing.
/// </summary>
/// <remarks>
///     Used only through the internal constructors of <see cref="FileReferenceResolver"/> and
///     <see cref="SandboxedFileResolver"/>; production code never supplies hooks. Every member is optional.
/// </remarks>
internal sealed class FileOpenHooks
{
    /// <summary>
    ///     Gets the action run with the full path just before the file is opened.
    /// </summary>
    public Action<string>? BeforeOpen { get; init; }

    /// <summary>
    ///     Gets the opener that replaces the default open, or <see langword="null"/> for the default.
    /// </summary>
    public Func<string, SafeFileHandle>? Open { get; init; }

    /// <summary>
    ///     Gets the action run with the full path after the file was opened and before its handle is inspected.
    /// </summary>
    public Action<string>? AfterOpen { get; init; }

    /// <summary>
    ///     Gets the function that replaces the real path reported for an open handle (a <see langword="null"/> result
    ///     means "unavailable"), or <see langword="null"/> to ask the operating system.
    /// </summary>
    public Func<SafeFileHandle, string?>? RealPathOf { get; init; }
}
