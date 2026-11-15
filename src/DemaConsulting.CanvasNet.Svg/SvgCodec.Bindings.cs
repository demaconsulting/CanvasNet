using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Positions;

namespace DemaConsulting.CanvasNet.Svg;

public static partial class SvgCodec
{
    // ================================================================================================
    // Position bindings (cn:x1y1, cn:x2y2, cn:center, cn:xy)
    // ================================================================================================
    //
    // A position binding is an attribute in the namespace SvgPositionBinding.Namespace whose value
    // "#imageId selector" names an <image> of the same document and a position selector (the grammar
    // of --positions). Before anything is painted, a pre-pass:
    //
    //   1. walks the elements the painter reaches directly (the same cascade and transforms as
    //      RenderElement, descending only into g/symbol), so every element's local-to-device
    //      transform is known up front and a forward reference resolves;
    //   2. checks every binding (the element and attribute pair, the value, the image, the exclusions);
    //   3. loads each bound image ONCE through IImagePositionResolver.ResolveWithPositions, at the
    //      device size the painter would request, and keeps the surface in
    //      RenderContext.PreloadedImages, so the painter draws the very surface the positions come from;
    //   4. maps the single matching position's pixel through the painter's placement transform
    //      (preserveAspectRatio fit of the actual surface size, translate(x, y), the image's
    //      transform) and the inverse of the receiving element's transform, and writes both
    //      coordinates of the pair into the element before it is painted.
    //
    // A failed binding keeps the numbers written in the document and is reported through
    // SvgLoadOptions.BindingHandler; it never fails the render. Not supported (reported as
    // svg-binding-unsupported): an element or image inside defs, a pattern, marker, mask, clip path,
    // nested svg or other non-painted content, one referenced (itself or an ancestor) by a use or
    // feImage, a text with a list of coordinates, paths and polylines (follow-ups).

    /// <summary>
    ///     The elements and binding attributes that receive a coordinate pair, by element local name and attribute
    ///     local name, with the names of the attributes that receive the x and y coordinates.
    /// </summary>
    private static readonly Dictionary<(string Element, string Attribute), (string X, string Y)> BindingTargets = new()
    {
        [("line", "x1y1")] = ("x1", "y1"),
        [("line", "x2y2")] = ("x2", "y2"),
        [("circle", "center")] = ("cx", "cy"),
        [("ellipse", "center")] = ("cx", "cy"),
        [("text", "xy")] = ("x", "y"),
    };

    /// <summary>
    ///     The cascaded state and accumulated local-to-device transform of an element the painter reaches directly.
    /// </summary>
    /// <param name="State">The element's own cascaded render state.</param>
    /// <param name="Transform">The element's own composed transform (its <c>transform</c> attribute included).</param>
    private readonly record struct BindingLayout(RenderState State, Matrix3x2 Transform);

    /// <summary>
    ///     One binding attribute while it is resolved.
    /// </summary>
    private sealed class BindingWork
    {
        /// <summary>Gets the element that carries the binding.</summary>
        public required XElement Element { get; init; }

        /// <summary>Gets the binding attribute.</summary>
        public required XAttribute Attribute { get; init; }

        /// <summary>Gets the 0-based document-order index of <see cref="Element"/>.</summary>
        public required int Index { get; init; }

        /// <summary>Gets or sets the attribute names that receive the pair.</summary>
        public (string X, string Y)? Target { get; set; }

        /// <summary>Gets or sets the image id of the value.</summary>
        public string? ImageId { get; set; }

        /// <summary>Gets or sets the parsed selector of the value.</summary>
        public PositionSelector? Selector { get; set; }

        /// <summary>Gets or sets the bound <c>image</c> element, once it passed every check.</summary>
        public XElement? Image { get; set; }

        /// <summary>Gets or sets the resolved coordinates.</summary>
        public (double X, double Y)? Point { get; set; }

        /// <summary>Gets or sets the failure code, or <see langword="null"/> while pending or when resolved.</summary>
        public string? Code { get; set; }

        /// <summary>Gets or sets the reason of the failure.</summary>
        public string? Problem { get; set; }

