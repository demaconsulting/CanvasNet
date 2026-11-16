namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Tool.PandocFilter"/> namespace is the Pandoc filter of the tool: it draws the
///     models, charts, diagrams, drawings, and document pages of a Pandoc document in one process, for the Lua shim
///     <c>docs/filters/canvasnet.lua</c> (the <c>pandoc-filter</c> command).
/// </summary>
/// <remarks>
///     <see cref="PandocFilterEngine"/> runs the filter over a document read and written by <see cref="PandocAst"/>. Each
///     picture's attributes are read by <see cref="PictureAttributes"/>, its source found by <see cref="FilterPaths"/>, its
///     files hashed by <see cref="DependencyHasher"/>, and its key and cache files named by <see cref="PictureCache"/>;
///     <see cref="FilterSettings"/> holds the inputs of the run and the environment variables, and
///     <see cref="ToolPictureDrawer"/> draws a picture with the <c>render</c> and <c>flatten</c> commands.
///     <see cref="PandocFilterShim"/> is the embedded shim and its protocol. The logic is a literal port of the Lua filter
///     (version 16), so a document is drawn, named, and reported as before.
/// </remarks>
internal static class NamespaceDoc
{
}
