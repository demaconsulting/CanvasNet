// Portions derived from Vega (BSD-3-Clause): the image mark of vega-scenegraph (src/marks/image.js); see third-party/README.md
using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.ImageSources;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.VegaLite.Scenegraph;

/// <summary>
///     The painting of pictures (a part of <see cref="ScenePainter"/>).
/// </summary>
/// <remarks>
///     <para>
///         <b>Geometry</b> (<c>vega-scenegraph/src/marks/image.js</c>, <c>imageWidth</c>, <c>imageHeight</c>, <c>imageXOffset</c>, <c>imageYOffset</c> and <c>draw</c>). The width of the box is the
///         <see cref="SceneImage.Width"/>; without one it is the height times the aspect ratio of the picture when the aspect is kept and a height is given, else the natural width. The height
///         is symmetric. The anchor (<see cref="SceneImage.X"/>, <see cref="SceneImage.Y"/>) is moved by half the box for <c>center</c> and <c>middle</c> and by the whole box for <c>right</c>
///         and <c>bottom</c>; any other name is the left or top edge. A box without area draws nothing. When the aspect is kept, both the width and the height are given and the aspect ratio
///         of the picture differs from that of the box, the picture is fitted inside the box and centered on the other axis (<c>ar1 &lt; ar0</c>: the height becomes <c>w / ar0</c>, else the
///         width becomes <c>h * ar0</c>); with <see cref="SceneImage.Aspect"/> false it fills the box.
///     </para>
///     <para>
///         <b>Loading.</b> The picture is requested from the <see cref="SceneImageSource"/> at the size of its box in device pixels (the box that is known: the width and the height; one given
///         dimension is used for both when the other is unknown, and <see cref="NaturalDevicePixels"/> when neither is, a deliberate decision: a vector picture has no natural size). The
///         resolver says what kind of picture it returned (<see cref="IImageNaturalSizeResolver"/>, from <see cref="ImageSourceResult.IsNaturalSize"/>): a raster comes back at its own size and is drawn
///         at that size in scene units (a pixel is a unit at a scale of one, so the chart scales as a whole), even when that size happens to be the size of the request; a picture that was drawn
///         for the request (a vector picture, drawn sharp) has the requested size divided by the scale as its natural size. Only a resolver that does not say (a host's own
///         <see cref="IImageResolver"/>) leaves it to the size of the surface: one that comes back at exactly the size of the request is taken as drawn for it. The size that is returned decides
///         the aspect ratio of the picture, and a picture drawn to fill the request counts as filling the box.
///     </para>
///     <para>
///         <b>Drawing.</b> The box is filled with the picture as a tile that is not repeated, so the edges of a box on fractional pixels are anti-aliased, a reduced picture is area-averaged and an
///         enlarged one is interpolated (the core tile paint). The opacity of the item is multiplied into the alpha of a private copy of the picture. A picture that is not smoothed
///         (<see cref="SceneImage.Smooth"/> false) is resampled to the nearest device size with nearest-neighbor sampling first. The surface of the loader is never changed.
///     </para>
/// </remarks>
internal sealed partial class ScenePainter
{
    /// <summary>The size in device pixels, at a scale of one, of the request for a picture whose box has neither a width nor a height: a vector picture draws at this size and a raster ignores it.</summary>
    internal const double NaturalDevicePixels = 256;

    /// <summary>
    ///     Draws a picture.
    /// </summary>
    /// <param name="image">The picture item.</param>
    /// <param name="local">The context of the enclosing group.</param>
    /// <exception cref="VegaLiteRenderException">Thrown, located at the mark, when the picture would be more than <see cref="VegaLiteLimits.MaxImages"/> (the loader's limit).</exception>
    private void PaintImage(SceneImage image, Context local)
    {
        if (_images is null || !_images.HasResolver || image.Opacity <= 0 || image.Url.Length == 0 || !IsBoundedAt(local.Scale, image.X, image.Y))
        {
            return;
        }

        var given = (Width: image.Width, Height: image.Height);
        if (!IsValidLength(given.Width) || !IsValidLength(given.Height) || given.Width == 0 || given.Height == 0)
        {
            return;
        }

        // The size of the box that is known, in device pixels, which is what the resolver is asked for
        var requestWidth = given.Width ?? given.Height ?? NaturalDevicePixels;
        var requestHeight = given.Height ?? given.Width ?? NaturalDevicePixels;
        var pixelWidth = ToDevicePixels(requestWidth * local.Scale);
        var pixelHeight = ToDevicePixels(requestHeight * local.Scale);
        var surface = _images.Get(image.Url, pixelWidth, pixelHeight, image.Source, out var naturalSize);
        if (surface is null)
        {
            return;
        }

        if (!TryPlace(image, given, surface, (pixelWidth, pixelHeight), local.Scale, naturalSize, out var box))
        {
            return;
        }

        DrawPicture(surface, box, image, local);
    }

