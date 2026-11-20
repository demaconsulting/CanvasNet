namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     One help card as seen in one view of a <see cref="HelpCatalog"/>: a short Markdown text about one subject, a
///     node of the help tree.
/// </summary>
/// <remarks>
///     <para>
///         The identifier of a card is its path in the tree, for example <c>mermaid/gantt</c>. The text and the lists
///         belong to the view the card came from: <see cref="Markdown"/> writes the links in the library form, and a
///         link to a card or an example the view hides is plain text; <see cref="ChildIds"/> and
///         <see cref="SeeAlsoIds"/> name only visible cards. A host that wants its own link text uses
///         <see cref="HelpCatalog.FormatMarkdown"/>.
///     </para>
///     <para>Only the catalog creates cards; an instance is immutable and safe to share between threads.</para>
/// </remarks>
public sealed class HelpCard
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="HelpCard"/> class.
    /// </summary>
    /// <param name="id">The identifier (the path).</param>
    /// <param name="title">The title.</param>
    /// <param name="summary">The one-line summary.</param>
    /// <param name="tags">The tags.</param>
    /// <param name="aliases">The aliases.</param>
    /// <param name="markdown">The body in the library form of the view.</param>
    /// <param name="childIds">The visible children.</param>
    /// <param name="seeAlsoIds">The visible see-also targets.</param>
    /// <param name="examples">The examples.</param>
    internal HelpCard(
        string id,
        string title,
        string summary,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> aliases,
        string markdown,
        IReadOnlyList<string> childIds,
        IReadOnlyList<string> seeAlsoIds,
        IReadOnlyList<HelpExample> examples)
    {
        Id = id;
        Title = title;
        Summary = summary;
        Tags = tags;
        Aliases = aliases;
        Markdown = markdown;
        ChildIds = childIds;
        SeeAlsoIds = seeAlsoIds;
        Examples = examples;
    }

    /// <summary>
    ///     Gets the identifier of the card: its path in the tree, segments separated by <c>/</c>, for example
    ///     <c>mermaid/gantt</c>.
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Gets the name of the card among its siblings: the last segment of <see cref="Id"/>.
    /// </summary>
    public string Name => Id[(Id.LastIndexOf('/') + 1)..];

    /// <summary>
    ///     Gets the identifier of the structural parent card (the path without its last segment), or
    ///     <see langword="null"/> for a root card.
    /// </summary>
    /// <remarks>
    ///     The parent may be hidden in a filtered view; the nearest visible ancestor is the card whose
    ///     <see cref="HelpCatalog.GetChildren"/> lists this one.
    /// </remarks>
    public string? ParentId => Id.Contains('/', StringComparison.Ordinal) ? Id[..Id.LastIndexOf('/')] : null;

    /// <summary>
    ///     Gets the title of the card.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets the one-line summary of the card, shown in lists of cards.
    /// </summary>
    public string Summary { get; }

    /// <summary>
    ///     Gets the tags of the card, from the vocabulary of <see cref="HelpTags"/>: at least one scope tag, and the
    ///     format tags of the formats it is about.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    ///     Gets the other names that select the card among its siblings, for example <c>mmd</c> for <c>mermaid</c>.
    /// </summary>
    public IReadOnlyList<string> Aliases { get; }

    /// <summary>
    ///     Gets the Markdown body of the card: the numbers the code defines filled in, and every link written as
    ///     "the card <c>a/b</c>" or "the example <c>name</c>" (a link to a card or an example the view hides is the
    ///     plain title of the card, or "an example").
    /// </summary>
    public string Markdown { get; }

    /// <summary>
    ///     Gets the identifiers of the cards below this one that are visible in the view, in tree order: its children,
    ///     and in place of a hidden child that child's nearest visible descendants.
    /// </summary>
    public IReadOnlyList<string> ChildIds { get; }

    /// <summary>
    ///     Gets the identifiers of the related cards that are visible in the view, in order.
    /// </summary>
    public IReadOnlyList<string> SeeAlsoIds { get; }

    /// <summary>
    ///     Gets the examples of the card, in order.
    /// </summary>
    public IReadOnlyList<HelpExample> Examples { get; }
}
