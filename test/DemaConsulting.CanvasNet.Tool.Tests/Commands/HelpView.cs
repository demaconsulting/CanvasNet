using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Presentation;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Reads the combined help catalog of the tool (<see cref="HelpTopics.Catalog"/>) for the tests: the cards by
///     identifier, the trees, the examples, and the renderings the help command prints.
/// </summary>
internal static class HelpView
{
    /// <summary>
    ///     Gets every card, depth first.
    /// </summary>
    public static IReadOnlyList<HelpCard> All => HelpTopics.Catalog.Cards;

    /// <summary>
    ///     Gets a card by its identifier.
    /// </summary>
    /// <param name="id">The identifier, for example <c>mermaid/gantt</c>.</param>
    /// <returns>The card.</returns>
    public static HelpCard Get(string id)
    {
        return Assert.Single(All, c => c.Id == id);
    }

    /// <summary>
    ///     Gets the cards directly below a card, in order.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The child cards.</returns>
    public static IReadOnlyList<HelpCard> ChildrenOf(HelpCard card) => HelpTopics.Catalog.GetChildren(card);

    /// <summary>
    ///     Gets a card and every card below it, depth first.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The cards of the tree.</returns>
    public static IEnumerable<HelpCard> Subtree(HelpCard card)
    {
        yield return card;
        foreach (var child in ChildrenOf(card).SelectMany(Subtree))
        {
            yield return child;
        }
    }

    /// <summary>
    ///     Gets every example of every card, with its card, depth first.
    /// </summary>
    /// <returns>The examples.</returns>
    public static IEnumerable<(HelpCard Card, HelpExample Example)> AllExamples() =>
        All.SelectMany(c => c.Examples, (c, e) => (c, e));

    /// <summary>
    ///     Gets the examples of a card and of every card below it.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The card's own examples first, then those of its descendants, depth first.</returns>
    public static IEnumerable<(HelpCard Card, HelpExample Example)> SubtreeExamples(HelpCard card) =>
        Subtree(card).SelectMany(c => c.Examples, (c, e) => (c, e));

    /// <summary>
    ///     Gets the cards below a card that have examples of their own.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The descendants with examples, depth first.</returns>
    public static IEnumerable<HelpCard> CardsBelowWithExamples(HelpCard card) =>
        Subtree(card).Skip(1).Where(c => c.Examples.Count > 0);

    /// <summary>
    ///     Renders a card as the help command prints it as text.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The text.</returns>
    public static string RenderText(HelpCard card) => HelpTopics.Catalog.Render(card, HelpTopics.RenderOptions(HelpFormat.Text));

    /// <summary>
    ///     Renders a card as the help command prints it as JSON.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The document.</returns>
    public static string RenderJson(HelpCard card) => HelpTopics.Catalog.Render(card, HelpTopics.RenderOptions(HelpFormat.Json));

    /// <summary>
    ///     Gets the exact content of an example of a card or of a card below it, as <c>--example</c> prints it.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="name">The example name.</param>
    /// <returns>The content.</returns>
    public static string RenderExample(HelpCard card, string name) => HelpTopics.Catalog.RenderExample(card, name);

    /// <summary>
    ///     The members the tests read from a card as the command line shows it.
    /// </summary>
    /// <param name="card">The card.</param>
    extension(HelpCard card)
    {
        /// <summary>
        ///     Gets the identifier of the root card of the tree the card is in.
        /// </summary>
        public string RootId => card.Id.Split('/')[0];

        /// <summary>
        ///     Gets the help path of the card: its identifier with spaces between the segments.
        /// </summary>
        public string Path => card.Id.Replace('/', ' ');

        /// <summary>
        ///     Gets the command that prints the card.
        /// </summary>
        public string Command => $"canvasnet help {card.Id.Replace('/', ' ')}";

        /// <summary>
        ///     Gets the depth of the card: 0 for a root card.
        /// </summary>
        public int Depth => card.Id.Count(c => c == '/');

        /// <summary>
        ///     Gets the related cards of the card.
        /// </summary>
        public IReadOnlyList<string> SeeAlso => card.SeeAlsoIds;

        /// <summary>
        ///     Gets the body as the command line prints it: every link written as the <c>canvasnet help</c> command
        ///     that prints its target.
        /// </summary>
        public string CommandBody => HelpTopics.Catalog.FormatBody(card, HelpTopics.RenderOptions(HelpFormat.Text));
    }
}