    /// <summary>
    ///     Works out the rectangle a picture is drawn in, in the units of the enclosing group (Vega's <c>draw</c> of the image mark).
    /// </summary>
    /// <param name="image">The picture item.</param>
    /// <param name="given">The width and height of the item.</param>
    /// <param name="surface">The picture.</param>
    /// <param name="requested">The size in device pixels the picture was asked for.</param>
    /// <param name="scale">The uniform scale of the placement.</param>
    /// <param name="naturalSize">
    ///     <see langword="true"/> when the resolver says the picture is a raster at its own pixel size, <see langword="false"/> when it says the picture was drawn for the request, and
    ///     <see langword="null"/> when the resolver does not say (the size of the surface is compared with the request then).
    /// </param>
    /// <param name="box">Receives the rectangle: the left, top, width and height.</param>
    /// <returns><see langword="false"/> when nothing is drawn (a box without area, or one that cannot be drawn).</returns>
    internal static bool TryPlace(SceneImage image, (double? Width, double? Height) given, Surface surface, (int Width, int Height) requested, float scale, bool? naturalSize, out (double X, double Y, double Width, double Height) box)
    {
        box = default;
        var pictureWidth = (double)surface.Width;
        var pictureHeight = (double)surface.Height;

        // The kind of the picture is what the resolver says. A picture drawn for the request has that size in units (its pixels over the scale); a raster has its pixels as units.
        // Only a resolver that says nothing leaves the size of the surface to decide: a picture that has the size of the request counts as drawn for it
        var guessedForRequest = (surface.Width == requested.Width && surface.Height <= requested.Height) || (surface.Height == requested.Height && surface.Width <= requested.Width);
        var raster = naturalSize ?? !guessedForRequest;
        var naturalWidth = raster ? pictureWidth : pictureWidth / scale;
        var naturalHeight = raster ? pictureHeight : pictureHeight / scale;

        // The aspect ratio of the picture; a picture drawn to fill the request is the aspect ratio of the box it was drawn for
        var fillsBox = !raster && surface.Width == requested.Width && surface.Height == requested.Height && given.Width is not null && given.Height is not null;
        var ratio = fillsBox ? given.Width!.Value / given.Height!.Value : pictureWidth / pictureHeight;

        // imageWidth and imageHeight: the given size, else the size that keeps the aspect ratio of the picture from the other, else the natural size
        var width = given.Width ?? (image.Aspect && given.Height is > 0 ? given.Height.Value * ratio : naturalWidth);
        var height = given.Height ?? (image.Aspect && given.Width is > 0 ? given.Width.Value / ratio : naturalHeight);
        if (!(width > 0) || !(height > 0))
        {
            return false;
        }

        var x = image.X - AnchorOffset(image.Align, width, "center", "right");
        var y = image.Y - AnchorOffset(image.Baseline, height, "middle", "bottom");

        // Both dimensions given and a different aspect ratio: the picture is fitted inside the box and centered
        if (image.Aspect && given.Width is { } boxWidth && given.Height is { } boxHeight)
        {
            var boxRatio = boxWidth / boxHeight;
            if (ratio.CompareTo(boxRatio) != 0)
            {
                if (boxRatio < ratio)
                {
                    var fitted = width / ratio;
                    y += (height - fitted) / 2;
                    height = fitted;
                }
                else
                {
                    var fitted = height * ratio;
                    x += (width - fitted) / 2;
                    width = fitted;
                }
            }
        }

        if (!IsBoundedAt(scale, x, y, width, height))
        {
            return false;
        }

        box = (x, y, width, height);
        return true;
    }

