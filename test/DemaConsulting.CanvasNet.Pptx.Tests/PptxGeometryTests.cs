using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Geometry;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore xfrm chOff chExt flipH flipV prstGeom custGeom pathLst lnTo cubicBezTo quadBezTo rtTriangle pptx prst cust unrotated

/// <summary>
///     Unit-level tests for the Phase 1c DrawingML shape-geometry resolvers
///     (<see cref="PptxDocument.ResolveShapeFrame"/>, <see cref="PptxDocument.ResolveGroupChildTransform"/>,
///     <see cref="PptxDocument.ResolveShapeGeometry"/>, <see cref="PptxDocument.ResolveCustomGeometry"/>)
///     and <see cref="PptxPresetGeometry"/>'s preset-shape builders. Every resolver under test is a
///     plain static method operating on directly-constructed <see cref="XElement"/> fragments, so
///     these tests do not need a full in-memory <c>.pptx</c> package (reserved for
///     <see cref="PptxSystemIntegrationTests"/>'s end-to-end scenarios).
/// </summary>
public class PptxGeometryTests
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";

    /// <summary>Builds an <c>&lt;a:xfrm&gt;</c> element with the given off/ext/rot/flip attributes.</summary>
    private static XElement BuildXfrm(
        long offX, long offY, long extCx, long extCy,
        int? rot = null, bool? flipH = null, bool? flipV = null,
        long? chOffX = null, long? chOffY = null, long? chExtCx = null, long? chExtCy = null)
    {
        var xfrm = new XElement(
            A + "xfrm",
            new XElement(A + "off", new XAttribute("x", offX), new XAttribute("y", offY)),
            new XElement(A + "ext", new XAttribute("cx", extCx), new XAttribute("cy", extCy)));

        if (rot is not null)
        {
            xfrm.Add(new XAttribute("rot", rot.Value));
        }

        if (flipH is not null)
        {
            xfrm.Add(new XAttribute("flipH", flipH.Value ? "1" : "0"));
        }

        if (flipV is not null)
        {
            xfrm.Add(new XAttribute("flipV", flipV.Value ? "1" : "0"));
        }

        if (chOffX is not null && chOffY is not null)
        {
            xfrm.Add(new XElement(A + "chOff", new XAttribute("x", chOffX.Value), new XAttribute("y", chOffY.Value)));
        }

        if (chExtCx is not null && chExtCy is not null)
        {
            xfrm.Add(new XElement(A + "chExt", new XAttribute("cx", chExtCx.Value), new XAttribute("cy", chExtCy.Value)));
        }

        return xfrm;
    }

    /// <summary>Assert Vectors Close.</summary>
    private static void AssertVectorsClose(Vector2 expected, Vector2 actual, float tolerance = 0.01f)
    {
        Assert.True(
            MathF.Abs(expected.X - actual.X) <= tolerance && MathF.Abs(expected.Y - actual.Y) <= tolerance,
            $"Expected {expected} but was {actual}.");
    }

    // --- ResolveShapeFrame: translate/size ---------------------------------------------------

    /// <summary>Resolve Shape Frame - Plain Offset And Size - Maps Local Origin To Offset.</summary>
    [Fact]
    public void ResolveShapeFrame_PlainOffsetAndSize_MapsLocalOriginToOffset()
    {
        var xfrm = BuildXfrm(1000, 2000, 500, 300);

        var frame = PptxDocument.ResolveShapeFrame(xfrm);

        Assert.Equal(500f, frame.WidthEmu);
        Assert.Equal(300f, frame.HeightEmu);
        AssertVectorsClose(new Vector2(1000, 2000), Vector2.Transform(Vector2.Zero, frame.Transform));
        AssertVectorsClose(new Vector2(1500, 2300), Vector2.Transform(new Vector2(500, 300), frame.Transform));
    }

    /// <summary>Resolve Shape Frame - Missing Off - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveShapeFrame_MissingOff_ThrowsInvalidDataException()
    {
        var xfrm = new XElement(A + "xfrm", new XElement(A + "ext", new XAttribute("cx", 100), new XAttribute("cy", 100)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeFrame(xfrm));
    }

    /// <summary>Resolve Shape Frame - Missing Ext - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveShapeFrame_MissingExt_ThrowsInvalidDataException()
    {
        var xfrm = new XElement(A + "xfrm", new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeFrame(xfrm));
    }

    // --- ResolveShapeFrame: rotation ----------------------------------------------------------

    /// <summary>Resolve Shape Frame - Rotation90 Degrees - Rotates About Own Center Clockwise.</summary>
    [Fact]
    public void ResolveShapeFrame_Rotation90Degrees_RotatesAboutOwnCenterClockwise()
    {
        // 90 degrees clockwise (5,400,000 sixtieths of a degree) about the shape's own center
        // (50,50) within a 100x100 box at offset (0,0): local (100,0) - the top-right corner -
        // must land at the box's bottom-right corner (100,100) in parent space.
        var xfrm = BuildXfrm(0, 0, 100, 100, rot: 90 * 60000);

        var frame = PptxDocument.ResolveShapeFrame(xfrm);

        AssertVectorsClose(new Vector2(100, 100), Vector2.Transform(new Vector2(100, 0), frame.Transform));
        AssertVectorsClose(new Vector2(50, 50), Vector2.Transform(new Vector2(50, 50), frame.Transform));
    }

    /// <summary>Resolve Shape Frame - Missing Rot - Defaults To Zero Rotation.</summary>
    [Fact]
    public void ResolveShapeFrame_MissingRot_DefaultsToZeroRotation()
    {
        var xfrm = BuildXfrm(0, 0, 100, 100);

        var frame = PptxDocument.ResolveShapeFrame(xfrm);

        AssertVectorsClose(new Vector2(100, 0), Vector2.Transform(new Vector2(100, 0), frame.Transform));
    }

    // --- ResolveShapeFrame: flip --------------------------------------------------------------

    /// <summary>Resolve Shape Frame - Flip Horizontal - Mirrors About Own Center X.</summary>
    [Fact]
    public void ResolveShapeFrame_FlipHorizontal_MirrorsAboutOwnCenterX()
    {
        var xfrm = BuildXfrm(0, 0, 100, 50, flipH: true);

        var frame = PptxDocument.ResolveShapeFrame(xfrm);

        // Local (0,0) (top-left) must land at (100,0) (top-right) after a horizontal flip.
        AssertVectorsClose(new Vector2(100, 0), Vector2.Transform(Vector2.Zero, frame.Transform));
        AssertVectorsClose(new Vector2(0, 0), Vector2.Transform(new Vector2(100, 0), frame.Transform));
    }

    /// <summary>Resolve Shape Frame - Flip Vertical - Mirrors About Own Center Y.</summary>
    [Fact]
    public void ResolveShapeFrame_FlipVertical_MirrorsAboutOwnCenterY()
    {
        var xfrm = BuildXfrm(0, 0, 100, 50, flipV: true);

        var frame = PptxDocument.ResolveShapeFrame(xfrm);

        AssertVectorsClose(new Vector2(0, 50), Vector2.Transform(Vector2.Zero, frame.Transform));
    }

    /// <summary>Resolve Shape Frame - Invalid Rot Attribute - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveShapeFrame_InvalidRotAttribute_ThrowsInvalidDataException()
    {
        var xfrm = BuildXfrm(0, 0, 100, 100);
        xfrm.Add(new XAttribute("rot", "not-a-number"));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeFrame(xfrm));
    }

    /// <summary>
    ///     Resolve Shape Frame - Non Numeric Off Attribute - Throws Invalid Data Exception
    ///     (not the raw <see cref="FormatException"/> an explicit <c>(float?)</c> cast would
    ///     otherwise let escape).
    /// </summary>
    [Fact]
    public void ResolveShapeFrame_NonNumericOffAttribute_ThrowsInvalidDataException()
    {
        var xfrm = new XElement(
            A + "xfrm",
            new XElement(A + "off", new XAttribute("x", "abc"), new XAttribute("y", 0)),
            new XElement(A + "ext", new XAttribute("cx", 100), new XAttribute("cy", 100)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeFrame(xfrm));
    }

    /// <summary>
    ///     Resolve Shape Frame - Non Numeric Ext Attribute - Throws Invalid Data Exception
    ///     (not the raw <see cref="FormatException"/> an explicit <c>(float?)</c> cast would
    ///     otherwise let escape).
    /// </summary>
    [Fact]
    public void ResolveShapeFrame_NonNumericExtAttribute_ThrowsInvalidDataException()
    {
        var xfrm = new XElement(
            A + "xfrm",
            new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
            new XElement(A + "ext", new XAttribute("cx", "abc"), new XAttribute("cy", 100)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeFrame(xfrm));
    }

    /// <summary>
    ///     Resolve Shape Frame - Non Finite Off Attribute - Throws Invalid Data Exception
    ///     (a non-finite <c>&lt;a:off&gt;</c> value must be rejected just like a non-numeric one).
    /// </summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ResolveShapeFrame_NonFiniteOffAttribute_ThrowsInvalidDataException(string nonFiniteValue)
    {
        var xfrm = new XElement(
            A + "xfrm",
            new XElement(A + "off", new XAttribute("x", nonFiniteValue), new XAttribute("y", 0)),
            new XElement(A + "ext", new XAttribute("cx", 100), new XAttribute("cy", 100)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeFrame(xfrm));
    }

    /// <summary>
    ///     Resolve Shape Frame - Non Finite Ext Attribute - Throws Invalid Data Exception
    ///     (a non-finite <c>&lt;a:ext&gt;</c> value must be rejected just like a non-numeric one).
    /// </summary>
    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-Infinity")]
    public void ResolveShapeFrame_NonFiniteExtAttribute_ThrowsInvalidDataException(string nonFiniteValue)
    {
        var xfrm = new XElement(
            A + "xfrm",
            new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
            new XElement(A + "ext", new XAttribute("cx", nonFiniteValue), new XAttribute("cy", 100)));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeFrame(xfrm));
    }

    // --- ResolveGroupChildTransform -------------------------------------------------------------

    /// <summary>Resolve Group Child Transform - Identity Child Space - Matches Group Frame Directly.</summary>
    [Fact]
    public void ResolveGroupChildTransform_IdentityChildSpace_MatchesGroupFrameDirectly()
    {
        // Group placed at (1000,1000) sized 200x200, with its child space declared identical to
        // its own off/ext (chOff==off, chExt==ext) - a child's own local point should map exactly
        // as if composed only with the group's own plain frame.
        var groupXfrm = BuildXfrm(1000, 1000, 200, 200, chOffX: 1000, chOffY: 1000, chExtCx: 200, chExtCy: 200);

        var childTransform = PptxDocument.ResolveGroupChildTransform(groupXfrm);

        AssertVectorsClose(new Vector2(1000, 1000), Vector2.Transform(new Vector2(1000, 1000), childTransform));
        AssertVectorsClose(new Vector2(1200, 1200), Vector2.Transform(new Vector2(1200, 1200), childTransform));
    }

    /// <summary>Resolve Group Child Transform - Scaled Child Space - Scales Child Coordinates Into Group Box.</summary>
    [Fact]
    public void ResolveGroupChildTransform_ScaledChildSpace_ScalesChildCoordinatesIntoGroupBox()
    {
        // Group occupies a 100x100 box at (0,0) in its parent, but its children are authored in a
        // 200x200 child coordinate space starting at (0,0) - every child coordinate must be
        // halved when mapped into the group's own box.
        var groupXfrm = BuildXfrm(0, 0, 100, 100, chOffX: 0, chOffY: 0, chExtCx: 200, chExtCy: 200);

        var childTransform = PptxDocument.ResolveGroupChildTransform(groupXfrm);

        AssertVectorsClose(new Vector2(0, 0), Vector2.Transform(new Vector2(0, 0), childTransform));
        AssertVectorsClose(new Vector2(50, 50), Vector2.Transform(new Vector2(100, 100), childTransform));
        AssertVectorsClose(new Vector2(100, 100), Vector2.Transform(new Vector2(200, 200), childTransform));
    }

    /// <summary>Resolve Group Child Transform - Offset Child Space - Translates Child Origin Before Scaling.</summary>
    [Fact]
    public void ResolveGroupChildTransform_OffsetChildSpace_TranslatesChildOriginBeforeScaling()
    {
        var groupXfrm = BuildXfrm(0, 0, 100, 100, chOffX: 50, chOffY: 50, chExtCx: 100, chExtCy: 100);

        var childTransform = PptxDocument.ResolveGroupChildTransform(groupXfrm);

        // A child point at the declared child-space origin (50,50) must map to the group box's
        // own local origin (0,0).
        AssertVectorsClose(new Vector2(0, 0), Vector2.Transform(new Vector2(50, 50), childTransform));
    }

    /// <summary>Resolve Group Child Transform - Child Of Child - Composes Through Both Transforms.</summary>
    [Fact]
    public void ResolveGroupChildTransform_ChildOfChild_ComposesThroughBothTransforms()
    {
        // A child shape's own ResolveShapeFrame().Transform must compose (child-local-first) with
        // ResolveGroupChildTransform()'s result to yield the child's full parent-space transform.
        var groupXfrm = BuildXfrm(0, 0, 100, 100, chOffX: 0, chOffY: 0, chExtCx: 200, chExtCy: 200);
        var groupChildTransform = PptxDocument.ResolveGroupChildTransform(groupXfrm);

        var childXfrm = BuildXfrm(0, 0, 200, 200); // child declared at the full child coordinate space size
        var childFrame = PptxDocument.ResolveShapeFrame(childXfrm);

        var fullTransform = childFrame.Transform * groupChildTransform;

        // Child-local (200,200) (its own bottom-right corner) -> child space (200,200) -> group
        // box (100,100) (halved) -> parent space (100,100) (group itself is at (0,0) unrotated).
        AssertVectorsClose(new Vector2(100, 100), Vector2.Transform(new Vector2(200, 200), fullTransform));
    }

    /// <summary>Resolve Group Child Transform - Missing Ch Off Ch Ext - Defaults To Identity Child Space.</summary>
    [Fact]
    public void ResolveGroupChildTransform_MissingChOffChExt_DefaultsToIdentityChildSpace()
    {
        var groupXfrm = BuildXfrm(10, 10, 50, 50);

        var childTransform = PptxDocument.ResolveGroupChildTransform(groupXfrm);

        AssertVectorsClose(new Vector2(10, 10), Vector2.Transform(new Vector2(0, 0), childTransform));
    }

    /// <summary>
    ///     Resolve Group Child Transform - Ch Off Present With Missing Attribute - Throws Invalid
    ///     Data Exception (a present-but-incomplete <c>&lt;a:chOff&gt;</c> must fail closed, not
    ///     silently fall back to the "element absent" default).
    /// </summary>
    [Fact]
    public void ResolveGroupChildTransform_ChOffPresentWithMissingAttribute_ThrowsInvalidDataException()
    {
        var groupXfrm = new XElement(
            A + "xfrm",
            new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
            new XElement(A + "ext", new XAttribute("cx", 100), new XAttribute("cy", 100)),
            new XElement(A + "chOff", new XAttribute("x", 0))); // missing required 'y'

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveGroupChildTransform(groupXfrm));
    }

    /// <summary>
    ///     Resolve Group Child Transform - Ch Ext Present With Missing Attribute - Throws Invalid
    ///     Data Exception (a present-but-incomplete <c>&lt;a:chExt&gt;</c> must fail closed, not
    ///     silently fall back to the "element absent" default).
    /// </summary>
    [Fact]
    public void ResolveGroupChildTransform_ChExtPresentWithMissingAttribute_ThrowsInvalidDataException()
    {
        var groupXfrm = new XElement(
            A + "xfrm",
            new XElement(A + "off", new XAttribute("x", 0), new XAttribute("y", 0)),
            new XElement(A + "ext", new XAttribute("cx", 100), new XAttribute("cy", 100)),
            new XElement(A + "chExt", new XAttribute("cx", 100))); // missing required 'cy'

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveGroupChildTransform(groupXfrm));
    }

    // --- ResolveShapeGeometry: dispatch + failure modes ---------------------------------------

    /// <summary>Resolve Shape Geometry - Prst Geom Rect - Returns Rectangle Path.</summary>
    [Fact]
    public void ResolveShapeGeometry_PrstGeomRect_ReturnsRectanglePath()
    {
        var spPr = new XElement(
            A + "spPr",
            new XElement(A + "prstGeom", new XAttribute("prst", "rect")));

        var path = PptxDocument.ResolveShapeGeometry(spPr, 100, 50);

        var bounds = path.GetBounds();
        Assert.Equal(0f, bounds.X);
        Assert.Equal(0f, bounds.Y);
        Assert.Equal(100f, bounds.Width);
        Assert.Equal(50f, bounds.Height);
    }

    /// <summary>Resolve Shape Geometry - Neither Prst Geom Nor Cust Geom - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveShapeGeometry_NeitherPrstGeomNorCustGeom_ThrowsInvalidDataException()
    {
        var spPr = new XElement(A + "spPr");

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeGeometry(spPr, 100, 50));
    }

    /// <summary>Resolve Shape Geometry - Unsupported Preset - Throws Pptx Unsupported Feature Exception.</summary>
    [Fact]
    public void ResolveShapeGeometry_UnsupportedPreset_ThrowsPptxUnsupportedFeatureException()
    {
        var spPr = new XElement(
            A + "spPr",
            new XElement(A + "prstGeom", new XAttribute("prst", "someExoticPresetNotSupported")));

        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ResolveShapeGeometry(spPr, 100, 50));
        Assert.Equal("pptx-preset-geometry", ex.Feature);
    }

    /// <summary>Resolve Shape Geometry - Prst Geom Missing Prst Attribute - Throws Invalid Data Exception.</summary>
    [Fact]
    public void ResolveShapeGeometry_PrstGeomMissingPrstAttribute_ThrowsInvalidDataException()
    {
        var spPr = new XElement(A + "spPr", new XElement(A + "prstGeom"));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ResolveShapeGeometry(spPr, 100, 50));
    }

    // --- ResolveCustomGeometry: path command parsing -------------------------------------------

    /// <summary>Resolve Custom Geometry - Move Line Close - Produces Closed Triangle.</summary>
    [Fact]
    public void ResolveCustomGeometry_MoveLineClose_ProducesClosedTriangle()
    {
        var custGeom = new XElement(
            A + "custGeom",
            new XElement(
                A + "pathLst",
                new XElement(
                    A + "path",
                    new XAttribute("w", 100), new XAttribute("h", 100),
                    new XElement(A + "moveTo", new XElement(A + "pt", new XAttribute("x", 0), new XAttribute("y", 100))),
                    new XElement(A + "lnTo", new XElement(A + "pt", new XAttribute("x", 50), new XAttribute("y", 0))),
                    new XElement(A + "lnTo", new XElement(A + "pt", new XAttribute("x", 100), new XAttribute("y", 100))),
                    new XElement(A + "close"))));

        var path = PptxDocument.ResolveCustomGeometry(custGeom, 100, 100);

        Assert.Single(path.Subpaths);
        var subpath = path.Subpaths[0];
        Assert.True(subpath.IsClosed);
        AssertVectorsClose(new Vector2(0, 100), subpath.Start);
        Assert.Equal(PathCommandType.LineTo, subpath.Commands[0].Type);
        AssertVectorsClose(new Vector2(50, 0), subpath.Commands[0].EndPoint);
        Assert.Equal(PathCommandType.LineTo, subpath.Commands[1].Type);
        Assert.Equal(PathCommandType.Close, subpath.Commands[2].Type);
    }

    /// <summary>Resolve Custom Geometry - Cubic Bez To - Produces Cubic Bezier Command.</summary>
    [Fact]
    public void ResolveCustomGeometry_CubicBezTo_ProducesCubicBezierCommand()
    {
        var custGeom = new XElement(
            A + "custGeom",
            new XElement(
                A + "pathLst",
                new XElement(
                    A + "path",
                    new XAttribute("w", 100), new XAttribute("h", 100),
                    new XElement(A + "moveTo", new XElement(A + "pt", new XAttribute("x", 0), new XAttribute("y", 0))),
                    new XElement(
                        A + "cubicBezTo",
                        new XElement(A + "pt", new XAttribute("x", 10), new XAttribute("y", 0)),
                        new XElement(A + "pt", new XAttribute("x", 20), new XAttribute("y", 100)),
                        new XElement(A + "pt", new XAttribute("x", 30), new XAttribute("y", 100))))));

        var path = PptxDocument.ResolveCustomGeometry(custGeom, 100, 100);

        var command = path.Subpaths[0].Commands[0];
        Assert.Equal(PathCommandType.CubicBezierTo, command.Type);
        AssertVectorsClose(new Vector2(10, 0), command.Control1);
        AssertVectorsClose(new Vector2(20, 100), command.Control2);
        AssertVectorsClose(new Vector2(30, 100), command.EndPoint);
    }

    /// <summary>Resolve Custom Geometry - Quad Bez To - Produces Quadratic Bezier Command.</summary>
    [Fact]
    public void ResolveCustomGeometry_QuadBezTo_ProducesQuadraticBezierCommand()
    {
        var custGeom = new XElement(
            A + "custGeom",
            new XElement(
                A + "pathLst",
                new XElement(
                    A + "path",
                    new XAttribute("w", 100), new XAttribute("h", 100),
                    new XElement(A + "moveTo", new XElement(A + "pt", new XAttribute("x", 0), new XAttribute("y", 0))),
                    new XElement(
                        A + "quadBezTo",
                        new XElement(A + "pt", new XAttribute("x", 50), new XAttribute("y", 0)),
                        new XElement(A + "pt", new XAttribute("x", 100), new XAttribute("y", 100))))));

        var path = PptxDocument.ResolveCustomGeometry(custGeom, 100, 100);

        var command = path.Subpaths[0].Commands[0];
        Assert.Equal(PathCommandType.QuadraticBezierTo, command.Type);
        AssertVectorsClose(new Vector2(50, 0), command.Control1);
        AssertVectorsClose(new Vector2(100, 100), command.EndPoint);
    }

    /// <summary>Resolve Custom Geometry - Path Coordinate Space Scaled To Shape Extent.</summary>
    [Fact]
    public void ResolveCustomGeometry_PathCoordinateSpaceScaledToShapeExtent()
    {
        // The <a:path>'s own 50x50 coordinate space must be scaled 2x to fill the shape's
        // declared 100x100 extent.
        var custGeom = new XElement(
            A + "custGeom",
            new XElement(
                A + "pathLst",
                new XElement(
                    A + "path",
                    new XAttribute("w", 50), new XAttribute("h", 50),
                    new XElement(A + "moveTo", new XElement(A + "pt", new XAttribute("x", 0), new XAttribute("y", 0))),
                    new XElement(A + "lnTo", new XElement(A + "pt", new XAttribute("x", 50), new XAttribute("y", 50))))));

        var path = PptxDocument.ResolveCustomGeometry(custGeom, 100, 100);

        AssertVectorsClose(new Vector2(100, 100), path.Subpaths[0].Commands[0].EndPoint);
    }

    /// <summary>Resolve Custom Geometry - No Path Lst - Returns Empty Path.</summary>
    [Fact]
    public void ResolveCustomGeometry_NoPathLst_ReturnsEmptyPath()
    {
        var custGeom = new XElement(A + "custGeom");

        var path = PptxDocument.ResolveCustomGeometry(custGeom, 100, 100);

        Assert.Empty(path.Subpaths);
    }

    // --- PptxPresetGeometry: each supported preset produces non-empty, correctly-bounded geometry ---

    /// <summary>Pptx Preset Geometry - Build - Each Supported Preset - Produces Path Within Declared Bounds.</summary>
    [Theory]
    [InlineData("rect")]
    [InlineData("roundRect")]
    [InlineData("ellipse")]
    [InlineData("triangle")]
    [InlineData("rtTriangle")]
    [InlineData("diamond")]
    [InlineData("parallelogram")]
    [InlineData("trapezoid")]
    [InlineData("hexagon")]
    [InlineData("octagon")]
    [InlineData("pentagon")]
    [InlineData("chevron")]
    [InlineData("homePlate")]
    [InlineData("pie")]
    [InlineData("donut")]
    [InlineData("plus")]
    [InlineData("rightArrow")]
    [InlineData("leftArrow")]
    [InlineData("upArrow")]
    [InlineData("downArrow")]
    [InlineData("leftRightArrow")]
    [InlineData("upDownArrow")]
    [InlineData("star4")]
    [InlineData("star5")]
    [InlineData("curvedUpArrow")]
    [InlineData("curvedDownArrow")]
    [InlineData("curvedLeftArrow")]
    [InlineData("curvedRightArrow")]
    public void PptxPresetGeometry_Build_EachSupportedPreset_ProducesPathWithinDeclaredBounds(string prst)
    {
        const float w = 200f;
        const float h = 120f;

        var path = PptxPresetGeometry.Build(prst, w, h);

        Assert.NotEmpty(path.Subpaths);
        var bounds = path.GetBounds();
        Assert.True(bounds.X >= -0.01f, $"{prst}: bounds.X={bounds.X}");
        Assert.True(bounds.Y >= -0.01f, $"{prst}: bounds.Y={bounds.Y}");
        Assert.True(bounds.X + bounds.Width <= w + 0.01f, $"{prst}: right={bounds.X + bounds.Width}");
        Assert.True(bounds.Y + bounds.Height <= h + 0.01f, $"{prst}: bottom={bounds.Y + bounds.Height}");
    }

    /// <summary>Pptx Preset Geometry - Build - Up Arrow - Points Upward.</summary>
    [Fact]
    public void PptxPresetGeometry_Build_UpArrow_PointsUpward()
    {
        // The arrow's apex (topmost point) must be at the top edge (y approx 0), and the shaft
        // must occupy the bottom portion - a basic sanity check that the swapped-axis rotation
        // used to derive upArrow from rightArrow points the correct direction.
        var path = PptxPresetGeometry.Build("upArrow", 100, 100);
        var bounds = path.GetBounds();

        Assert.True(bounds.Y <= 0.5f, $"upArrow's top bound should be ~0, was {bounds.Y}");
    }

    /// <summary>Pptx Preset Geometry - Build - Down Arrow - Points Downward.</summary>
    [Fact]
    public void PptxPresetGeometry_Build_DownArrow_PointsDownward()
    {
        var upPath = PptxPresetGeometry.Build("upArrow", 100, 100);
        var downPath = PptxPresetGeometry.Build("downArrow", 100, 100);

        // downArrow's apex should be at the bottom (near y=100), the mirror image of upArrow's
        // apex at the top - verified by comparing each path's lowest and highest vertex y.
        var upMinY = upPath.Subpaths[0].Commands.Min(c => c.EndPoint.Y);
        var downMaxY = downPath.Subpaths[0].Commands.Max(c => c.EndPoint.Y);

        Assert.True(upMinY <= 10f, $"upArrow's min y should be near 0, was {upMinY}");
        Assert.True(downMaxY >= 90f, $"downArrow's max y should be near 100, was {downMaxY}");
    }

    /// <summary>Pptx Preset Geometry - Build - Unsupported Preset - Throws With Feature Token.</summary>
    [Fact]
    public void PptxPresetGeometry_Build_UnsupportedPreset_ThrowsWithFeatureToken()
    {
        var ex = Assert.Throws<PptxUnsupportedFeatureException>(() => PptxPresetGeometry.Build("notARealPreset", 100, 100));

        Assert.Equal("pptx-preset-geometry", ex.Feature);
    }

    /// <summary>Pptx Preset Geometry - Build - Non Positive Size - Returns Empty Path.</summary>
    [Fact]
    public void PptxPresetGeometry_Build_NonPositiveSize_ReturnsEmptyPath()
    {
        Assert.Same(Path.Empty, PptxPresetGeometry.Build("rect", 0, 100));
        Assert.Same(Path.Empty, PptxPresetGeometry.Build("rect", 100, 0));
    }

    /// <summary>Pptx Preset Geometry - Build - Curved Up Arrow - Points Upward Without Throwing.</summary>
    [Fact]
    public void PptxPresetGeometry_Build_CurvedUpArrow_PointsUpwardWithoutThrowing()
    {
        // This is the exact regression scenario from the real-world corpus crash report:
        // Build must no longer throw PptxUnsupportedFeatureException for "curvedUpArrow", and
        // the resulting path's apex (its topmost point) must reach the declared top edge
        // (y approx 0), mirroring the existing upArrow/downArrow direction checks.
        var path = PptxPresetGeometry.Build("curvedUpArrow", 200, 120);

        Assert.NotEmpty(path.Subpaths);
        var bounds = path.GetBounds();
        Assert.True(bounds.Y <= 0.5f, $"curvedUpArrow's top bound should be ~0, was {bounds.Y}");
    }

    /// <summary>Pptx Preset Geometry - Build - Curved Arrow Siblings - Each Points Toward Its Own Declared Edge.</summary>
    [Theory]
    [InlineData("curvedDownArrow")]
    [InlineData("curvedLeftArrow")]
    [InlineData("curvedRightArrow")]
    public void PptxPresetGeometry_Build_CurvedArrowSiblings_EachPointsTowardItsOwnDeclaredEdge(string prst)
    {
        // curvedUpArrow's three siblings are derived via the same Mirror/RotateQuarter
        // transforms already used for upArrow/downArrow/leftArrow - this proves each sibling's
        // own apex reaches its own named edge of the declared (0,0)-(w,h) bounds.
        const float w = 200f;
        const float h = 120f;

        var path = PptxPresetGeometry.Build(prst, w, h);
        Assert.NotEmpty(path.Subpaths);

        var bounds = path.GetBounds();
        switch (prst)
        {
            case "curvedDownArrow":
                Assert.True(bounds.Y + bounds.Height >= h - 0.5f, $"curvedDownArrow's bottom bound should be ~{h}, was {bounds.Y + bounds.Height}");
                break;
            case "curvedLeftArrow":
                Assert.True(bounds.X <= 0.5f, $"curvedLeftArrow's left bound should be ~0, was {bounds.X}");
                break;
            case "curvedRightArrow":
                Assert.True(bounds.X + bounds.Width >= w - 0.5f, $"curvedRightArrow's right bound should be ~{w}, was {bounds.X + bounds.Width}");
                break;
        }
    }
}
