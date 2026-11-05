// cspell:ignore svg
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Tool.Imaging;

/// <summary>
///     SVG sources of <see cref="SourceLoader"/>.
/// </summary>
internal static partial class SourceLoader
{
    /// <summary>
    ///     Reads the intrinsic size of an SVG file without rendering it.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file.</returns>
    private static SourceInfo ProbeSvg(string path, long bytes)
    {
        var info = Read(path, SourceKind.Svg, () => SvgCodec.GetInfo(path));
        return new SourceInfo(path, SourceKind.Svg, bytes, info.Width, info.Height, info.Channels, info.HasAlpha, true, 1, 1, null, [], [], []);
    }

    /// <summary>
    ///     Renders an SVG file at its final size and flattens it over the background.
    /// </summary>
    /// <param name="info">The facts about the file.</param>
    /// <param name="options">The loading options.</param>
    /// <returns>The loaded source.</returns>
    /// <remarks>
    ///     A vector source is rendered directly at the target size, which is sharper than rendering at the natural
    ///     size and resampling. Transparent areas are flattened over white unless another background color is given;
    ///     a background with zero alpha keeps the transparency. An <c>image</c> that refers to another file (a raster,
    ///     an SVG file, a model, a chart, or a page of a document) is loaded through the image resolver of
    ///     <c>CanvasNetViewer</c> (see <see cref="SvgImages"/>), confined to the folder of the SVG file; what it reports
    ///     is part of the result.
    /// </remarks>
    private static LoadedSource LoadSvg(SourceInfo info, SourceOptions options)
    {
        var (width, height) = ComputeTargetSize(info.Path, info.Width, info.Height, options);
        var operations = new List<AppliedOperation> { Operation("render", ("width", width), ("height", height)) };
        var images = new SvgImages(info.Path, options);
        var loadOptions = new SvgLoadOptions { ImageResolver = images.Resolver };
        Surface surface;
        try
        {
            surface = Read(info.Path, SourceKind.Svg, () => SvgCodec.LoadWithFontFaces(info.Path, width, height, null, loadOptions));
        }
        catch (ImageSourceException ex)
        {
            throw images.Map(ex);
        }

        try
        {
            if (options.Strict)
            {
                images.ThrowForUnresolved();
            }

            var background = options.Background ?? new Rgba32(255, 255, 255, 255);
            if (background.A > 0)
            {
                surface = Replace(surface, surface.FlattenOver(background));
                operations.Add(Operation("flatten", ("background", FormatColor(background))));
            }

            operations.AddRange(images.Operations());
            return new LoadedSource(surface, info, 1, true, images.Notes(), [.. images.Resolver.Echo], operations);
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }
}