        /// <summary>
        ///     Records a failure.
        /// </summary>
        /// <param name="code">The failure code.</param>
        /// <param name="problem">The reason, for the message.</param>
        public void Fail(string code, string problem)
        {
            Code = code;
            Problem = problem;
        }
    }

    /// <summary>
    ///     Tells whether a document has any attribute in the CanvasNet namespace, so a document without bindings pays
    ///     nothing for the pre-pass.
    /// </summary>
    /// <param name="root">The document's root element.</param>
    /// <returns><see langword="true"/> when at least one attribute is in <see cref="SvgPositionBinding.Namespace"/>.</returns>
    private static bool HasPositionBindings(XElement root) =>
        root.DescendantsAndSelf().Any(e => e.Attributes().Any(a => a.Name.NamespaceName == SvgPositionBinding.Namespace));

    /// <summary>
    ///     Resolves every position binding of the document before it is painted: loads each bound image once, writes the
    ///     resolved coordinates into the receiving elements, and reports every binding to
    ///     <see cref="RenderContext.BindingHandler"/>.
    /// </summary>
    /// <param name="root">The document's root element.</param>
    /// <param name="rootState">The root element's cascaded state, as <c>RenderDocument</c> computed it.</param>
    /// <param name="fitTransform">The root viewBox-fit transform.</param>
    /// <param name="context">The fixed per-document render context; receives the preloaded images.</param>
    /// <param name="scope">The root scope, whose shared work budget is charged for each load.</param>
    /// <exception cref="InvalidDataException">Thrown for malformed presentation or transform data, as painting would.</exception>
    private static void ResolvePositionBindings(XElement root, RenderState rootState, Matrix3x2 fitTransform, RenderContext context, RenderScope scope)
    {
        // The geometry of every element the painter reaches directly, and the elements a use or feImage draws again
        var layout = BuildBindingLayout(root, rootState, fitTransform, context);
        var referenced = CollectReferencedElements(root, context);

        // Every binding attribute, in document order, checked on its own
        var works = new List<BindingWork>();
        var index = 0;
        foreach (var element in root.DescendantsAndSelf())
        {
            foreach (var attribute in element.Attributes().Where(a => a.Name.NamespaceName == SvgPositionBinding.Namespace))
            {
                var work = new BindingWork { Element = element, Attribute = attribute, Index = index };
                CheckBinding(work, layout, referenced, context);
                works.Add(work);
            }

            index++;
        }

        // Each bound image is loaded once, with the union of its selectors, then its bindings are matched
        foreach (var group in works.Where(w => w.Code is null).GroupBy(w => w.Image!))
        {
            ResolveImageBindings(group.Key, [.. group], layout[group.Key], layout, context, scope);
        }

        // The outcomes are reported in document order, before anything is painted
        if (context.BindingHandler is { } handler)
        {
            foreach (var work in works)
            {
                handler(ToReport(work));
            }
        }
    }

    /// <summary>
    ///     Walks the elements the painter reaches directly and records their cascaded state and transform, mirroring
    ///     <see cref="RenderElement"/>: the same cascade and transform composition, the same skipped and non-rendering
    ///     elements, descent only into <c>g</c> and <c>symbol</c>, and no subtree with a non-finite transform.
    /// </summary>
    /// <param name="root">The document's root element.</param>
    /// <param name="rootState">The root element's cascaded state.</param>
    /// <param name="fitTransform">The root viewBox-fit transform.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <returns>The layout of every directly painted element.</returns>
    private static Dictionary<XElement, BindingLayout> BuildBindingLayout(XElement root, RenderState rootState, Matrix3x2 fitTransform, RenderContext context)
    {
        var layout = new Dictionary<XElement, BindingLayout>();
        var pending = new Stack<(XElement Element, RenderState State, Matrix3x2 Transform, int Depth)>();
        foreach (var child in root.Elements().Reverse())
        {
            pending.Push((child, rootState, fitTransform, 0));
        }

        while (pending.Count > 0)
        {
            var (element, parentState, parentTransform, depth) = pending.Pop();
            var name = element.Name.LocalName;
            if (depth >= MaxElementDepth || NonRenderingElements.Contains(name) || SkippedElements.Contains(name))
            {
                continue;
            }

            var state = ApplyPresentationAttributes(parentState, element, context);
            var transform = ParseTransformAttribute(element) * parentTransform;
            if (!IsFiniteTransform(transform))
            {
                continue;
            }

            layout[element] = new BindingLayout(state, transform);
            if (name is "g" or "symbol")
            {
                foreach (var child in element.Elements().Reverse())
                {
                    pending.Push((child, state, transform, depth + 1));
                }
            }
        }

        return layout;
    }

