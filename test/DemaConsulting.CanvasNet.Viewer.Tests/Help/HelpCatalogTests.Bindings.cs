using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Viewer.Tests.Help;

/// <summary>
///     Tests that the <c>svg/bindings</c> library card states what the library does, and that the framed-figure recipe
///     recommends a binding before the manual ways to find a point.
/// </summary>
public partial class HelpCatalogTests
{
    /// <summary>
    ///     Test that the <c>svg/bindings</c> card is a child of <c>svg</c>, names the namespace of the library, lists every
    ///     binding warning code as an item, and names the strict error code.
    /// </summary>
    [Fact]
    public void HelpCatalog_SvgBindings_ListsTheNamespaceAndEveryCode()
    {
        // Arrange
        var card = HelpCardText.Card("svg/bindings");
        var terms = HelpCardText.Items("svg/bindings").Select(i => i.Term).ToList();
        var text = HelpCardText.TextOf("svg/bindings");
        string[] codes =
        [
            SvgPositionBinding.MalformedCode, SvgPositionBinding.ImageMissingCode, SvgPositionBinding.UnsupportedCode,
            SvgPositionBinding.UnmatchedCode, SvgPositionBinding.AmbiguousCode, SvgPositionBinding.UnavailableCode,
            SvgPositionBinding.SingularCode
        ];

        // Assert
        Assert.Contains("bindings", HelpCardText.Card("svg").ChildIds.Select(id => id.Split('/')[^1]));
        Assert.Contains(SvgPositionBinding.Namespace, text, StringComparison.Ordinal);
        Assert.All(codes, code => Assert.Contains(code, terms));
        Assert.Contains(ErrorCodes.SvgBindingFailed, text, StringComparison.Ordinal);
        Assert.Contains("svg/framed-figure", card.SeeAlsoIds);
    }

    /// <summary>
    ///     Test that the framed-figure card recommends, in this order, a binding, then the positions the render reports
    ///     with the image x and y added by hand, then a labeled grid.
    /// </summary>
    [Fact]
    public void HelpCatalog_FramedFigure_RecommendsBindingsFirst()
    {
        // Arrange
        var text = HelpCardText.Flatten(HelpCardText.Card("svg/framed-figure").Markdown);

        // Act
        var binding = text.IndexOf("cn:x2y2", StringComparison.Ordinal);
        var positions = text.IndexOf("ViewerImage.Positions", StringComparison.Ordinal);
        var grid = text.IndexOf("labeled grid", StringComparison.Ordinal);

        // Assert
        Assert.True(binding >= 0 && binding < positions && positions < grid, $"binding {binding}, positions {positions}, grid {grid}");
    }
}
