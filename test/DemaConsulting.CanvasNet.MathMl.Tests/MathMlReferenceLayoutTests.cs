using System.Globalization;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.MathMl.Layout;
using DemaConsulting.CanvasNet.MathMl.Syntax;

namespace DemaConsulting.CanvasNet.MathMl.Tests;

/// <summary>
///     Reference layout tests: CanvasNet's layout of the original fixtures in <c>Fixtures/reference</c> is compared with
///     the boxes that MathML Core in headless Chrome computes for them with the same font (the <c>*.layout.json</c>
///     goldens, made offline by <c>tools/visual-diff/update-mathml-goldens.ps1</c>; see the README there). The table
///     extensions of MathML 3 that Chrome ignores are compared with headless Firefox (<c>Fixtures/reference-firefox</c>).
///     No browser runs at test time.
/// </summary>
/// <remarks>
///     At a font size of 100 CSS pixels, the width, height and baseline of the <c>math</c> element and the box of every
///     element agree within <see cref="Tolerance"/> pixels of Chrome's; the boxes of the tables, rows, cells and
///     <c>mspace</c> contents of the Firefox fixtures, relative to their table, within <see cref="FirefoxTolerance"/>.
/// </remarks>
public class MathMlReferenceLayoutTests
{
    /// <summary>The tolerance of a position or a size, in CSS pixels at a font size of 100: one pixel (the browser rounds
    ///     the bounds of its glyphs to whole pixels) plus the layout unit of Chrome (1/64 pixel).</summary>
    private const double Tolerance = 1 + (1.0 / 64);

    /// <summary>The tolerance of the Firefox comparison: one pixel (Firefox snaps lines and borders to whole pixels)
    ///     plus the layout unit of Firefox (1/60 pixel).</summary>
    private const double FirefoxTolerance = 1 + (1.0 / 60);

    /// <summary>The folder of the fixtures and goldens.</summary>
    private static readonly string Folder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference");

    /// <summary>The folder of the Firefox fixtures and goldens.</summary>
    private static readonly string FirefoxFolder = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reference-firefox");

    /// <summary>The elements whose boxes the Firefox comparison checks (the table structure and the fixed-size contents).</summary>
    private static readonly HashSet<string> FirefoxTags = new(StringComparer.Ordinal) { "mtable", "mtr", "mtd", "mspace" };

