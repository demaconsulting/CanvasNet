namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Mermaid"/> namespace renders diagrams written in the Mermaid v11 text
///     language onto a CanvasNet surface. It reads a diagram from a string, a stream or a file and produces a validated
///     document (<see cref="MermaidDocument"/>) that describes the diagram (<see cref="MermaidDocument.DiagramType"/>,
///     <see cref="MermaidDocument.IsSupported"/>, <see cref="MermaidDocument.Title"/>, <see cref="MermaidDocument.Direction"/>,
///     <see cref="MermaidDocument.NodeCount"/>, <see cref="MermaidDocument.EdgeCount"/>) or fails with a
///     <see cref="MermaidSyntaxException"/> carrying a precise <see cref="MermaidLocation"/> (file, line and column), a stable
///     code, a hint and the expected tokens. The document can be measured
///     (<see cref="MermaidDocument.Measure(MermaidRenderOptions?, System.Threading.CancellationToken)"/>) and drawn onto a
///     surface (<see cref="MermaidDocument.Render(MermaidRenderOptions?, System.Threading.CancellationToken)"/>) under the
///     options of <see cref="MermaidRenderOptions"/>, with warnings (<see cref="MermaidDiagnostic"/>) and unsupported
///     features (<see cref="MermaidUnsupportedFeature"/>) reported through handlers or, in strict mode, a
///     <see cref="MermaidUnsupportedFeatureException"/>. Every code is listed in <see cref="MermaidDiagnosticCodes"/>.
/// </summary>
/// <remarks>
///     <para>
///         Opening is configured through <see cref="MermaidOpenOptions"/>, rendering through
///         <see cref="MermaidRenderOptions"/> and resource budgets through <see cref="MermaidLimits"/>. The syntax,
///         configuration, layout, rendering and scene-graph stages are internal; only the document surface is public so that
///         the internals can evolve. Every diagram type Mermaid knows is recognized; this version draws flowcharts
///         (<c>flowchart</c> and <c>graph</c>), sequence diagrams (<c>sequenceDiagram</c>), class diagrams
///         (<c>classDiagram</c>), state diagrams (<c>stateDiagram</c>), entity relationship diagrams (<c>erDiagram</c>),
///         pie charts (<c>pie</c>), gantt charts (<c>gantt</c>) and mindmaps (<c>mindmap</c>), and reports any other type as
///         <see cref="MermaidDiagnosticCodes.DiagramUnsupported"/> with
///         an empty picture.
///     </para>
///     <para>
///         Rendering is static, offline and deterministic: nothing is fetched from the network, no script runs, links and
///         click handlers are not followed, fonts come from the bundled Liberation families (Mermaid's default
///         <c>"trebuchet ms", verdana, arial, sans-serif</c> resolves to Liberation Sans), and two renders of the same
///         document and options give identical pixels and identical reports. The resource limits of
///         <see cref="MermaidLimits"/> bound the work, not the clock, so the cancellation token of a call is the only
///         wall-clock bound.
///     </para>
///     <para>
///         Third-party provenance and attribution: this package implements the Mermaid text language and is not affiliated
///         with or endorsed by the Mermaid project. Its preprocessing, detection, parsers, configuration defaults, themes and
///         layouts are ports of mermaid, js-yaml, dagre, layout-base, cose-base, cytoscape-cose-bilkent, cytoscape and
///         sanitize-url (all MIT) with attribution in the
///         <c>third-party</c> folder, whose license texts ship in the package under <c>licenses\</c>.
///     </para>
///     <para>
///         A diagram is drawn onto a <see cref="DemaConsulting.CanvasNet.Canvas.Surface"/>
///         (<see cref="DemaConsulting.CanvasNet.Codecs"/> saves it). <see cref="MermaidImageSource"/> lets another document
///         draw a <c>.mmd</c> file through <see cref="DemaConsulting.CanvasNet.ImageSources"/>, and the
///         <c>DemaConsulting.CanvasNet.Viewer</c> package renders and compares diagrams with the rest of its formats.
///     </para>
/// </remarks>
/// <example>
///     Parse a flowchart, read what it declares, and draw it at its natural size:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Mermaid;
///
///     var diagram = MermaidDocument.Parse("""
///         flowchart LR
///             order[Order received] --> check{In stock?}
///             check -- yes --> ship([Ship])
///             check -- no --> wait[(Backlog)]
///         """);
///     Console.WriteLine($"{diagram.DiagramType} {diagram.Direction}: {diagram.NodeCount} nodes, {diagram.EdgeCount} edges");
///     using var image = diagram.Render();
///     PngCodec.Save(image, "order.png");
///     </code>
/// </example>
/// <example>
///     Draw a flowchart twice as large with the <c>forest</c> theme on white, failing on anything that is not supported:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Canvas;
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Mermaid;
///
///     var diagram = MermaidDocument.Open("pipeline.mmd");
///     var options = new MermaidRenderOptions
///     {
///         Theme = "forest",
///         Scale = 2,
///         Background = new Rgba32(255, 255, 255, 255),
///         ThrowOnUnsupportedFeature = true,
///         WarningHandler = warning => Console.Error.WriteLine(warning),
///     };
///     using var image = diagram.Render(options);
///     PngCodec.Save(image, "pipeline.png");
///     </code>
/// </example>
/// <example>
///     Draw a sequence diagram in the <c>forest</c> theme and read its participants, messages and boxes:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Mermaid;
///
///     var diagram = MermaidDocument.Parse("""
///         sequenceDiagram
///             actor Buyer
///             participant Shop
///             Buyer->>+Shop: Place the order
///             Shop-->>-Buyer: Order confirmed
///             Note over Buyer,Shop: The receipt is sent by email
///         """);
///     Console.WriteLine($"{diagram.NodeCount} participants, {diagram.EdgeCount} messages, {diagram.SubgraphCount} boxes");
///     using var image = diagram.Render(new MermaidRenderOptions { Theme = "forest" });
///     PngCodec.Save(image, "checkout.png");
///     </code>
/// </example>
/// <example>
///     Show a picture inside a node (<c>@{ img: ... }</c>), loaded only from the diagram's own folder through the resolver of
///     the <c>DemaConsulting.CanvasNet.Viewer</c> package, which draws every CanvasNet format (an OpenSCAD model here):
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Mermaid;
///     using DemaConsulting.CanvasNet.Viewer;
///
///     var diagram = MermaidDocument.Parse("""
///         flowchart TD
///             part@{ img: "bracket.scad", label: "Bracket", pos: "t", w: 120, h: 120 }
///             part --> assembly[Assembly]
///         """);
///     var options = new MermaidRenderOptions { ImageResolver = CanvasNetViewer.CreateImageResolver("models") };
///     using var image = diagram.Render(options);
///     PngCodec.Save(image, "assembly.png");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
