using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     SVG sources of <see cref="SourceLoader"/>.
/// </summary>
public static partial class SourceLoader
{
    /// <summary>
    ///     Reads the intrinsic size of an SVG file without rendering it.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>The facts about the file.</returns>
    private static SourceInfo ProbeSvg(SourceInput input, long bytes)
    {
        var info = Read(input.Name, SourceKind.Svg, () =>
        {
            using var stream = input.Open();
            return SvgCodec.GetInfo(stream);
        });
        return new SourceInfo(input.Name, SourceKind.Svg, bytes, info.Width, info.Height, info.Channels, info.HasAlpha, true, 1, 1, null, [], [], []);
    }

    /// <summary>
    ///     Renders an SVG file at its final size and flattens it over the background.
    /// </summary>
    /// <param name="info">The facts about the file.</param>
    /// <param name="options">The loading options.</param>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="context">The hooks of the caller.</param>
    /// <returns>The loaded source.</returns>
    /// <remarks>
    ///     A vector source is rendered directly at the target size, which is sharper than rendering at the natural
    ///     size and resampling. Transparent areas are flattened over white unless another background color is given;
    ///     a background with zero alpha keeps the transparency. An <c>image</c> that refers to another file (a raster,
    ///     an SVG file, a model, a chart, or a page of a document) is loaded through the image resolver of
    ///     <c>CanvasNetViewer</c> (see <see cref="SvgImages"/>), based at the folder of the SVG file and under <see cref="SourceOptions.FileAccess"/>; what it reports
    ///     is part of the result. When the caller supplies the resolver of the nested pictures
    ///     (<see cref="LoadContext.ScopedResolver"/>, as the resolver of an embedding document does) that resolver is used
    ///     instead and it, not the result, collects what the nested pictures report. The position bindings of the file
    ///     (<c>cn:x2y2="#image selector"</c>, see <c>SourceLoader.SvgBindings.cs</c>) are resolved by the library from the
    ///     pictures it draws; each resolved one is a <c>bind</c> operation, each other one a warning, and with
    ///     <see cref="SourceOptions.Strict"/> the error <c>svg-binding-failed</c> after the pictures that were not drawn.
    ///     The authoring hints of the file (see <see cref="HintNotes"/>) are warnings that never fail, even with
    ///     <see cref="SourceOptions.Strict"/>.
    /// </remarks>
    private static LoadedSource LoadSvg(SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
    {
        var (width, height) = ComputeTargetSize(info.Path, info.Width, info.Height, options);
        var operations = new List<AppliedOperation> { Operation("render", ("width", width), ("height", height)) };

        // The canvas is charged before it exists, and before the nested pictures are loaded, so they see what is left
        context.ChargeSurface(width, height);
        if (context.UseScopedResolver)
        {
            return LoadScopedSvg(info, options, input, context.ScopedResolver, width, height, operations);
        }

        var images = new SvgImages(info.Path, options);
        var bindings = new List<SvgPositionBinding>();
        var hints = new List<SvgHint>();
        var loadOptions = new SvgLoadOptions { ImageResolver = images.Resolver, BindingHandler = bindings.Add, HintHandler = hints.Add };
        Surface surface;
        try
        {
            surface = Read(info.Path, SourceKind.Svg, () => LoadSvgSurface(input, width, height, loadOptions));
        }
        catch (ImageSourceException ex)
        {
            throw images.Map(ex);
        }

        try
        {
            // A picture that was not drawn is checked first, so a denied picture is file-access-denied, not a binding
            var notes = images.Notes();
            var bindingNotes = BindingNotes(bindings);
            if (options.Strict)
            {
                images.ThrowForUnresolved();
                ThrowForFailedBindings(bindingNotes, Path.GetFileName(info.Path));
            }

            var background = options.Background ?? new Rgba32(255, 255, 255, 255);
            if (background.A > 0)
            {
                surface = Replace(surface, surface.FlattenOver(background));
                operations.Add(Operation("flatten", ("background", FormatColor(background))));
            }

            operations.AddRange(images.Operations());
            operations.AddRange(BindOperations(bindings));
            return new LoadedSource(surface, info, 1, true, [.. notes, .. bindingNotes, .. HintNotes(hints)], [.. images.Resolver.Echo], operations);
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Renders an SVG file whose nested pictures are loaded by a resolver of the caller.
    /// </summary>
    /// <param name="info">The facts about the file.</param>
    /// <param name="options">The loading options.</param>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="resolver">The resolver of the nested pictures, scoped to this file, or <see langword="null"/> for none.</param>
    /// <param name="width">The width to render at.</param>
    /// <param name="height">The height to render at.</param>
    /// <param name="operations">The operations so far.</param>
    /// <returns>
    ///     The loaded source. Its only notes are the position bindings of the file that were not resolved (warnings) and
    ///     its <c>bind</c> operations; the notes of the nested pictures, and the <c>--strict</c> check of both, belong to
    ///     the resolver that owns <paramref name="resolver"/>. A nested file reports no authoring hints: only the file
    ///     being rendered does.
    /// </returns>
    private static LoadedSource LoadScopedSvg(
        SourceInfo info,
        SourceOptions options,
        SourceInput input,
        IImageResolver? resolver,
        int width,
        int height,
        List<AppliedOperation> operations)
    {
        var bindings = new List<SvgPositionBinding>();
        var surface = Read(
            info.Path,
            SourceKind.Svg,
            () => LoadSvgSurface(input, width, height, new SvgLoadOptions { ImageResolver = resolver, BindingHandler = bindings.Add }));
        try
        {
            var background = options.Background ?? new Rgba32(255, 255, 255, 255);
            if (background.A > 0)
            {
                surface = Replace(surface, surface.FlattenOver(background));
                operations.Add(Operation("flatten", ("background", FormatColor(background))));
            }

            operations.AddRange(BindOperations(bindings));
            return new LoadedSource(surface, info, 1, true, BindingNotes(bindings), [], operations);
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    /// <summary>
    ///     Turns the authoring hints the SVG library reported into warnings; a hand-drawn arrowhead points at the help
    ///     card that explains the fix.
    /// </summary>
    /// <param name="hints">The hints, at most one per code.</param>
    /// <returns>
    ///     One <c>svg</c> warning per hint, with the hint code (for example <c>svg-arrowhead-shape</c>). A hint never
    ///     fails a render, not even with <c>--strict</c>: it points at a likely authoring mistake, not at something
    ///     that could not be drawn.
    /// </returns>
    internal static List<ImageDiagnostic> HintNotes(IEnumerable<SvgHint> hints)
    {
        return [.. hints.Select(h => new ImageDiagnostic("svg", "warning", h.Code, h.Code == SvgHint.ArrowheadShapeCode ? h.Message + " (see 'canvasnet help svg arrows')" : h.Message))];
    }

    /// <summary>
    ///     Renders an SVG source to a surface with the codec.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="loadOptions">The options of the codec, with the resolver of the nested pictures.</param>
    /// <returns>The surface.</returns>
    private static Surface LoadSvgSurface(SourceInput input, int width, int height, SvgLoadOptions loadOptions)
    {
        using var stream = input.Open();
        return SvgCodec.LoadWithFontFaces(stream, width, height, null, loadOptions);
    }
}
