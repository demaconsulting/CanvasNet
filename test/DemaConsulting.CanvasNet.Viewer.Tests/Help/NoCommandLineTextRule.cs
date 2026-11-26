using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.HelpCards.Catalog;
using DemaConsulting.HelpCards.Testing;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     The CanvasNet rule that a library card has no command-line text: its title, summary, body, example descriptions,
///     and example contents never name the <c>canvasnet</c> tool or a <c>--option</c>, and no example is a command line.
/// </summary>
/// <remarks>
///     The library cards are for every host, not only the command-line tool, which adds its flags and command examples
///     with overlays. The validator of DemaConsulting.HelpCards applies the rule to every card; the allow-list holds the
///     text that names the tool for another reason. The rule is stateless and safe to use from several threads.
/// </remarks>
internal sealed class NoCommandLineTextRule : IHelpCardRule
{
    /// <summary>
    ///     The identifier of the rule in the problems it reports.
    /// </summary>
    public const string RuleId = "canvasnet-no-command-line-text";

    /// <summary>
    ///     Matches the name of the command-line tool, as a word, case-sensitively (the product name is CanvasNet).
    /// </summary>
    public static readonly Regex ToolName = new(@"\bcanvasnet\b", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Matches a command-line option: two dashes, a letter, and at least one more letter, digit, or dash, not preceded
    ///     by a word character or a dash (so the one-letter Mermaid edges <c>--o</c> and <c>--x</c> and the arrows
    ///     <c>--&gt;</c> and <c>---</c> do not match).
    /// </summary>
    public static readonly Regex CommandLineOption = new(@"(?<![\w-])--[a-z][a-z0-9-]+", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     The text that may contain the tool name without being command-line text.
    /// </summary>
    public static readonly string[] AllowList =
    [
        // The XML namespace of the position bindings of an SVG file (svg/bindings and the framed-flow example)
        "https://demaconsulting.com/canvasnet"
    ];

    /// <summary>
    ///     Checks one card for command-line text.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <returns>One error for each distinct tool name or option found, and one for each command example.</returns>
    public IEnumerable<HelpProblem> Check(HelpCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        // Every string of the card that a reader sees, with the allowed text removed
        var texts = new[] { card.Title, card.Summary, card.Body }
            .Concat(card.Examples.SelectMany(e => new[] { e.Description, e.Content }))
            .Select(text => AllowList.Aggregate(text, (t, allowed) => t.Replace(allowed, string.Empty, StringComparison.Ordinal)));

        // The tool names and options found, each once
        var found = texts
            .SelectMany(t => ToolName.Matches(t).Concat(CommandLineOption.Matches(t)).Select(m => m.Value))
            .Distinct(StringComparer.Ordinal)
            .Select(value => Problem(card, $"The library card names the command line: '{value}'."));

        // A command example belongs to the tool's overlay, not to the library card
        var commands = card.Examples
            .Where(e => e.Kind == HelpExample.CommandKind)
            .Select(e => Problem(card, $"The library example '{e.Name}' is a command line."));
        return found.Concat(commands).ToList();
    }

    /// <summary>
    ///     Creates an error of the rule for a card.
    /// </summary>
    /// <param name="card">The card.</param>
    /// <param name="message">The message.</param>
    /// <returns>The problem.</returns>
    private static HelpProblem Problem(HelpCard card, string message)
    {
        return new HelpProblem(HelpSeverity.Error, RuleId, card.SourceName, card.LocalId + ".md", 0, message);
    }

    /// <summary>
    ///     Checks every card of the library catalog with this rule, for a test that applies it alone.
    /// </summary>
    /// <returns>The problems of every library card.</returns>
    public static IReadOnlyList<HelpProblem> CheckLibrary()
    {
        var rule = new NoCommandLineTextRule();
        return CanvasNetHelp.Catalog.Cards.SelectMany(rule.Check).ToList();
    }
}
