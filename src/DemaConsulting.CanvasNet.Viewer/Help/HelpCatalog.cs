using System.Collections.ObjectModel;
using System.Reflection;

namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     The catalog of help cards about the drawing and file formats, for hosts that give the knowledge to a person or
///     an agent programmatically: what to write in a file, how to make it look good, how files compose, what is
///     reported, the limits, and what the host controls.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="Default"/> holds the library cards, embedded in this assembly. A card is a short Markdown text
///         with a title, a summary, tags from <see cref="HelpTags"/>, aliases, children (the help tree), related cards,
///         and tiny examples. <see cref="Find"/> resolves a card by its path, <see cref="Filter"/> gives a view of only
///         the cards with some tags, and <see cref="RenderText"/> and <see cref="RenderJson"/> write a card for an
///         agent. <see cref="Extend"/> layers a host's own cards and additions on the library cards, which is how the
///         <c>canvasnet</c> command-line tool adds its command-line sections.
///     </para>
///     <para>
///         <b>Filtered views.</b> In a view each card is visible or hidden by its own tags; a hidden parent does not
///         hide its children. <see cref="Roots"/> lists the visible cards that have no visible ancestor, and
///         <see cref="GetChildren"/> lists the nearest visible descendants of a card, so the visible children of a
///         hidden card move up to its nearest visible ancestor (or to the roots). <see cref="Find"/> still walks the
///         full path through hidden cards, but returns <see langword="null"/> for a hidden card. Related cards and
///         in-text links never point at a hidden card: a related hidden card is left out, and a link to a hidden card
///         or example is written as plain text (the card's title, or "an example"), so an agent is never sent to a
///         card it cannot read.
///     </para>
///     <para>
///         A catalog is immutable once created, and every member is safe to call from several threads at once.
///     </para>
/// </remarks>
public sealed partial class HelpCatalog
{
    /// <summary>
    ///     The prefix of the logical names of the embedded library help resources.
    /// </summary>
    internal const string LibraryResourcePrefix = "DemaConsulting.CanvasNet.Viewer.Help";

