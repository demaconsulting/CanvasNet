using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;
using DemaConsulting.CanvasNet.Rendering;
using GeoPath = DemaConsulting.CanvasNet.Geometry.Path;
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

    /// <summary>Shapes_StrokeRoundRect_WithSolidStyle_PaintsPixels.</summary>
    [Fact]
    public void Shapes_StrokeRoundRect_WithSolidStyle_PaintsPixels()
    {
        var canvas = new RenderCanvas(NewSurface());
        canvas.StrokeRoundRect(4, 4, 10, 10, 3, new StrokeStyle(1f), new Rgba32(0, 255, 255, 255));
        Assert.True(CountAlphaPixels(canvas.Surface) > 15);
    }

    /// <summary>Shapes_StrokeRoundRect_RadiusLargerThanHalfShorterSide_ClampsAndPaintsPixels.</summary>
    [Fact]
    public void Shapes_StrokeRoundRect_RadiusLargerThanHalfShorterSide_ClampsAndPaintsPixels()
    {
        // Radius (100) far exceeds half of the shorter dimension (8x8 -> clamp to 4); exercises
        // the clamped-radius code path rather than a straightforward small-radius round rect.
        var canvas = new RenderCanvas(NewSurface());
        canvas.StrokeRoundRect(4, 4, 8, 8, 100, new StrokeStyle(1f), new Rgba32(0, 255, 255, 255));
        Assert.True(CountAlphaPixels(canvas.Surface) > 15);
    }

    /// <summary>
    ///     Proves FillRoundRect on a non-square rectangle fills exactly the analytic rounded
    ///     rectangle: area within tolerance, cut corners unfilled, straight edges in place.
    /// </summary>
    [Fact]
    public void Shapes_FillRoundRect_NonSquare_MatchesAnalyticRoundedRectangle()
    {
        // Arrange: 100x40 rect at (10, 10), radius 8 (area = 4000 - (4 - pi) * 64)
        var canvas = new RenderCanvas(NewSurface(120, 60));

        // Act
        canvas.FillRoundRect(10, 10, 100, 40, 8, new Rgba32(0, 0, 0, 255));

        // Assert
        AssertAnalyticRoundedRect(canvas.Surface, 10, 10, 100, 40, 8);
    }

    /// <summary>
    ///     Proves <see cref="CornerRoundEffect"/> applied to a non-square rectangle fills the
    ///     analytic rounded rectangle (same shape as FillRoundRect).
    /// </summary>
    [Fact]
    public void Shapes_CornerRoundEffect_NonSquareRectangle_MatchesAnalyticRoundedRectangle()
    {
        // Arrange
        var canvas = new RenderCanvas(NewSurface(120, 60));
        var rounded = CornerRoundEffect.Apply(GeoPath.Rectangle(10, 10, 100, 40), 8f);

        // Act
        canvas.FillPath(rounded, new Rgba32(0, 0, 0, 255));

        // Assert
        AssertAnalyticRoundedRect(canvas.Surface, 10, 10, 100, 40, 8);
    }

    private static void AssertAnalyticRoundedRect(Surface surface, int x, int y, int w, int h, int r)
    {
        var area = 0.0;
        for (var py = 0; py < surface.Height; py++)
        {
            for (var px = 0; px < surface.Width; px++)
            {
                area += surface[px, py].A / 255.0;
            }
        }

        var expected = (w * h) - ((4.0 - Math.PI) * r * r);
        Assert.InRange(area, expected * 0.997, expected * 1.003);

        // Straight edges in place
        Assert.Equal(255, surface[x + (w / 2), y].A);
        Assert.Equal(255, surface[x + (w / 2), y + h - 1].A);
        Assert.Equal(255, surface[x, y + (h / 2)].A);
        Assert.Equal(255, surface[x + w - 1, y + (h / 2)].A);
        Assert.Equal(0, surface[x + (w / 2), y - 1].A);
        Assert.Equal(0, surface[x + w, y + (h / 2)].A);

        // Corners cut: extreme corner pixels empty, point inside the arc (70%) filled,
        // point outside the arc (130%) empty
        var d = Math.Sqrt(0.5) * r;
        foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
        {
            var cx = sx < 0 ? x + r : x + w - r;
            var cy = sy < 0 ? y + r : y + h - r;
            Assert.Equal(0, surface[sx < 0 ? x : x + w - 1, sy < 0 ? y : y + h - 1].A);
            Assert.Equal(255, surface[(int)Math.Floor(cx + (sx * 0.7 * d)), (int)Math.Floor(cy + (sy * 0.7 * d))].A);
            Assert.Equal(0, surface[(int)Math.Floor(cx + (sx * 1.3 * d)), (int)Math.Floor(cy + (sy * 1.3 * d))].A);
        }
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
