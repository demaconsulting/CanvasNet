using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx blipfill srcrect embed sppr nvpicpr nvpr cnvpr cnvpicpr srgb hlink folhlink calibri asvg prst cust

/// <summary>
///     Unit-level tests for the Phase 1e picture-shape resolvers and painting primitive
///     (<see cref="PptxDocument.ResolvePictureSurface"/>, <see cref="PptxDocument.ResolveSrcRect"/>,
///     <see cref="PptxDocument.PaintPicture"/>). The <see cref="PptxDocument.PaintPicture"/>/
///     <see cref="PptxDocument.ResolveSrcRect"/> related tests operate on directly-constructed
///     <see cref="XElement"/> fragments/in-memory <see cref="Surface"/> instances;
///     <see cref="PptxDocument.ResolvePictureSurface"/> additionally needs a
///     real package/relationship-resolution context, so those tests build a small, self-contained
///     in-memory <c>.pptx</c>-shaped package (mirroring <see cref="PptxSystemIntegrationTests"/>'s
///     own <c>BuildPackage</c>/<see cref="ZipArchive"/> pattern, at unit-test scope). A full
///     decode-crop-paint, end-to-end scenario driven through the public <see cref="PptxDocument"/>
///     API lives in <see cref="PptxSystemIntegrationTests"/> instead.
/// </summary>
public class PptxImagesTests
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    // --- ResolveSrcRect ------------------------------------------------------------------------

    /// <summary>Proves an absent <c>&lt;a:srcRect&gt;</c> resolves to <see langword="null"/> (no crop).</summary>
    [Fact]
    public void ResolveSrcRect_NoSrcRectElement_ReturnsNull()
    {
        var blipFill = new XElement(PresentationNs + "blipFill");

        var result = PptxDocument.ResolveSrcRect(blipFill);

        Assert.Null(result);
    }

    /// <summary>Proves every edge attribute is parsed and scaled from OOXML's 1/100000-of-a-percent unit.</summary>
    [Fact]
    public void ResolveSrcRect_AllEdgesPresent_ParsesScaledFractions()
    {
        var blipFill = new XElement(
            PresentationNs + "blipFill",
            new XElement(A + "srcRect",
                new XAttribute("l", "10000"),
                new XAttribute("t", "20000"),
                new XAttribute("r", "30000"),
                new XAttribute("b", "40000")));

        var result = PptxDocument.ResolveSrcRect(blipFill);

        Assert.NotNull(result);
        Assert.Equal(0.1f, result.Left, 0.0001f);
        Assert.Equal(0.2f, result.Top, 0.0001f);
        Assert.Equal(0.3f, result.Right, 0.0001f);
        Assert.Equal(0.4f, result.Bottom, 0.0001f);
    }

    /// <summary>Proves an absent individual edge attribute defaults to <c>0</c> (no crop on that edge), per the OOXML schema default.</summary>
    [Fact]
    public void ResolveSrcRect_SomeEdgesAbsent_DefaultsToZero()
    {
        var blipFill = new XElement(
            PresentationNs + "blipFill",
            new XElement(A + "srcRect", new XAttribute("l", "10000")));

        var result = PptxDocument.ResolveSrcRect(blipFill);

        Assert.NotNull(result);
        Assert.Equal(0.1f, result.Left, 0.0001f);
        Assert.Equal(0f, result.Top);
        Assert.Equal(0f, result.Right);
        Assert.Equal(0f, result.Bottom);
    }

    /// <summary>Proves a non-numeric edge attribute value throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ResolveSrcRect_NonNumericEdgeAttribute_ThrowsInvalidDataException()
    {
        var blipFill = new XElement(
            PresentationNs + "blipFill",
            new XElement(A + "srcRect", new XAttribute("l", "not-a-number")));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveSrcRect(blipFill));
    }

    /// <summary>Proves a non-finite edge attribute value throws <see cref="InvalidDataException"/>.</summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ResolveSrcRect_NonFiniteEdgeAttribute_ThrowsInvalidDataException(string nonFiniteValue)
    {
        var blipFill = new XElement(
            PresentationNs + "blipFill",
            new XElement(A + "srcRect", new XAttribute("l", nonFiniteValue)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveSrcRect(blipFill));
    }

    /// <summary>Proves <see cref="PptxDocument.ResolveSrcRect"/> rejects a null argument.</summary>
    [Fact]
    public void ResolveSrcRect_NullBlipFillElement_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PptxDocument.ResolveSrcRect(null!));
    }

    // --- PaintPicture ----------------------------------------------------------------------------

    /// <summary>Builds a small, fully-opaque two-tone test image: red left half, blue right half.</summary>
    private static Surface BuildTwoToneImage(int width, int height)
    {
        var image = new Surface(width, height);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                image[x, y] = x < width / 2
                    ? new Rgba32(255, 0, 0, 255)
                    : new Rgba32(0, 0, 255, 255);
            }
        }

        return image;
    }

    /// <summary>Proves an identity-transformed, uncropped picture paints the source image's pixels at the expected destination location, left-to-right unflipped.</summary>
    [Fact]
    public void PaintPicture_IdentityTransformNoCrop_PaintsImagePixelsUnflipped()
    {
        using var image = BuildTwoToneImage(10, 10);
        using var surface = new Surface(10, 10);

        // Shape occupies the full [0,10]x[0,10] surface box: widthEmu/heightEmu = 10, identity transform.
        PptxDocument.PaintPicture(surface, image, srcRect: null, Matrix3x2.Identity, widthEmu: 10f, heightEmu: 10f);

        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[2, 5]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[8, 5]);
    }

    /// <summary>Proves no <c>(1 - v)</c> row flip is applied: a top/bottom asymmetric image paints with the same row order in surface space (unlike the PDF renderer's own y-up image convention).</summary>
    [Fact]
    public void PaintPicture_TopBottomAsymmetricImage_PaintsWithoutVerticalFlip()
    {
        using var image = new Surface(4, 4);
        for (var x = 0; x < 4; x++)
        {
            image[x, 0] = new Rgba32(255, 255, 0, 255); // top row: yellow
            image[x, 1] = new Rgba32(0, 0, 0, 255);
            image[x, 2] = new Rgba32(0, 0, 0, 255);
            image[x, 3] = new Rgba32(0, 255, 255, 255); // bottom row: cyan
        }

        using var surface = new Surface(4, 4);

        PptxDocument.PaintPicture(surface, image, srcRect: null, Matrix3x2.Identity, widthEmu: 4f, heightEmu: 4f);

        // No vertical flip: surface row 0 (top) must match image row 0 (top) - yellow; surface row
        // 3 (bottom) must match image row 3 (bottom) - cyan. A PDF-style (1-v) flip would reverse
        // this (yellow at the bottom, cyan at the top).
        Assert.Equal(new Rgba32(255, 255, 0, 255), surface[1, 0]);
        Assert.Equal(new Rgba32(0, 255, 255, 255), surface[1, 3]);
    }

    /// <summary>Proves a <see cref="PptxSrcRect"/> crop re-maps the sampled region: cropping away the left half of a two-tone image leaves only its right (blue) half visible.</summary>
    [Fact]
    public void PaintPicture_WithSrcRectCroppingLeftHalf_SamplesOnlyRightHalf()
    {
        using var image = BuildTwoToneImage(10, 10);
        using var surface = new Surface(10, 10);
        var srcRect = new PptxSrcRect(Left: 0.5f, Top: 0f, Right: 0f, Bottom: 0f);

        PptxDocument.PaintPicture(surface, image, srcRect, Matrix3x2.Identity, widthEmu: 10f, heightEmu: 10f);

        // The entire painted destination now samples only the image's right (blue) half.
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[1, 5]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[8, 5]);
    }

    /// <summary>Proves a translated shape-to-surface transform shifts the painted footprint accordingly.</summary>
    [Fact]
    public void PaintPicture_TranslatedTransform_ShiftsPaintedFootprint()
    {
        using var image = BuildTwoToneImage(10, 10);
        using var surface = new Surface(30, 30);
        var transform = Matrix3x2.CreateTranslation(10f, 10f);

        PptxDocument.PaintPicture(surface, image, srcRect: null, transform, widthEmu: 10f, heightEmu: 10f);

        // Untouched outside the shifted footprint.
        Assert.Equal(default, surface[2, 2]);
        // Inside the shifted footprint, pixels resolve exactly as the identity case would, offset by (10,10).
        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[12, 15]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[18, 15]);
    }

    /// <summary>Proves a degenerate (non-invertible, zero-area) composed transform paints nothing rather than throwing.</summary>
    [Fact]
    public void PaintPicture_DegenerateTransform_PaintsNothing()
    {
        using var image = BuildTwoToneImage(10, 10);
        using var surface = new Surface(10, 10);

        // Scale(0,0) composed with CreateScale(widthEmu, heightEmu) is non-invertible.
        PptxDocument.PaintPicture(surface, image, srcRect: null, Matrix3x2.CreateScale(0f), widthEmu: 10f, heightEmu: 10f);

        for (var y = 0; y < 10; y++)
        {
            for (var x = 0; x < 10; x++)
            {
                Assert.Equal(default, surface[x, y]);
            }
        }
    }

    /// <summary>Proves <see cref="PptxDocument.PaintPicture"/> rejects a null surface/image argument.</summary>
    [Fact]
    public void PaintPicture_NullSurfaceOrImage_ThrowsArgumentNullException()
    {
        using var surface = new Surface(10, 10);
        using var image = new Surface(10, 10);

        Assert.Throws<ArgumentNullException>(
            () => PptxDocument.PaintPicture(null!, image, null, Matrix3x2.Identity, 10f, 10f));
        Assert.Throws<ArgumentNullException>(
            () => PptxDocument.PaintPicture(surface, null!, null, Matrix3x2.Identity, 10f, 10f));
    }

    /// <summary>
    ///     Regression test for the raw-overwrite alpha-compositing bug: proves
    ///     <see cref="PptxDocument.PaintPicture"/> now alpha-blends each sampled source pixel
    ///     "over" the existing destination pixel (standard Porter-Duff "over" compositing) rather
    ///     than overwriting it outright. Covers a fully transparent source pixel with a
    ///     non-matching (near-white) stored RGB (background must show through unchanged - this is
    ///     the exact real-world scenario this bug was confirmed against, a logo PNG with an
    ///     unassociated-alpha white matte), a fully opaque source pixel (exact replacement), and a
    ///     partially transparent (anti-aliased) source pixel (exact blended bytes per the
    ///     documented formula).
    /// </summary>
    [Fact]
    public void PaintPicture_SourceImageWithAlphaChannel_AlphaBlendsOntoExistingBackground()
    {
        using var image = new Surface(3, 1);
        image[0, 0] = new Rgba32(255, 255, 255, 0); // fully transparent, non-matching near-white RGB
        image[1, 0] = new Rgba32(10, 20, 30, 255); // fully opaque
        image[2, 0] = new Rgba32(200, 100, 50, 128); // partially transparent (anti-aliased)

        using var surface = new Surface(3, 1);
        var background = new Rgba32(0, 255, 0, 255); // known solid green background
        surface[0, 0] = background;
        surface[1, 0] = background;
        surface[2, 0] = background;

        PptxDocument.PaintPicture(surface, image, srcRect: null, Matrix3x2.Identity, widthEmu: 3f, heightEmu: 1f);

        // Fully transparent source pixel: background must be completely unchanged.
        Assert.Equal(background, surface[0, 0]);
        // Fully opaque source pixel: exact replacement.
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[1, 0]);
        // Partially transparent source pixel: exact Porter-Duff "over" blended bytes.
        Assert.Equal(new Rgba32(100, 177, 25, 255), surface[2, 0]);
    }

    // --- ResolvePictureClipPath ------------------------------------------------------------------

    /// <summary>Proves an explicit <c>&lt;a:prstGeom prst="rect"&gt;</c> resolves to <see langword="null"/> (no clip - equivalent to the full bounding-box rectangle).</summary>
    [Fact]
    public void ResolvePictureClipPath_RectPreset_ReturnsNull()
    {
        var spPr = new XElement(
            PresentationNs + "spPr",
            new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst")));

        var result = PptxDocument.ResolvePictureClipPath(spPr, 100f, 100f);

        Assert.Null(result);
    }

    /// <summary>Proves a <c>&lt;p:spPr&gt;</c> with neither <c>&lt;a:prstGeom&gt;</c> nor <c>&lt;a:custGeom&gt;</c> resolves to <see langword="null"/> (no clip, schema-edge-case tolerance).</summary>
    [Fact]
    public void ResolvePictureClipPath_NoGeometryElement_ReturnsNull()
    {
        var spPr = new XElement(PresentationNs + "spPr");

        var result = PptxDocument.ResolvePictureClipPath(spPr, 100f, 100f);

        Assert.Null(result);
    }

    /// <summary>Proves a non-<c>rect</c> preset (<c>ellipse</c>) resolves to a non-null <see cref="Path"/>, sized to the shape's own local box.</summary>
    [Fact]
    public void ResolvePictureClipPath_EllipsePreset_ReturnsNonNullPath()
    {
        var spPr = new XElement(
            PresentationNs + "spPr",
            new XElement(A + "prstGeom", new XAttribute("prst", "ellipse"), new XElement(A + "avLst")));

        var result = PptxDocument.ResolvePictureClipPath(spPr, 100f, 100f);

        Assert.NotNull(result);
        Assert.NotEqual(Path.Empty, result);
    }

    /// <summary>Proves a second non-<c>rect</c> preset (<c>roundRect</c>) also resolves to a non-null <see cref="Path"/>.</summary>
    [Fact]
    public void ResolvePictureClipPath_RoundRectPreset_ReturnsNonNullPath()
    {
        var spPr = new XElement(
            PresentationNs + "spPr",
            new XElement(A + "prstGeom", new XAttribute("prst", "roundRect"), new XElement(A + "avLst")));

        var result = PptxDocument.ResolvePictureClipPath(spPr, 100f, 100f);

        Assert.NotNull(result);
    }

    /// <summary>Proves an <c>&lt;a:custGeom&gt;</c> element resolves to a non-null <see cref="Path"/> via the same reused <see cref="PptxDocument.ResolveCustomGeometry"/> resolver auto-shapes already use.</summary>
    [Fact]
    public void ResolvePictureClipPath_CustGeom_ReturnsNonNullPath()
    {
        var spPr = new XElement(
            PresentationNs + "spPr",
            new XElement(
                A + "custGeom",
                new XElement(
                    A + "pathLst",
                    new XElement(
                        A + "path",
                        new XAttribute("w", "100"),
                        new XAttribute("h", "100"),
                        new XElement(A + "moveTo", new XElement(A + "pt", new XAttribute("x", "0"), new XAttribute("y", "0"))),
                        new XElement(A + "lnTo", new XElement(A + "pt", new XAttribute("x", "100"), new XAttribute("y", "0"))),
                        new XElement(A + "lnTo", new XElement(A + "pt", new XAttribute("x", "100"), new XAttribute("y", "100"))),
                        new XElement(A + "close")))));

        var result = PptxDocument.ResolvePictureClipPath(spPr, 100f, 100f);

        Assert.NotNull(result);
        Assert.NotEqual(Path.Empty, result);
    }

    /// <summary>Proves a <c>&lt;a:prstGeom&gt;</c> with no <c>prst</c> attribute throws <see cref="InvalidDataException"/>, mirroring <see cref="PptxDocument.ResolveShapeGeometry"/>'s identical check.</summary>
    [Fact]
    public void ResolvePictureClipPath_PrstGeomMissingPrstAttribute_ThrowsInvalidDataException()
    {
        var spPr = new XElement(PresentationNs + "spPr", new XElement(A + "prstGeom"));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolvePictureClipPath(spPr, 100f, 100f));
    }

    /// <summary>Proves an unsupported preset name propagates <see cref="PptxUnsupportedFeatureException"/> unchanged, exactly as it already propagates for an auto-shape with the same preset.</summary>
    [Fact]
    public void ResolvePictureClipPath_UnsupportedPreset_ThrowsPptxUnsupportedFeatureException()
    {
        var spPr = new XElement(
            PresentationNs + "spPr",
            new XElement(A + "prstGeom", new XAttribute("prst", "not-a-real-preset"), new XElement(A + "avLst")));

        Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolvePictureClipPath(spPr, 100f, 100f));
    }

    /// <summary>Proves <see cref="PptxDocument.ResolvePictureClipPath"/> rejects a null argument.</summary>
    [Fact]
    public void ResolvePictureClipPath_NullSpPrElement_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PptxDocument.ResolvePictureClipPath(null!, 100f, 100f));
    }

    // --- ResolvePictureGeometryPath ----------------------------------------------------------
    //
    // Unlike ResolvePictureClipPath (which collapses "no geometry" and an explicit "rect" preset
    // to null, a pure image-content-clip optimization), ResolvePictureGeometryPath always returns
    // a concrete path - needed so a picture's own <a:ln> stroke outlines a real boundary (an
    // implicit/explicit rectangle, a preset, or a custom geometry) rather than being silently
    // dropped. See this method's own remarks in PptxDocument.Images.cs.

    /// <summary>Proves an explicit <c>&lt;a:prstGeom prst="rect"&gt;</c> resolves to a non-null rectangle <see cref="Path"/> - unlike <see cref="PptxDocument.ResolvePictureClipPath"/>'s own "rect collapses to null" behavior.</summary>
    [Fact]
    public void ResolvePictureGeometryPath_RectPreset_ReturnsNonNullRectanglePath()
    {
        var spPr = new XElement(
            PresentationNs + "spPr",
            new XElement(A + "prstGeom", new XAttribute("prst", "rect"), new XElement(A + "avLst")));

        var result = PptxDocument.ResolvePictureGeometryPath(spPr, 100f, 100f);

        Assert.NotEqual(Path.Empty, result);
    }

    /// <summary>Proves a <c>&lt;p:spPr&gt;</c> with neither <c>&lt;a:prstGeom&gt;</c> nor <c>&lt;a:custGeom&gt;</c> resolves to the implicit full-rectangle fallback - unlike <see cref="PptxDocument.ResolvePictureClipPath"/>'s own "no geometry collapses to null" behavior.</summary>
    [Fact]
    public void ResolvePictureGeometryPath_NoGeometryElement_ReturnsImplicitRectanglePath()
    {
        var spPr = new XElement(PresentationNs + "spPr");

        var result = PptxDocument.ResolvePictureGeometryPath(spPr, 100f, 100f);

        Assert.NotEqual(Path.Empty, result);
    }

    /// <summary>Proves a non-<c>rect</c> preset (<c>ellipse</c>) resolves to a non-null <see cref="Path"/>, sized to the shape's own local box.</summary>
    [Fact]
    public void ResolvePictureGeometryPath_EllipsePreset_ReturnsNonNullPath()
    {
        var spPr = new XElement(
            PresentationNs + "spPr",
            new XElement(A + "prstGeom", new XAttribute("prst", "ellipse"), new XElement(A + "avLst")));

        var result = PptxDocument.ResolvePictureGeometryPath(spPr, 100f, 100f);

        Assert.NotEqual(Path.Empty, result);
    }

    /// <summary>Proves an <c>&lt;a:custGeom&gt;</c> element resolves to a non-null <see cref="Path"/> via the same reused <see cref="PptxDocument.ResolveCustomGeometry"/> resolver auto-shapes already use.</summary>
    [Fact]
    public void ResolvePictureGeometryPath_CustGeom_ReturnsNonNullPath()
    {
        var spPr = new XElement(
            PresentationNs + "spPr",
            new XElement(
                A + "custGeom",
                new XElement(
                    A + "pathLst",
                    new XElement(
                        A + "path",
                        new XAttribute("w", "100"),
                        new XAttribute("h", "100"),
                        new XElement(A + "moveTo", new XElement(A + "pt", new XAttribute("x", "0"), new XAttribute("y", "0"))),
                        new XElement(A + "lnTo", new XElement(A + "pt", new XAttribute("x", "100"), new XAttribute("y", "0"))),
                        new XElement(A + "lnTo", new XElement(A + "pt", new XAttribute("x", "100"), new XAttribute("y", "100"))),
                        new XElement(A + "close")))));

        var result = PptxDocument.ResolvePictureGeometryPath(spPr, 100f, 100f);

        Assert.NotEqual(Path.Empty, result);
    }

    /// <summary>Proves a <c>&lt;a:prstGeom&gt;</c> with no <c>prst</c> attribute throws <see cref="InvalidDataException"/>, mirroring <see cref="PptxDocument.ResolvePictureClipPath"/>'s/<see cref="PptxDocument.ResolveShapeGeometry"/>'s identical check.</summary>
    [Fact]
    public void ResolvePictureGeometryPath_PrstGeomMissingPrstAttribute_ThrowsInvalidDataException()
    {
        var spPr = new XElement(PresentationNs + "spPr", new XElement(A + "prstGeom"));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolvePictureGeometryPath(spPr, 100f, 100f));
    }

    /// <summary>Proves an unsupported preset name propagates <see cref="PptxUnsupportedFeatureException"/> unchanged, exactly as it already propagates for an auto-shape/<see cref="PptxDocument.ResolvePictureClipPath"/> with the same preset.</summary>
    [Fact]
    public void ResolvePictureGeometryPath_UnsupportedPreset_ThrowsPptxUnsupportedFeatureException()
    {
        var spPr = new XElement(
            PresentationNs + "spPr",
            new XElement(A + "prstGeom", new XAttribute("prst", "not-a-real-preset"), new XElement(A + "avLst")));

        Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolvePictureGeometryPath(spPr, 100f, 100f));
    }

    /// <summary>Proves <see cref="PptxDocument.ResolvePictureGeometryPath"/> rejects a null argument.</summary>
    [Fact]
    public void ResolvePictureGeometryPath_NullSpPrElement_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => PptxDocument.ResolvePictureGeometryPath(null!, 100f, 100f));
    }

    // --- PaintPicture clipPath ---------------------------------------------------------------------

    /// <summary>Proves an ellipse clip path leaves a bounding-box corner sample unpainted (background shows through) while the shape's own center is painted with the source image's pixel - the confirmed real-world bug this fix resolves.</summary>
    [Fact]
    public void PaintPicture_EllipseClipPath_CornerUnpaintedCenterPainted()
    {
        using var image = new Surface(20, 20);
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                image[x, y] = new Rgba32(10, 20, 30, 255);
            }
        }

        using var surface = new Surface(20, 20);
        var background = new Rgba32(255, 255, 255, 255);
        for (var y = 0; y < 20; y++)
        {
            for (var x = 0; x < 20; x++)
            {
                surface[x, y] = background;
            }
        }

        var clipPath = PptxPresetGeometry.Build("ellipse", 20f, 20f);

        PptxDocument.PaintPicture(surface, image, srcRect: null, Matrix3x2.Identity, widthEmu: 20f, heightEmu: 20f, clipPath);

        // Well inside the ellipse: image content.
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[10, 10]);
        // Bounding-box corner, well outside the ellipse: unpainted background.
        Assert.Equal(background, surface[0, 0]);
        Assert.Equal(background, surface[19, 19]);
    }

    /// <summary>Proves a <see langword="null"/> <c>clipPath</c> (the default) continues painting the full unclipped rectangle, a direct regression guard for every pre-existing <see cref="PptxDocument.PaintPicture"/> call in this file.</summary>
    [Fact]
    public void PaintPicture_NullClipPath_PaintsFullRectangleUnclipped()
    {
        using var image = BuildTwoToneImage(10, 10);
        using var surface = new Surface(10, 10);

        PptxDocument.PaintPicture(surface, image, srcRect: null, Matrix3x2.Identity, widthEmu: 10f, heightEmu: 10f, clipPath: null);

        Assert.Equal(new Rgba32(255, 0, 0, 255), surface[0, 0]);
        Assert.Equal(new Rgba32(0, 0, 255, 255), surface[9, 9]);
    }

    // --- ResolvePictureSurface (needs a real package/relationship-resolution context) -----------

    private static readonly XNamespace PresentationNs = "http://schemas.openxmlformats.org/presentationml/2006/main";

    /// <summary>
    ///     Builds a minimal, navigable, single-slide in-memory <c>.pptx</c>-shaped package whose
    ///     slide has a single relationship (<c>rId2</c>) to a media part at
    ///     <c>ppt/media/image1.&lt;mediaExtension&gt;</c> containing <paramref name="mediaBytes"/>,
    ///     resolved to <paramref name="mediaContentType"/> via a <c>[Content_Types].xml</c>
    ///     <c>&lt;Default&gt;</c> extension mapping - exactly enough package structure for
    ///     <see cref="PptxDocument.ResolvePictureSurface"/>'s relationship/content-type/part-bytes
    ///     resolution path to be exercised without a full slide/layout/master/theme chain (unlike
    ///     <see cref="PptxSystemIntegrationTests"/>'s own, fuller package-building helpers).
    /// </summary>
    private static (MemoryStream Package, string SlidePartPath) BuildMinimalImagePackage(
        string mediaExtension, string mediaContentType, byte[] mediaBytes, bool linkInsteadOfEmbed = false)
    {
        var relationshipXml = linkInsteadOfEmbed
            ? $"""<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="https://example.invalid/image.{mediaExtension}" TargetMode="External" />"""
            : $"""<Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/image" Target="../media/image1.{mediaExtension}" />""";

        var contentTypesXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml" />
              <Default Extension="xml" ContentType="application/xml" />
              <Default Extension="{mediaExtension}" ContentType="{mediaContentType}" />
            </Types>
            """;

        const string packageRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="ppt/presentation.xml" />
            </Relationships>
            """;

        const string presentationXml =
            """
            <p:presentation xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
              <p:sldSz cx="9144000" cy="6858000"/>
              <p:sldIdLst><p:sldId id="256" r:id="rId2"/></p:sldIdLst>
            </p:presentation>
            """;

        const string presentationRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slide" Target="slides/slide1.xml" />
            </Relationships>
            """;

        const string slideXml =
            """
            <p:sld xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
            </p:sld>
            """;

        var slideRelsXml =
            $"""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideLayout" Target="../slideLayouts/slideLayout1.xml" />
              {relationshipXml}
            </Relationships>
            """;

        const string layoutXml =
            """
            <p:sldLayout xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
            </p:sldLayout>
            """;

        const string layoutRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/slideMaster" Target="../slideMasters/slideMaster1.xml" />
            </Relationships>
            """;

        const string masterXml =
            """
            <p:sldMaster xmlns:p="http://schemas.openxmlformats.org/presentationml/2006/main">
              <p:cSld><p:spTree/></p:cSld>
              <p:clrMap bg1="lt1" tx1="dk1" bg2="lt2" tx2="dk2"/>
              <p:txStyles/>
            </p:sldMaster>
            """;

        const string masterRelsXml =
            """
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/theme" Target="../theme/theme1.xml" />
            </Relationships>
            """;

        const string themeXml =
            """
            <a:theme xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" name="TestTheme">
              <a:themeElements>
                <a:clrScheme name="Test">
                  <a:dk1><a:srgbClr val="101010"/></a:dk1>
                  <a:lt1><a:srgbClr val="F0F0F0"/></a:lt1>
                  <a:dk2><a:srgbClr val="202020"/></a:dk2>
                  <a:lt2><a:srgbClr val="E0E0E0"/></a:lt2>
                  <a:accent1><a:srgbClr val="4472C4"/></a:accent1>
                  <a:accent2><a:srgbClr val="ED7D31"/></a:accent2>
                  <a:accent3><a:srgbClr val="A5A5A5"/></a:accent3>
                  <a:accent4><a:srgbClr val="FFC000"/></a:accent4>
                  <a:accent5><a:srgbClr val="5B9BD5"/></a:accent5>
                  <a:accent6><a:srgbClr val="70AD47"/></a:accent6>
                  <a:hlink><a:srgbClr val="0563C1"/></a:hlink>
                  <a:folHlink><a:srgbClr val="954F72"/></a:folHlink>
                </a:clrScheme>
                <a:fontScheme name="TestFonts">
                  <a:majorFont><a:latin typeface="Calibri Light"/><a:ea typeface=""/><a:cs typeface=""/></a:majorFont>
                  <a:minorFont><a:latin typeface="Calibri"/><a:ea typeface=""/><a:cs typeface=""/></a:minorFont>
                </a:fontScheme>
              </a:themeElements>
            </a:theme>
            """;

        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteTextEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteTextEntry(archive, "_rels/.rels", packageRelsXml);
            WriteTextEntry(archive, "ppt/presentation.xml", presentationXml);
            WriteTextEntry(archive, "ppt/_rels/presentation.xml.rels", presentationRelsXml);
            WriteTextEntry(archive, "ppt/slides/slide1.xml", slideXml);
            WriteTextEntry(archive, "ppt/slides/_rels/slide1.xml.rels", slideRelsXml);
            WriteTextEntry(archive, "ppt/slideLayouts/slideLayout1.xml", layoutXml);
            WriteTextEntry(archive, "ppt/slideLayouts/_rels/slideLayout1.xml.rels", layoutRelsXml);
            WriteTextEntry(archive, "ppt/slideMasters/slideMaster1.xml", masterXml);
            WriteTextEntry(archive, "ppt/slideMasters/_rels/slideMaster1.xml.rels", masterRelsXml);
            WriteTextEntry(archive, "ppt/theme/theme1.xml", themeXml);

            if (!linkInsteadOfEmbed)
            {
                WriteBinaryEntry(archive, $"ppt/media/image1.{mediaExtension}", mediaBytes);
            }
        }

        stream.Position = 0;
        return (stream, "ppt/slides/slide1.xml");
    }

    private static void WriteTextEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, Encoding.UTF8);
        writer.Write(content);
    }

    private static void WriteBinaryEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name);
        using var entryStream = entry.Open();
        entryStream.Write(content, 0, content.Length);
    }

    /// <summary>Builds a minimal, valid single-pixel PNG's encoded bytes, via the core <see cref="PngCodec"/>.</summary>
    private static byte[] BuildPngBytes(Rgba32 color)
    {
        using var surface = new Surface(1, 1);
        surface[0, 0] = color;
        using var buffer = new MemoryStream();
        PngCodec.Save(surface, buffer);
        return buffer.ToArray();
    }

    /// <summary>Builds a <c>&lt;p:blipFill&gt;</c> element declaring <c>&lt;a:blip r:embed="rId2"/&gt;</c>.</summary>
    private static XElement BuildBlipFillEmbed(string relationshipId = "rId2") =>
        new(PresentationNs + "blipFill", new XElement(A + "blip", new XAttribute(R + "embed", relationshipId)));

    /// <summary>Proves a well-formed embedded PNG reference decodes successfully into a <see cref="Surface"/> with the expected pixel content.</summary>
    [Fact]
    public void ResolvePictureSurface_EmbeddedPng_DecodesSurfaceWithExpectedPixel()
    {
        var expectedColor = new Rgba32(10, 20, 30, 255);
        var pngBytes = BuildPngBytes(expectedColor);
        var (package, slidePartPath) = BuildMinimalImagePackage("png", "image/png", pngBytes);
        using var stream = package;
        using var document = PptxDocument.Open(stream);

        var surface = document.ResolvePictureSurface(slidePartPath, BuildBlipFillEmbed());

        Assert.Equal(1, surface.Width);
        Assert.Equal(1, surface.Height);
        Assert.Equal(expectedColor, surface[0, 0]);
    }

    /// <summary>Proves a <c>&lt;p:blipFill&gt;</c> with no <c>&lt;a:blip&gt;</c> child throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ResolvePictureSurface_NoBlipElement_ThrowsInvalidDataException()
    {
        var (package, slidePartPath) = BuildMinimalImagePackage("png", "image/png", BuildPngBytes(default));
        using var stream = package;
        using var document = PptxDocument.Open(stream);

        var blipFill = new XElement(PresentationNs + "blipFill");

        Assert.Throws<InvalidDataException>(() => document.ResolvePictureSurface(slidePartPath, blipFill));
    }

    /// <summary>Proves an <c>&lt;a:blip&gt;</c> with neither <c>r:embed</c> nor <c>r:link</c> throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ResolvePictureSurface_BlipMissingEmbedAndLink_ThrowsInvalidDataException()
    {
        var (package, slidePartPath) = BuildMinimalImagePackage("png", "image/png", BuildPngBytes(default));
        using var stream = package;
        using var document = PptxDocument.Open(stream);

        var blipFill = new XElement(PresentationNs + "blipFill", new XElement(A + "blip"));

        Assert.Throws<InvalidDataException>(() => document.ResolvePictureSurface(slidePartPath, blipFill));
    }

    /// <summary>Proves a linked-only (<c>r:link</c>, no <c>r:embed</c>) picture throws <see cref="PptxUnsupportedFeatureException"/> with feature token <c>"pptx-image-link"</c>.</summary>
    [Fact]
    public void ResolvePictureSurface_LinkedBlipOnly_ThrowsPptxUnsupportedFeatureExceptionWithImageLinkToken()
    {
        var (package, slidePartPath) = BuildMinimalImagePackage("png", "image/png", [], linkInsteadOfEmbed: true);
        using var stream = package;
        using var document = PptxDocument.Open(stream);

        var blipFill = new XElement(PresentationNs + "blipFill", new XElement(A + "blip", new XAttribute(R + "link", "rId2")));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => document.ResolvePictureSurface(slidePartPath, blipFill));
        Assert.Equal("pptx-image-link", ex.Feature);
    }

    /// <summary>The Microsoft SVG blip extension's own XML namespace (<c>&lt;asvg:svgBlip&gt;</c>).</summary>
    private static readonly XNamespace Asvg = "http://schemas.microsoft.com/office/drawing/2016/SVG/main";

    /// <summary>The <c>uri</c> attribute value identifying the Microsoft SVG blip extension.</summary>
    private const string SvgBlipExtensionUri = "{96DAC541-7B7A-43D3-8B79-37D633B846F1}";

    /// <summary>Builds a minimal, valid SVG document's UTF-8 encoded bytes with the given intrinsic pixel size.</summary>
    private static byte[] BuildSvgBytes(int width, int height) =>
        Encoding.UTF8.GetBytes(
            $"""<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {width} {height}"><rect width="{width}" height="{height}" fill="#112233"/></svg>""");

    /// <summary>
    ///     Builds a <c>&lt;p:blipFill&gt;</c> element declaring only a Microsoft SVG extension
    ///     fallback (<c>&lt;a:blip&gt;&lt;a:extLst&gt;&lt;a:ext uri="{96DAC541-7B7A-43D3-8B79-
    ///     37D633B846F1}"&gt;&lt;asvg:svgBlip r:embed="..."/&gt;&lt;/a:ext&gt;&lt;/a:extLst&gt;
    ///     &lt;/a:blip&gt;</c>), with no raster <c>r:embed</c>/<c>r:link</c> on the <c>&lt;a:blip&gt;</c>
    ///     element itself.
    /// </summary>
    private static XElement BuildSvgOnlyBlipFill(string? svgEmbedId) =>
        new(
            PresentationNs + "blipFill",
            new XElement(
                A + "blip",
                new XElement(
                    A + "extLst",
                    new XElement(
                        A + "ext",
                        new XAttribute("uri", SvgBlipExtensionUri),
                        svgEmbedId is null
                            ? new XElement(Asvg + "svgBlip")
                            : new XElement(Asvg + "svgBlip", new XAttribute(R + "embed", svgEmbedId))))));

    /// <summary>
    ///     Proves an <c>&lt;a:blip&gt;</c> declaring neither <c>r:embed</c> nor <c>r:link</c>, but
    ///     only a Microsoft SVG extension (<c>&lt;a:extLst&gt;/&lt;a:ext uri="{96DAC541-7B7A-43D3-
    ///     8B79-37D633B846F1}"&gt;&lt;asvg:svgBlip r:embed="..."/&gt;&lt;/a:ext&gt;</c>) fallback,
    ///     now resolves to a non-null, correctly-sized <see cref="Surface"/> rasterized via
    ///     <c>DemaConsulting.CanvasNet.Svg</c>'s <c>SvgCodec</c>, instead of throwing.
    /// </summary>
    [Fact]
    public void ResolvePictureSurface_SvgOnlyBlipExtension_ReturnsDecodedSurfaceFromAsvgSvgBlipEmbed()
    {
        var svgBytes = BuildSvgBytes(10, 20);
        var (package, slidePartPath) = BuildMinimalImagePackage("svg", "image/svg+xml", svgBytes);
        using var stream = package;
        using var document = PptxDocument.Open(stream);

        var surface = document.ResolvePictureSurface(slidePartPath, BuildSvgOnlyBlipFill("rId2"));

        Assert.Equal(10, surface.Width);
        Assert.Equal(20, surface.Height);
    }

    /// <summary>
    ///     Proves a Microsoft SVG blip extension present but with no <c>r:embed</c> attribute of
    ///     its own on <c>&lt;asvg:svgBlip&gt;</c> (genuinely malformed - nothing left to fall back
    ///     to) still throws <see cref="InvalidDataException"/>.
    /// </summary>
    [Fact]
    public void ResolvePictureSurface_SvgBlipExtensionMissingEmbed_ThrowsInvalidDataException()
    {
        var (package, slidePartPath) = BuildMinimalImagePackage("png", "image/png", BuildPngBytes(default));
        using var stream = package;
        using var document = PptxDocument.Open(stream);

        Assert.Throws<InvalidDataException>(() => document.ResolvePictureSurface(slidePartPath, BuildSvgOnlyBlipFill(svgEmbedId: null)));
    }

    /// <summary>
    ///     Proves a well-formed SVG-only blip whose own <c>viewBox</c> declares intrinsic
    ///     dimensions exceeding <see cref="Surface.MaxDimension"/> is still decoded - not rejected
    ///     outright - but its resolved <see cref="Surface"/> is clamped to
    ///     <see cref="Surface.MaxDimension"/> on both axes, exercising
    ///     <c>ResolveSvgOnlyPictureSurface</c>'s own <c>Math.Clamp(..., 1, Surface.MaxDimension)</c>
    ///     safety policy - a regression guard against this clamp silently being removed while an
    ///     oversized SVG would otherwise reach <c>SvgCodec.Load</c> unclamped.
    /// </summary>
    [Fact]
    public void ResolvePictureSurface_SvgOnlyBlipExceedsMaxDimension_ClampsSurfaceToMaxDimensionOnBothAxes()
    {
        var svgBytes = BuildSvgBytes(Surface.MaxDimension + 1000, Surface.MaxDimension + 2000);
        var (package, slidePartPath) = BuildMinimalImagePackage("svg", "image/svg+xml", svgBytes);
        using var stream = package;
        using var document = PptxDocument.Open(stream);

        var surface = document.ResolvePictureSurface(slidePartPath, BuildSvgOnlyBlipFill("rId2"));

        Assert.Equal(Surface.MaxDimension, surface.Width);
        Assert.Equal(Surface.MaxDimension, surface.Height);
    }

    /// <summary>Proves an unrecognized media content type (for example an EMF vector picture) throws <see cref="PptxUnsupportedFeatureException"/> with feature token <c>"pptx-image-format"</c>.</summary>
    [Fact]
    public void ResolvePictureSurface_UnsupportedContentType_ThrowsPptxUnsupportedFeatureExceptionWithImageFormatToken()
    {
        var (package, slidePartPath) = BuildMinimalImagePackage("emf", "image/x-emf", [1, 2, 3, 4]);
        using var stream = package;
        using var document = PptxDocument.Open(stream);

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => document.ResolvePictureSurface(slidePartPath, BuildBlipFillEmbed()));
        Assert.Equal("pptx-image-format", ex.Feature);
    }

    /// <summary>Proves <see cref="PptxDocument.ResolvePictureSurface"/> rejects a null argument.</summary>
    [Fact]
    public void ResolvePictureSurface_NullArguments_ThrowsArgumentNullException()
    {
        var (package, slidePartPath) = BuildMinimalImagePackage("png", "image/png", BuildPngBytes(default));
        using var stream = package;
        using var document = PptxDocument.Open(stream);

        Assert.Throws<ArgumentNullException>(() => document.ResolvePictureSurface(null!, BuildBlipFillEmbed()));
        Assert.Throws<ArgumentNullException>(() => document.ResolvePictureSurface(slidePartPath, null!));
    }
}
