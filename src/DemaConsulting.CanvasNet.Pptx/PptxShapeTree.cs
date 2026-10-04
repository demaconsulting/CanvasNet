using System.Numerics;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore grpsp pptx sppr

/// <summary>
///     A single node of a slide's full shape tree (Phase 1e), produced by
///     <see cref="PptxDocument.ParseShapeTree"/>: a closed hierarchy mirroring every shape kind
///     this phase recognizes inside a <c>&lt;p:spTree&gt;</c>/<c>&lt;p:grpSp&gt;</c> - a <c>
///     &lt;p:sp&gt;</c> (<see cref="PptxSpShapeNode"/>), a <c>&lt;p:pic&gt;</c>
///     (<see cref="PptxPictureShapeNode"/>), a <c>&lt;p:graphicFrame&gt;</c>
///     (<see cref="PptxGraphicFrameShapeNode"/>), or a nested <c>&lt;p:grpSp&gt;</c>
///     (<see cref="PptxGroupShapeNode"/>). <c>&lt;p:cxnSp&gt;</c>/<c>&lt;p:contentPart&gt;</c>/
///     any other element kind are not represented at all - <see cref="PptxDocument.ParseShapeTree"/>
///     silently skips them (see its own remarks).
/// </summary>
internal abstract record PptxShapeTreeNode
{
    private protected PptxShapeTreeNode()
    {
    }
}

/// <summary>
///     A <c>&lt;p:sp&gt;</c> shape-tree node - either a placeholder shape (when
///     <paramref name="Placeholder"/> is non-null) or a freeform shape (when it is
///     <see langword="null"/>).
/// </summary>
/// <param name="ShapeElement">The raw <c>&lt;p:sp&gt;</c> element.</param>
/// <param name="Placeholder">
///     The shape's resolved <see cref="PptxPlaceholder"/> (see
///     <see cref="PptxPlaceholderParser.TryParsePlaceholder"/>), or <see langword="null"/> for a
///     freeform shape with no <c>&lt;p:nvSpPr&gt;/&lt;p:nvPr&gt;/&lt;p:ph&gt;</c> descendant.
/// </param>
internal sealed record PptxSpShapeNode(XElement ShapeElement, PptxPlaceholder? Placeholder) : PptxShapeTreeNode;

/// <summary>A <c>&lt;p:pic&gt;</c> picture-shape-tree node.</summary>
/// <param name="PicElement">The raw <c>&lt;p:pic&gt;</c> element.</param>
internal sealed record PptxPictureShapeNode(XElement PicElement) : PptxShapeTreeNode;

/// <summary>A <c>&lt;p:graphicFrame&gt;</c> shape-tree node declaring a table.</summary>
/// <param name="GraphicFrameElement">The raw <c>&lt;p:graphicFrame&gt;</c> element.</param>
/// <param name="Table">The eagerly-parsed <see cref="PptxTable"/> (see <see cref="PptxDocument.ParseTable"/>).</param>
internal sealed record PptxGraphicFrameShapeNode(XElement GraphicFrameElement, PptxTable Table) : PptxShapeTreeNode;

/// <summary>A <c>&lt;p:grpSp&gt;</c> group shape-tree node, recursively carrying its own children.</summary>
/// <param name="GroupElement">The raw <c>&lt;p:grpSp&gt;</c> element.</param>
/// <param name="ChildTransform">
///     The transform mapping this group's own child coordinate space into its parent's coordinate
///     space (see <see cref="PptxDocument.ResolveGroupChildTransform"/>). Each child node's own
///     full parent-relative transform is that child's own resolved frame transform composed with
///     this transform (child-local first, then this transform - row-vector convention).
/// </param>
/// <param name="Children">This group's immediate shape-tree children, in document order.</param>
internal sealed record PptxGroupShapeNode(XElement GroupElement, Matrix3x2 ChildTransform, IReadOnlyList<PptxShapeTreeNode> Children) : PptxShapeTreeNode;
