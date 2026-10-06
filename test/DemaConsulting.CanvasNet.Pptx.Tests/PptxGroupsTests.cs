using System.Numerics;
using System.Xml.Linq;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Fonts;

namespace DemaConsulting.CanvasNet.Pptx.Tests;

// cspell:ignore pptx grpsp nvsppr nvpr nvgrpsppr grpsppr cxnsp xfrm chOff chExt sptree

/// <summary>
///     Unit-level tests for the Phase 1e recursive shape-tree walker
///     (<c>PptxDocument.ShapeTree.cs</c>'s <see cref="PptxDocument.ParseShapeTree"/>) and its
///     closed <see cref="PptxShapeTreeNode"/> hierarchy, plus the Phase 1e-extracted
///     <see cref="PptxPlaceholderParser.TryParsePlaceholder"/> helper. Every resolver under test is
///     a plain static method operating on directly-constructed <see cref="XElement"/> fragments,
///     mirroring <see cref="PptxGeometryTests"/>/<see cref="PptxTablesTests"/>'s own
///     "no full in-memory package needed" style.
/// </summary>
public class PptxGroupsTests
{
    private static readonly XNamespace A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private static readonly XNamespace P = "http://schemas.openxmlformats.org/presentationml/2006/main";

    private static PptxTheme BuildTestTheme() =>
        new(
            new PptxColorScheme(
                new Rgba32(10, 10, 10, 255), new Rgba32(20, 20, 20, 255), new Rgba32(30, 30, 30, 255), new Rgba32(40, 40, 40, 255),
                new Rgba32(50, 50, 50, 255), new Rgba32(60, 60, 60, 255), new Rgba32(70, 70, 70, 255), new Rgba32(80, 80, 80, 255),
                new Rgba32(90, 90, 90, 255), new Rgba32(100, 100, 100, 255), new Rgba32(110, 110, 110, 255), new Rgba32(120, 120, 120, 255)),
            new PptxFontScheme(
                new PptxFontCollection("ThemeMajorLatin", "MajorEA", "MajorCS"),
                new PptxFontCollection("ThemeMinorLatin", "MinorEA", "MinorCS")));

    private static Func<PptxTheme> ThemeResolver(PptxTheme theme) => () => theme;

    /// <summary>Builds a non-placeholder (freeform) <c>&lt;p:sp&gt;</c> element.</summary>
    private static XElement BuildFreeformSp(string name = "Freeform") =>
        new(P + "sp",
            new XElement(P + "nvSpPr",
                new XElement(P + "cNvPr", new XAttribute("id", 1), new XAttribute("name", name)),
                new XElement(P + "cNvSpPr"),
                new XElement(P + "nvPr")));

    /// <summary>Builds a placeholder <c>&lt;p:sp&gt;</c> element with a <c>&lt;p:ph&gt;</c> descendant.</summary>
    private static XElement BuildPlaceholderSp(string type = "title", uint? idx = null)
    {
        var ph = new XElement(P + "ph", new XAttribute("type", type));
        if (idx is not null)
        {
            ph.Add(new XAttribute("idx", idx.Value));
        }

        return new XElement(P + "sp",
            new XElement(P + "nvSpPr",
                new XElement(P + "cNvPr", new XAttribute("id", 1), new XAttribute("name", "Title")),
                new XElement(P + "cNvSpPr"),
                new XElement(P + "nvPr", ph)));
    }

    private static XElement BuildPic(string name = "Picture") =>
        new(P + "pic",
            new XElement(P + "nvPicPr",
                new XElement(P + "cNvPr", new XAttribute("id", 1), new XAttribute("name", name)),
                new XElement(P + "cNvPicPr"),
                new XElement(P + "nvPr")));

