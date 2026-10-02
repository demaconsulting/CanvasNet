using System.Numerics;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.Drawing;
using DemaConsulting.CanvasNet.Geometry;

// cspell:ignore bbox unflipped
namespace DemaConsulting.CanvasNet.Pdf;

public sealed partial class PdfDocument
{
    /// <summary>
    ///     The maximum device-pixel width/height <see cref="RenderTilingPatternCell"/> allows a
    ///     tile surface to be allocated at, before failing closed. Bounds a pathological/adversarial
    ///     combination of a tiny <c>/XStep</c>/<c>/YStep</c> and an extreme pattern-to-device
    ///     scale factor from attempting a many-gigabyte (or integer-overflowing) <see cref="Surface"/>
    ///     allocation - analogous in spirit to <see cref="MaxFormNestingDepth"/>'s own "fail
    ///     closed against pathological/adversarial input" rationale.
    /// </summary>
    private const int MaxTileSurfaceDimension = 2048;

    /// <summary>
    ///     Builds a <see cref="ResolvedPattern"/> of <see cref="ResolvedPattern.PatternKind.Tiling"/>
    ///     from an already-resolved <c>/PatternType 1</c> pattern stream.
    /// </summary>
    /// <param name="patternStream">The already-resolved <c>/PatternType 1</c> pattern stream.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="patternStream"/> is not a stream, when <c>/BBox</c> is
    ///     missing or does not have exactly 4 elements, when <c>/XStep</c>/<c>/YStep</c> is
    ///     missing, non-finite, or zero, or when <c>/PaintType</c> is missing or is neither
    ///     <c>1</c> nor <c>2</c> - propagated from <see cref="ReadOptionalMatrix"/> for a
    ///     malformed <c>/Matrix</c>.
    /// </exception>
    private ResolvedPattern BuildTilingPattern(PdfObject patternStream)
    {
        if (patternStream.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("/PatternType 1 pattern must resolve to a stream.");
        }

        var bbox = RequireNumberArray(patternStream, "BBox");
        if (bbox.Length != 4)
        {
            throw new InvalidDataException("/BBox must have exactly 4 elements.");
        }

        var xStep = RequireNumberEntry(patternStream, "XStep");
        if (!double.IsFinite(xStep) || xStep == 0)
        {
            throw new InvalidDataException("/XStep must be a finite, non-zero number.");
        }

        var yStep = RequireNumberEntry(patternStream, "YStep");
        if (!double.IsFinite(yStep) || yStep == 0)
        {
            throw new InvalidDataException("/YStep must be a finite, non-zero number.");
        }

        var matrix = ReadOptionalMatrix(patternStream);

        var paintType = RequireIntEntry(patternStream, "PaintType");
        if (paintType is not (1 or 2))
        {
            throw new InvalidDataException($"/PaintType must be 1 or 2; got {paintType}.");
        }

        var ownResources = patternStream.Get("Resources");
        var resolvedResources = ownResources is null ? _resources : Resolve(ownResources);
        var contentBytes = GetStreamDecodedBytes(patternStream);

        return new ResolvedPattern
        {
            Kind = ResolvedPattern.PatternKind.Tiling,
            Matrix = matrix,
            BBox = bbox,
            XStep = (float)xStep,
            YStep = (float)yStep,
            PaintType = paintType,
            Resources = resolvedResources,
            ContentBytes = contentBytes,
        };
    }

