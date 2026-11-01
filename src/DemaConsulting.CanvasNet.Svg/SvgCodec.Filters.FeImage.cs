// cspell:ignore feimage
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    /// <summary>
    ///     Evaluates one <c>feImage</c> primitive against the current filter buffer.
    /// </summary>
    /// <param name="element">The <c>feImage</c> element.</param>
    /// <param name="chain">The mutable evaluation state of the enclosing filter chain (buffer size, region origin, transform, state, context and scope).</param>
    /// <param name="primitiveSubregion">The primitive's own resolved subregion.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    /// <returns>A new, independent output buffer.</returns>
    private static Surface ApplyFeImage(
        XElement element,
        FilterChain chain,
        PixelRect primitiveSubregion,
        ref int totalElements)
    {
        var output = new Surface(chain.SourceGraphic.Width, chain.SourceGraphic.Height);
        var href = GetHrefAttribute(element);
        if (href == null)
        {
            return output;
        }

        if (TryParseDataUri(href, out var mimeType, out var base64Payload))
        {
            RenderFeImageDataUri(element, mimeType, base64Payload, chain.Scope.FilterWorkBudget, primitiveSubregion, output);
            return output;
        }

        RenderFeImageReference(href, chain, output, ref totalElements);
        return output;
    }

    /// <summary>
    ///     Decodes a <c>feImage</c> data-URI payload and samples it into
    ///     <paramref name="output"/> at <paramref name="primitiveSubregion"/>; tolerantly leaves
    ///     <paramref name="output"/> untouched on any budget, decode or transform failure.
    /// </summary>
    /// <param name="element">The <c>feImage</c> element.</param>
    /// <param name="mimeType">The parsed MIME type token.</param>
    /// <param name="base64Payload">The raw base64 payload.</param>
    /// <param name="filterWorkBudget">The shared cumulative filter work budget.</param>
    /// <param name="primitiveSubregion">The primitive's own resolved subregion.</param>
    /// <param name="output">The output buffer to paint into.</param>
    private static void RenderFeImageDataUri(
        XElement element,
        string mimeType,
        string base64Payload,
        FilterWorkBudget filterWorkBudget,
        PixelRect primitiveSubregion,
        Surface output)
    {
        if (!filterWorkBudget.TryCharge(base64Payload.Length) || primitiveSubregion.IsEmpty)
        {
            return;
        }

        var decodeRaster = ResolveRasterDecoder(mimeType);
        if (decodeRaster == null)
        {
            return;
        }

        var decodedSurface = TryDecodeImageSurface(decodeRaster, base64Payload);
        if (decodedSurface == null)
        {
            return;
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
            return;
        }

        var sampled = SampleImageIntoRegion(decodedSurface, primitiveSubregion, imageToPixel, pixelToImage, nearest: false);
        output.CopyFrom(sampled, primitiveSubregion.X, primitiveSubregion.Y);
    }

    /// <summary>
    ///     Renders a <c>feImage</c> element-reference <c>href</c> into <paramref name="output"/>
    ///     through <see cref="RenderElement"/>; tolerantly leaves <paramref name="output"/>
    ///     untouched when the reference dangles or a depth/budget limit is hit.
    /// </summary>
    /// <param name="href">The element's <c>href</c> value.</param>
    /// <param name="chain">The mutable evaluation state of the enclosing filter chain.</param>
    /// <param name="output">The output buffer to render into.</param>
    /// <param name="totalElements">The running total-rendered-elements count.</param>
    private static void RenderFeImageReference(
        string href,
        FilterChain chain,
        Surface output,
        ref int totalElements)
    {
        var scope = chain.Scope;
        var hrefId = ExtractFragmentId(href);
        if (hrefId == null || !chain.Context.IdIndex.TryGetValue(hrefId, out var target) || scope.UseDepth >= MaxUseDepth)
        {
            return;
        }

        var regionArea = (long)output.Width * output.Height;
        if (!scope.FilterWorkBudget.TryCharge(regionArea))
        {
            return;
        }

        var localToTemp = chain.Transform * Matrix3x2.CreateTranslation(-chain.RegionPixelX, -chain.RegionPixelY);
        var targetState = RenderState.Initial with { ViewportWidth = chain.State.ViewportWidth, ViewportHeight = chain.State.ViewportHeight };
        var targetContext = chain.Context with { Surface = output };
        RenderElement(target, targetState, localToTemp, targetContext, scope.NestedUse(), ref totalElements);
    }
}
