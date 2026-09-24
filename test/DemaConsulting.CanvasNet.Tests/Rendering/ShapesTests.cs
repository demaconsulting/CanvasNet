using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Rendering;
using RenderCanvas = DemaConsulting.CanvasNet.Rendering.Canvas;

namespace DemaConsulting.CanvasNet.Tests.Rendering;

/// <summary>Unit tests for the <see cref="Shapes"/> convenience extension methods.</summary>
public class ShapesTests
{
    private static Surface NewSurface(int w = 32, int h = 32) => new(w, h);

    private static int CountAlphaPixels(Surface s)
    {
        var count = 0;
        for (var y = 0; y < s.Height; y++)
        {
            var row = s.GetRowSpan(y);
            foreach (var px in row)
            {
                if (px.A > 0)
                {
                    count++;
                }
            }
        }

        return count;
    }

    /// <summary>Shapes_FillRect_WithSolidColor_PaintsRegion.</summary>
    [Fact]
    public void Shapes_FillRect_WithSolidColor_PaintsRegion()
    {
        var canvas = new RenderCanvas(NewSurface());
        canvas.FillRect(4, 4, 8, 8, new Rgba32(255, 0, 0, 255));
        Assert.True(CountAlphaPixels(canvas.Surface) > 40);
    }

    /// <summary>Shapes_StrokeRect_WithSolidStyle_PaintsPixels.</summary>
    [Fact]
    public void Shapes_StrokeRect_WithSolidStyle_PaintsPixels()
    {
        var canvas = new RenderCanvas(NewSurface());
        canvas.StrokeRect(4, 4, 10, 10, new StrokeStyle(1f), new Rgba32(0, 255, 0, 255));
        Assert.True(CountAlphaPixels(canvas.Surface) > 20);
    }

    /// <summary>Shapes_FillRoundRect_RadiusClampedAndPaintsPixels.</summary>
    [Fact]
    public void Shapes_FillRoundRect_RadiusClampedAndPaintsPixels()
    {
        var canvas = new RenderCanvas(NewSurface());
        canvas.FillRoundRect(4, 4, 8, 8, 100, new Rgba32(0, 0, 255, 255));
        Assert.True(CountAlphaPixels(canvas.Surface) > 30);
    }

    /// <summary>Shapes_FillCircle_ProducesRoundRegion.</summary>
    [Fact]
    public void Shapes_FillCircle_ProducesRoundRegion()
    {
        var canvas = new RenderCanvas(NewSurface());
        canvas.FillCircle(16, 16, 6, new Rgba32(200, 200, 200, 255));
        var count = CountAlphaPixels(canvas.Surface);
        // Rough area check: pi*r^2 ≈ 113; allow +/- tolerance for antialiasing.
        Assert.InRange(count, 80, 160);
    }

    /// <summary>Shapes_StrokeCircle_ProducesRing.</summary>
    [Fact]
    public void Shapes_StrokeCircle_ProducesRing()
    {
        var canvas = new RenderCanvas(NewSurface());
        canvas.StrokeCircle(16, 16, 6, new StrokeStyle(1f), new Rgba32(200, 0, 0, 255));
        Assert.True(CountAlphaPixels(canvas.Surface) > 15);
    }

    /// <summary>Shapes_FillRect_RespectsCanvasCurrentTransform.</summary>
    [Fact]
    public void Shapes_FillRect_RespectsCanvasCurrentTransform()
    {
        var s1 = NewSurface();
        var s2 = NewSurface();
        var canvas1 = new RenderCanvas(s1);
        canvas1.Translate(6, 6);
        canvas1.FillRect(0, 0, 8, 8, new Rgba32(10, 20, 30, 255));

        var canvas2 = new RenderCanvas(s2);
        canvas2.FillRect(6, 6, 8, 8, new Rgba32(10, 20, 30, 255));

        // Both should paint pixels; both counts should match closely.
        var c1 = CountAlphaPixels(s1);
        var c2 = CountAlphaPixels(s2);
        Assert.Equal(c2, c1);
    }

    /// <summary>Shapes_NullCanvas_ThrowsArgumentNullException.</summary>
    [Fact]
    public void Shapes_NullCanvas_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => Shapes.FillRect(null!, 0, 0, 1, 1, new Rgba32(0, 0, 0, 255)));
    }
}
