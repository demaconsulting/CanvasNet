using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using GeoPath = DemaConsulting.CanvasNet.Geometry.Path;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Rendering;

/// <summary>Unit tests for the <see cref="RenderCanvas"/> class.</summary>
public class CanvasTests
{
    private static Surface NewSurface(int w = 32, int h = 32) => new(w, h);

    private static GeoPath Triangle() => new PathBuilder()
        .MoveTo(new Vector2(4, 4))
        .LineTo(new Vector2(20, 4))
        .LineTo(new Vector2(12, 20))
        .Close()
        .Build();

    private static byte[] SurfaceBytes(Surface s)
    {
        var buffer = new byte[s.Width * s.Height * 4];
        var i = 0;
        for (var y = 0; y < s.Height; y++)
        {
            s.GetRowSpanBytes(y).CopyTo(buffer.AsSpan(i, s.Width * 4));
            i += s.Width * 4;
        }

        return buffer;
    }

    /// <summary>Canvas_Constructor_WithSurface_HasIdentityTransform.</summary>
    [Fact]
    public void Canvas_Constructor_WithSurface_HasIdentityTransform()
    {
        var canvas = new RenderCanvas(NewSurface());
        Assert.Equal(Matrix3x2.Identity, canvas.CurrentTransform);
    }

