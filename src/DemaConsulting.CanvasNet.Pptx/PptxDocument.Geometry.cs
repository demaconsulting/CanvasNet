using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore xfrm chOff chExt flipH flipV lnTo cubicBezTo quadBezTo prstGeom custGeom pathLst pptx prst unrotated unflipped

/// <summary>
///     Implements the <see cref="PptxDocument"/> DrawingML shape geometry resolvers (Phase 1c):
///     <c>&lt;a:xfrm&gt;</c> position/size/rotation/flip, <c>&lt;p:grpSp&gt;</c> child-transform
///     composition, <c>&lt;a:prstGeom&gt;</c> preset shapes (delegated to
///     <see cref="PptxPresetGeometry"/>), and <c>&lt;a:custGeom&gt;</c> custom path geometry - see
///     <c>pptx-document.md</c>'s "Geometry and Paint (Phase 1c)" design section for the full
///     transform math and the exact supported/deferred preset-geometry boundary.
/// </summary>
public sealed partial class PptxDocument
{
    /// <summary>
    ///     Resolves a shape's <c>&lt;a:xfrm&gt;</c> element into a <see cref="PptxShapeFrame"/>:
    ///     the transform mapping that shape's own local, unrotated/unflipped geometry coordinate
    ///     space (<c>(0,0)</c> to <c>(ext.cx, ext.cy)</c>) into its parent's coordinate space.
    /// </summary>
    /// <param name="xfrmElement">The shape's <c>&lt;a:xfrm&gt;</c> element (from <c>&lt;p:spPr&gt;</c>).</param>
    /// <returns>The resolved <see cref="PptxShapeFrame"/>.</returns>
    /// <remarks>
    ///     <para>
    ///     Per ECMA-376 (ISO/IEC 29500) §20.1.7.6, a shape's local geometry is first placed at
    ///     <c>&lt;a:off x= y=/&gt;</c> with size <c>&lt;a:ext cx= cy=/&gt;</c>; <c>flipH</c>/
    ///     <c>flipV</c> then mirror the shape about its own center, and <c>rot</c> (in 60,000ths
    ///     of a degree, clockwise) then rotates it about that same center - flip is applied
    ///     before rotation, matching PowerPoint's own documented and empirically-verified
    ///     transform order.
    ///     </para>
    ///     <para>
    ///     Because this library's coordinate space is y-down (screen-like - see
    ///     <see cref="Path.Rectangle"/>'s own clockwise-in-y-down convention), a positive OOXML
    ///     <c>rot</c> (clockwise on screen) maps directly to <see cref="Matrix3x2.CreateRotation(float)"/>
    ///     applied to that same y-down space with no sign negation: a mathematically
    ///     "counterclockwise" rotation matrix, interpreted in a y-down frame, already reads as
    ///     clockwise on screen.
    ///     </para>
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="xfrmElement"/> has no <c>&lt;a:off&gt;</c>/<c>&lt;a:ext&gt;</c>
    ///     child, or either child has a missing or non-numeric required attribute.
    /// </exception>
    internal static PptxShapeFrame ResolveShapeFrame(XElement xfrmElement)
    {
        var (offX, offY) = ParseOff(xfrmElement);
        var (extCx, extCy) = ParseExt(xfrmElement);

        var rotAttr = (string?)xfrmElement.Attribute("rot");
        var rot60000ths = 0;
        if (rotAttr is not null && !int.TryParse(rotAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out rot60000ths))
        {
            throw new InvalidDataException($"An <a:xfrm> element has a non-numeric 'rot' attribute value '{rotAttr}'.");
        }

        var flipH = (bool?)xfrmElement.Attribute("flipH") ?? false;
        var flipV = (bool?)xfrmElement.Attribute("flipV") ?? false;

        var transform = BuildLocalToParentTransform(extCx, extCy, rot60000ths, flipH, flipV, offX, offY);
        return new PptxShapeFrame(transform, extCx, extCy);
    }

