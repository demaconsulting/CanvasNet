// cspell:ignore feimage
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

public static partial class SvgCodec
{
    /// <summary>
    ///     Evaluates one <c>feImage</c> primitive against the current filter buffer.
    /// </summary>
    /// <param name="element">The <c>feImage</c> element.</param>
    /// <param name="width">The filter buffer width.</param>
    /// <param name="height">The filter buffer height.</param>
    /// <param name="primitiveSubregion">The primitive's own resolved subregion.</param>
    /// <param name="transform">The referencing element's accumulated transform.</param>
    /// <param name="regionPixelX">The enclosing filter region's absolute pixel-space X origin.</param>
    /// <param name="regionPixelY">The enclosing filter region's absolute pixel-space Y origin.</param>
    /// <param name="state">The referencing element's own cascaded render state.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="useDepth">The current <c>use</c>-reference nesting depth.</param>
    /// <param name="elementDepth">The current element-recursion depth.</param>
    /// <param name="markerDepth">The current marker-recursion depth.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <param name="workBudget">The shared geometry work budget.</param>
    /// <param name="filterWorkBudget">The shared cumulative filter work budget.</param>
    /// <param name="boundsPrePassBudget">The shared cumulative bounds-pre-pass work budget.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyFeImage(
        XElement element,
        int width,
        int height,
        PixelRect primitiveSubregion,
        Matrix3x2 transform,
        int regionPixelX,
        int regionPixelY,
        RenderState state,
        RenderContext context,
        int useDepth,
        int elementDepth,
        int markerDepth,
        ref int totalElements,
        GeometryWorkBudget workBudget,
        FilterWorkBudget filterWorkBudget,
        BoundsPrePassWorkBudget boundsPrePassBudget)
    {
        var output = new Surface(width, height);
        var href = GetHrefAttribute(element);
        if (href == null)
        {
            return output;
        }

        if (TryParseDataUri(href, out var mimeType, out var base64Payload))
        {
            if (!filterWorkBudget.TryCharge(base64Payload.Length) || primitiveSubregion.IsEmpty)
            {
                return output;
            }

            var decodeRaster = ResolveRasterDecoder(mimeType);
            if (decodeRaster == null)
            {
                return output;
            }

            Surface decodedSurface;
            try
            {
                var bytes = Convert.FromBase64String(base64Payload);
                using var payloadStream = new MemoryStream(bytes);
                decodedSurface = decodeRaster(payloadStream);
            }
            catch (Exception ex) when (ex is FormatException or InvalidDataException or ArgumentOutOfRangeException or IOException)
            {
                return output;
            }

            var fitTransform = ComputePreserveAspectRatioFit(
                Vector2.Zero,
                new Vector2(decodedSurface.Width, decodedSurface.Height),
                primitiveSubregion.Width,
                primitiveSubregion.Height,
                GetPreserveAspectRatio(element));
            var imageToPixel = fitTransform * Matrix3x2.CreateTranslation(primitiveSubregion.X, primitiveSubregion.Y);
            if (!IsFiniteTransform(imageToPixel) || !Matrix3x2.Invert(imageToPixel, out var pixelToImage))
            {
                return output;
            }

            var sampled = SampleImageIntoRegion(
                decodedSurface,
                primitiveSubregion.X,
                primitiveSubregion.Y,
                primitiveSubregion.Width,
                primitiveSubregion.Height,
                pixelToImage);
            CopySurfaceInto(sampled, output, primitiveSubregion.X, primitiveSubregion.Y);
            return output;
        }

        var hrefId = ExtractFragmentId(href);
        if (hrefId == null || !context.IdIndex.TryGetValue(hrefId, out var target) || useDepth >= MaxUseDepth)
        {
            return output;
        }

        var regionArea = (long)width * height;
        if (!filterWorkBudget.TryCharge(regionArea))
        {
            return output;
        }

        var localToTemp = transform * Matrix3x2.CreateTranslation(-regionPixelX, -regionPixelY);
        var targetState = RenderState.Initial with { ViewportWidth = state.ViewportWidth, ViewportHeight = state.ViewportHeight };
        var targetContext = context with { Surface = output };
        RenderElement(
            target,
            targetState,
            localToTemp,
            targetContext,
            useDepth + 1,
            elementDepth + 1,
            markerDepth,
            ref totalElements,
            workBudget,
            filterWorkBudget,
            boundsPrePassBudget);
        return output;
    }
}