    private static XElement BuildXfrm(long offX, long offY, long extCx, long extCy, long? chOffX = null, long? chOffY = null, long? chExtCx = null, long? chExtCy = null)
    {
        var xfrm = new XElement(
            A + "xfrm",
            new XElement(A + "off", new XAttribute("x", offX), new XAttribute("y", offY)),
            new XElement(A + "ext", new XAttribute("cx", extCx), new XAttribute("cy", extCy)));

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

    private static XElement BuildGrpSp(XElement xfrm, params XElement[] children) =>
        new(P + "grpSp",
            [
                new XElement(P + "nvGrpSpPr",
                    new XElement(P + "cNvPr", new XAttribute("id", 1), new XAttribute("name", "Group")),
                    new XElement(P + "cNvGrpSpPr"),
                    new XElement(P + "nvPr")),
                new XElement(P + "grpSpPr", xfrm),
                .. children,
            ]);

    private static XElement BuildTbl() =>
        new(A + "tbl",
            new XElement(A + "tblGrid", new XElement(A + "gridCol", new XAttribute("w", 1000f))),
            new XElement(A + "tr", new XAttribute("h", 1000f), new XElement(A + "tc")));

    private static XElement BuildGraphicFrame(string uriSuffix = "/table") =>
        new(P + "graphicFrame",
            new XElement(A + "graphic",
                new XElement(A + "graphicData", new XAttribute("uri", $"http://schemas.openxmlformats.org/drawingml/2006{uriSuffix}"), BuildTbl())));

    // --- ParseShapeTree: dispatch --------------------------------------------------------------

    /// <summary>Proves a <c>&lt;p:sp&gt;</c> with no <c>&lt;p:ph&gt;</c> produces a <see cref="PptxSpShapeNode"/> with a null placeholder.</summary>
    [Fact]
    public void ParseShapeTree_FreeformShape_ProducesSpShapeNodeWithNullPlaceholder()
    {
        var spTree = new XElement(P + "spTree", BuildFreeformSp());

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        var node = Assert.IsType<PptxSpShapeNode>(Assert.Single(nodes));
        Assert.Null(node.Placeholder);
    }

    /// <summary>Proves a <c>&lt;p:sp&gt;</c> with a <c>&lt;p:ph&gt;</c> produces a <see cref="PptxSpShapeNode"/> with a resolved placeholder.</summary>
    [Fact]
    public void ParseShapeTree_PlaceholderShape_ProducesSpShapeNodeWithResolvedPlaceholder()
    {
        var spTree = new XElement(P + "spTree", BuildPlaceholderSp("body", 2));

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        var node = Assert.IsType<PptxSpShapeNode>(Assert.Single(nodes));
        Assert.NotNull(node.Placeholder);
        Assert.Equal("body", node.Placeholder.Type);
        Assert.Equal(2u, node.Placeholder.Idx);
    }

    /// <summary>Proves a <c>&lt;p:pic&gt;</c> produces a <see cref="PptxPictureShapeNode"/>.</summary>
    [Fact]
    public void ParseShapeTree_Picture_ProducesPictureShapeNode()
    {
        var pic = BuildPic();
        var spTree = new XElement(P + "spTree", pic);

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        var node = Assert.IsType<PptxPictureShapeNode>(Assert.Single(nodes));
        Assert.Same(pic, node.PicElement);
    }

    /// <summary>Proves a <c>&lt;p:cxnSp&gt;</c> produces a <see cref="PptxConnectorShapeNode"/> wrapping the raw element, dispatched in the same tree walk as every other shape kind (so its document-order position, and therefore z-order among siblings, is preserved).</summary>
    [Fact]
    public void ParseShapeTree_Connector_ProducesConnectorShapeNode()
    {
        var cxnSp = new XElement(P + "cxnSp",
            new XElement(P + "nvCxnSpPr", new XElement(P + "cNvPr", new XAttribute("id", 2), new XAttribute("name", "Connector")), new XElement(P + "cNvCxnSpPr"), new XElement(P + "nvPr")),
            new XElement(P + "spPr"));
        var spTree = new XElement(P + "spTree", BuildFreeformSp(), cxnSp);

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        Assert.Equal(2, nodes.Count);
        Assert.IsType<PptxSpShapeNode>(nodes[0]);
        var node = Assert.IsType<PptxConnectorShapeNode>(nodes[1]);
        Assert.Same(cxnSp, node.CxnSpElement);
    }

    /// <summary>Proves a <c>&lt;p:graphicFrame&gt;</c> declaring a table produces a <see cref="PptxGraphicFrameShapeNode"/> with its table eagerly parsed.</summary>
    [Fact]
    public void ParseShapeTree_GraphicFrameTable_ProducesGraphicFrameShapeNodeWithParsedTable()
    {
        var spTree = new XElement(P + "spTree", BuildGraphicFrame());

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        var node = Assert.IsType<PptxGraphicFrameShapeNode>(Assert.Single(nodes));
        Assert.NotNull(node.Table);
        Assert.Single(node.Table.ColumnWidthsEmu);
    }

    /// <summary>Proves a slide with no <c>&lt;p:graphicFrame&gt;</c> never invokes the theme resolver (deferred theme resolution).</summary>
    [Fact]
    public void ParseShapeTree_NoGraphicFrame_NeverInvokesThemeResolver()
    {
        var spTree = new XElement(P + "spTree", BuildFreeformSp(), BuildPic());
        var invoked = false;

        PptxDocument.ParseShapeTree(spTree, () =>
        {
            invoked = true;
            return BuildTestTheme();
        });

        Assert.False(invoked);
    }

    /// <summary>Proves an unrecognized element kind (<c>&lt;p:contentPart&gt;</c>) is silently skipped - unlike <c>&lt;p:cxnSp&gt;</c>, which is now recognized and dispatched (see <see cref="ParseShapeTree_Connector_ProducesConnectorShapeNode"/>).</summary>
    [Fact]
    public void ParseShapeTree_UnrecognizedElements_SilentlySkipped()
    {
        var spTree = new XElement(P + "spTree",
            new XElement(P + "contentPart"),
            BuildFreeformSp());

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        Assert.Single(nodes);
        Assert.IsType<PptxSpShapeNode>(nodes[0]);
    }

    // --- ParseShapeTree: groups -----------------------------------------------------------------

    /// <summary>Proves a <c>&lt;p:grpSp&gt;</c> produces a <see cref="PptxGroupShapeNode"/> whose children are its own direct siblings (not a nested <c>&lt;p:spTree&gt;</c>), with its child transform resolved from its own <c>&lt;a:xfrm&gt;</c>.</summary>
    [Fact]
    public void ParseShapeTree_Group_ProducesGroupShapeNodeWithDirectSiblingChildren()
    {
        var xfrm = BuildXfrm(1000, 1000, 200, 200, chOffX: 1000, chOffY: 1000, chExtCx: 200, chExtCy: 200);
        var grpSp = BuildGrpSp(xfrm, BuildFreeformSp(), BuildPic());
        var spTree = new XElement(P + "spTree", grpSp);

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        var group = Assert.IsType<PptxGroupShapeNode>(Assert.Single(nodes));
        Assert.Equal(2, group.Children.Count);
        Assert.IsType<PptxSpShapeNode>(group.Children[0]);
        Assert.IsType<PptxPictureShapeNode>(group.Children[1]);

        // A 1-to-1 offset/extent group with matching chOff/chExt resolves to an identity
        // child transform (mirrors PptxGeometryTests's own "identity when ratios match" case).
        var expected = PptxDocument.ResolveGroupChildTransform(xfrm);
        Assert.Equal(expected, group.ChildTransform);
    }

    /// <summary>Proves nested <c>&lt;p:grpSp&gt;</c> elements each resolve their own child transform and recurse into their own direct children.</summary>
    [Fact]
    public void ParseShapeTree_NestedGroups_EachResolveOwnChildTransformAndChildren()
    {
        var innerXfrm = BuildXfrm(0, 0, 100, 100, chOffX: 0, chOffY: 0, chExtCx: 200, chExtCy: 200);
        var innerGroup = BuildGrpSp(innerXfrm, BuildFreeformSp("Inner"));

        var outerXfrm = BuildXfrm(0, 0, 100, 100, chOffX: 0, chOffY: 0, chExtCx: 200, chExtCy: 200);
        var outerGroup = BuildGrpSp(outerXfrm, innerGroup);

        var spTree = new XElement(P + "spTree", outerGroup);

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        var outer = Assert.IsType<PptxGroupShapeNode>(Assert.Single(nodes));
        var inner = Assert.IsType<PptxGroupShapeNode>(Assert.Single(outer.Children));
        var leaf = Assert.IsType<PptxSpShapeNode>(Assert.Single(inner.Children));
        Assert.Null(leaf.Placeholder);

        Assert.Equal(PptxDocument.ResolveGroupChildTransform(outerXfrm), outer.ChildTransform);
        Assert.Equal(PptxDocument.ResolveGroupChildTransform(innerXfrm), inner.ChildTransform);
    }

    /// <summary>Proves a <c>&lt;p:grpSp&gt;</c> with no <c>&lt;p:grpSpPr&gt;/&lt;a:xfrm&gt;</c> element throws <see cref="InvalidDataException"/>.</summary>
    [Fact]
    public void ParseShapeTree_GroupMissingXfrm_ThrowsInvalidDataException()
    {
        var grpSp = new XElement(P + "grpSp",
            new XElement(P + "nvGrpSpPr",
                new XElement(P + "cNvPr", new XAttribute("id", 1), new XAttribute("name", "Group")),
                new XElement(P + "cNvGrpSpPr"),
                new XElement(P + "nvPr")),
            new XElement(P + "grpSpPr"));
        var spTree = new XElement(P + "spTree", grpSp);

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme())));
    }

    /// <summary>Proves a <c>&lt;p:graphicFrame&gt;</c> inside a group still resolves its table via the propagated theme resolver.</summary>
    [Fact]
    public void ParseShapeTree_GraphicFrameInsideGroup_ParsesTableUsingPropagatedThemeResolver()
    {
        var xfrm = BuildXfrm(0, 0, 100, 100);
        var grpSp = BuildGrpSp(xfrm, BuildGraphicFrame());
        var spTree = new XElement(P + "spTree", grpSp);

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        var group = Assert.IsType<PptxGroupShapeNode>(Assert.Single(nodes));
        var graphicFrameNode = Assert.IsType<PptxGraphicFrameShapeNode>(Assert.Single(group.Children));
        Assert.NotNull(graphicFrameNode.Table);
        Assert.Single(graphicFrameNode.Table.ColumnWidthsEmu);
    }

    /// <summary>Builds a chain of <paramref name="depth"/> nested <c>&lt;p:grpSp&gt;</c> elements, each wrapping the next, with <paramref name="leaf"/> as the innermost element's own child.</summary>
    private static XElement BuildNestedGroupChain(int depth, XElement leaf)
    {
        var current = leaf;
        for (var i = 0; i < depth; i++)
        {
            current = BuildGrpSp(BuildXfrm(0, 0, 100, 100), current);
        }

        return current;
    }

    /// <summary>
    ///     Regression test for the group-shape-nesting-depth safety limit (see the companion
    ///     code-review finding's bug-fix rationale): a <c>&lt;p:grpSp&gt;</c> chain nested one
    ///     level beyond <c>PptxDocument.MaxGroupShapeNestingDepth</c> (100) throws
    ///     <see cref="InvalidDataException"/> rather than overflowing the call stack.
    /// </summary>
    [Fact]
    public void ParseShapeTree_GroupNestingExceedsMaxDepth_ThrowsInvalidDataException()
    {
        var spTree = new XElement(P + "spTree", BuildNestedGroupChain(101, BuildFreeformSp()));

        Assert.Throws<InvalidDataException>(() => PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme())));
    }

    /// <summary>
    ///     Proves a <c>&lt;p:grpSp&gt;</c> chain nested exactly at the documented maximum depth
    ///     (100 levels) still parses successfully - the boundary case guarding against the depth
    ///     cap ever being tightened by accident.
    /// </summary>
    [Fact]
    public void ParseShapeTree_GroupNestingAtMaxDepth_ParsesSuccessfully()
    {
        var spTree = new XElement(P + "spTree", BuildNestedGroupChain(100, BuildFreeformSp()));

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()));

        var group = Assert.IsType<PptxGroupShapeNode>(Assert.Single(nodes));
        for (var i = 0; i < 99; i++)
        {
            group = Assert.IsType<PptxGroupShapeNode>(Assert.Single(group.Children));
        }

        Assert.IsType<PptxSpShapeNode>(Assert.Single(group.Children));
    }

    // --- ParseShapeTree: containUnsupportedGraphicFrames (companion planning report's fix) -----

    /// <summary>
    ///     Regression test (see the companion planning report's bug-fix rationale): with
    ///     <c>containUnsupportedGraphicFrames: true</c> (as passed by <c>GetLayout</c>/
    ///     <c>GetMaster</c>), an unsupported-kind <c>&lt;p:graphicFrame&gt;</c> (a chart, here)
    ///     is silently skipped - its own <see cref="PptxUnsupportedFeatureException"/> must not
    ///     abort the rest of the shape tree - while a sibling freeform shape still parses.
    /// </summary>
    [Fact]
    public void ParseShapeTree_GraphicFrameUnsupportedKindWithContainmentEnabled_SkipsNodeKeepsSiblings()
    {
        var spTree = new XElement(P + "spTree", BuildGraphicFrame("/chart"), BuildFreeformSp());

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()), containUnsupportedGraphicFrames: true);

        var node = Assert.IsType<PptxSpShapeNode>(Assert.Single(nodes));
        Assert.Null(node.Placeholder);
    }

    /// <summary>
    ///     Regression test: with the default <c>containUnsupportedGraphicFrames: false</c> (as
    ///     used by <c>GetSlide</c>), an unsupported-kind <c>&lt;p:graphicFrame&gt;</c> still
    ///     throws <see cref="PptxUnsupportedFeatureException"/>, locking in the slide-level
    ///     hard-fail behavior proven by <c>PptxFixturesCorpusTests.cs</c>'s own slide-level-throw
    ///     tests as a guard against this containment ever being widened by accident.
    /// </summary>
    [Fact]
    public void ParseShapeTree_GraphicFrameUnsupportedKindWithContainmentDisabled_StillThrows()
    {
        var spTree = new XElement(P + "spTree", BuildGraphicFrame("/chart"), BuildFreeformSp());

        Assert.Throws<PptxUnsupportedFeatureException>(() => PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme())));
    }

    /// <summary>
    ///     Proves <c>containUnsupportedGraphicFrames</c> propagates through the recursive
    ///     <c>&lt;p:grpSp&gt;</c> self-call, mirroring how <c>themeResolver</c>/
    ///     <c>tableStyleResolver</c>/<c>colorMapResolver</c> are already propagated (see
    ///     <see cref="ParseShapeTree_GraphicFrameInsideGroup_ParsesTableUsingPropagatedThemeResolver"/>).
    /// </summary>
    [Fact]
    public void ParseShapeTree_GraphicFrameInsideGroupUnsupportedKindWithContainmentEnabled_SkipsNodeKeepsSiblings()
    {
        var xfrm = BuildXfrm(0, 0, 100, 100);
        var grpSp = BuildGrpSp(xfrm, BuildGraphicFrame("/chart"), BuildFreeformSp());
        var spTree = new XElement(P + "spTree", grpSp);

        var nodes = PptxDocument.ParseShapeTree(spTree, ThemeResolver(BuildTestTheme()), containUnsupportedGraphicFrames: true);

        var group = Assert.IsType<PptxGroupShapeNode>(Assert.Single(nodes));
        var child = Assert.IsType<PptxSpShapeNode>(Assert.Single(group.Children));
        Assert.Null(child.Placeholder);
    }

    // --- PptxPlaceholderParser.TryParsePlaceholder ----------------------------------------------

    /// <summary>Proves <see cref="PptxPlaceholderParser.TryParsePlaceholder"/> returns null for a freeform shape.</summary>
    [Fact]
    public void TryParsePlaceholder_FreeformShape_ReturnsNull()
    {
        Assert.Null(PptxPlaceholderParser.TryParsePlaceholder(BuildFreeformSp()));
    }

    /// <summary>Proves <see cref="PptxPlaceholderParser.TryParsePlaceholder"/> applies the <c>type</c>/<c>idx</c> schema defaults when omitted.</summary>
    [Fact]
    public void TryParsePlaceholder_NoTypeOrIdxAttributes_DefaultsToObjAndZero()
    {
        var sp = new XElement(P + "sp",
            new XElement(P + "nvSpPr",
                new XElement(P + "cNvPr", new XAttribute("id", 1), new XAttribute("name", "Ph")),
                new XElement(P + "cNvSpPr"),
                new XElement(P + "nvPr", new XElement(P + "ph"))));

        var placeholder = PptxPlaceholderParser.TryParsePlaceholder(sp);

        Assert.NotNull(placeholder);
        Assert.Equal("obj", placeholder.Type);
        Assert.Equal(0u, placeholder.Idx);
    }

    /// <summary>Proves <see cref="PptxPlaceholderParser.TryParsePlaceholder"/> rejects a non-numeric <c>idx</c> attribute.</summary>
    [Fact]
    public void TryParsePlaceholder_NonNumericIdx_ThrowsInvalidDataException()
    {
        var sp = new XElement(P + "sp",
            new XElement(P + "nvSpPr",
                new XElement(P + "cNvPr", new XAttribute("id", 1), new XAttribute("name", "Ph")),
                new XElement(P + "cNvSpPr"),
                new XElement(P + "nvPr", new XElement(P + "ph", new XAttribute("idx", "not-a-number")))));

        Assert.Throws<InvalidDataException>(() => PptxPlaceholderParser.TryParsePlaceholder(sp));
    }
}