    /// <summary>
    ///     Resolves a <c>&lt;p:grpSp&gt;</c>'s own <c>&lt;a:xfrm&gt;</c> (which, unlike an
    ///     ordinary shape's, additionally declares <c>&lt;a:chOff&gt;</c>/<c>&lt;a:chExt&gt;</c> -
    ///     the child coordinate space every immediate child shape's own <c>&lt;a:off&gt;</c>/
    ///     <c>&lt;a:ext&gt;</c> is expressed in) into the transform mapping that child coordinate
    ///     space into the group's parent coordinate space.
    /// </summary>
    /// <param name="groupXfrmElement">The group's own <c>&lt;a:xfrm&gt;</c> element (from <c>&lt;p:grpSpPr&gt;</c>).</param>
    /// <returns>
    ///     The transform mapping a point in the group's child coordinate space (<c>chOff</c> to
    ///     <c>chOff + chExt</c>) into the group's parent coordinate space. A child shape's own
    ///     full parent-relative transform is <c>ResolveShapeFrame(childXfrm).Transform</c>
    ///     composed with this returned transform (child-local first, then this transform - row-
    ///     vector convention, matching <see cref="Path.Transform(Matrix3x2)"/>).
    /// </returns>
    /// <remarks>
    ///     A group has its own position/size (<c>&lt;a:off&gt;</c>/<c>&lt;a:ext&gt;</c>, resolved
    ///     via <see cref="ResolveShapeFrame"/> exactly like any other shape, including its own
    ///     flip/rotation about its own center) AND a child coordinate space
    ///     (<c>&lt;a:chOff&gt;</c>/<c>&lt;a:chExt&gt;</c>) every immediate child's own <c>off</c>/
    ///     <c>ext</c> is expressed in - composing these correctly requires first mapping a child
    ///     coordinate into the group's own local <c>(0,0)</c>-<c>(ext.cx, ext.cy)</c> box (a
    ///     translate by <c>-chOff</c> then a scale by <c>ext/chExt</c>), and only then applying
    ///     the group's own resolved frame transform.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="groupXfrmElement"/> has no <c>&lt;a:off&gt;</c>/<c>&lt;a:ext&gt;</c>
    ///     child, or either child (or a present <c>&lt;a:chOff&gt;</c>/<c>&lt;a:chExt&gt;</c>
    ///     child) has a missing or non-numeric required attribute.
    /// </exception>
    internal static Matrix3x2 ResolveGroupChildTransform(XElement groupXfrmElement)
    {
        var groupFrame = ResolveShapeFrame(groupXfrmElement);

        var (chOffX, chOffY) = ParseOptionalPoint(groupXfrmElement, "chOff", default, default);
        var (chExtCx, chExtCy) = ParseOptionalPoint(groupXfrmElement, "chExt", groupFrame.WidthEmu, groupFrame.HeightEmu);

        // A zero-width/height child coordinate space cannot be meaningfully scaled from - fall
        // back to an identity scale factor of 1 along that axis rather than dividing by zero.
        var scaleX = chExtCx == 0f ? 1f : groupFrame.WidthEmu / chExtCx;
        var scaleY = chExtCy == 0f ? 1f : groupFrame.HeightEmu / chExtCy;

        var childToLocal = Matrix3x2.CreateTranslation(-chOffX, -chOffY) * Matrix3x2.CreateScale(scaleX, scaleY);
        return childToLocal * groupFrame.Transform;
    }

    /// <summary>
    ///     Resolves a shape's geometry - its <c>&lt;p:spPr&gt;</c>'s <c>&lt;a:prstGeom&gt;</c> or
    ///     <c>&lt;a:custGeom&gt;</c> child - into a <see cref="Path"/> sized to this shape's own
    ///     local <c>(0,0)</c>-<c>(widthEmu, heightEmu)</c> coordinate space (the same space
    ///     <see cref="ResolveShapeFrame"/>'s returned <see cref="PptxShapeFrame.Transform"/> maps
    ///     from).
    /// </summary>
    /// <param name="spPrElement">The shape's <c>&lt;p:spPr&gt;</c> element.</param>
    /// <param name="widthEmu">The shape's own declared width, in EMU (<see cref="PptxShapeFrame.WidthEmu"/>).</param>
    /// <param name="heightEmu">The shape's own declared height, in EMU (<see cref="PptxShapeFrame.HeightEmu"/>).</param>
    /// <returns>The resolved, shape-local <see cref="Path"/>.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="spPrElement"/> declares neither <c>&lt;a:prstGeom&gt;</c>
    ///     nor <c>&lt;a:custGeom&gt;</c>.
    /// </exception>
    /// <exception cref="PptxUnsupportedFeatureException">
    ///     Thrown when <c>&lt;a:prstGeom&gt;</c> names a preset this phase does not support - see
    ///     <see cref="PptxPresetGeometry.Build"/>.
    /// </exception>
    internal static Path ResolveShapeGeometry(XElement spPrElement, float widthEmu, float heightEmu)
    {
        var prstGeom = spPrElement.Element(DrawingNamespace + "prstGeom");
        if (prstGeom is not null)
        {
            var prst = (string?)prstGeom.Attribute("prst") ??
                throw new InvalidDataException("An <a:prstGeom> element has no 'prst' attribute.");
            return PptxPresetGeometry.Build(prst, widthEmu, heightEmu);
        }

        var custGeom = spPrElement.Element(DrawingNamespace + "custGeom");
        if (custGeom is not null)
        {
            return ResolveCustomGeometry(custGeom, widthEmu, heightEmu);
        }

        throw new InvalidDataException("A <p:spPr> element has neither <a:prstGeom> nor <a:custGeom>.");
    }

