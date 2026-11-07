// cspell:ignore scad openscad
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Scad.Tests;

/// <summary>
///     Tests of the <c>def.</c> fragment parameters of <see cref="ScadImageSource"/>: <c>part.scad#def.width=20</c> sets a
///     variable of the model exactly as <c>-D width=20</c> does, a value or a name that is not usable refuses the link, and
///     the source declares the prefix to the resolver.
/// </summary>
public class ScadImageSourceDefineTests
{
    /// <summary>A model whose width is a variable.</summary>
    private const string Box = "width = 5; cube([width, 5, 5], center = true);";

    /// <summary>The same model with the width written into it.</summary>
    private const string WideBox = "width = 20; cube([width, 5, 5], center = true);";

    /// <summary>
    ///     Creates a request for a model with a fragment.
    /// </summary>
    /// <param name="source">The text of the model.</param>
    /// <param name="fragment">The fragment text, without the hash.</param>
    /// <param name="remaining">The pixels the source may allocate.</param>
    /// <returns>The request, for a 40 by 40 picture.</returns>
    private static ImageSourceRequest Request(string source, string fragment = "", long remaining = long.MaxValue)
    {
        return new ImageSourceRequest
        {
            Href = "part.scad" + (fragment.Length > 0 ? "#" + fragment : string.Empty),
            Fragment = ImageFragment.Parse(fragment),
            FileName = "part.scad",
            Content = System.Text.Encoding.UTF8.GetBytes(source),
            PixelWidth = 40,
            PixelHeight = 40,
            Budget = new ImageBudget(remaining)
        };
    }

    /// <summary>
    ///     Determines whether two surfaces have the same size and the same pixels.
    /// </summary>
    /// <param name="first">The first surface.</param>
    /// <param name="second">The second surface.</param>
    /// <returns><see langword="true"/> when they are equal.</returns>
    private static bool SamePixels(Surface first, Surface second)
    {
        if (first.Width != second.Width || first.Height != second.Height)
        {
            return false;
        }

        for (var y = 0; y < first.Height; y++)
        {
            for (var x = 0; x < first.Width; x++)
            {
                if (first[x, y] != second[x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Test that the source declares the <c>def.</c> prefix and no parameter name.
    /// </summary>
    [Fact]
    public void ScadImageSource_FragmentParameterPrefixes_DeclaresDef()
    {
        // Arrange
        var source = (IFragmentImageSource)new ScadImageSource();

        // Act / Assert
        Assert.Equal(["def."], source.FragmentParameterPrefixes);
        Assert.Equal("def.", ScadImageSource.DefinePrefix);
        Assert.Empty(source.FragmentParameters);
    }

    /// <summary>
    ///     Test that <c>def.width=20</c> replaces the assignment in the model, so the picture is the one of the model that has
    ///     that value, and differs from the model as it is.
    /// </summary>
    [Fact]
    public void ScadImageSource_Render_DefineFragment_OverridesTheAssignment()
    {
        // Arrange
        var source = new ScadImageSource();

        // Act
        using var overridden = source.Render(Request(Box, "def.width=20")).Surface;
        using var expected = source.Render(Request(WideBox)).Surface;
        using var plain = source.Render(Request(Box)).Surface;

        // Assert
        Assert.True(SamePixels(overridden, expected));
        Assert.False(SamePixels(overridden, plain));
    }

    /// <summary>
    ///     Test that the last value of a name wins, and that a percent-encoded text value, a boolean and a name in another
    ///     case behave as <c>-D</c> would.
    /// </summary>
    [Fact]
    public void ScadImageSource_Render_DefineFragment_LastWinsAndValuesAreTyped()
    {
        // Arrange
        const string label = "label = \"x\"; if (label == \"A\") { cube(10, center = true); } else { sphere(4, $fn = 16); }";
        const string show = "show = false; if (show) { cube(10, center = true); } else { sphere(4, $fn = 16); }";
        var source = new ScadImageSource();

        // Act
        using var last = source.Render(Request(Box, "def.width=5&def.width=20")).Surface;
        using var expected = source.Render(Request(WideBox)).Surface;
        using var quoted = source.Render(Request(label, "def.label=%22A%22")).Surface;
        using var unquoted = source.Render(Request(label, "def.label=A")).Surface;
        using var plainLabel = source.Render(Request(label)).Surface;
        using var shown = source.Render(Request(show, "def.show=true")).Surface;
        using var otherName = source.Render(Request(Box, "def.WIDTH=20")).Surface;
        using var plain = source.Render(Request(Box)).Surface;

        // Assert
        Assert.True(SamePixels(last, expected));
        Assert.True(SamePixels(quoted, unquoted));
        Assert.False(SamePixels(quoted, plainLabel));
        Assert.True(SamePixels(quoted, shown), "the cube of a true flag is the cube of the text A");
        Assert.True(SamePixels(otherName, plain), "WIDTH is another variable than width");
    }

    /// <summary>
    ///     Test that an unusable value (a vector) or a name the evaluator does not accept refuses the link with the
    ///     invalid-fragment code before anything is charged.
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    [Theory]
    [InlineData("def.width=[1,2]")]
    [InlineData("def.1x=3")]
    [InlineData("def.a%20b=3")]
    public void ScadImageSource_Render_InvalidDefine_IsRefusedBeforeCharging(string fragment)
    {
        // Arrange
        var request = Request(Box, fragment, 1_000_000);

        // Act
        var ex = Assert.Throws<ImageSourceRefusedException>(() => new ScadImageSource().Render(request));

        // Assert
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, ex.Code);
        Assert.Contains("'def.", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("(Parameter", ex.Message, StringComparison.Ordinal);
        Assert.Equal(1_000_000L, request.Budget!.Remaining);
    }

    /// <summary>
    ///     Test through the resolver that a <c>def.</c> link is drawn without a warning, that the picture is the overridden
    ///     one, and that a parameter of another kind is still reported as unknown.
    /// </summary>
    [Fact]
    public void FolderImageResolver_Resolve_ScadDefineLink_IsUnderstoodAndOthersAreUnknown()
    {
        // Arrange
        var directory = Path.Combine(Path.GetTempPath(), "canvasnet-scad-def-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "part.scad"), Box);
            var resolver = new FolderImageResolver(directory, [new ScadImageSource()]);

            // Act
            using var overridden = resolver.Resolve("part.scad#def.width=20", 40, 40);
            using var ignored = resolver.Resolve("part.scad#def.width=20&param.width=1", 40, 40);

            // Assert: the first link is silent, the second warns only about param.width; both draw
            Assert.NotNull(overridden);
            Assert.NotNull(ignored);
            var note = Assert.Single(resolver.Diagnostics);
            Assert.Equal(FolderImageResolver.FragmentUnknownParameterCode, note.Code);
            Assert.Contains("'param.width'", note.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