    /// <summary>
    ///     Collects the elements that a <c>use</c> or an <c>feImage</c> draws again (their <c>#id</c> targets), so a
    ///     binding inside such content is reported as unsupported rather than resolved for one of its instances only.
    /// </summary>
    /// <param name="root">The document's root element.</param>
    /// <param name="context">The fixed per-document render context (its id index).</param>
    /// <returns>The referenced elements.</returns>
    private static HashSet<XElement> CollectReferencedElements(XElement root, RenderContext context)
    {
        var referenced = new HashSet<XElement>();
        foreach (var element in root.Descendants().Where(e => e.Name.LocalName is "use" or "feImage"))
        {
            var href = GetHrefAttribute(element)?.Trim();
            if (href is { Length: > 1 } && href[0] == '#' && context.IdIndex.TryGetValue(href[1..], out var target))
            {
                referenced.Add(target);
            }
        }

        return referenced;
    }

    /// <summary>
    ///     Checks one binding on its own: the element and attribute pair, the value, the image, and the exclusions. On
    ///     success the work knows its target attributes, selector and image; otherwise it carries its failure.
    /// </summary>
    /// <param name="work">The binding.</param>
    /// <param name="layout">The layout of the directly painted elements.</param>
    /// <param name="referenced">The elements a <c>use</c> or <c>feImage</c> draws again.</param>
    /// <param name="context">The fixed per-document render context.</param>
    private static void CheckBinding(BindingWork work, Dictionary<XElement, BindingLayout> layout, HashSet<XElement> referenced, RenderContext context)
    {
        // The pair the attribute names must exist on this element
        var elementName = work.Element.Name.LocalName;
        if (!BindingTargets.TryGetValue((elementName, work.Attribute.Name.LocalName), out var target))
        {
            work.Fail(SvgPositionBinding.UnsupportedCode, $"the attribute is not a binding of a <{elementName}> (use x1y1 or x2y2 on line, center on circle or ellipse, xy on text)");
            return;
        }

        work.Target = target;

        // The value: '#', the image id up to the first white space, then the selector
        if (!TryParseBindingValue(work.Attribute.Value, out var imageId, out var selector, out var problem))
        {
            work.Fail(SvgPositionBinding.MalformedCode, problem!);
            return;
        }

        work.ImageId = imageId;
        work.Selector = selector;

        // The id is document-local and must name an image
        if (!context.IdIndex.TryGetValue(imageId!, out var image) || image.Name.LocalName != "image")
        {
            work.Fail(
                SvgPositionBinding.ImageMissingCode,
                image is null ? $"no element has the id '{imageId}'" : $"the id '{imageId}' names a <{image.Name.LocalName}>, not an <image>");
            return;
        }

        // Both ends must be painted once, directly: not in defs, a pattern, marker, mask or clip path, and not drawn
        // again by a use or feImage
        if (!layout.ContainsKey(work.Element) || IsReferenced(work.Element, referenced))
        {
            work.Fail(SvgPositionBinding.UnsupportedCode, "the element is not painted directly (it is inside defs, a pattern, marker, mask, clip path or nested svg, or referenced by use)");
            return;
        }

        if (!layout.ContainsKey(image) || IsReferenced(image, referenced))
        {
            work.Fail(SvgPositionBinding.UnsupportedCode, $"the image '{imageId}' is not painted directly (it is inside defs, a pattern, marker, mask, clip path or nested svg, or referenced by use)");
            return;
        }

        // A text with a list of coordinates places each character; only the first scalar x/y is bound
        if (elementName == "text" && (IsCoordinateList(work.Element, "x") || IsCoordinateList(work.Element, "y")))
        {
            work.Fail(SvgPositionBinding.UnsupportedCode, "the text has a list of x or y coordinates; only a single x and y can be bound");
            return;
        }

        // Only an external picture, loaded by a host resolver that reports positions, has positions
        var href = GetHrefAttribute(image)?.Trim();
        if (string.IsNullOrEmpty(href) || href[0] == '#' || href.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            work.Fail(SvgPositionBinding.UnsupportedCode, $"the image '{imageId}' does not link an external file");
            return;
        }

        if (context.ImageResolver is not IImagePositionResolver)
        {
            work.Fail(SvgPositionBinding.UnsupportedCode, "the image resolver of the render cannot report positions");
            return;
        }

        work.Image = image;
    }

