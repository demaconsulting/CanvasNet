using System.Globalization;
using System.Text;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Hardening tests through the public API: tables, spans, stretching, nesting and lengths fail closed with the
///     documented <see cref="MathMlDiagnosticCodes.LimitExceeded"/> errors that name their limits, the work of a table is
///     charged per grid slot, and a seeded mutation fuzzer over the original fixtures only ever sees the documented
///     exceptions. The tests measure work and limits, never the clock.
/// </summary>
public class MathMlHardeningTests
{
    /// <summary>
    ///     Validates with a seeded mutation fuzzer over every reference fixture (truncated texts, deleted ranges, inserted
    ///     MathML elements, attributes and references, duplicated and swapped lines, flipped characters) that opening,
    ///     measuring and rendering, in normal and strict mode, only ever throw the documented exceptions, each syntax and
    ///     render error with a <c>mathml-*</c> code and a location, and that every successful render has the size of its
    ///     measure.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Hardening_MutatedFixtures_ThrowOnlyDocumentedExceptions()
    {
        // Arrange
        string[] tokens =
        [
            "<mrow>", "</mrow>", "<mi>x</mi>", "<mn>12</mn>", "<mo>(</mo>", "<mo>)</mo>", "<mo stretchy=\"true\">&#x2192;</mo>",
            "<mo largeop=\"true\" movablelimits=\"true\">&#x2211;</mo>", "<mtext>t</mtext>", "<ms>s</ms>", "<mspace width=\"9em\"/>",
            "<mspace height=\"99999999em\" depth=\"-3em\"/>", "<mfrac>", "</mfrac>", "<mfrac linethickness=\"99999999px\">", "<msqrt>", "</msqrt>",
            "<mroot>", "</mroot>", "<msub>", "</msub>", "<msup>", "</msup>", "<msubsup>", "</msubsup>", "<munder>", "</munder>",
            "<mover accent=\"true\">", "</mover>", "<munderover>", "</munderover>", "<mmultiscripts>", "</mmultiscripts>",
            "<mprescripts/>", "<none/>", "<mtable>", "</mtable>", "<mtr>", "</mtr>", "<mtd>", "</mtd>", "<mtd rowspan=\"0\">",
            "<mtd columnspan=\"1000\">", "<mtd rowspan=\"65535\" columnspan=\"2\">", "<mtable columnalign=\"left right\" rowalign=\"top axis\">",
            "<mtable columnspacing=\"99999999em\" rowspacing=\"-1ex\" framespacing=\"2em\" frame=\"dashed\">",
            "<mtable columnlines=\"solid dashed none\" rowlines=\"dashed\" frame=\"solid\">", "<mstyle displaystyle=\"true\" scriptlevel=\"+9\">",
            "</mstyle>", "<mpadded width=\"+99999999em\" voffset=\"-2em\">", "</mpadded>", "<mphantom>", "</mphantom>", "<merror>", "</merror>",
            "<semantics>", "<annotation>a</annotation>", "</semantics>", "<menclose>", "<mfenced>", " mathvariant=\"bold\"",
            " mathsize=\"300%\"", " style=\"display: none\"", " dir=\"rtl\"", "&alpha;", "&#x1D400;", "&InvisibleTimes;", "&bogus;",
            "<!-- c -->", "<![CDATA[x]]>", "<", ">", "/>", "\"", "&", "\n",
        ];
        var random = new Random(20261125);
        var fixtures = new[] { "reference", "reference-firefox" }
            .SelectMany(folder => Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", folder), "*.mml"))
            .Order(StringComparer.Ordinal)
            .Select(File.ReadAllText)
            .ToList();
        var unexpected = new List<string>();

        // Act
        var round = 0;
        foreach (var fixture in fixtures)
        {
            for (var i = 0; i < 24; i++)
            {
                var text = Mutate(fixture, tokens, random);
                var problem = Exercise(text, strict: round++ % 3 == 0);
                if (problem is not null)
                {
                    unexpected.Add($"{problem}\n--- text ---\n{text}");
                }
            }
        }

        // Assert
        Assert.True(fixtures.Count >= 46);
        Assert.True(unexpected.Count == 0, string.Join("\n=====\n", unexpected.Take(3)));
    }

    /// <summary>
    ///     Validates that a table with more grid slots than <see cref="MathMlLimits.MaxTableCells"/> fails with a located
    ///     limit error, and renders under the default limits.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Hardening_TableCells_AreLimited()
    {
        // Arrange: a 20 by 20 table of digits
        var document = MathMlDocument.Parse(Table(20, 20), new MathMlOpenOptions { SourceName = "table.mml" });
        var options = new MathMlRenderOptions { Limits = new MathMlLimits { MaxTableCells = 399 } };

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => document.Measure(options, TestContext.Current.CancellationToken));
        var size = document.Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxTableCells", ex.Message, StringComparison.Ordinal);
        Assert.Equal("table.mml", ex.Location.SourceName);
        Assert.True(ex.Location.IsKnown);
        Assert.False(string.IsNullOrEmpty(ex.Hint));
        Assert.True(size.Width > 0);
    }

