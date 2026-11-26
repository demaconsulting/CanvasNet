using System.Text;
using System.Text.RegularExpressions;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.MathMl;
using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.CanvasNet.Viewer.Imaging;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Unit tests for the MathML help cards and the MathML user guide: every equation they show renders in strict mode
///     without a diagnostic, the composition blocks draw the equation they link, the diagnostics they cite are reported by
///     their triggers, the advice they give holds for the real layout, the cards cover the authoring tasks, and the lists
///     of the cards and the guide are the same.
/// </summary>
public partial class HelpCatalogTests
{
    /// <summary>
    ///     The MathML cards that teach one authoring task, each with at least one source example.
    /// </summary>
    private static readonly string[] MathMlTaskCards =
    [
        "basics", "fractions", "scripts", "large-operators", "fences", "matrices", "aligned", "units", "symbols", "accents",
        "formulas", "style", "pitfalls"
    ];

    /// <summary>
    ///     Matches a fenced code block of a card or a guide: its language and its content.
    /// </summary>
    private static readonly Regex FencedBlock =
        new(@"^```(?<language>[a-z-]+)\n(?<content>.*?)\n```$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Singleline, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Matches a Pandoc code block of class <c>mathml</c> (<c>```{.mathml ...}</c>) of a guide, the live block and the
    ///     copy shown inside a <c>markdown</c> block alike: its content.
    /// </summary>
    private static readonly Regex PandocMathMlBlock =
        new(@"^```\{\.mathml(?:[ \t][^\n]*)?\}\n(?<content>.*?)\n```$", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.Singleline, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Matches the equation of the rendering example of the MathML guide: the C# raw string passed to
    ///     <c>MathMlDocument.Parse</c>.
    /// </summary>
    private static readonly Regex RawStringEquation =
        new(@"MathMlDocument\.Parse\(""""""\n(?<text>.*?)\n[ ]*""""""\)", RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromSeconds(5));

    /// <summary>
    ///     Test that every whole equation (a fenced <c>xml</c> block or a Pandoc <c>{.mathml}</c> block that starts with
    ///     <c>&lt;math</c>) of a MathML card renders through
    ///     <see cref="CanvasNetViewer.Render(Stream, string, ViewerRenderOptions?)"/> in strict mode without a diagnostic;
    ///     fragments of single elements are not equations and are not rendered.
    /// </summary>
    [Fact]
    public void HelpCatalog_MathMlCards_FencedEquations_RenderStrictWithoutDiagnostics()
    {
        // Arrange: the whole equations of the MathML cards
        var equations = HelpCardText.Subtree(HelpCardText.Card("mathml"))
            .SelectMany(card => MathMlEquations(card.Markdown).Select(equation => (card.Id, Equation: equation)))
            .ToList();

        // Act / Assert: there are some, and each renders strictly without a diagnostic
        Assert.True(equations.Count >= 4, $"Only {equations.Count} fenced equations were found in the MathML cards.");
        foreach (var (id, equation) in equations)
        {
            AssertRendersStrictlyWithoutDiagnostics(id, equation);
        }
    }

    /// <summary>
    ///     Test that every whole equation (a fenced <c>xml</c> block or a Pandoc <c>{.mathml}</c> block, including the copy
    ///     shown inside a <c>markdown</c> block, that starts with <c>&lt;math</c>) of the MathML user guide renders in
    ///     strict mode without a diagnostic.
    /// </summary>
    [Fact]
    public void HelpCatalog_MathMlGuide_FencedEquations_RenderStrictWithoutDiagnostics()
    {
        // Arrange: the whole equations of the guide
        var guide = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-math-ml.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var equations = MathMlEquations(guide).ToList();

        // Act / Assert: there are some, the Pandoc blocks among them, and each renders strictly without a diagnostic
        Assert.True(equations.Count >= 3, $"Only {equations.Count} fenced equations were found in the MathML guide.");
        Assert.True(PandocMathMlBlock.Matches(guide).Count >= 2, "The Pandoc mathml blocks of the MathML guide were not found.");
        foreach (var equation in equations)
        {
            AssertRendersStrictlyWithoutDiagnostics("canvas-net-math-ml.md", equation);
        }
    }

    /// <summary>
    ///     Test that the rendering example of the MathML user guide (Example 78) states the numbers the library reports for
    ///     its equation: the display and the element count, the natural size and baseline, the pixel size at scale 3, and
    ///     the inline size, in the library block and in the Viewer block.
    /// </summary>
    [Fact]
    public void HelpCatalog_MathMlGuide_RenderingExample_StatesTheMeasuredNumbers()
    {
        // Arrange: the section of Example 78 and the equation it parses (its raw string without the indentation)
        var guide = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-math-ml.md")).Replace("\r\n", "\n", StringComparison.Ordinal);
        var start = guide.IndexOf("### Example 78", StringComparison.Ordinal);
        var end = guide.IndexOf("### Example 79", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "The section of Example 78 was not found in the MathML guide.");
        var section = guide[start..end];
        var raw = RawStringEquation.Match(section);
        Assert.True(raw.Success, "The equation of Example 78 was not found.");
        var text = string.Join("\n", raw.Groups["text"].Value.Split('\n').Select(line => line.StartsWith("    ", StringComparison.Ordinal) ? line[4..] : line));

        // Act: the numbers the library reports
        var cancellationToken = TestContext.Current.CancellationToken;
        var equation = MathMlDocument.Parse(text);
        var natural = equation.Measure(cancellationToken: cancellationToken);
        using var picture = equation.Render(new MathMlRenderOptions { Scale = 3 }, cancellationToken);
        var inline = equation.Measure(new MathMlRenderOptions { Display = MathMlDisplay.Inline }, cancellationToken);

        // Assert: the guide states them
        Assert.Contains($"// Output: {equation.Display}, {equation.ElementCount} elements", section, StringComparison.Ordinal);
        Assert.Contains($"// Output: {natural.Width}x{natural.Height}, baseline {natural.Baseline}", section, StringComparison.Ordinal);
        Assert.Contains($"// {picture.Width}x{picture.Height} pixels", section, StringComparison.Ordinal);
        Assert.Contains($"// Output: {inline.Width}x{inline.Height}\n", section, StringComparison.Ordinal);
        Assert.Contains($"// Output: inline, {inline.Width}x{inline.Height}", section, StringComparison.Ordinal);

        // Assert: the numbers the guide's text and the size of the quadratic placements are written with
        Assert.Equal((MathMlDisplay.Block, 22), (equation.Display, equation.ElementCount));
        Assert.Equal(new MathMlSize(138, 44, 32), natural);
        Assert.Equal((413, 131), (picture.Width, picture.Height));
        Assert.Equal((96, 33), (inline.Width, inline.Height));
    }

    /// <summary>
    ///     Test that the SVG figure, the Mermaid flowchart, and the Vega-Lite chart of the <c>mathml/composition</c> card
    ///     render in strict mode without a diagnostic beside the quadratic example, each drawing the equation once, and that
    ///     the natural size the card quotes is the size a probe reports.
    /// </summary>
    [Fact]
    public void HelpCatalog_MathMlComposition_FigureBlocks_RenderStrictWithTheEquation()
    {
        // Arrange: the blocks of the card and the equation they link
        var blocks = FencedBlock.Matches(HelpCardText.Card("mathml/composition").Markdown)
            .Select(m => (Language: m.Groups["language"].Value, Content: m.Groups["content"].Value))
            .ToList();
        using var directory = new TempDirectory();
        var equation = HelpCardText.SaveExample(directory, "mathml", "quadratic", "quadratic.mml");
        var files = new Dictionary<string, string> { ["xml"] = "figure.svg", ["mermaid"] = "flow.mmd", ["json"] = "chart.vl.json" };

        // Act / Assert: one block of each language, each drawing the equation once in strict mode
        Assert.Equal(["xml", "mermaid", "json"], blocks.Select(b => b.Language));
        foreach (var (language, content) in blocks)
        {
            var path = FixtureFactory.WriteText(directory.Path, files[language], content);
            using var loaded = SourceLoader.Load(path, new SourceOptions { Strict = true });
            Assert.Empty(loaded.Diagnostics);
            Assert.Equal(["quadratic.mml"], EmbeddedHrefs(loaded));
        }

        // Assert: the natural size the card quotes
        var info = SourceLoader.Probe(equation);
        Assert.Contains($"the quadratic formula is {info.Width} by {info.Height} at its natural", HelpCardText.TextOf("mathml/composition"), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that every <c>mathml-*</c> code the <c>mathml/diagnostics</c> and <c>mathml/unsupported</c> cards list is
    ///     reported by a trigger through <see cref="CanvasNetViewer.Render(Stream, string, ViewerRenderOptions?)"/>: as a
    ///     warning note (with the code in its detail), as an unsupported note, or as the error the card names (with the code
    ///     as its detail code); only the generic warning code and the code of an internal layout failure have no trigger.
    /// </summary>
    [Fact]
    public void HelpCatalog_MathMlDiagnostics_CitedCodes_AreReportedByTheirTriggers()
    {
        // Arrange: a trigger for each code, with the limits it needs
        var triggers = new (string Code, string Markup, string Expected, MathMlLimits? Limits)[]
        {
            (MathMlDiagnosticCodes.ElementUnsupported, "<mfenced><mi>x</mi></mfenced>", "unsupported", null),
            (MathMlDiagnosticCodes.AttributeUnsupported, "<mo linebreak=\"newline\">+</mo>", "unsupported", null),
            (MathMlDiagnosticCodes.CssUnsupported, "<mi style=\"border: 1px solid\">x</mi>", "unsupported", null),
            (MathMlDiagnosticCodes.AttributeInvalid, "<mspace width=\"3\"/>", "warning", null),
            (MathMlDiagnosticCodes.ChildCount, "<mfrac><mi>a</mi></mfrac>", "warning", null),
            (MathMlDiagnosticCodes.TextMissingGlyph, "<mi>&#xE000;</mi>", "warning", null),
            (MathMlDiagnosticCodes.StretchUnavailable, "<mover><mi>x</mi><mo stretchy=\"true\">^</mo></mover>", "warning", null),
            (MathMlDiagnosticCodes.DiagnosticsSuppressed, "<mspace width=\"3\"/><mspace width=\"4\"/>", "warning", new MathMlLimits { MaxDiagnostics = 1 }),
            (MathMlDiagnosticCodes.LimitExceeded, "<mi>a</mi><mi>b</mi><mi>c</mi>", ErrorCodes.BudgetExceeded, new MathMlLimits { MaxElements = 2 }),
            (MathMlDiagnosticCodes.SyntaxError, "<mi>x</mo>", ErrorCodes.MathMlError, null),
        };
        var cited = HelpCardText.Items("mathml/diagnostics").Concat(HelpCardText.Items("mathml/unsupported"))
            .Select(i => i.Term)
            .Where(t => t.StartsWith("mathml-", StringComparison.Ordinal))
            .ToList();

        // Act / Assert: each trigger is reported as the card says
        foreach (var (code, markup, expected, limits) in triggers)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes($"<math>{markup}</math>"));
            var options = new ViewerRenderOptions { MathMlLimits = limits };
            if (expected is "warning" or "unsupported")
            {
                using var image = CanvasNetViewer.Render(stream, "trigger.mml", options);
                var found = expected == "warning"
                    ? image.Diagnostics.Any(d => d.Code == MathMlImageSource.WarningCode && d.Detail == code && d.Severity == ImageDiagnosticSeverity.Warning)
                    : image.Diagnostics.Any(d => d.Code == code && d.Severity == ImageDiagnosticSeverity.Unsupported);
                Assert.True(found, $"The trigger of '{code}' did not report it.");
            }
            else
            {
                var ex = Assert.Throws<ViewerException>(() => CanvasNetViewer.Render(stream, "trigger.mml", options));
                Assert.Equal((expected, code), (ex.Code, ex.DetailCode));
            }
        }

        // Assert: every code the cards cite has a trigger, but the generic warning and the internal failure
        Assert.True(cited.Count >= 12, $"Only {cited.Count} codes are cited by the MathML cards.");
        Assert.Empty(cited.Except(triggers.Select(t => t.Code).Append(MathMlImageSource.WarningCode).Append(MathMlDiagnosticCodes.RenderFailed), StringComparer.Ordinal));
    }

    /// <summary>
    ///     Test the spacing advice of the <c>mathml/pitfalls</c> card on the real layout: parentheses with
    ///     <c>stretchy="false"</c> make f(x) narrower than stretchy ones, and a negative number in its own row makes
    ///     <c>= -40</c> narrower than a minus spaced like a binary operator.
    /// </summary>
    [Fact]
    public void HelpCatalog_MathMlPitfalls_StretchyFalseParentheses_AreNarrower()
    {
        // Arrange: the advice of the card, and the two forms of each construct
        var pitfalls = HelpCardText.TextOf("mathml/pitfalls");
        const string Stretchy = "<math><mi>f</mi><mo>(</mo><mi>x</mi><mo>)</mo></math>";
        const string Tight = "<math><mi>f</mi><mo stretchy=\"false\">(</mo><mi>x</mi><mo stretchy=\"false\">)</mo></math>";
        const string Binary = "<math><mi>T</mi><mo>=</mo><mo>&minus;</mo><mn>40</mn></math>";
        const string Signed = "<math><mi>T</mi><mo>=</mo><mrow><mo>&minus;</mo><mn>40</mn></mrow></math>";

        // Act: the natural widths
        static int Width(string text) => MathMlDocument.Parse(text).Measure(cancellationToken: TestContext.Current.CancellationToken).Width;

        // Assert: the card gives the advice, and the advised form is narrower
        Assert.Contains("<mo stretchy=\"false\">(</mo>", pitfalls, StringComparison.Ordinal);
        Assert.Contains("<mrow><mo>&minus;</mo><mn>3</mn></mrow>", pitfalls, StringComparison.Ordinal);
        Assert.True(Width(Tight) < Width(Stretchy), $"f(x) is {Width(Tight)} wide with tight parentheses and {Width(Stretchy)} with stretchy ones.");
        Assert.True(Width(Signed) < Width(Binary), $"T = -40 is {Width(Signed)} wide with the sign in its row and {Width(Binary)} without.");
    }

    /// <summary>
    ///     Test that the MathML cards cover the authoring tasks: each task card exists below the root with at least one
    ///     source example, the root links the pitfalls and the composition cards, the format cards have their five sections,
    ///     and every example of the tree is a source example in a <c>.mml</c> file.
    /// </summary>
    [Fact]
    public void HelpCatalog_MathMlCards_CoverTheAuthoringTasks()
    {
        // Arrange
        var root = HelpCardText.Card("mathml");

        // Act / Assert: the task cards and their examples
        foreach (var task in MathMlTaskCards)
        {
            var card = HelpCardText.Card($"mathml/{task}");
            Assert.Contains(card.Id, root.ChildIds);
            Assert.True(card.Examples.Any(e => e.Kind == HelpExample.SourceKind), $"The card '{card.Id}' has no source example.");
        }

        // Assert: the root leads to the pitfalls and to composition, and has its aliases
        Assert.Contains("mathml/pitfalls", root.Markdown, StringComparison.Ordinal);
        Assert.Contains("mathml/composition", root.Markdown, StringComparison.Ordinal);
        Assert.Equal(["mml", "equation"], root.Aliases);

        // Assert: the reference cards and their sections
        Assert.NotEmpty(HelpCardText.Bullets("mathml/elements", "Supported"));
        Assert.NotEmpty(HelpCardText.Bullets("mathml/attributes", "Supported"));
        Assert.NotEmpty(HelpCardText.Bullets("mathml/options", "Options"));
        Assert.NotEmpty(HelpCardText.Bullets("mathml/diagnostics", "Diagnostics"));
        Assert.NotEmpty(HelpCardText.Bullets("mathml/unsupported", "Unsupported"));
        Assert.NotEmpty(HelpCardText.Bullets("mathml/differences", "Known differences"));

        // Assert: every example is a source example of an equation file
        var examples = HelpCardText.Subtree(root).SelectMany(c => c.Examples).ToList();
        Assert.True(examples.Count >= 25, $"Only {examples.Count} MathML examples were found.");
        Assert.All(examples, e => Assert.Equal((HelpExample.SourceKind, true), (e.Kind, e.FileName!.EndsWith(".mml", StringComparison.Ordinal))));
    }

    /// <summary>
    ///     Test that the MathML cards say what the lists of the MathML user guide say, item for item and in order: the
    ///     <c>## Supported</c> bullets of <c>mathml/elements</c> and <c>mathml/attributes</c> are the items of the guide's
    ///     element and attribute lists, and every item of the guide's code list (<c>**code**: kind: reported when</c>) is a
    ///     bullet of <c>mathml/unsupported</c> or <c>mathml/diagnostics</c>.
    /// </summary>
    [Fact]
    public void HelpCatalog_MathMlCards_ElementAttributeAndCodeItems_MatchUserGuide()
    {
        // Arrange: the guide
        var guide = File.ReadAllLines(Path.Combine(FindRepositoryRoot(), "docs", "user_guide", "canvas-net-math-ml.md"));
        (string Card, string Heading, int MinimumRows)[] lists =
        [
            ("elements", "### Supported MathML Elements", 10),
            ("attributes", "### Supported MathML Attributes", 12),
        ];

        // Act: the bullets that the items of the guide's lists promise, compared with the cards
        var differences = new List<string>();
        foreach (var (card, heading, minimumRows) in lists)
        {
            var items = GuideItems(guide, heading).Select(i => i.Term + i.Tail).ToList();
            Assert.True(items.Count >= minimumRows, $"Only {items.Count} items were found in the {card} list of the guide.");
            CompareInOrder($"mathml/{card} ## Supported", items, HelpCardText.Bullets($"mathml/{card}", "Supported"), differences);
        }

        var codeItems = GuideItems(guide, "### MathML Unsupported Features and Strict Mode").ToList();
        CompareCodeItems(codeItems, "mathml", differences);

        // Assert: the code list has an item for every code of the library but the generic warning, and nothing differs
        Assert.True(codeItems.Count >= 11, $"Only {codeItems.Count} items were found in the code list of the guide.");
        Assert.True(differences.Count == 0, string.Join(Environment.NewLine, differences));
    }

    /// <summary>
    ///     Gets the whole equations of a Markdown text: the fenced <c>xml</c> blocks whose content starts with
    ///     <c>&lt;math</c>, then the Pandoc <c>{.mathml}</c> blocks whose content starts with <c>&lt;math</c> (a copy
    ///     shown inside a <c>markdown</c> block included).
    /// </summary>
    /// <param name="markdown">The Markdown text, with line feeds.</param>
    /// <returns>The content of each such block, in order.</returns>
    private static IEnumerable<string> MathMlEquations(string markdown)
    {
        var xml = FencedBlock.Matches(markdown)
            .Where(m => m.Groups["language"].Value == "xml")
            .Select(m => m.Groups["content"].Value);
        var pandoc = PandocMathMlBlock.Matches(markdown).Select(m => m.Groups["content"].Value);
        return xml.Concat(pandoc).Where(content => content.StartsWith("<math", StringComparison.Ordinal));
    }

    /// <summary>
    ///     Renders an equation in strict mode and checks that it draws a picture without a diagnostic.
    /// </summary>
    /// <param name="where">Where the equation is written, for the message.</param>
    /// <param name="equation">The MathML text.</param>
    private static void AssertRendersStrictlyWithoutDiagnostics(string where, string equation)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(equation));
        using var image = CanvasNetViewer.Render(stream, "equation.mml", new ViewerRenderOptions { ThrowOnUnsupportedFeature = true });
        Assert.True(image.Diagnostics.Count == 0, $"An equation of '{where}' reports: {string.Join("; ", image.Diagnostics.Select(d => d.Message))}");
        Assert.True(image.Surface.Width > 0 && image.Surface.Height > 0);
    }
}
