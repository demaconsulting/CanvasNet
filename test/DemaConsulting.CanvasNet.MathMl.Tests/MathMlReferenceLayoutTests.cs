using System.Globalization;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.MathMl.Layout;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Reference layout tests: CanvasNet's layout of the original fixtures in <c>Fixtures/reference</c> is compared with
///     the boxes that MathML Core in headless Chrome computes for them with the same font (the <c>*.layout.json</c>
///     goldens, made offline by <c>tools/visual-diff/update-mathml-goldens.ps1</c>; see the README there). No browser
///     runs at test time.
/// </summary>
/// <remarks>
///     At a font size of 100 CSS pixels, the width, height and baseline of the <c>math</c> element and the box of every
///     element agree within <see cref="Tolerance"/> pixels.
/// </remarks>
public class MathMlReferenceLayoutTests
{
    /// <summary>The tolerance of a position or a size, in CSS pixels at a font size of 100: one pixel (the browser rounds
    ///     the bounds of its glyphs to whole pixels) plus the layout unit of Chrome (1/64 pixel).</summary>
    private const double Tolerance = 1 + (1.0 / 64);

    /// <summary>The folder of the fixtures and goldens.</summary>
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference");

    /// <summary>
    ///     The values that are known to differ from the browser's, by fixture and value name, each with its reason (also
    ///     recorded in <c>Fixtures/reference/README.md</c>).
    /// </summary>
    private static readonly Dictionary<string, string> Exceptions = new(StringComparer.Ordinal)
    {
        // Chrome measures the ink of an automatic italic mi on the untransformed character (the upright f has no
        // descender) but draws and advances the italic glyph, which descends; CanvasNet measures what it draws
        ["tokens-mi-italic 1 <mi> height"] = "Chrome measures the ink of the untransformed character",
        ["operators-spacing 10 <mi> height"] = "Chrome measures the ink of the untransformed character",

        // Chrome's hinted bounds of the arrow and of the under brace are a pixel shorter than their outlines rounded out,
        // and Chrome floors the MATH constants to its layout unit (1/64 pixel); together just beyond the tolerance
        ["arrow-over math height"] = "Chrome's hinted arrow bounds and its 1/64 pixel MATH constants",
        ["arrow-over 1 <munderover> height"] = "Chrome's hinted arrow bounds and its 1/64 pixel MATH constants",
        ["arrow-over 1/1 <mi> y"] = "Chrome's hinted arrow bounds and its 1/64 pixel MATH constants",
        ["overbrace-underbrace math height"] = "Chrome's hinted under brace bounds and its 1/64 pixel MATH constants",
    };

    /// <summary>
    ///     Gets the names of the reference fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> Fixtures()
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(Folder, "*.mml").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Validates that every fixture has a golden and every golden has its fixture.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        var fixtures = Directory.GetFiles(Folder, "*.mml").Select(Path.GetFileNameWithoutExtension).ToList();
        var goldens = Directory.GetFiles(Folder, "*.layout.json").Select(file => Path.GetFileName(file)[..^".layout.json".Length]).ToList();

        // Act
        var missing = fixtures.Except(goldens).ToList();
        var orphans = goldens.Except(fixtures).ToList();

