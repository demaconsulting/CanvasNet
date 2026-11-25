namespace DemaConsulting.CanvasNet.MathMl.Layout;

/// <summary>
///     The <c>DemaConsulting.CanvasNet.MathMl.Layout</c> namespace lays an equation tree out into a tree of boxes, as
///     MathML Core lays it out, over the OpenType MATH table of the bundled Noto Sans Math font.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="LayoutEngine"/> walks the tree and dispatches each element: tokens to <see cref="TokenLayout"/>,
///         rows and grouping elements to <see cref="RowLayout"/>, and every element that this version does not draw to
///         a row of its children with a report. <see cref="StyleResolver"/> computes the style of each element (the user
///         agent stylesheet of MathML Core and the presentational attributes), <see cref="MathFont"/> supplies the
///         constants, glyphs and metrics, and the result is a tree of <see cref="MathBox"/> that the painter draws.
///     </para>
///     <para>
///         Everything here is internal. A layout is deterministic: it depends only on the tree, the options of the call
///         and the bundled fonts.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
