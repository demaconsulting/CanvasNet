using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Regression test for the raw-overwrite alpha-compositing bug in <c>PdfDocument.Images.cs</c>'s
///     private <c>CompositeImageOntoSurface</c> method: proves it now alpha-blends each sampled
///     source pixel "over" the existing destination pixel (standard Porter-Duff "over"
///     compositing, via the shared internal <c>Rgba32.CompositeOver</c> helper) rather than
///     overwriting it outright.
/// </summary>
/// <remarks>
///     <c>CompositeImageOntoSurface</c> is <c>private</c>, and every decoded PDF image XObject
///     this package currently produces is always fully opaque (<c>/SMask</c>/<c>/Mask</c> are not
///     consulted - see that method's own remarks), so there is no public, end-to-end way to drive
///     a non-opaque source pixel through it. This test instead invokes
///     <c>CompositeImageOntoSurface</c> directly via reflection against a synthetic source
///     <see cref="Surface"/> with a genuine alpha channel, following the same reflection-based
///     internals-testing approach already used elsewhere in this test suite (for example
///     <c>PdfDocumentSymbolicEncodingTests</c>'s reflection-based private static field access) -
///     widening the method's accessibility purely for a test is avoided, consistent with that
///     precedent. The owning <see cref="PdfDocument"/> instance is created via
///     <see cref="RuntimeHelpers.GetUninitializedObject"/> (bypassing its private constructor,
///     which requires a real, parseable PDF byte buffer) since <c>CompositeImageOntoSurface</c>
///     only reads/writes the instance's own <c>_surface</c> field, which this test sets directly.
/// </remarks>
public class PdfDocumentImageCompositingTests
{
    /// <summary>
    ///     Invokes the private instance method <c>PdfDocument.CompositeImageOntoSurface(Surface, Matrix3x2)</c>
    ///     against a <see cref="PdfDocument"/> instance whose private <c>_surface</c> field has
    ///     been set to <paramref name="destination"/>, via reflection.
    /// </summary>
    private static void InvokeCompositeImageOntoSurface(Surface destination, Surface image, Matrix3x2 ctm)
    {
        var document = (PdfDocument)RuntimeHelpers.GetUninitializedObject(typeof(PdfDocument));

        var surfaceField = typeof(PdfDocument).GetField("_surface", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new MissingFieldException(nameof(PdfDocument), "_surface");
        surfaceField.SetValue(document, destination);

        var method = typeof(PdfDocument).GetMethod(
            "CompositeImageOntoSurface",
            BindingFlags.NonPublic | BindingFlags.Instance,
            [typeof(Surface), typeof(Matrix3x2)])
            ?? throw new MissingMethodException(nameof(PdfDocument), "CompositeImageOntoSurface");

        method.Invoke(document, [image, ctm]);
    }

    /// <summary>
    ///     Proves <c>CompositeImageOntoSurface</c> alpha-blends each sampled source pixel "over"
    ///     the existing destination pixel: a fully transparent source pixel with a non-matching
    ///     (near-white) stored RGB leaves the background completely unchanged (the exact
    ///     real-world scenario this bug was confirmed against - a logo image with an
    ///     unassociated-alpha white matte), a fully opaque source pixel exactly replaces the
    ///     background, and a partially transparent (anti-aliased) source pixel blends to the
    ///     exact expected bytes per the documented Porter-Duff "over" formula.
    /// </summary>
    [Fact]
    public void CompositeImageOntoSurface_SourceImageWithAlphaChannel_AlphaBlendsOntoExistingBackground()
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

        // Maps the image's unit square [0,1]x[0,1] directly onto the 3x1 destination surface:
        // column 0 -> u in [0, 1/3), column 1 -> u in [1/3, 2/3), column 2 -> u in [2/3, 1).
        var ctm = Matrix3x2.CreateScale(3f, 1f);

        InvokeCompositeImageOntoSurface(surface, image, ctm);

        // Fully transparent source pixel: background must be completely unchanged.
        Assert.Equal(background, surface[0, 0]);
        // Fully opaque source pixel: exact replacement.
        Assert.Equal(new Rgba32(10, 20, 30, 255), surface[1, 0]);
        // Partially transparent source pixel: exact Porter-Duff "over" blended bytes.
        Assert.Equal(new Rgba32(100, 177, 25, 255), surface[2, 0]);
    }
}
