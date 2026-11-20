namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     An in-text link of a help card that a host writes in its own words (see <see cref="HelpCatalog.FormatMarkdown"/>).
/// </summary>
/// <remarks>
///     <para>
///         A card body links to another card with <c>[[a/b]]</c>, to an example of a card (or of a card below it)
///         with <c>[[a/b#name]]</c>, and to an example of the card holding the link (or of a card below it) with
///         <c>[[#name]]</c>. The library writes these as "the card <c>a/b</c>" and "the example <c>name</c>"; the
///         command-line tool writes them as the <c>canvasnet help</c> command that prints the target. Links inside
///         code spans and code blocks are literal text and never links.
///     </para>
///     <para>
///         A host only ever receives a link whose target is visible in the catalog view it formats; a link to a hidden
///         card or example becomes plain text instead. Instances are immutable and safe to share between threads.
///     </para>
/// </remarks>
public sealed class HelpLink
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="HelpLink"/> class.
    /// </summary>
    /// <param name="cardId">The target card, or <see langword="null"/> for a link to an example of the containing card.</param>
    /// <param name="exampleName">The example name, or <see langword="null"/> for a card link.</param>
    /// <param name="exampleCardId">The card that owns the example, or <see langword="null"/> for a card link.</param>
    internal HelpLink(string? cardId, string? exampleName, string? exampleCardId)
    {
        CardId = cardId;
        ExampleName = exampleName;
        ExampleCardId = exampleCardId;
    }

    /// <summary>
    ///     Gets the identifier of the target card as written (<c>a/b</c> of <c>[[a/b]]</c> or <c>[[a/b#name]]</c>), or
    ///     <see langword="null"/> for <c>[[#name]]</c>, which names an example of the card holding the link or of a card
    ///     below it. In a view that hides the card written in an example link, it is the card that owns the example
    ///     (<see cref="ExampleCardId"/>), so it never names a hidden card.
    /// </summary>
    public string? CardId { get; }

    /// <summary>
    ///     Gets the name of the target example, or <see langword="null"/> for a link to a card.
    /// </summary>
    public string? ExampleName { get; }

    /// <summary>
    ///     Gets the identifier of the card that owns the target example (the link's card or a card below it), or
    ///     <see langword="null"/> for a link to a card.
    /// </summary>
    public string? ExampleCardId { get; }
}
