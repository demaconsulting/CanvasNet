namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     A tiny example of a help card: a source file that renders on its own, a snippet that refers to other files, or
///     (only in a host's extension, such as the command-line tool) a command line.
/// </summary>
/// <remarks>
///     Examples are read from embedded resources when the catalog is loaded and never change afterwards, so an instance
///     is immutable and safe to share between threads. Only the catalog creates them.
/// </remarks>
public sealed class HelpExample
{
    /// <summary>
    ///     The kind of an example whose content is a complete source file that renders on its own.
    /// </summary>
    public const string SourceKind = "source";

    /// <summary>
    ///     The kind of an example whose content is a source file that refers to sibling files, so it is shown but cannot
    ///     be rendered alone.
    /// </summary>
    public const string SnippetKind = "snippet";

    /// <summary>
    ///     The kind of an example whose content is a command line; only an extension of the catalog (see
    ///     <see cref="HelpCatalog.Extend"/>) has such examples, never the library cards.
    /// </summary>
    public const string CommandKind = "command";

    /// <summary>
    ///     Initializes a new instance of the <see cref="HelpExample"/> class.
    /// </summary>
    /// <param name="name">The name, unique in the tree of the root card.</param>
    /// <param name="kind">The kind: <see cref="SourceKind"/>, <see cref="SnippetKind"/>, or <see cref="CommandKind"/>.</param>
    /// <param name="language">The language of the content.</param>
    /// <param name="fileName">The file name of a source or a snippet, or <see langword="null"/> for a command.</param>
    /// <param name="description">The one-line description.</param>
    /// <param name="content">The exact content: LF line endings and no trailing newline.</param>
    internal HelpExample(string name, string kind, string language, string? fileName, string description, string content)
    {
        Name = name;
        Kind = kind;
        Language = language;
        FileName = fileName;
        Description = description;
        Content = content;
    }

    /// <summary>
    ///     Gets the name of the example, unique within the tree of its root card, for example <c>gantt</c>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the kind of the example: <see cref="SourceKind"/>, <see cref="SnippetKind"/>, or
    ///     <see cref="CommandKind"/>.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    ///     Gets the language of the content, for example <c>mermaid</c>, <c>openscad</c>, <c>json</c>, <c>xml</c>, or
    ///     <c>shell</c>; suitable as the info string of a Markdown code fence.
    /// </summary>
    public string Language { get; }

    /// <summary>
    ///     Gets the file name to save a source or a snippet as (its extension selects the format, and a snippet's
    ///     sibling files are named relative to it), or <see langword="null"/> for a command.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    ///     Gets the one-line description of the example.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Gets the exact text of the example, with LF line endings and no trailing newline.
    /// </summary>
    public string Content { get; }
}
