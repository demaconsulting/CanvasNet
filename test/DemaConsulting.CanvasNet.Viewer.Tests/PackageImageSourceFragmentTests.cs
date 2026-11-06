// cspell:ignore pptx vsdx
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Pptx;
using DemaConsulting.CanvasNet.TestSupport;
using DemaConsulting.CanvasNet.Vsdx;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     Tests of the page selection of <see cref="PptxImageSource"/> and <see cref="VsdxImageSource"/> by the fragment of a
///     link (<c>deck.pptx#slide=2</c>, <c>drawing.vsdx#page=2</c>), on the shared two-slide and two-page fixtures. They
///     live here because the multi-page fixtures are shared support of this project.
/// </summary>
public class PackageImageSourceFragmentTests
{
    /// <summary>
    ///     Creates a request for a link with a fragment into a file.
    /// </summary>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">The bytes of the file.</param>
    /// <param name="fragment">The fragment text, without the hash.</param>
    /// <param name="budget">The budget, or <see langword="null"/> for none.</param>
    /// <returns>The request.</returns>
    private static ImageSourceRequest Linked(string name, byte[] bytes, string fragment, ImageBudget? budget = null)
    {
        return new ImageSourceRequest
        {
            Href = name + "#" + fragment,
            FileName = name,
            Content = bytes,
            Fragment = ImageFragment.Parse(fragment),
            Budget = budget
        };
    }

    /// <summary>
    ///     Test that no fragment and <c>#page=1</c> draw the first slide, which has its red rectangle on the left.
    /// </summary>
    [Fact]
    public void PptxImageSource_Render_NoFragmentOrPageOne_DrawsFirstSlide()
    {
        // Arrange
        var bytes = FixtureFactory.CreatePptx();
        var source = new PptxImageSource();

        // Act
        using var none = source.Render(Linked("deck.pptx", bytes, string.Empty)).Surface;
        using var one = source.Render(Linked("deck.pptx", bytes, "page=1")).Surface;

        // Assert
        Assert.Equal((FixtureFactory.PptxSlideWidth, FixtureFactory.PptxSlideHeight), (none.Width, none.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), none[50, 180]);
        Assert.Equal(new Rgba32(255, 0, 0, 255), one[50, 180]);
        Assert.Equal(0, one[400, 180].A);
    }

    /// <summary>
    ///     Test that <c>#slide=2</c> and <c>#page=2</c> both draw the second slide, whose blue rectangle covers the top half.
    /// </summary>
    /// <param name="fragment">The fragment.</param>
    [Theory]
    [InlineData("slide=2")]
    [InlineData("page=2")]
    [InlineData("SLIDE=02")]
    [InlineData("page=1&slide=2")]
    [InlineData("slide=1&page=2")]
    public void PptxImageSource_Render_SlideOrPageTwo_DrawsSecondSlide(string fragment)
    {
        // Arrange
        var bytes = FixtureFactory.CreatePptx();

        // Act
        using var surface = new PptxImageSource().Render(Linked("deck.pptx", bytes, fragment)).Surface;

        // Assert: blue over the top half, transparent below
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[240, 90]);
        Assert.Equal(0, surface[240, 270].A);
    }

    /// <summary>
    ///     Test that a slide beyond the last is refused with the page-out-of-range code before anything is charged, and an
    ///     unusable value with the invalid-fragment code.
    /// </summary>
    [Fact]
    public void PptxImageSource_Render_SlideBeyondTheLastOrInvalid_IsRefused()
    {
        // Arrange
        var bytes = FixtureFactory.CreatePptx();
        var budget = new ImageBudget(1_000_000);
        var source = new PptxImageSource();

        // Act
        var outOfRange = Assert.Throws<ImageSourceRefusedException>(() => source.Render(Linked("deck.pptx", bytes, "slide=3", budget)));
        var invalid = Assert.Throws<ImageSourceRefusedException>(() => source.Render(Linked("deck.pptx", [1, 2, 3], "slide=0")));

        // Assert
        Assert.Equal(FolderImageResolver.PageOutOfRangeCode, outOfRange.Code);
        Assert.Equal("slide 3 does not exist: the presentation has 2 slides", outOfRange.Message);
        Assert.Equal(1_000_000L, budget.Remaining);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, invalid.Code);
    }

    /// <summary>
    ///     Test that the PPTX source declares <c>page</c> and <c>slide</c> and the VSDX source only <c>page</c>.
    /// </summary>
    [Fact]
    public void PackageImageSources_FragmentParameters_AreDeclared()
    {
        // Act / Assert
        Assert.Equal(["page", "slide"], ((IFragmentImageSource)new PptxImageSource()).FragmentParameters);
        Assert.Equal(["page"], ((IFragmentImageSource)new VsdxImageSource()).FragmentParameters);
    }

    /// <summary>
    ///     Test that the VSDX source draws page 1 by default and page 2, at the size of that page, with
    ///     <c>#page=2</c>.
    /// </summary>
    [Fact]
    public void VsdxImageSource_Render_PageFragment_DrawsTheChosenPage()
    {
        // Arrange
        var bytes = FixtureFactory.CreateVsdx();
        var source = new VsdxImageSource();

        // Act
        using var first = source.Render(Linked("drawing.vsdx", bytes, string.Empty)).Surface;
        using var second = source.Render(Linked("drawing.vsdx", bytes, "page=2")).Surface;

        // Assert: page 1 is 4 by 3 inches with red at the top-left; page 2 is 3 by 4 inches with blue over its bottom half
        Assert.Equal((FixtureFactory.VsdxPageWidth, FixtureFactory.VsdxPageHeight), (first.Width, first.Height));
        Assert.Equal(new Rgba32(255, 0, 0, 255), first[40, 40]);
        Assert.Equal((288, 384), (second.Width, second.Height));
        Assert.Equal(new Rgba32(0, 0, 255, 255), second[144, 320]);
        Assert.Equal(0, second[144, 60].A);
    }

    /// <summary>
    ///     Test that a VSDX page beyond the last and an unusable value are refused with the matching codes.
    /// </summary>
    [Fact]
    public void VsdxImageSource_Render_PageBeyondTheLastOrInvalid_IsRefused()
    {
        // Arrange
        var bytes = FixtureFactory.CreateVsdx();
        var budget = new ImageBudget(1_000_000);
        var source = new VsdxImageSource();

        // Act
        var outOfRange = Assert.Throws<ImageSourceRefusedException>(() => source.Render(Linked("drawing.vsdx", bytes, "page=3", budget)));
        var invalid = Assert.Throws<ImageSourceRefusedException>(() => source.Render(Linked("drawing.vsdx", [1, 2, 3], "page=x")));

        // Assert
        Assert.Equal(FolderImageResolver.PageOutOfRangeCode, outOfRange.Code);
        Assert.Equal("page 3 does not exist: the document has 2 pages", outOfRange.Message);
        Assert.Equal(1_000_000L, budget.Remaining);
        Assert.Equal(FolderImageResolver.FragmentInvalidCode, invalid.Code);
    }
}
