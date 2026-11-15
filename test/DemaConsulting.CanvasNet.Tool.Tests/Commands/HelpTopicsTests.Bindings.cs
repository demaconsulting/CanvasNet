using DemaConsulting.CanvasNet.Svg;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.Tests.Commands;

/// <summary>
///     Tests that the <c>svg bindings</c> help card states what the library does, and that the framed-figure recipe
///     recommends a binding before the manual ways to find a point.
/// </summary>
public partial class HelpTopicsTests
{
    /// <summary>
    ///     Test that the <c>svg bindings</c> card is a child of <c>svg</c>, names the namespace of the library, lists every
    ///     binding warning code as an item, and names the strict error code.
    /// </summary>
    [Fact]
    public void HelpTopics_SvgBindings_ListsTheNamespaceAndEveryCode()
    {
        // Arrange
        var card = HelpTopics.Get("svg/bindings");
        var terms = HelpCardText.Items("svg/bindings").Select(i => i.Term).ToList();
        var text = HelpCardText.TextOf("svg/bindings");
        string[] codes =
        [
            SvgPositionBinding.MalformedCode, SvgPositionBinding.ImageMissingCode, SvgPositionBinding.UnsupportedCode,
            SvgPositionBinding.UnmatchedCode, SvgPositionBinding.AmbiguousCode, SvgPositionBinding.UnavailableCode,
            SvgPositionBinding.SingularCode
        ];

        // Assert
        Assert.Contains("bindings", HelpTopics.Get("svg").ChildIds.Select(id => id.Split('/')[^1]));
        Assert.Contains(SvgPositionBinding.Namespace, text, StringComparison.Ordinal);
        Assert.All(codes, code => Assert.Contains(code, terms));
        Assert.Contains(ErrorCodes.SvgBindingFailed, text, StringComparison.Ordinal);
        Assert.Contains("svg/framed-figure", card.SeeAlso);
    }

    /// <summary>
    ///     Test that the framed-figure card recommends, in this order, a binding, then <c>--positions</c> with the image x
    ///     and y added by hand, then the grid probe.
    /// </summary>
    [Fact]
    public void HelpTopics_FramedFigure_RecommendsBindingsFirst()
    {
        // Arrange
        var text = HelpCardText.TextOf("svg/framed-figure");

        // Act
        var binding = text.IndexOf("cn:x2y2", StringComparison.Ordinal);
        var positions = text.IndexOf("positions[].pixel", StringComparison.Ordinal);
        var grid = text.IndexOf("--grid", StringComparison.Ordinal);

        // Assert
        Assert.True(binding >= 0 && binding < positions && positions < grid, $"binding {binding}, positions {positions}, grid {grid}");
    }
}
