namespace DemaConsulting.CanvasNet.PlantUml;

/// <summary>
///     The <c>DemaConsulting.CanvasNet.PlantUml</c> namespace renders diagrams written in the PlantUML text language
///     onto a CanvasNet <c>Surface</c>, statically, offline and deterministically.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="PlantUmlDocument"/> parses a text (<c>Parse</c>, <c>Open</c>), describes it without drawing it
///         (<see cref="PlantUmlDocument.DiagramType"/>, <see cref="PlantUmlDocument.IsSupported"/>, the title and the node,
///         edge and subgraph counts), measures it and renders it. Sequence diagrams are drawn with PlantUML's default
///         look; every other PlantUML diagram type is recognized and reported as
///         <see cref="PlantUmlDiagnosticCodes.DiagramUnsupported"/>. Every construct that is recognized but not supported
///         is reported with a stable <c>plantuml-*</c> code and a location (<see cref="PlantUmlUnsupportedFeature"/>), and
///         strict mode (<see cref="PlantUmlRenderOptions.ThrowOnUnsupportedFeature"/>) turns the first one into an
///         exception. Untrusted text is bounded by <see cref="PlantUmlLimits"/>.
///     </para>
///     <para>
///         Text is measured and drawn with the fonts bundled with CanvasNet only (PlantUML's <c>SansSerif</c> is Liberation
///         Sans, which has the metrics of Arial), so a rendering is the same on every machine. The library never uses the
///         network. The package is not affiliated with the PlantUML project; the default look and layout geometry are
///         derived from the MIT-licensed distribution of PlantUML (see the package's third-party notice).
///     </para>
/// </remarks>
/// <example>
///     <code>
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.PlantUml;
///
///     var diagram = PlantUmlDocument.Parse("""
///         @startuml
///         Alice -> Bob : hello
///         Bob --> Alice : ok
///         @enduml
///         """);
///     using var image = diagram.Render(new PlantUmlRenderOptions { Scale = 2 });
///     PngCodec.Save(image, "hello.png");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