    /// <summary>
    ///     Parses the value of a binding attribute: <c>#imageId selector</c>, split at the first white space after the id.
    /// </summary>
    /// <param name="value">The attribute value.</param>
    /// <param name="imageId">The image id, without the <c>#</c>.</param>
    /// <param name="selector">The parsed selector.</param>
    /// <param name="problem">Why the value is not valid.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    private static bool TryParseBindingValue(string value, out string? imageId, out PositionSelector? selector, out string? problem)
    {
        imageId = null;
        selector = null;
        var text = value.Trim();
        if (text.Length < 2 || text[0] != '#' || char.IsWhiteSpace(text[1]))
        {
            problem = "the value must be '#imageId selector'";
            return false;
        }

        var split = text.AsSpan().IndexOfAny(" \t\r\n");
        if (split < 0)
        {
            problem = "the value has no selector after the image id";
            return false;
        }

        imageId = text[1..split];
        return PositionSelector.TryParse(text[(split + 1)..].Trim(), out selector, out problem);
    }

    /// <summary>
    ///     Tells whether an element or one of its ancestors is drawn again by a <c>use</c> or <c>feImage</c>.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="referenced">The referenced elements.</param>
    /// <returns><see langword="true"/> when the element is inside referenced content.</returns>
    private static bool IsReferenced(XElement element, HashSet<XElement> referenced) =>
        element.AncestorsAndSelf().Any(referenced.Contains);

    /// <summary>
    ///     Tells whether a coordinate attribute holds more than one value.
    /// </summary>
    /// <param name="element">The element.</param>
    /// <param name="name">The attribute name.</param>
    /// <returns><see langword="true"/> when the trimmed value contains white space or a comma.</returns>
    private static bool IsCoordinateList(XElement element, string name) =>
        ((string?)element.Attribute(name))?.Trim().AsSpan().IndexOfAny(" \t\r\n,") >= 0;