    /// <summary>Canvas_Constructor_WithNullSurface_ThrowsArgumentNullException.</summary>
    [Fact]
    public void Canvas_Constructor_WithNullSurface_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new RenderCanvas(null!));
    }

    /// <summary>Canvas_Save_ThenRestore_RestoresPreviousTransform.</summary>
    [Fact]
    public void Canvas_Save_ThenRestore_RestoresPreviousTransform()
    {
        var canvas = new RenderCanvas(NewSurface());
        canvas.Translate(3, 4);
        var before = canvas.CurrentTransform;
        canvas.Save();
        canvas.Translate(10, 20);
        Assert.NotEqual(before, canvas.CurrentTransform);
        canvas.Restore();
        Assert.Equal(before, canvas.CurrentTransform);
    }

    /// <summary>Canvas_Save_NestedSaveRestore_UnwindsInLifoOrder.</summary>
    [Fact]
    public void Canvas_Save_NestedSaveRestore_UnwindsInLifoOrder()
    {
        var canvas = new RenderCanvas(NewSurface());
        canvas.Save();
        canvas.Translate(1, 0);
        canvas.Save();
        canvas.Translate(0, 2);
        var innermost = canvas.CurrentTransform;
        canvas.Restore();
        // After restoring inner: only the (1,0) translation should remain.
        Assert.Equal(Matrix3x2.CreateTranslation(1, 0), canvas.CurrentTransform);
        canvas.Restore();
        Assert.Equal(Matrix3x2.Identity, canvas.CurrentTransform);
        Assert.NotEqual(innermost, canvas.CurrentTransform);
    }

    /// <summary>Canvas_Restore_OnEmptyStack_ThrowsInvalidOperationException.</summary>
    [Fact]
    public void Canvas_Restore_OnEmptyStack_ThrowsInvalidOperationException()
    {
        var canvas = new RenderCanvas(NewSurface());
        Assert.Throws<InvalidOperationException>(() => canvas.Restore());
    }

    /// <summary>Canvas_Translate_ThenTranslate_ComposesAdditively.</summary>
    [Fact]
    public void Canvas_Translate_ThenTranslate_ComposesAdditively()
    {
        var canvas = new RenderCanvas(NewSurface());
        canvas.Translate(3, 4);
        canvas.Translate(2, -1);
        // Applying to origin yields (5, 3).
        var mapped = Vector2.Transform(Vector2.Zero, canvas.CurrentTransform);
        Assert.Equal(5f, mapped.X, 5);
        Assert.Equal(3f, mapped.Y, 5);
    }

    /// <summary>Canvas_RotateDegrees_ThenTranslate_AppliesTranslationInRotatedFrame.</summary>
    [Fact]
    public void Canvas_RotateDegrees_ThenTranslate_AppliesTranslationInRotatedFrame()
    {
        // Compose: RotateDegrees(90), then Translate(10, 0). With prepend semantics,
        // p' = translate(10,0) * rotate(90) applied to p means "translate first, then rotate".
        // So Vector2(0,0) -> translate to (10,0) -> rotate 90 -> approx (0, 10).
        var canvas = new RenderCanvas(NewSurface());
        canvas.RotateDegrees(90);
        canvas.Translate(10, 0);
        var mapped = Vector2.Transform(Vector2.Zero, canvas.CurrentTransform);
        Assert.Equal(0f, mapped.X, 3);
        Assert.Equal(10f, mapped.Y, 3);
    }

    /// <summary>Canvas_Translate_ThenRotateDegrees_AppliesRotationInTranslatedFrame.</summary>
    [Fact]
    public void Canvas_Translate_ThenRotateDegrees_AppliesRotationInTranslatedFrame()
    {
        // Compose: Translate(10, 0), then RotateDegrees(90). p' = rotate(90) * translate(10,0)
        // means "rotate first, then translate". So Vector2(0,0) -> rotate 90 -> (0,0) -> translate(10,0) -> (10, 0).
        var canvas = new RenderCanvas(NewSurface());
        canvas.Translate(10, 0);
        canvas.RotateDegrees(90);
        var mapped = Vector2.Transform(Vector2.Zero, canvas.CurrentTransform);
        Assert.Equal(10f, mapped.X, 3);
        Assert.Equal(0f, mapped.Y, 3);
    }

    /// <summary>Canvas_FillPath_NoTransform_ProducesByteIdenticalOutputToPathFillerFill.</summary>
    [Fact]
    public void Canvas_FillPath_NoTransform_ProducesByteIdenticalOutputToPathFillerFill()
    {
        var s1 = NewSurface();
        var s2 = NewSurface();
        var path = Triangle();

        var canvas = new RenderCanvas(s1);
        canvas.FillPath(path, new Rgba32(255, 0, 0, 255));

        PathFiller.Fill(s2, path, new Rgba32(255, 0, 0, 255));

        Assert.Equal(SurfaceBytes(s2), SurfaceBytes(s1));
    }

    /// <summary>Canvas_StrokePath_NoTransform_ProducesByteIdenticalOutputToPathStrokerPlusPathFiller.</summary>
    [Fact]
    public void Canvas_StrokePath_NoTransform_ProducesByteIdenticalOutputToPathStrokerPlusPathFiller()
    {
        var s1 = NewSurface();
        var s2 = NewSurface();
        var path = Triangle();
        var style = new StrokeStyle(2f);

        var canvas = new RenderCanvas(s1);
        canvas.StrokePath(path, style, new Rgba32(0, 255, 0, 255));

        var outline = PathStroker.Stroke(path, style);
        PathFiller.Fill(s2, outline, new Rgba32(0, 255, 0, 255));

        Assert.Equal(SurfaceBytes(s2), SurfaceBytes(s1));
    }

    /// <summary>Canvas_FillPath_WithTranslate_ShiftsOutputByExpectedPixels.</summary>
    [Fact]
    public void Canvas_FillPath_WithTranslate_ShiftsOutputByExpectedPixels()
    {
        var s1 = NewSurface();
        var s2 = NewSurface();
        var basePath = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(6, 2))
            .LineTo(new Vector2(6, 6))
            .LineTo(new Vector2(2, 6))
            .Close()
            .Build();

        var color = new Rgba32(10, 20, 30, 255);
        var canvas = new RenderCanvas(s1);
        canvas.Translate(8, 4);
        canvas.FillPath(basePath, color);

        // Reference: manually build the translated path and fill directly.
        var translated = new PathBuilder()
            .MoveTo(new Vector2(10, 6))
            .LineTo(new Vector2(14, 6))
            .LineTo(new Vector2(14, 10))
            .LineTo(new Vector2(10, 10))
            .Close()
            .Build();
        PathFiller.Fill(s2, translated, color);

        Assert.Equal(SurfaceBytes(s2), SurfaceBytes(s1));
    }

    /// <summary>
    ///     Canvas_FillPath_GradientWithRotateAndTranslate_MatchesPreTransformedGradientFill.
    /// </summary>
    /// <remarks>
    ///     Regression test: <see cref="RenderCanvas.FillPath(GeoPath, Gradient, FillRule)"/> must
    ///     compose the Canvas's current transform into the <see cref="Gradient"/>'s own coordinate
    ///     mapping (<see cref="Gradient.Transform"/>), not just into the filled path - otherwise a
    ///     transformed Canvas moves the filled geometry while the gradient is still sampled in the
    ///     old, untransformed local frame, so the gradient no longer visually follows the
    ///     transformed shape. Verified by comparing against manually pre-transforming both the
    ///     path and the gradient (via <see cref="Gradient.WithTransform"/>) and filling with the
    ///     static <see cref="PathFiller"/> directly, with no Canvas transform involved at all -
    ///     the two must produce byte-identical output.
    /// </remarks>
    [Fact]
    public void Canvas_FillPath_GradientWithRotateAndTranslate_MatchesPreTransformedGradientFill()
    {
        var s1 = NewSurface();
        var s2 = NewSurface();
        var basePath = new PathBuilder()
            .MoveTo(new Vector2(2, 2))
            .LineTo(new Vector2(14, 2))
            .LineTo(new Vector2(14, 14))
            .LineTo(new Vector2(2, 14))
            .Close()
            .Build();

        var gradient = new LinearGradient(
            new Vector2(2, 2),
            new Vector2(14, 14),
            [new GradientStop(0f, new Rgba32(255, 0, 0, 255)), new GradientStop(1f, new Rgba32(0, 0, 255, 255))]);

        var canvas = new RenderCanvas(s1);
        // 30 degrees (not a multiple of 90) rotates the diagonal gradient vector away from any
        // reflective/rotational symmetry the shape or gradient might otherwise coincidentally
        // share with a 90-degree rotation - which would let a bugged, untransformed-gradient fill
        // accidentally match the correct output and defeat this regression test.
        canvas.RotateDegrees(30);
        canvas.Translate(10, 4);
        canvas.FillPath(basePath, gradient);

        // Reference: pre-transform both the path and the gradient's own mapping with the exact
        // same composed transform, then fill directly via the static PathFiller with no Canvas
        // transform involved.
        var transformedPath = basePath.Transform(canvas.CurrentTransform);
        var transformedGradient = gradient.WithTransform(canvas.CurrentTransform);
        PathFiller.Fill(s2, transformedPath, transformedGradient);

        Assert.Equal(SurfaceBytes(s2), SurfaceBytes(s1));

        // Sanity check that this scenario actually exercises the bug this test guards against:
        // transforming the path alone while leaving the gradient in its old, untransformed local
        // frame (the pre-fix behavior) must produce a visibly DIFFERENT result from the correct,
        // fully-composed reference above - otherwise this test could pass "by accident" even
        // without Canvas.FillPath(Path, Gradient, FillRule) composing the transform into the
        // gradient.
        var s3 = NewSurface();
        PathFiller.Fill(s3, transformedPath, gradient);
        Assert.NotEqual(SurfaceBytes(s2), SurfaceBytes(s3));
    }

    /// <summary>Canvas_FillPath_NullPath_ThrowsArgumentNullException.</summary>
    [Fact]
    public void Canvas_FillPath_NullPath_ThrowsArgumentNullException()
    {
        var canvas = new RenderCanvas(NewSurface());
        Assert.Throws<ArgumentNullException>(() => canvas.FillPath((GeoPath)null!, new Rgba32(0, 0, 0, 255)));
    }

    /// <summary>Canvas_StrokePath_NullStyle_ThrowsArgumentNullException.</summary>
    [Fact]
    public void Canvas_StrokePath_NullStyle_ThrowsArgumentNullException()
    {
        var canvas = new RenderCanvas(NewSurface());
        Assert.Throws<ArgumentNullException>(() => canvas.StrokePath(Triangle(), null!, new Rgba32(0, 0, 0, 255)));
    }

    /// <summary>Canvas_FillPath_InvalidFillRule_ThrowsArgumentOutOfRangeException.</summary>
    [Fact]
    public void Canvas_FillPath_InvalidFillRule_ThrowsArgumentOutOfRangeException()
    {
        var canvas = new RenderCanvas(NewSurface());
        Assert.Throws<ArgumentOutOfRangeException>(() => canvas.FillPath(Triangle(), new Rgba32(0, 0, 0, 255), (FillRule)999));
    }
}