    /// <summary>
    ///     Resolves an <c>&lt;a:custGeom&gt;</c> element's <c>&lt;a:pathLst&gt;</c> into a
    ///     <see cref="Path"/>, scaling each <c>&lt;a:path&gt;</c>'s own declared <c>w</c>/<c>h</c>
    ///     coordinate space to the shape's actual <paramref name="widthEmu"/>/
    ///     <paramref name="heightEmu"/>.
    /// </summary>
    /// <param name="custGeomElement">The <c>&lt;a:custGeom&gt;</c> element.</param>
    /// <param name="widthEmu">The shape's own declared width, in EMU.</param>
    /// <param name="heightEmu">The shape's own declared height, in EMU.</param>
    /// <returns>The resolved, shape-local <see cref="Path"/> (one subpath per <c>&lt;a:path&gt;</c>/<c>&lt;a:moveTo&gt;</c>).</returns>
    /// <remarks>
    ///     Every <c>&lt;a:path&gt;</c> under <c>&lt;a:pathLst&gt;</c> is resolved (not only the
    ///     first), each independently scaled by its own declared <c>w</c>/<c>h</c> (defaulting to
    ///     <paramref name="widthEmu"/>/<paramref name="heightEmu"/>, i.e. an identity scale, when
    ///     omitted - per the OOXML schema default). Supported path commands are
    ///     <c>&lt;a:moveTo&gt;</c>, <c>&lt;a:lnTo&gt;</c>, <c>&lt;a:cubicBezTo&gt;</c>,
    ///     <c>&lt;a:quadBezTo&gt;</c>, and <c>&lt;a:close&gt;</c>; <c>&lt;a:arcTo&gt;</c> (a
    ///     distinct, OOXML-specific center-parameterized arc command, not this phase's scope) and
    ///     any other command element are ignored rather than throwing, since a custom-geometry
    ///     path commonly mixes supported and (rare) unsupported commands and Phase 1c's goal is a
    ///     reasonable best-effort outline rather than full fidelity for every custom path.
    /// </remarks>
    internal static Path ResolveCustomGeometry(XElement custGeomElement, float widthEmu, float heightEmu)
    {
        var builder = new PathBuilder();
        var pathLst = custGeomElement.Element(DrawingNamespace + "pathLst");
        if (pathLst is null)
        {
            return Path.Empty;
        }

        foreach (var pathElement in pathLst.Elements(DrawingNamespace + "path"))
        {
            AppendCustomPath(builder, pathElement, widthEmu, heightEmu);
        }

        return builder.Build();
    }