    /// <summary>
    ///     Loads one bound image once, with the union of the selectors of its bindings, keeps the surface for the
    ///     painter, and resolves each binding against the positions of that surface.
    /// </summary>
    /// <param name="image">The <c>image</c> element.</param>
    /// <param name="works">Its pending bindings, in document order.</param>
    /// <param name="imageLayout">The image's cascaded state and transform.</param>
    /// <param name="layout">The layout of every directly painted element.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The root scope, whose shared work budget is charged for the load.</param>
    private static void ResolveImageBindings(
        XElement image,
        List<BindingWork> works,
        BindingLayout imageLayout,
        Dictionary<XElement, BindingLayout> layout,
        RenderContext context,
        RenderScope scope)
    {
        // At most PositionSelection.MaxSelectors distinct selectors are asked for; the bindings beyond are unsupported
        var texts = works.Select(w => w.Selector!.Text).Distinct(StringComparer.Ordinal).ToList();
        foreach (var work in works.Where(w => texts.IndexOf(w.Selector!.Text) >= PositionSelection.MaxSelectors))
        {
            work.Fail(SvgPositionBinding.UnsupportedCode, $"the image has more than {PositionSelection.MaxSelectors} distinct selectors");
        }

        works = [.. works.Where(w => w.Code is null)];
        var selection = PositionSelection.Parse(texts.Take(PositionSelection.MaxSelectors));

        // The placement, exactly as RenderImageWithEffects reads it
        var state = imageLayout.State;
        var x = GetFloatAttribute(image, "x", state, PercentageAxis.Horizontal);
        var y = GetFloatAttribute(image, "y", state, PercentageAxis.Vertical);
        var width = GetFloatAttribute(image, "width", state, PercentageAxis.Horizontal);
        var height = GetFloatAttribute(image, "height", state, PercentageAxis.Vertical);
        var resolution = LoadBoundImage(image, width, height, imageLayout.Transform, selection, context, scope, out var problem);
        if (resolution is null)
        {
            works.ForEach(w => w.Fail(SvgPositionBinding.UnavailableCode, problem!));
            return;
        }

        if (resolution.Positions is not { } positions)
        {
            works.ForEach(w => w.Fail(SvgPositionBinding.UnsupportedCode, $"the source of the image '{w.ImageId}' reports no positions"));
            return;
        }

        // The painter's placement: the fit of the actual surface into the placement, its origin, the image transform
        var surface = resolution.Surface;
        var fit = ComputePreserveAspectRatioFit(
            Vector2.Zero, new Vector2(surface.Width, surface.Height), width, height, GetPreserveAspectRatio(image));
        var imageToDevice = Affine.From(fit) * Affine.From(Matrix3x2.CreateTranslation(x, y)) * Affine.From(imageLayout.Transform);
        foreach (var work in works)
        {
            MatchBinding(work, positions, resolution.PositionsTruncated, imageToDevice, layout[work.Element].Transform);
        }
    }

    /// <summary>
    ///     Loads a bound image through the host resolver, once, at the device size the painter would request, and keeps
    ///     the surface (or the fact that there is none) in <see cref="RenderContext.PreloadedImages"/>.
    /// </summary>
    /// <param name="image">The <c>image</c> element.</param>
    /// <param name="width">The placement width in local units.</param>
    /// <param name="height">The placement height in local units.</param>
    /// <param name="transform">The image's accumulated local-to-device transform.</param>
    /// <param name="selection">The union of the selectors of its bindings.</param>
    /// <param name="context">The fixed per-document render context.</param>
    /// <param name="scope">The root scope, whose shared work budget is charged as for painting.</param>
    /// <param name="problem">Why nothing was loaded, when the result is <see langword="null"/>.</param>
    /// <returns>The picture and its positions, or <see langword="null"/> when the picture is not drawn.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resolver throws a <see cref="FormatException"/>.</exception>
    private static ImageResolution? LoadBoundImage(
        XElement image,
        float width,
        float height,
        Matrix3x2 transform,
        PositionSelection selection,
        RenderContext context,
        RenderScope scope,
        out string? problem)
    {
        // Nothing is drawn, and nothing is asked again by the painter, for a placement of no size
        context.PreloadedImages[image] = null;
        if (!float.IsFinite(width) || !float.IsFinite(height) || width <= 0f || height <= 0f)
        {
            problem = "the image has no width or height, so it is not drawn";
            return null;
        }

        // The same request size and the same charge as a painted image (RenderImageWithEffects charges only the
        // effects pipeline for a preloaded one)
        var (pixelWidth, pixelHeight) = ComputeImageDevicePixels(width, height, transform);
        if (!scope.FilterWorkBudget.TryCharge(((long)pixelWidth * pixelHeight) + ResolverCallOverheadUnits))
        {
            problem = "the work budget of the render is used up, so the image is not drawn";
            return null;
        }

        ImageResolution? resolution;
        try
        {
            resolution = ((IImagePositionResolver)context.ImageResolver!).ResolveWithPositions(
                GetHrefAttribute(image)!.Trim(), pixelWidth, pixelHeight, selection);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("The SVG image resolver threw a FormatException.", ex);
        }

        var surface = ChargeResolvedExcess(resolution?.Surface, pixelWidth, pixelHeight, scope);
        if (surface is null)
        {
            problem = "the image was not drawn (see the warnings about the image)";
            return null;
        }

        context.PreloadedImages[image] = surface;
        problem = null;
        return resolution;
    }

