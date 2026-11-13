namespace DemaConsulting.CanvasNet.Scad;

// cspell:ignore lexer lexing

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Scad"/> namespace is the front end for the OpenSCAD language: it reads
///     OpenSCAD-language source from a string, a stream or a file and produces a validated document
///     (<see cref="ScadDocument"/>) that reports file-scope <c>include</c>/<c>use</c> directives
///     (<see cref="ScadDirective"/>) and the names of file-scope modules and functions, or fails with a
///     <see cref="ScadSyntaxException"/> carrying a precise <see cref="ScadSourceLocation"/>. The document can then be
///     evaluated (<see cref="ScadDocument.Evaluate(ScadEvaluationOptions?, System.Threading.CancellationToken)"/>) under
///     resource limits (<see cref="ScadLimits"/>) to obtain the program's <c>echo</c> output, warnings
///     (<see cref="ScadDiagnostic"/>) and unsupported features (<see cref="ScadUnsupportedFeature"/>), or a
///     <see cref="ScadEvaluationException"/> with the failing location. The same program can be turned into a Render3D
///     scene (<see cref="ScadDocument.BuildScene(ScadRenderOptions?, System.Threading.CancellationToken)"/>) or drawn onto a
///     surface (<see cref="ScadDocument.Render(int, int, ScadRenderOptions?, System.Threading.CancellationToken)"/>) under
///     the options of <see cref="ScadRenderOptions"/>.
/// </summary>
/// <remarks>
///     <para>
///         Open behavior is configured through <see cref="ScadOpenOptions"/> (which also defines the file-access policy for
///         <c>include</c>/<c>use</c>), evaluation through <see cref="ScadEvaluationOptions"/> and rendering through
///         <see cref="ScadRenderOptions"/>. The lexer, the syntax tree, the parser, the evaluator and the geometry builder
///         are internal (the <c>Language</c>, <c>Evaluation</c> and <c>Geometry</c> subsystems); only the document
///         surface is public so that the internals can evolve.
///     </para>
///     <para>
///         Rendering is honest about transparency: a <c>color</c> alpha below 1 is a translucent surface, the
///         <c>%</c> modifier draws a translucent ghost and <c>#</c> a translucent highlight (see
///         <see cref="ScadRenderOptions.GhostColor"/> and <see cref="ScadRenderOptions.HighlightColor"/>).
///     </para>
///     <para>
///         See <see cref="ScadDocument"/> for the entry points (<c>Render</c> and <c>BuildScene</c> evaluate the model
///         themselves, so do not call <c>Evaluate</c> first) and <see cref="ScadRenderOptions"/> for the view; its
///         <see cref="ScadRenderOptions.Camera"/> is a
///         <see cref="DemaConsulting.CanvasNet.Render3D.SceneRendering.Camera"/> and its
///         <see cref="ScadRenderOptions.SceneOptions"/> are
///         <see cref="DemaConsulting.CanvasNet.Render3D.SceneRendering.SceneRenderOptions"/> of the
///         <see cref="DemaConsulting.CanvasNet.Render3D.SceneRendering"/> namespace, and
///         <see cref="ScadDocument.BuildScene(ScadRenderOptions?, System.Threading.CancellationToken)"/> returns a
///         <see cref="DemaConsulting.CanvasNet.Render3D.SceneModel.Scene"/> of
///         <see cref="DemaConsulting.CanvasNet.Render3D.SceneModel"/>. <see cref="ScadImageSource"/> lets an SVG document
///         (see <c>DemaConsulting.CanvasNet.Svg</c>) draw a <c>.scad</c> file through
///         <see cref="DemaConsulting.CanvasNet.ImageSources"/>, and the <c>DemaConsulting.CanvasNet.Viewer</c> package
///         renders models, with anchors, through <c>ImageRenderer</c>. A diagnostic carries a stable
///         <see cref="ScadDiagnostic.Code"/>; branch on it, never on the message.
///     </para>
///     <para>
///         Clean-room provenance: this package was written from general knowledge of the language only;
///         no GPL-licensed OpenSCAD source code, grammar, test corpus or manual text was consulted, copied
///         or paraphrased. Exact parity with any other implementation is a non-goal.
///     </para>
/// </remarks>
/// <example>
///     Open a model, draw it while collecting its <c>echo</c> output and warnings, and save the picture. The includes of
///     the file are found next to it:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.Scad;
///
///     var document = ScadDocument.Open("bracket.scad");
///     var options = new ScadRenderOptions
///     {
///         Evaluation = new ScadEvaluationOptions
///         {
///             EchoHandler = line => Console.WriteLine($"echo: {line}"),
///             WarningHandler = warning => Console.WriteLine($"{warning.Code}: {warning.Message}"),
///         },
///     };
///     using var image = document.Render(800, 600, options);   // evaluates the model itself
///     PngCodec.Save(image, "bracket.png");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