    /// <summary>
    ///     Renders exactly one tile cell of <paramref name="pattern"/> onto a dedicated offscreen
    ///     <see cref="Surface"/>, sized in device pixels from <paramref name="pattern"/>'s own
    ///     <c>/XStep</c>/<c>/YStep</c> scaled by <paramref name="patternToDevice"/>'s own
    ///     <see cref="MatrixScale"/>, and wraps the rendered surface in a <see cref="TilePaint"/>.
    /// </summary>
    /// <param name="pattern">The resolved tiling pattern. Must have <see cref="ResolvedPattern.Kind"/> of <see cref="ResolvedPattern.PatternKind.Tiling"/>.</param>
    /// <param name="patternToDevice">The pattern-space-to-device-space transform (see <see cref="PatternToDeviceTransform"/>), used as the returned <see cref="TilePaint"/>'s own <see cref="TilePaint.Transform"/>.</param>
    /// <param name="tint">
    ///     For a <c>/PaintType 2</c> (uncolored) pattern, the tint color to recolor every
    ///     non-transparent rendered tile pixel's RGB to (preserving that pixel's own alpha); for
    ///     a <c>/PaintType 1</c> (colored) pattern, <see langword="null"/> (the cell's own
    ///     content-declared colors are used unmodified).
    /// </param>
    /// <returns>
    ///     A <see cref="TilePaint"/> wrapping the rendered tile surface. The caller is
    ///     responsible for disposing <see cref="TilePaint.Surface"/> once painting completes -
    ///     see <see cref="TilePaint"/>'s own remarks.
    /// </returns>
    /// <remarks>
    ///     Renders by swapping <see cref="_surface"/>/<see cref="_resources"/>/<see cref="_gs"/>/
    ///     <see cref="_gsStack"/> (saving the invoker's own values first), incrementing the
    ///     existing <see cref="_formNestingDepth"/> counter (reused, not a new counter - bounded
    ///     by the existing <see cref="MaxFormNestingDepth"/>), and calling
    ///     <see cref="ExecuteOperators"/> against the pattern's own decoded content bytes -
    ///     structurally identical to <see cref="OpDrawFormXObject"/>, restoring every swapped
    ///     field in a <c>finally</c> block.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the current nesting depth already equals <see cref="MaxFormNestingDepth"/>,
    ///     or propagated from nested execution of the pattern's own content stream.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the computed tile surface dimensions exceed <see cref="MaxTileSurfaceDimension"/>
    ///     (feature <c>pdf-pattern-tile-too-large</c>).
    /// </exception>
    private TilePaint RenderTilingPatternCell(ResolvedPattern pattern, Matrix3x2 patternToDevice, Rgba32? tint)
    {
        var scale = MatrixScale(patternToDevice);
        var xStepAbs = Math.Abs(pattern.XStep);
        var yStepAbs = Math.Abs(pattern.YStep);

        var tileWidth = Math.Max((int)Math.Ceiling(xStepAbs * scale), 1);
        var tileHeight = Math.Max((int)Math.Ceiling(yStepAbs * scale), 1);

        if (tileWidth > MaxTileSurfaceDimension || tileHeight > MaxTileSurfaceDimension)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-pattern-tile-too-large",
                $"Tiling pattern tile dimensions ({tileWidth}x{tileHeight} device pixels) exceed the " +
                $"maximum supported dimension of {MaxTileSurfaceDimension}.");
        }

        if (_formNestingDepth >= MaxFormNestingDepth)
        {
            throw new InvalidDataException(
                $"Tiling pattern nesting exceeds the maximum supported depth of {MaxFormNestingDepth}.");
        }

        var tileSurface = new Surface(tileWidth, tileHeight);

        // Maps pattern-space BBox-local coordinates (the same coordinates the pattern's own
        // content stream draws in) onto the tile surface's own pixel space: translate the BBox's
        // lower-left corner to the origin, then scale by the tile surface's pixels-per-pattern-
        // space-unit ratio - independently in X/Y, matching the independent X/Y tile sizing
        // above. Deliberately never flips Y (unlike the page's own baseCtm): TilePaintEvaluator's
        // own sampling math (wrappedY -> ty) performs no flip either, so both sides of the
        // pattern-space <-> tile-pixel-space mapping must agree on the same (unflipped)
        // convention.
        var cellTransform =
            Matrix3x2.CreateTranslation(-(float)pattern.BBox![0], -(float)pattern.BBox[1]) *
            Matrix3x2.CreateScale(tileWidth / xStepAbs, tileHeight / yStepAbs);

        var savedSurface = _surface;
        var savedResources = _resources;
        var savedGs = _gs;
        var savedGsStack = _gsStack;
        var savedPathBuilder = _pathBuilder;
        var savedCurrentPoint = _currentPoint;
        var savedSubpathStart = _subpathStart;
        var savedHasOpenSubpath = _hasOpenSubpath;
        _formNestingDepth++;
        try
        {
            _surface = tileSurface;
            _resources = pattern.Resources;
            _gs = new GraphicsState { CurrentTransform = cellTransform };
            _gsStack = new Stack<GraphicsState>();

            // The outer path-painting operator ('f'/'S'/etc.) that led here may already have
            // called _pathBuilder.Build() without yet calling _pathBuilder.Clear() (that clear
            // only happens after PaintCurrentPath's fill/stroke branches return) - so, unlike
            // OpDrawFormXObject (which is only ever invoked via 'Do', never mid-path-paint), this
            // nested ExecuteOperators call must not reuse the live outer path-builder state: a
            // fresh one is substituted for the duration of the tile's own content stream, and the
            // original is restored afterward, so the tile's own 're'/'m'/'l'/... operators cannot
            // corrupt the host's own in-flight path.
            _pathBuilder = new PathBuilder();
            _currentPoint = default;
            _subpathStart = default;
            _hasOpenSubpath = false;
            ExecuteOperators(pattern.ContentBytes!);
        }
        finally
        {
            _formNestingDepth--;
            _surface = savedSurface;
            _resources = savedResources;
            _gs = savedGs;
            _gsStack = savedGsStack;
            _pathBuilder = savedPathBuilder;
            _currentPoint = savedCurrentPoint;
            _subpathStart = savedSubpathStart;
            _hasOpenSubpath = savedHasOpenSubpath;
        }

        if (pattern.PaintType == 2 && tint is { } tintColor)
        {
            RecolorTileSurface(tileSurface, tintColor);
        }

        return new TilePaint(tileSurface, patternToDevice, pattern.XStep, pattern.YStep);
    }

    /// <summary>
    ///     Recolors every non-transparent pixel of <paramref name="surface"/> to
    ///     <paramref name="tint"/>'s own RGB, preserving each pixel's own alpha - the documented
    ///     <c>/PaintType 2</c> (uncolored tiling pattern) semantic: the cell's own content
    ///     declares only coverage/shape (via its alpha), and the caller-supplied tint supplies
    ///     the actual color.
    /// </summary>
    private static void RecolorTileSurface(Surface surface, Rgba32 tint)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            for (var x = 0; x < surface.Width; x++)
            {
                var pixel = surface[x, y];
                if (pixel.A == 0)
                {
                    continue;
                }

                surface[x, y] = new Rgba32(tint.R, tint.G, tint.B, pixel.A);
            }
        }
    }
}