    /// <summary>
    ///     Matches one binding against the positions of its picture and maps the single match into the user space of the
    ///     receiving element.
    /// </summary>
    /// <param name="work">The binding.</param>
    /// <param name="positions">The positions of the picture, in its pixels.</param>
    /// <param name="truncated">Whether matching positions were cut by the cap.</param>
    /// <param name="imageToDevice">The painter's transform from the picture's pixels to device pixels.</param>
    /// <param name="elementTransform">The receiving element's local-to-device transform.</param>
    private static void MatchBinding(
        BindingWork work,
        IReadOnlyList<ImagePosition> positions,
        bool truncated,
        Affine imageToDevice,
        Matrix3x2 elementTransform)
    {
        // Exactly one position must match
        var matches = positions.Where(work.Selector!.Matches).Take(2).ToList();
        if (matches.Count > 1)
        {
            work.Fail(SvgPositionBinding.AmbiguousCode, $"the selector '{work.Selector.Text}' matched more than one position; exactly one is needed");
            return;
        }

        if (truncated)
        {
            work.Fail(SvgPositionBinding.AmbiguousCode, $"more than {PositionCollector.MaxPositions} positions matched the selectors of the image, so the match is not known to be unique");
            return;
        }

        if (matches.Count == 0)
        {
            work.Fail(SvgPositionBinding.UnmatchedCode, $"the selector '{work.Selector.Text}' matched no position of the image '{work.ImageId}'");
            return;
        }

        if (matches[0].Pixel is not { } pixel)
        {
            work.Fail(SvgPositionBinding.UnavailableCode, $"the position matched by '{work.Selector.Text}' has no pixel");
            return;
        }

        // pixel x imageToDevice x inverse(elementToDevice), in double; both transforms must be invertible
        if (!imageToDevice.TryInvert(out _) || !Affine.From(elementTransform).TryInvert(out var deviceToElement))
        {
            work.Fail(SvgPositionBinding.SingularCode, "the transform of the element or of the image cannot be inverted");
            return;
        }

        var device = imageToDevice.Apply(pixel.X, pixel.Y);
        var local = deviceToElement.Apply(device.X, device.Y);
        if (!double.IsFinite(local.X) || !double.IsFinite(local.Y))
        {
            work.Fail(SvgPositionBinding.SingularCode, "the transform of the element or of the image cannot be inverted");
            return;
        }

        // Both coordinates are written together, rounded, before the element is painted
        var point = (X: RoundCoordinate(local.X), Y: RoundCoordinate(local.Y));
        work.Element.SetAttributeValue(work.Target!.Value.X, FormatCoordinate(point.X));
        work.Element.SetAttributeValue(work.Target.Value.Y, FormatCoordinate(point.Y));
        work.Point = point;
    }

    /// <summary>
    ///     Rounds a resolved coordinate to 3 decimals, without a negative zero.
    /// </summary>
    /// <param name="value">The coordinate.</param>
    /// <returns>The rounded coordinate.</returns>
    private static double RoundCoordinate(double value)
    {
        var rounded = Math.Round(value, 3, MidpointRounding.AwayFromZero);
        return rounded == 0 ? 0 : rounded;
    }

