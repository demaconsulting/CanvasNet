// cspell:ignore jpeg tiff jpeg2000
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     Raster sources (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000) of <see cref="SourceLoader"/>.
/// </summary>
public static partial class SourceLoader
{
    /// <summary>
    ///     Reads the header of a raster file.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="kind">The kind of the file (a raster kind).</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file.</returns>
    private static SourceInfo ProbeRaster(SourceInput input, SourceKind kind, long bytes)
    {
        var info = Read(input.Name, kind, () => GetRasterInfo(input, kind));
        var orientation = kind == SourceKind.Jpeg ? Read(input.Name, kind, () => ReadOrientation(input)) : null;
        return new SourceInfo(
            input.Name,
            kind,
            bytes,
            info.Width,
            info.Height,
            info.Channels,
            info.HasAlpha,
            info.CanDecode,
            info.FrameCount,
            1,
            orientation,
            [],
            [],
            []);
    }

    /// <summary>
    ///     Loads a raster file: decodes it, orients it, resizes it, and flattens it as the options say.
    /// </summary>
    /// <param name="info">The facts about the file.</param>
    /// <param name="options">The loading options.</param>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="context">The hooks of the caller.</param>
    /// <returns>The loaded source.</returns>
    /// <remarks>
    ///     The declared size is charged to the pixel budget of the caller (when there is one) before the size limit is
    ///     applied and before anything is decoded, so a picture that is far too large is refused for the budget, as a
    ///     warning of the embedding document, rather than as an error.
    /// </remarks>
    private static LoadedSource LoadRaster(SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
    {
        // Charge the declared size first, then refuse a file that is too large, and a result that would be too large,
        // before decoding anything
        context.ChargeSurface(info.Width, info.Height);
        EnsureStoredSizeSupported(info);
        var orientation = options.AutoOrient && info.ExifOrientation is > 1 ? info.ExifOrientation : null;
        var swapped = orientation >= 5;
        var orientedWidth = swapped ? info.Height : info.Width;
        var orientedHeight = swapped ? info.Width : info.Height;
        var (width, height) = ComputeTargetSize(info.Path, orientedWidth, orientedHeight, options);

        var operations = new List<AppliedOperation>();
        var surface = Read(info.Path, info.Format, () => DecodeRaster(input, info.Format));
        try
        {
            if (orientation is { } exif)
            {
                surface = Replace(surface, surface.ApplyExifOrientation(exif));
                operations.Add(Operation("auto-orient", ("orientation", exif)));
            }

            if (surface.Width != width || surface.Height != height)
            {
                surface = Replace(surface, surface.Resize(width, height, options.Filter));

                // The filter is only reported when one was chosen, so the common result keeps its short form
                operations.Add(options.Filter == ResamplingMode.Auto
                    ? Operation("resize", ("width", width), ("height", height))
                    : Operation("resize", ("width", width), ("height", height), ("filter", ImagePipeline.FilterName(options.Filter))));
            }

            // A raster source keeps its transparency unless a background color was asked for
            if (options.Background is { A: > 0 } background)
            {
                surface = Replace(surface, surface.FlattenOver(background));
                operations.Add(Operation("flatten", ("background", FormatColor(background))));
            }

            return new LoadedSource(surface, info, 1, false, [], [], operations);
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Disposes a surface and returns the surface that replaces it.
    /// </summary>
    /// <param name="old">The surface to dispose.</param>
    /// <param name="replacement">The replacement.</param>
    /// <returns><paramref name="replacement"/>.</returns>
    private static Surface Replace(Surface old, Surface replacement)
    {
        old.Dispose();
        return replacement;
    }

    /// <summary>
    ///     Reads the header of a raster file with the codec of its kind.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The header information.</returns>
    private static ImageInfo GetRasterInfo(SourceInput input, SourceKind kind)
    {
        using var stream = input.Open();
        return kind switch
        {
            SourceKind.Png => PngCodec.GetInfo(stream),
            SourceKind.Jpeg => JpegCodec.GetInfo(stream),
            SourceKind.Bmp => BmpCodec.GetInfo(stream),
            SourceKind.Tiff => TiffCodec.GetInfo(stream),
            SourceKind.Gif => GifCodec.GetInfo(stream),
            _ => Jpeg2000Codec.GetInfo(stream)
        };
    }

    /// <summary>
    ///     Reads the EXIF orientation of a JPEG source.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <returns>The orientation (1 to 8), or <see langword="null"/> when the file has none.</returns>
    private static int? ReadOrientation(SourceInput input)
    {
        using var stream = input.Open();
        return JpegCodec.ReadExifOrientation(stream);
    }

    /// <summary>
    ///     Reads the size a raster source declares in its header, without decoding any pixel.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The declared width and height.</returns>
    /// <remarks>
    ///     A GIF is the exception to "the header only": its codec decodes the first frame to describe it, which would
    ///     allocate the picture this read is meant to guard, so the size of the Logical Screen Descriptor (bytes 6 to 9,
    ///     little-endian) is read directly. The other codecs read the header only.
    /// </remarks>
    /// <exception cref="InvalidDataException">Thrown when the header is malformed.</exception>
    internal static (int Width, int Height) ReadDeclaredRasterSize(SourceInput input, SourceKind kind)
    {
        if (kind == SourceKind.Gif)
        {
            using var gif = input.Open();
            var header = new byte[10];
            if (gif.ReadAtLeast(header, header.Length, throwOnEndOfStream: false) < header.Length)
            {
                throw new InvalidDataException("The GIF data is truncated.");
            }

            return (header[6] | (header[7] << 8), header[8] | (header[9] << 8));
        }

        var info = GetRasterInfo(input, kind);
        return (info.Width, info.Height);
    }

    /// <summary>
    ///     Decodes a raster file with the codec of its kind.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The pixels.</returns>
    private static Surface DecodeRaster(SourceInput input, SourceKind kind)
    {
        using var stream = input.Open();
        return kind switch
        {
            SourceKind.Png => PngCodec.Load(stream),
            SourceKind.Jpeg => JpegCodec.Load(stream),
            SourceKind.Bmp => BmpCodec.Load(stream),
            SourceKind.Tiff => TiffCodec.Load(stream),
            SourceKind.Gif => GifCodec.Load(stream),
            _ => Jpeg2000Codec.Load(stream)
        };
    }
}