    /// <summary>Appends one <c>&lt;a:path&gt;</c> element's commands to <paramref name="builder"/>, scaled to the shape's actual size.</summary>
    private static void AppendCustomPath(PathBuilder builder, XElement pathElement, float widthEmu, float heightEmu)
    {
        var pathW = (float?)pathElement.Attribute("w") ?? widthEmu;
        var pathH = (float?)pathElement.Attribute("h") ?? heightEmu;
        var scaleX = pathW == 0f ? 1f : widthEmu / pathW;
        var scaleY = pathH == 0f ? 1f : heightEmu / pathH;

        Vector2 Scale(XElement ptElement)
        {
            var x = (float?)ptElement.Attribute("x") ?? 0f;
            var y = (float?)ptElement.Attribute("y") ?? 0f;
            return new Vector2(x * scaleX, y * scaleY);
        }

        foreach (var command in pathElement.Elements())
        {
            var points = command.Elements(DrawingNamespace + "pt").Select(Scale).ToArray();
            if (command.Name == DrawingNamespace + "moveTo" && points.Length >= 1)
            {
                builder.MoveTo(points[0]);
            }
            else if (command.Name == DrawingNamespace + "lnTo" && points.Length >= 1)
            {
                builder.LineTo(points[0]);
            }
            else if (command.Name == DrawingNamespace + "cubicBezTo" && points.Length >= 3)
            {
                builder.CubicBezierTo(points[0], points[1], points[2]);
            }
            else if (command.Name == DrawingNamespace + "quadBezTo" && points.Length >= 2)
            {
                builder.QuadraticBezierTo(points[0], points[1]);
            }
            else if (command.Name == DrawingNamespace + "close")
            {
                builder.Close();
            }
        }
    }

    /// <summary>Parses an <c>&lt;a:off x= y=/&gt;</c> element, required to be present.</summary>
    private static (float X, float Y) ParseOff(XElement xfrmElement)
    {
        var off = xfrmElement.Element(DrawingNamespace + "off") ??
            throw new InvalidDataException("An <a:xfrm> element has no <a:off> element.");
        return (
            (float?)off.Attribute("x") ?? throw new InvalidDataException("An <a:off> element has no 'x' attribute."),
            (float?)off.Attribute("y") ?? throw new InvalidDataException("An <a:off> element has no 'y' attribute."));
    }

    /// <summary>Parses an <c>&lt;a:ext cx= cy=/&gt;</c> element, required to be present.</summary>
    private static (float Cx, float Cy) ParseExt(XElement xfrmElement)
    {
        var ext = xfrmElement.Element(DrawingNamespace + "ext") ??
            throw new InvalidDataException("An <a:xfrm> element has no <a:ext> element.");
        return (
            (float?)ext.Attribute("cx") ?? throw new InvalidDataException("An <a:ext> element has no 'cx' attribute."),
            (float?)ext.Attribute("cy") ?? throw new InvalidDataException("An <a:ext> element has no 'cy' attribute."));
    }

    /// <summary>
    ///     Parses an optional two-attribute child element (<c>&lt;a:chOff x= y=/&gt;</c> or
    ///     <c>&lt;a:chExt cx= cy=/&gt;</c>), falling back to the given defaults when the element
    ///     itself is absent (both elements are schema-optional on a group's <c>&lt;a:xfrm&gt;</c>).
    /// </summary>
    private static (float First, float Second) ParseOptionalPoint(XElement xfrmElement, string elementName, float defaultFirst, float defaultSecond)
    {
        var element = xfrmElement.Element(DrawingNamespace + elementName);
        if (element is null)
        {
            return (defaultFirst, defaultSecond);
        }

        var firstAttrName = elementName == "chOff" ? "x" : "cx";
        var secondAttrName = elementName == "chOff" ? "y" : "cy";
        return (
            (float?)element.Attribute(firstAttrName) ?? defaultFirst,
            (float?)element.Attribute(secondAttrName) ?? defaultSecond);
    }

    /// <summary>
    ///     Builds the transform mapping a shape's local <c>(0,0)</c>-<c>(extCx, extCy)</c>
    ///     geometry coordinate space into its parent's coordinate space: flip about the shape's
    ///     own center, then rotate about that same center, then translate to <c>(offX, offY)</c>
    ///     - see <see cref="ResolveShapeFrame"/>'s remarks for the verified operation order.
    /// </summary>
    private static Matrix3x2 BuildLocalToParentTransform(
        float extCx, float extCy, int rot60000ths, bool flipH, bool flipV, float offX, float offY)
    {
        var center = new Vector2(extCx / 2f, extCy / 2f);
        var flipScale = new Vector2(flipH ? -1f : 1f, flipV ? -1f : 1f);
        var angleRadians = rot60000ths / 60000f * (MathF.PI / 180f);

        return Matrix3x2.CreateTranslation(-center)
            * Matrix3x2.CreateScale(flipScale)
            * Matrix3x2.CreateRotation(angleRadians)
            * Matrix3x2.CreateTranslation(center)
            * Matrix3x2.CreateTranslation(offX, offY);
    }
}