    /// <summary>
    ///     Formats a rounded coordinate as the attribute text (invariant culture, at most 3 decimals).
    /// </summary>
    /// <param name="value">The rounded coordinate.</param>
    /// <returns>The text.</returns>
    private static string FormatCoordinate(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    ///     Builds the report of a binding.
    /// </summary>
    /// <param name="work">The binding.</param>
    /// <returns>The report.</returns>
    private static SvgPositionBinding ToReport(BindingWork work)
    {
        var element = work.Element;
        var id = (string?)element.Attribute("id");
        var prefix = element.GetPrefixOfNamespace(SvgPositionBinding.Namespace) ?? "cn";
        var subject = $"<{element.Name.LocalName}{(id is null ? string.Empty : $" id=\"{id}\"")}> {prefix}:{work.Attribute.Name.LocalName}=\"{work.Attribute.Value}\"";
        var message = work.Point is { } point
            ? $"{subject} is at {FormatCoordinate(point.X)},{FormatCoordinate(point.Y)}"
            : $"{subject} was not resolved, so the numbers in the file are kept: {work.Problem}";
        return new SvgPositionBinding
        {
            Element = element.Name.LocalName,
            ElementId = id,
            ElementIndex = work.Index,
            Attribute = work.Attribute.Name.LocalName,
            Value = work.Attribute.Value,
            ImageId = work.ImageId,
            Selector = work.Selector?.Text,
            XAttribute = work.Target?.X,
            YAttribute = work.Target?.Y,
            X = work.Point?.X,
            Y = work.Point?.Y,
            Code = work.Point is null ? work.Code ?? SvgPositionBinding.UnavailableCode : null,
            Message = message
        };
    }

    /// <summary>
    ///     A 2D affine transform in double precision, in the row-vector convention of <see cref="Matrix3x2"/>
    ///     (<c>point x A x B</c> applies A, then B), used so that composing and inverting the binding transforms adds no
    ///     single-precision error of its own.
    /// </summary>
    /// <param name="M11">The first element of the first row.</param>
    /// <param name="M12">The second element of the first row.</param>
    /// <param name="M21">The first element of the second row.</param>
    /// <param name="M22">The second element of the second row.</param>
    /// <param name="M31">The horizontal translation.</param>
    /// <param name="M32">The vertical translation.</param>
    private readonly record struct Affine(double M11, double M12, double M21, double M22, double M31, double M32)
    {
        /// <summary>
        ///     Converts a single-precision transform.
        /// </summary>
        /// <param name="m">The transform.</param>
        /// <returns>The same transform in double precision.</returns>
        public static Affine From(Matrix3x2 m) => new(m.M11, m.M12, m.M21, m.M22, m.M31, m.M32);

        /// <summary>
        ///     Composes two transforms: the result applies <paramref name="a"/>, then <paramref name="b"/>.
        /// </summary>
        /// <param name="a">The transform applied first.</param>
        /// <param name="b">The transform applied second.</param>
        /// <returns>The composition.</returns>
        public static Affine operator *(Affine a, Affine b) => new(
            (a.M11 * b.M11) + (a.M12 * b.M21),
            (a.M11 * b.M12) + (a.M12 * b.M22),
            (a.M21 * b.M11) + (a.M22 * b.M21),
            (a.M21 * b.M12) + (a.M22 * b.M22),
            (a.M31 * b.M11) + (a.M32 * b.M21) + b.M31,
            (a.M31 * b.M12) + (a.M32 * b.M22) + b.M32);

        /// <summary>
        ///     Applies the transform to a point.
        /// </summary>
        /// <param name="x">The horizontal coordinate.</param>
        /// <param name="y">The vertical coordinate.</param>
        /// <returns>The transformed point.</returns>
        public (double X, double Y) Apply(double x, double y) =>
            ((x * M11) + (y * M21) + M31, (x * M12) + (y * M22) + M32);

        /// <summary>
        ///     Inverts the transform.
        /// </summary>
        /// <param name="inverse">The inverse, when the transform is invertible.</param>
        /// <returns><see langword="false"/> when the determinant is zero or a value is not finite.</returns>
        public bool TryInvert(out Affine inverse)
        {
            var determinant = (M11 * M22) - (M12 * M21);
            if (determinant == 0 || !double.IsFinite(determinant))
            {
                inverse = default;
                return false;
            }

            var reciprocal = 1 / determinant;
            inverse = new Affine(
                M22 * reciprocal,
                -M12 * reciprocal,
                -M21 * reciprocal,
                M11 * reciprocal,
                ((M21 * M32) - (M22 * M31)) * reciprocal,
                ((M12 * M31) - (M11 * M32)) * reciprocal);
            return double.IsFinite(inverse.M11) && double.IsFinite(inverse.M22) && double.IsFinite(inverse.M31) && double.IsFinite(inverse.M32);
        }
    }
}
