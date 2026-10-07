namespace DemaConsulting.CanvasNet.Vsdx;

// cspell:ignore vsdx Visio davehoward jgreywolfvsdxjs

/// <summary>
///     Identifies which end of a 1-D (connector) shape a single <see cref="VsdxConnect"/> entry
///     describes - derived from a parsed <c>&lt;Connect&gt;</c> element's <c>FromCell</c>
///     attribute, always literally <c>"BeginX"</c> or <c>"EndX"</c> in every inspected sample
///     (see the format reference's §7.1: "the X component of the endpoint pair implicitly
///     carries its paired Y; Visio never emits a separate <c>BeginY</c>/<c>EndY</c>
///     <c>&lt;Connect&gt;</c> row because glue is defined per-point, not per-axis").
/// </summary>
internal enum VsdxConnectEndpoint
{
    /// <summary>The connector's <c>BeginX</c>/<c>BeginY</c> endpoint.</summary>
    Begin,

    /// <summary>The connector's <c>EndX</c>/<c>EndY</c> endpoint.</summary>
    End
}

/// <summary>
///     A single resolved <c>&lt;Connect&gt;</c> entry from a page's <c>&lt;Connects&gt;</c>
///     section (see <c>VsdxDocument.Connects.cs</c>): which endpoint of which connector shape is
///     glued to which target shape, and whether that glue is whole-shape-pin or connection-
///     point-indexed.
/// </summary>
/// <remarks>
///     <para>
///         Confirmed against both of this milestone's worked real-world examples (see the format
///         reference's §7.1): <c>davehoward-test4-connectors.vsdx</c>'s page1.xml -
///         <c>&lt;Connect FromSheet='7' FromCell='EndX' FromPart='12' ToSheet='5' ToCell='PinX'
///         ToPart='3'/&gt;</c> (whole-shape-pin glue, <see cref="IsWholeShapePin"/> <see
///         langword="true"/>, <see cref="ConnectionPointIndex"/> <see langword="null"/>) - and
///         <c>jgreywolfvsdxjs-connectors.vsdx</c>'s page1.xml - <c>&lt;Connect FromSheet='42'
///         FromCell='EndX' FromPart='12' ToSheet='41' ToCell='Connections.X1' ToPart='100'/&gt;</c>
///         (connection-point-indexed glue, <see cref="IsWholeShapePin"/> <see langword="false"/>,
///         <see cref="ConnectionPointIndex"/> <c>1</c>).
///     </para>
///     <para>
///         This milestone's renderer never dereferences <see cref="TargetShapeId"/>/
///         <see cref="ToCell"/>/<see cref="ToPart"/> to compute a connector's rendered endpoints -
///         see <see cref="VsdxConnectorEndpoints"/>'s own remarks for why the connector shape's
///         own pre-baked <c>BeginX/Y</c>/<c>EndX/Y</c> cells are trusted directly instead. These
///         fields are parsed and retained purely for connection identity (a future hit-testing/
///         re-layout capability - see the format reference's §7.2 point 1), so a <c>&lt;Connect&gt;</c>
///         entry whose <see cref="TargetShapeId"/> cannot be resolved within the page (a dangling
///         glue target) is harmless to retain unresolved: nothing in this milestone ever looks it
///         up.
///     </para>
/// </remarks>
/// <param name="ConnectorShapeId">The connector (1-D) shape's own <c>ID</c> - the parsed <c>&lt;Connect&gt;</c> element's <c>FromSheet</c> attribute.</param>
/// <param name="Endpoint">Which of the connector's own endpoints this entry describes.</param>
/// <param name="FromPart">The parsed <c>FromPart</c> attribute's raw value - a numeric "part code" identifying the connector's own geometry handle (<c>"9"</c> = begin point, <c>"12"</c> = end point in every inspected sample).</param>
/// <param name="TargetShapeId">The target shape's own <c>ID</c> - the parsed <c>&lt;Connect&gt;</c> element's <c>ToSheet</c> attribute.</param>
/// <param name="ToCell">The parsed <c>ToCell</c> attribute's raw value - either <c>"PinX"</c> (whole-shape-pin glue) or <c>"Connections.X«i»"</c> (connection-point-indexed glue).</param>
/// <param name="ToPart">The parsed <c>ToPart</c> attribute's raw value - a numeric "part code" for the target side (<c>"3"</c> = whole-shape pin glue; <c>"100"</c>, <c>"101"</c>, ... = connection-point glue).</param>
/// <param name="IsWholeShapePin"><see langword="true"/> when <see cref="ToCell"/> is literally <c>"PinX"</c> (a dynamic, shape-to-shape glue that re-routes if the target moves/resizes).</param>
/// <param name="ConnectionPointIndex">The 1-based connection-point index parsed from a <c>"Connections.X«i»"</c> <see cref="ToCell"/>, or <see langword="null"/> when <see cref="IsWholeShapePin"/> is <see langword="true"/> or <see cref="ToCell"/> did not match that pattern.</param>
internal sealed record VsdxConnect(
    string ConnectorShapeId,
    VsdxConnectEndpoint Endpoint,
    string FromPart,
    string TargetShapeId,
    string ToCell,
    string ToPart,
    bool IsWholeShapePin,
    int? ConnectionPointIndex);