    /// <summary>
    ///     The library catalog, loaded on first use.
    /// </summary>
    private static readonly Lazy<HelpCatalog> DefaultCatalog = new(LoadDefault, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    ///     The structural data shared by every view of the catalog: every card, merged with its extensions.
    /// </summary>
    private readonly HelpData _data;

    /// <summary>
    ///     The identifiers of the cards visible in this view, or <see langword="null"/> when every card is visible.
    /// </summary>
    private readonly HashSet<string>? _visible;

    /// <summary>
    ///     The visible cards of this view, by identifier.
    /// </summary>
    private readonly Dictionary<string, HelpCard> _byId;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HelpCatalog"/> class: a view of some data.
    /// </summary>
    /// <param name="data">The structural data.</param>
    /// <param name="visible">The identifiers of the visible cards, or <see langword="null"/> for every card.</param>
    private HelpCatalog(HelpData data, HashSet<string>? visible)
    {
        _data = data;
        _visible = visible;

        // Build the cards of this view, depth first, with the links written for what this view shows
        var cards = new List<HelpCard>();
        _byId = new Dictionary<string, HelpCard>(StringComparer.Ordinal);
        foreach (var node in data.Order.Where(n => IsVisible(n.Id)))
        {
            var card = new HelpCard(
                node.Id,
                node.Title,
                node.Summary,
                AsReadOnly(node.Tags),
                AsReadOnly(node.Aliases),
                WriteBody(node, null),
                AsReadOnly(VisibleBelow(node.ChildIds)),
                AsReadOnly(node.SeeAlso.Where(IsVisible)),
                AsReadOnly(node.Examples));
            cards.Add(card);
            _byId.Add(card.Id, card);
        }

        Cards = cards.AsReadOnly();
        Roots = AsReadOnly(VisibleBelow(data.RootIds).Select(id => _byId[id]));
    }

    /// <summary>
    ///     Gets the library catalog: every help card embedded in this assembly, unfiltered.
    /// </summary>
    /// <remarks>
    ///     The cards are read and validated once, on first use, from a thread-safe lazy value; every later call returns
    ///     the same instance.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when an embedded card is malformed (a defect of the library, which its unit tests prevent); the
    ///     message names the card file and the line.
    /// </exception>
    public static HelpCatalog Default => DefaultCatalog.Value;

    /// <summary>
    ///     Gets the visible cards of this view, depth first in tree order (each root followed by the cards below it).
    /// </summary>
    public IReadOnlyList<HelpCard> Cards { get; }

    /// <summary>
    ///     Gets the visible cards that have no visible ancestor, in tree order: the root cards of an unfiltered catalog,
    ///     and in a filtered view also the nearest visible descendants of hidden root cards.
    /// </summary>
    public IReadOnlyList<HelpCard> Roots { get; }

    /// <summary>
    ///     Finds a card by its path.
    /// </summary>
    /// <param name="path">
    ///     The path: segments separated by <c>/</c> or white space, each the name or an alias of a card below the
    ///     previous one (the first a root card), matched ignoring case; for example <c>mermaid/gantt</c>,
    ///     <c>mmd gantt</c>, or <c>MMD/Gantt</c>. Hidden cards on the way are walked through.
    /// </param>
    /// <returns>
    ///     The card, or <see langword="null"/> when the path is empty, names no card, or names a card this view hides.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is <see langword="null"/>.</exception>
    public HelpCard? Find(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        // Walk the full structural tree, so a visible card below a hidden one is still found by its path
        var segments = path.Split(['/', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        IReadOnlyList<string> candidates = _data.RootIds;
        HelpNode? node = null;
        foreach (var segment in segments)
        {
            node = candidates.Select(id => _data.ById[id]).FirstOrDefault(n => n.IsNamed(segment));
            if (node == null)
            {
                return null;
            }

            candidates = node.ChildIds;
        }

        return node != null && _byId.TryGetValue(node.Id, out var card) ? card : null;
    }

    /// <summary>
    ///     Gets the cards below a card that are visible in this view: its visible children and, in place of a hidden
    ///     child, that child's nearest visible descendants, in tree order.
    /// </summary>
    /// <param name="card">A card visible in this view (a card of another view with the same identifier is accepted).</param>
    /// <returns>The cards; empty for a leaf.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the card is not visible in this view.</exception>
    public IReadOnlyList<HelpCard> GetChildren(HelpCard card)
    {
        return AsReadOnly(Require(card).ChildIds.Select(id => _byId[id]));
    }

    /// <summary>
    ///     Gets the related cards of a card that are visible in this view, in order.
    /// </summary>
    /// <param name="card">A card visible in this view (a card of another view with the same identifier is accepted).</param>
    /// <returns>The cards; empty when there are none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the card is not visible in this view.</exception>
    public IReadOnlyList<HelpCard> GetSeeAlso(HelpCard card)
    {
        return AsReadOnly(Require(card).SeeAlsoIds.Select(id => _byId[id]));
    }

    /// <summary>
    ///     Gets a view of this catalog with only the cards that have some tags and not others.
    /// </summary>
    /// <param name="include">
    ///     The tags of which a visible card must have at least one; <see langword="null"/> or empty to keep every card.
    /// </param>
    /// <param name="exclude">The tags of which a visible card must have none; <see langword="null"/> or empty for none.</param>
    /// <returns>
    ///     The view: a card is visible when it is visible in this catalog, it has an included tag (or nothing is
    ///     included), and it has no excluded tag. See the remarks of <see cref="HelpCatalog"/> for hidden parents and
    ///     links. Creating a view does not change this catalog.
    /// </returns>
    /// <exception cref="ArgumentException">
    ///     Thrown when a tag is <see langword="null"/> or is not one of <see cref="HelpTags.All"/>.
    /// </exception>
    public HelpCatalog Filter(IEnumerable<string>? include, IEnumerable<string>? exclude = null)
    {
        var included = RequireTags(include, nameof(include));
        var excluded = RequireTags(exclude, nameof(exclude));

        // A card of this view stays visible when its own tags pass both lists
        var visible = new HashSet<string>(
            _data.Order
                .Where(n => IsVisible(n.Id))
                .Where(n => included.Count == 0 || n.Tags.Any(included.Contains))
                .Where(n => !n.Tags.Any(excluded.Contains))
                .Select(n => n.Id),
            StringComparer.Ordinal);
        return new HelpCatalog(_data, visible);
    }

    /// <summary>
    ///     Writes the body of a card with each visible link in the host's own words.
    /// </summary>
    /// <param name="card">A card visible in this view (a card of another view with the same identifier is accepted).</param>
    /// <param name="formatLink">
    ///     Writes one link, for example as a command that prints the target; it is called only for links whose target
    ///     card or example is visible in this view, in the order they appear.
    /// </param>
    /// <returns>
    ///     The Markdown body with the links written by <paramref name="formatLink"/>; a link to a hidden card or example
    ///     is plain text, as in <see cref="HelpCard.Markdown"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the card is not visible in this view.</exception>
    public string FormatMarkdown(HelpCard card, Func<HelpLink, string> formatLink)
    {
        ArgumentNullException.ThrowIfNull(formatLink);
        return WriteBody(_data.ById[Require(card).Id], formatLink);
    }

    /// <summary>
    ///     Gets a catalog with the cards of another assembly layered on this one, so a host can add its own cards and
    ///     its own sections of the library cards (the command-line tool adds its commands and options this way).
    /// </summary>
    /// <param name="assembly">The assembly whose embedded resources hold the cards.</param>
    /// <param name="resourcePrefix">
    ///     The prefix of the logical resource names: a card is <c>&lt;prefix&gt;/&lt;path&gt;.md</c> (for example
    ///     <c>MyHost.Help/widgets.md</c>) and an example file is <c>&lt;prefix&gt;.Examples.&lt;root&gt;-&lt;file&gt;</c>.
    /// </param>
    /// <param name="tokens">
    ///     Extra <c>{{name}}</c> tokens of the host's cards, or <see langword="null"/> for none; they may not redefine a
    ///     library token.
    /// </param>
    /// <returns>
    ///     The combined catalog, unfiltered. A host card with <c>extends: &lt;id&gt;</c> (at the path of that card) is an
    ///     overlay: its body is appended to the card's body, its related cards and examples are appended to the card's,
    ///     and its title and summary, if given, replace the card's. Any other host card starts a new root tree, listed
    ///     after the roots of this catalog. This catalog does not change.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> or <paramref name="resourcePrefix"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when this catalog is a filtered view (extend the unfiltered catalog, then filter), when a host card or
    ///     example is malformed (the message names the file and the line), when an overlay names no card, when a new
    ///     card is below a card of this catalog, or when a token redefines a library token.
    /// </exception>
    /// <remarks>Reads the embedded resources of <paramref name="assembly"/> once, during the call.</remarks>
    public HelpCatalog Extend(Assembly assembly, string resourcePrefix, IReadOnlyDictionary<string, string>? tokens = null)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(resourcePrefix);

        var (cards, examples) = ReadResources(assembly, resourcePrefix);
        return ExtendWith(cards, examples, tokens);
    }

    /// <summary>
    ///     Gets whether a card is visible in this view.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns><see langword="true"/> when the card is visible.</returns>
    private bool IsVisible(string id)
    {
        return _visible == null || _visible.Contains(id);
    }

    /// <summary>
    ///     Lists the nearest visible cards at or below some cards, in tree order.
    /// </summary>
    /// <param name="ids">The cards to start from.</param>
    /// <returns>Each visible card, and in place of each hidden card its nearest visible descendants.</returns>
    private IEnumerable<string> VisibleBelow(IEnumerable<string> ids)
    {
        foreach (var id in ids)
        {
            if (IsVisible(id))
            {
                yield return id;
                continue;
            }

            foreach (var below in VisibleBelow(_data.ById[id].ChildIds))
            {
                yield return below;
            }
        }
    }

    /// <summary>
    ///     Gets the card of this view with the identifier of a card.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>The card of this view.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="card"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when the card is not visible in this view.</exception>
    private HelpCard Require(HelpCard card)
    {
        ArgumentNullException.ThrowIfNull(card);
        return _byId.TryGetValue(card.Id, out var own)
            ? own
            : throw new ArgumentException($"The help card '{card.Id}' is not visible in this catalog.", nameof(card));
    }

    /// <summary>
    ///     Validates a list of tags against the vocabulary.
    /// </summary>
    /// <param name="tags">The tags, or <see langword="null"/>.</param>
    /// <param name="parameter">The parameter name, for the exception.</param>
    /// <returns>The tags as a set; empty for <see langword="null"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when a tag is <see langword="null"/> or unknown.</exception>
    private static HashSet<string> RequireTags(IEnumerable<string>? tags, string parameter)
    {
        // A null tag is reported like an unknown one (a set keeps a null element)
        var set = new HashSet<string>(tags ?? [], StringComparer.Ordinal);
        var unknown = set
            .Where(t => t is null || !HelpTags.All.Contains(t))
            .Select(t => t ?? "null")
            .FirstOrDefault();
        if (unknown != null)
        {
            throw new ArgumentException(
                $"The help tag '{unknown}' is unknown; the tags are {string.Join(", ", HelpTags.All)}.",
                parameter);
        }

        return set;
    }

    /// <summary>
    ///     Copies a sequence into an immutable list.
    /// </summary>
    /// <typeparam name="T">The item type.</typeparam>
    /// <param name="items">The items.</param>
    /// <returns>The read-only list.</returns>
    private static ReadOnlyCollection<T> AsReadOnly<T>(IEnumerable<T> items)
    {
        return items.ToList().AsReadOnly();
    }

    /// <summary>
    ///     Loads the library catalog from the resources of this assembly.
    /// </summary>
    /// <returns>The catalog.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a card is malformed.</exception>
    private static HelpCatalog LoadDefault()
    {
        var (cards, examples) = ReadResources(typeof(HelpCatalog).Assembly, LibraryResourcePrefix);
        return Create(cards, examples, LibraryRootOrder);
    }
}