    /// <summary>
    ///     Gets how far an anchor lies from the start edge of a box (Vega's <c>imageXOffset</c> and <c>imageYOffset</c>).
    /// </summary>
    /// <param name="anchor">The anchor name.</param>
    /// <param name="length">The length of the box.</param>
    /// <param name="middle">The name of the middle anchor.</param>
    /// <param name="end">The name of the end anchor.</param>
    /// <returns>Half the length for the middle, the length for the end and zero for any other name.</returns>
    private static double AnchorOffset(string anchor, double length, string middle, string end)
    {
        if (anchor == middle)
        {
            return length / 2;
        }

        return anchor == end ? length : 0;
    }

    /// <summary>
    ///     Draws a picture into a rectangle.
    /// </summary>
    /// <param name="surface">The picture, which belongs to the loader.</param>
    /// <param name="box">The rectangle in the units of the enclosing group.</param>
    /// <param name="image">The picture item (the opacity and the smoothing).</param>
    /// <param name="local">The context of the enclosing group.</param>
    private static void DrawPicture(Surface surface, (double X, double Y, double Width, double Height) box, SceneImage image, Context local)
    {
        // The rectangle in surface pixels (the transform is a uniform scale and a translation)
        var corner = Vector2.Transform(new Vector2((float)box.X, (float)box.Y), local.Transform);
        var width = (float)(box.Width * local.Scale);
        var height = (float)(box.Height * local.Scale);
        if (!float.IsFinite(corner.X) || !float.IsFinite(corner.Y) || !(width > 0) || !(height > 0) || !float.IsFinite(width) || !float.IsFinite(height))
        {
            return;
        }

        // The pixels the tile paint samples: the picture, or a nearest-neighbor copy at the device size, with the opacity folded into its alpha
        Surface? work = null;
        try
        {
            var tile = surface;
            if (!image.Smooth)
            {
                work = surface.Resize(
                    Math.Clamp((int)Math.Round(width), 1, Surface.MaxDimension),
                    Math.Clamp((int)Math.Round(height), 1, Surface.MaxDimension),
                    ResamplingMode.Nearest);
                tile = work;
            }

            if (image.Opacity < 1)
            {
                work ??= surface.Clone();
                MultiplyAlpha(work, Math.Clamp(image.Opacity, 0, 1));
                tile = work;
            }

            var mapping = Matrix3x2.CreateScale(width / tile.Width, height / tile.Height) * Matrix3x2.CreateTranslation(corner);
            var paint = new TilePaint(tile, mapping, tile.Width, tile.Height);
            PathFiller.Fill(local.Target, Path.Rectangle(corner.X, corner.Y, width, height), paint, FillRule.NonZero, FlattenTolerance);
        }
        finally
        {
            work?.Dispose();
        }
    }

    /// <summary>
    ///     Multiplies the alpha of every pixel of a surface by a factor.
    /// </summary>
    /// <param name="surface">The surface, which is changed (the straight alpha of the pixels is scaled; the colors stay).</param>
    /// <param name="factor">The factor from 0 to 1.</param>
    private static void MultiplyAlpha(Surface surface, double factor)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            var row = surface.GetRowSpan(y);
            for (var x = 0; x < row.Length; x++)
            {
                row[x].A = (byte)Math.Round(row[x].A * factor, MidpointRounding.AwayFromZero);
            }
        }
    }

    /// <summary>
    ///     Determines whether a given width or height can be used: it is absent, or finite and not negative.
    /// </summary>
    /// <param name="length">The length, or null.</param>
    /// <returns><see langword="true"/> for a usable length.</returns>
    private static bool IsValidLength(double? length) => length is null || (double.IsFinite(length.Value) && length.Value >= 0);

    /// <summary>
    ///     Rounds a length in device pixels up to whole pixels and clamps it to the sizes a surface can have.
    /// </summary>
    /// <param name="length">The length in device pixels.</param>
    /// <returns>A value from 1 to <see cref="Surface.MaxDimension"/>.</returns>
    /// <remarks>A small tolerance is subtracted before rounding up, so a length that is whole up to floating-point noise does not gain a pixel.</remarks>
    private static int ToDevicePixels(double length) =>
        double.IsNaN(length) ? 1 : (int)Math.Clamp(Math.Ceiling(length - 1e-3), 1d, Surface.MaxDimension);
}