    /// <summary>
    ///     Validates that huge spans fail on the number of grid slots before the grid is allocated or charged: with a work
    ///     budget smaller than the slots, the error still names <see cref="MathMlLimits.MaxTableCells"/>.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Hardening_HugeSpans_FailBeforeAllocation()
    {
        // Arrange: one cell spanning 1,000 columns and all of 200 rows is 200,000 slots
        var rows = string.Concat(Enumerable.Repeat("<mtr/>", 199));
        var document = MathMlDocument.Parse($"<math><mtable><mtr><mtd columnspan=\"1000\" rowspan=\"0\"><mi>x</mi></mtd></mtr>{rows}</mtable></math>");
        var options = new MathMlRenderOptions { Limits = new MathMlLimits { MaxLayoutWork = 5_000 } };

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => document.Render(options, TestContext.Current.CancellationToken));
        var defaults = Assert.Throws<MathMlRenderException>(() => document.Render(cancellationToken: TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxTableCells", ex.Message, StringComparison.Ordinal);
        Assert.Contains("MathMlLimits.MaxTableCells", defaults.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the work of a table is charged per grid slot: a table whose spans make 50,000 slots exceeds a
    ///     work budget of 20,000 that the same elements without spans fit in.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Hardening_LayoutWork_ChargesTableSlots()
    {
        // Arrange
        var rows = string.Concat(Enumerable.Repeat("<mtr><mtd><mi>x</mi></mtd></mtr>", 49));
        var spanned = MathMlDocument.Parse($"<math><mtable><mtr><mtd columnspan=\"1000\"><mi>x</mi></mtd></mtr>{rows}</mtable></math>");
        var plain = MathMlDocument.Parse($"<math><mtable><mtr><mtd><mi>x</mi></mtd></mtr>{rows}</mtable></math>");
        var options = new MathMlRenderOptions { Limits = new MathMlLimits { MaxLayoutWork = 20_000 } };

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => spanned.Measure(options, TestContext.Current.CancellationToken));
        var size = plain.Measure(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxLayoutWork", ex.Message, StringComparison.Ordinal);
        Assert.True(size.Height > 0);
    }

    /// <summary>
    ///     Validates that an operator stretched along the inline axis over a huge script fails on
    ///     <see cref="MathMlLimits.MaxAssemblyParts"/> instead of emitting a glyph per pixel.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Hardening_HorizontalStretch_IsLimited()
    {
        // Arrange: the underscript is 400em wide at the script size (60% of 16 px)
        var document = MathMlDocument.Parse("<math><munder><mo>&#x2192;</mo><mspace width=\"400em\"/></munder></math>");
        var options = new MathMlRenderOptions { Limits = new MathMlLimits { MaxAssemblyParts = 20 } };

        // Act
        var ex = Assert.Throws<MathMlRenderException>(() => document.Measure(options, TestContext.Current.CancellationToken));
        var size = document.Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, ex.Code);
        Assert.Contains("MathMlLimits.MaxAssemblyParts", ex.Message, StringComparison.Ordinal);
        Assert.True(size.Width > 3_500);
    }

    /// <summary>
    ///     Validates that deeply nested fractions are bounded: deeper than <see cref="MathMlLimits.MaxNestingDepth"/> they
    ///     fail while they are read (no stack overflow), and within it a work budget smaller than their layout stops it.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Hardening_DeepFractions_AreBounded()
    {
        // Arrange
        static string Nested(int depth) =>
            "<math>" + string.Concat(Enumerable.Repeat("<mfrac><mi>a</mi>", depth)) + "<mi>b</mi>" + string.Concat(Enumerable.Repeat("</mfrac>", depth)) + "</math>";

        // Act
        var deep = Assert.Throws<MathMlSyntaxException>(() => MathMlDocument.Parse(Nested(300)));
        var document = MathMlDocument.Parse(Nested(100));
        var work = Assert.Throws<MathMlRenderException>(() => document.Measure(new MathMlRenderOptions { Limits = new MathMlLimits { MaxLayoutWork = 200 } }, TestContext.Current.CancellationToken));
        var size = document.Measure(cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, deep.Code);
        Assert.Contains("MathMlLimits.MaxNestingDepth", deep.Message, StringComparison.Ordinal);
        Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, work.Code);
        Assert.Contains("MathMlLimits.MaxLayoutWork", work.Message, StringComparison.Ordinal);
        Assert.True(size.Height > 0);
    }

    /// <summary>
    ///     Validates that huge lengths (a line thickness, a column spacing) are clamped and fail closed on the size of the
    ///     surface instead of overflowing.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Hardening_HugeLengths_AreRefused()
    {
        // Arrange
        var thick = MathMlDocument.Parse("<math><mfrac linethickness=\"99999999em\"><mi>a</mi><mi>b</mi></mfrac></math>");
        var spaced = MathMlDocument.Parse("<math><mtable columnspacing=\"99999999em\"><mtr><mtd><mi>a</mi></mtd><mtd><mi>b</mi></mtd></mtr></mtable></math>");

        // Act
        var errors = new[] { thick, spaced }
            .Select(document => Assert.Throws<MathMlRenderException>(() => document.Render(cancellationToken: TestContext.Current.CancellationToken)))
            .ToList();

        // Assert
        Assert.All(errors, error => Assert.Equal(MathMlDiagnosticCodes.LimitExceeded, error.Code));
        Assert.All(errors, error => Assert.Contains("Surface.MaxDimension", error.Message, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Validates that large tables within the limits render, deterministically: two renders of a 40 by 40 table with
    ///     spans, lines and a frame are identical.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_Hardening_LargeTablesWithinLimits_Render()
    {
        // Arrange
        var document = MathMlDocument.Parse(Table(40, 40, "columnlines=\"solid dashed\" rowlines=\"dashed\" frame=\"solid\"", "<mtd columnspan=\"3\" rowspan=\"2\"><mi>x</mi></mtd>"));
        var options = new MathMlRenderOptions { FontSize = 8 };

        // Act
        using var first = document.Render(options, TestContext.Current.CancellationToken);
        using var second = document.Render(options, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.Width > 40 * 8);
        Assert.True(MathMlTestHelpers.PixelsEqual(first, second));
    }

    /// <summary>
    ///     Builds a table of digits.
    /// </summary>
    /// <param name="rows">The number of rows.</param>
    /// <param name="columns">The number of columns.</param>
    /// <param name="attributes">The attributes of the <c>mtable</c>.</param>
    /// <param name="first">The markup of the first cell, or null for a digit.</param>
    /// <returns>The MathML text.</returns>
    private static string Table(int rows, int columns, string attributes = "", string? first = null)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"<math><mtable {attributes}>");
        for (var r = 0; r < rows; r++)
        {
            builder.Append("<mtr>");
            for (var c = 0; c < columns; c++)
            {
                builder.Append(r == 0 && c == 0 && first is not null ? first : string.Create(CultureInfo.InvariantCulture, $"<mtd><mn>{(r + c) % 10}</mn></mtd>"));
            }

            builder.Append("</mtr>");
        }

        return builder.Append("</mtable></math>").ToString();
    }

    /// <summary>
    ///     Opens, measures and renders a text and checks every outcome against the documented ones.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="strict">Whether unsupported features throw.</param>
    /// <returns>A description of an undocumented outcome, or null.</returns>
    private static string? Exercise(string text, bool strict)
    {
        try
        {
            var document = MathMlDocument.Parse(text, new MathMlOpenOptions { SourceName = "fuzz.mml" });
            var options = new MathMlRenderOptions { FontSize = 8, ThrowOnUnsupportedFeature = strict };
            var size = document.Measure(options, TestContext.Current.CancellationToken);
            using var surface = document.Render(options, TestContext.Current.CancellationToken);
            return (surface.Width, surface.Height) == (size.Width, size.Height) ? null : $"render {surface.Width}x{surface.Height} differs from measure {size.Width}x{size.Height}";
        }
        catch (MathMlSyntaxException ex)
        {
            // An error has a location, except in a text without any markup (it has no root element to locate)
            var located = (ex.Location.IsKnown && ex.Location.SourceName == "fuzz.mml") || !text.Contains('<', StringComparison.Ordinal);
            return ex.Code is MathMlDiagnosticCodes.SyntaxError or MathMlDiagnosticCodes.LimitExceeded && located
                ? null
                : $"syntax error without a code or a location: {ex.Code} {ex.Message}";
        }
        catch (MathMlRenderException ex)
        {
            return ex.Code == MathMlDiagnosticCodes.LimitExceeded && ex.Location.IsKnown ? null : $"render error: {ex.Code} {ex.Message} {ex.InnerException}";
        }
        catch (MathMlUnsupportedFeatureException) when (strict)
        {
            return null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
        catch (Exception ex)
        {
            return $"undocumented {ex.GetType().Name}: {ex}";
        }
    }

    /// <summary>
    ///     Mutates a text: truncates it, deletes a range, inserts a token, duplicates or swaps a line, or flips a
    ///     character, one to three times.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tokens">The tokens to insert.</param>
    /// <param name="random">The seeded generator.</param>
    /// <returns>The mutated text.</returns>
    private static string Mutate(string text, string[] tokens, Random random)
    {
        var builder = new StringBuilder(text);
        var count = random.Next(1, 4);
        for (var i = 0; i < count; i++)
        {
            var position = random.Next(builder.Length + 1);
            switch (random.Next(6))
            {
                case 0 when builder.Length > 0:
                    builder.Length = position;
                    break;
                case 1 when builder.Length > 0:
                    var start = Math.Min(position, builder.Length - 1);
                    builder.Remove(start, Math.Min(random.Next(1, 16), builder.Length - start));
                    break;
                case 2:
                case 3:
                    builder.Insert(position, tokens[random.Next(tokens.Length)]);
                    break;
                case 4:
                    var all = builder.ToString().Split('\n');
                    if (all.Length > 2)
                    {
                        var a = random.Next(all.Length);
                        var b = random.Next(all.Length);
                        (all[a], all[b]) = (all[b], all[a]);
                        builder.Clear().Append(string.Join('\n', all));
                    }

                    break;
                default:
                    if (builder.Length > 0)
                    {
                        var at = Math.Min(position, builder.Length - 1);
                        const string Flips = "<>/=\"'&;#x0-e m\n";
                        builder[at] = Flips[random.Next(Flips.Length)];
                    }

                    break;
            }
        }

        return builder.ToString();
    }
}
