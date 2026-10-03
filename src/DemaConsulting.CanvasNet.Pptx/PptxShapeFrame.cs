using System.Numerics;

namespace DemaConsulting.CanvasNet.Pptx;

// cspell:ignore pptx unrotated unflipped

/// <summary>
///     The resolved placement of a single shape: the transform mapping that shape's own local,
///     unrotated/unflipped geometry coordinate space (<c>(0,0)</c> to
///     <c>(WidthEmu, HeightEmu)</c>) into its parent's coordinate space (the slide itself, or -
///     for a shape nested inside a <c>&lt;p:grpSp&gt;</c> - the enclosing group's child coordinate
///     space; see <see cref="PptxDocument.ResolveGroupChildTransform"/>'s remarks for how a
///     group's own frame composes with its children's), together with the shape's own declared
///     width/height (needed by preset/custom geometry builders, which size their output to
///     exactly this box before <see cref="Transform"/> is applied).
/// </summary>
/// <param name="Transform">
///     The transform mapping a point in this shape's own local geometry coordinate space into its
///     parent's coordinate space, row-vector convention (matching
///     <see cref="Geometry.Path.Transform(Matrix3x2)"/>: <c>Vector2.Transform(p, Transform)</c>).
/// </param>
/// <param name="WidthEmu">
///     The shape's own declared width (<c>&lt;a:ext cx="..."/&gt;</c>), in EMU - the width a
///     preset/custom geometry builder sizes its local-space output to, before
///     <see cref="Transform"/> is applied.
/// </param>
/// <param name="HeightEmu">
///     The shape's own declared height (<c>&lt;a:ext cy="..."/&gt;</c>), in EMU - the height a
///     preset/custom geometry builder sizes its local-space output to, before
///     <see cref="Transform"/> is applied.
/// </param>
internal sealed record PptxShapeFrame(Matrix3x2 Transform, float WidthEmu, float HeightEmu);
