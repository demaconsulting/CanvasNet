// cspell:ignore jpeg tiff jpeg2000
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     Raster sources (PNG, JPEG, BMP, TIFF, GIF, JPEG 2000) of <see cref="SourceLoader"/>.
/// </summary>
internal static partial class SourceLoader
{
    /// <summary>
    ///     Reads the header of a raster file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="kind">The kind of the file (a raster kind).</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file.</returns>
    private static SourceInfo ProbeRaster(string path, SourceKind kind, long bytes)
    {
        var info = Read(path, kind, () => GetRasterInfo(path, kind));
        var orientation = kind == SourceKind.Jpeg ? Read(path, kind, () => ExifOrientation.Read(path)) : null;
        return new SourceInfo(
            path,
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
    /// <returns>The loaded source.</returns>
    private static LoadedSource LoadRaster(SourceInfo info, SourceOptions options)
    {
        // Refuse a file that is too large, and a result that would be too large, before decoding anything
        EnsureStoredSizeSupported(info);
        var orientation = options.AutoOrient && info.ExifOrientation is > 1 ? info.ExifOrientation : null;
        var swapped = orientation >= 5;
        var orientedWidth = swapped ? info.Height : info.Width;
        var orientedHeight = swapped ? info.Width : info.Height;
        var (width, height) = ComputeTargetSize(info.Path, orientedWidth, orientedHeight, options);

        var operations = new List<AppliedOperation>();
        var surface = Read(info.Path, info.Format, () => DecodeRaster(info.Path, info.Format));
        try
        {
            if (orientation is { } exif)
            {
                surface = Replace(surface, ExifOrientation.Apply(surface, exif));
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
    /// <param name="path">The path of the file.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The header information.</returns>
    private static ImageInfo GetRasterInfo(string path, SourceKind kind)
    {
        return kind switch
        {
            SourceKind.Png => PngCodec.GetInfo(path),
            SourceKind.Jpeg => JpegCodec.GetInfo(path),
            SourceKind.Bmp => BmpCodec.GetInfo(path),
            SourceKind.Tiff => TiffCodec.GetInfo(path),
            SourceKind.Gif => GifCodec.GetInfo(path),
            _ => Jpeg2000Codec.GetInfo(path)
        };
    }

    /// <summary>
    ///     Decodes a raster file with the codec of its kind.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="kind">The kind.</param>
    /// <returns>The pixels.</returns>
    private static Surface DecodeRaster(string path, SourceKind kind)
    {
        return kind switch
        {
            SourceKind.Png => PngCodec.Load(path),
            SourceKind.Jpeg => JpegCodec.Load(path),
            SourceKind.Bmp => BmpCodec.Load(path),
            SourceKind.Tiff => TiffCodec.Load(path),
            SourceKind.Gif => GifCodec.Load(path),
            _ => Jpeg2000Codec.Load(path)
        };
    }
}
