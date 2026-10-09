namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio WALKGLUE davehoward jgreywolfvsdxjs

/// <summary>
///     A 1-D (connector) shape's resolved, page-space begin/end endpoint coordinates, read
///     directly from its own already-resolved <c>BeginX</c>/<c>BeginY</c>/<c>EndX</c>/<c>EndY</c>
///     cells (see <c>VsdxDocument.Shapes.cs</c>'s <c>ResolveShape</c>, which populates
///     <see cref="VsdxShapeNode.ConnectorEndpoints"/> only when both <c>BeginX</c> and <c>EndX</c>
///     are present in the shape's merged, effective cell bag).
/// </summary>
/// <remarks>
///     <para>
///         <strong>Trusted, not recomputed</strong>: a connector shape's <c>BeginX</c>/<c>BeginY</c>/
///         <c>EndX</c>/<c>EndY</c> cells always carry a <c>V</c> attribute already pre-evaluated
///         by Visio at save time - even though their <c>F=</c> formula attribute is frequently
///         <c>_WALKGLUE(...)</c> (whole-shape-pin glue, confirmed against
///         <c>davehoward-test4-connectors.vsdx</c>'s page1.xml Shape ID='6'/'7') or
///         <c>PAR(PNT(Sheet.N!Connections.X«i»,Sheet.N!Connections.Y«i»))</c> (connection-point
///         glue, confirmed against <c>jgreywolfvsdxjs-connectors.vsdx</c>'s page1.xml Shape
///         ID='42') - this unit deliberately never implements a formula evaluator, and instead
///         reads the already-resolved
///         <c>V</c> value directly, per the format reference's §7.2 glue-point resolution
///         algorithm: "even though these are 'formulas,' the pre-baked <c>V</c> values are already
///         the final glued page coordinates and a renderer again does not need a formula
///         evaluator: just trust <c>V</c>."
///     </para>
///     <para>
///         This is a <em>static</em>, one-shot resolution - it does not track a target shape
///         moving after the package was saved (no live glue-constraint solver is implemented),
///         consistent with this unit's render-a-snapshot scope.
///     </para>
/// </remarks>
/// <param name="BeginX">The connector's resolved begin-point X coordinate, in page-space inches.</param>
/// <param name="BeginY">The connector's resolved begin-point Y coordinate, in page-space inches.</param>
/// <param name="EndX">The connector's resolved end-point X coordinate, in page-space inches.</param>
/// <param name="EndY">The connector's resolved end-point Y coordinate, in page-space inches.</param>
internal sealed record VsdxConnectorEndpoints(
    double BeginX,
    double BeginY,
    double EndX,
    double EndY);