        // Assert
        Assert.Empty(missing);
        Assert.Empty(orphans);
        Assert.True(fixtures.Count >= 38);
    }

    /// <summary>
    ///     Validates that the layout of a fixture matches the browser's: the size and baseline of the equation and the box
    ///     of every element.
    /// </summary>
    /// <param name="fixture">The fixture name.</param>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void CanvasNetMathMl_ReferenceLayout_MatchesChrome(string fixture)
    {
        // Arrange
        var document = MathMlDocument.Open(Path.Combine(Folder, fixture + ".mml"));
        using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(Folder, fixture + ".layout.json")));
        var fontSize = golden.RootElement.GetProperty("fontSize").GetDouble();

        // Act
        var root = document.LayoutBox(new MathMlRenderOptions { FontSize = fontSize }, TestContext.Current.CancellationToken);
        var boxes = BoxesByPath(document.Root, root);

        // Assert: the math element, then every element the browser gave a box
        var mismatches = new StringBuilder();
        var math = golden.RootElement.GetProperty("math");
        Compare(mismatches, fixture, "math width", root.Width, math.GetProperty("width").GetDouble());
        Compare(mismatches, fixture, "math height", root.Ascent + root.Descent, math.GetProperty("height").GetDouble());
        Compare(mismatches, fixture, "math baseline", root.Ascent, math.GetProperty("baseline").GetDouble());
        foreach (var element in golden.RootElement.GetProperty("elements").EnumerateArray())
        {
            var path = element.GetProperty("path").GetString()!;
            var box = element.GetProperty("box").EnumerateArray().Select(value => value.GetDouble()).ToArray();
            if (!boxes.TryGetValue(path, out var ours))
            {
                // An element the browser does not display has an empty box, and CanvasNet lays none out
                if (box[2] != 0 || box[3] != 0)
                {
                    mismatches.AppendLine(CultureInfo.InvariantCulture, $"{path} <{element.GetProperty("tag").GetString()}>: no box");
                }

                continue;
            }

            var tag = $"{path} <{element.GetProperty("tag").GetString()}>";
            Compare(mismatches, fixture, tag + " x", ours.X, box[0]);
            Compare(mismatches, fixture, tag + " y", ours.Y + root.Ascent, box[1]);
            Compare(mismatches, fixture, tag + " width", ours.Width, box[2]);
            Compare(mismatches, fixture, tag + " height", ours.Height, box[3]);
        }

        Assert.True(mismatches.Length == 0, $"{fixture}:\n{mismatches}");
    }

    /// <summary>
    ///     Maps the boxes of the layout to the paths of their elements (the indices of the element children from the
    ///     <c>math</c> element), as the goldens name them.
    /// </summary>
    /// <param name="math">The <c>math</c> element.</param>
    /// <param name="root">The box of the <c>math</c> element.</param>
    /// <returns>The content box of each element relative to the root origin, by path.</returns>
    private static Dictionary<string, MathRect> BoxesByPath(MathElement math, MathBox root)
    {
        // The path of every element, from the tree
        var paths = new Dictionary<MathElement, string>(ReferenceEqualityComparer.Instance);
        void Name(MathElement element, string path)
        {
            var index = 0;
            foreach (var child in element.ElementChildren)
            {
                var childPath = path.Length == 0 ? index.ToString(CultureInfo.InvariantCulture) : $"{path}/{index}";
                paths[child] = childPath;
                Name(child, childPath);
                index++;
            }
        }

        Name(math, string.Empty);

        // The box of every element, from the layout
        var result = new Dictionary<string, MathRect>(StringComparer.Ordinal);
        foreach (var (box, x, y) in MathMlTestHelpers.Flatten(root))
        {
            if (box.Element is { } element && paths.TryGetValue(element, out var path))
            {
                result[path] = new MathRect(x, y - box.Ascent, box.Width, box.Ascent + box.Descent);
            }
        }

        return result;
    }

    /// <summary>
    ///     Records a value that differs from the browser's by more than the tolerance.
    /// </summary>
    /// <param name="mismatches">The record of the mismatches.</param>
    /// <param name="fixture">The fixture name.</param>
    /// <param name="what">The name of the value.</param>
    /// <param name="actual">CanvasNet's value.</param>
    /// <param name="expected">The browser's value.</param>
    private static void Compare(StringBuilder mismatches, string fixture, string what, double actual, double expected)
    {
        if (Math.Abs(actual - expected) > Tolerance && !Exceptions.ContainsKey($"{fixture} {what}"))
        {
            mismatches.AppendLine(CultureInfo.InvariantCulture, $"{what}: {actual:0.###} (Chrome {expected:0.###})");
        }
    }
}