    /// <summary>
    ///     The values that are known to differ from the browser's, by fixture and value name, each with its reason (also
    ///     recorded in <c>Fixtures/reference/README.md</c>).
    /// </summary>
    private static readonly Dictionary<string, string> Exceptions = new(StringComparer.Ordinal)
    {
        // Chrome measures the ink of an automatic italic mi on the untransformed character (the upright f has no
        // descender) but draws and advances the italic glyph, which descends; CanvasNet measures what it draws
        ["tokens-mi-italic 4 <mi> height"] = "Chrome measures the ink of the untransformed character",
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
    public static TheoryData<string> Fixtures() => FixturesOf(Folder);

    /// <summary>
    ///     Gets the names of the Firefox reference fixtures.
    /// </summary>
    /// <returns>The names, without extension.</returns>
    public static TheoryData<string> FirefoxFixtures() => FixturesOf(FirefoxFolder);

    /// <summary>
    ///     Validates that every fixture has a golden and every golden has its fixture, in both reference folders.
    /// </summary>
    [Fact]
    public void CanvasNetMathMl_ReferenceGoldens_CoverEveryFixture()
    {
        // Arrange
        static (List<string> Missing, List<string> Orphans, int Count) Check(string folder)
        {
            var fixtures = Directory.GetFiles(folder, "*.mml").Select(file => Path.GetFileNameWithoutExtension(file)).ToList();
            var goldens = Directory.GetFiles(folder, "*.layout.json").Select(file => Path.GetFileName(file)[..^".layout.json".Length]).ToList();
            return (fixtures.Except(goldens).ToList(), goldens.Except(fixtures).ToList(), fixtures.Count);
        }

        // Act
        var chrome = Check(Folder);
        var firefox = Check(FirefoxFolder);

        // Assert
        Assert.Empty(chrome.Missing);
        Assert.Empty(chrome.Orphans);
        Assert.True(chrome.Count >= 42);
        Assert.Empty(firefox.Missing);
        Assert.Empty(firefox.Orphans);
        Assert.True(firefox.Count >= 4);
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
    ///     Validates that the table extensions of MathML 3 (<c>columnalign</c>, <c>rowalign</c>, the spacing, the lines
    ///     and the frame) lay a fixture out as Firefox does: the size of every table and the box of every row, cell and
    ///     <c>mspace</c> relative to its table (the token metrics and the line box of Firefox are not CanvasNet's
    ///     reference, so the fixtures hold <c>mspace</c> contents taller than a line).
    /// </summary>
    /// <param name="fixture">The fixture name.</param>
    [Theory]
    [MemberData(nameof(FirefoxFixtures))]
    public void CanvasNetMathMl_ReferenceLayout_MatchesFirefox(string fixture)
    {
        // Arrange
        var document = MathMlDocument.Open(Path.Combine(FirefoxFolder, fixture + ".mml"));
        using var golden = JsonDocument.Parse(File.ReadAllText(Path.Combine(FirefoxFolder, fixture + ".layout.json")));
        var fontSize = golden.RootElement.GetProperty("fontSize").GetDouble();
        var elements = golden.RootElement.GetProperty("elements").EnumerateArray()
            .Select(element => (
                Path: element.GetProperty("path").GetString()!,
                Tag: element.GetProperty("tag").GetString()!,
                Box: element.GetProperty("box").EnumerateArray().Select(value => value.GetDouble()).ToArray()))
            .ToList();
        var tags = elements.ToDictionary(element => element.Path, element => element.Tag, StringComparer.Ordinal);

        // Act
        var root = document.LayoutBox(new MathMlRenderOptions { FontSize = fontSize }, TestContext.Current.CancellationToken);
        var boxes = BoxesByPath(document.Root, root);

        // Assert: every table, row, cell and mspace, relative to its outermost table
        var mismatches = new StringBuilder();
        var compared = 0;
        foreach (var (path, tag, box) in elements.Where(element => FirefoxTags.Contains(element.Tag)))
        {
            var segments = path.Split('/');
            var table = Enumerable.Range(1, segments.Length).Select(count => string.Join('/', segments.Take(count))).First(prefix => tags[prefix] == "mtable" || prefix == path);
            var origin = elements.First(element => element.Path == table).Box;
            if (!boxes.TryGetValue(path, out var ours) || !boxes.TryGetValue(table, out var ourTable))
            {
                mismatches.AppendLine(CultureInfo.InvariantCulture, $"{path} <{tag}>: no box");
                continue;
            }

            var name = $"{path} <{tag}>";
            CompareFirefox(mismatches, name + " x", ours.X - ourTable.X, box[0] - origin[0]);
            CompareFirefox(mismatches, name + " y", ours.Y - ourTable.Y, box[1] - origin[1]);
            CompareFirefox(mismatches, name + " width", ours.Width, box[2]);
            CompareFirefox(mismatches, name + " height", ours.Height, box[3]);
            compared++;
        }

        Assert.True(compared > 0, $"{fixture}: nothing compared");
        Assert.True(mismatches.Length == 0, $"{fixture}:\n{mismatches}");
    }

    /// <summary>
    ///     Gets the names of the fixtures of a folder.
    /// </summary>
    /// <param name="folder">The folder.</param>
    /// <returns>The names, without extension.</returns>
    private static TheoryData<string> FixturesOf(string folder)
    {
        var data = new TheoryData<string>();
        foreach (var file in Directory.GetFiles(folder, "*.mml").Order(StringComparer.Ordinal))
        {
            data.Add(Path.GetFileNameWithoutExtension(file));
        }

        return data;
    }

    /// <summary>
    ///     Records a value that differs from Firefox's by more than its tolerance.
    /// </summary>
    /// <param name="mismatches">The record of the mismatches.</param>
    /// <param name="what">The name of the value.</param>
    /// <param name="actual">CanvasNet's value.</param>
    /// <param name="expected">Firefox's value.</param>
    private static void CompareFirefox(StringBuilder mismatches, string what, double actual, double expected)
    {
        if (Math.Abs(actual - expected) > FirefoxTolerance)
        {
            mismatches.AppendLine(CultureInfo.InvariantCulture, $"{what}: {actual:0.###} (Firefox {expected:0.###})");
        }
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
