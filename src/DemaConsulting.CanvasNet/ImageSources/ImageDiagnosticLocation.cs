namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     Where in a file a note points: the file, the line and column, and (for JSON) the pointer.
/// </summary>
/// <param name="File">The file the note is about, as the format library reported it; <see langword="null"/> when unknown.</param>
/// <param name="Line">The 1-based line, or <see langword="null"/> when unknown.</param>
/// <param name="Column">The 1-based column, or <see langword="null"/> when unknown.</param>
/// <param name="Pointer">A JSON pointer (RFC 6901) into a JSON file, or <see langword="null"/> when there is none.</param>
public sealed record ImageDiagnosticLocation(string? File, int? Line, int? Column, string? Pointer);
