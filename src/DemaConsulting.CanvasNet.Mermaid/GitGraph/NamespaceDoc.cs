namespace DemaConsulting.CanvasNet.Mermaid.GitGraph;

/// <summary>
///     The <c>GitGraph</c> namespace reads and lays out git graphs (<c>gitGraph</c>): a port of the Langium git graph
///     grammar of <c>@mermaid-js/parser</c>, of Mermaid's git graph database (<c>gitGraphAst.ts</c> and the
///     <c>populate</c> of <c>gitGraphParser.ts</c>) and of its git graph renderer (<c>gitGraphRenderer.ts</c>).
/// </summary>
/// <remarks>
///     <para>
///         <c>GitGraphParser</c> lexes and parses the preprocessed text into statements, and runs them as Mermaid's
///         database runs them (<c>GitGraphDatabase</c>): commits, branches, checkouts, merges and cherry-picks with
///         Mermaid's errors, the commit ids Mermaid generates (from a fixed pseudo-random sequence), the title and the
///         accessibility texts, into a <c>GitGraphDiagram</c>. A site configuration that renames or reorders the main
///         branch runs the statements again when the diagram is drawn.
///     </para>
///     <para>
///         <c>GitGraphRenderer</c> draws the diagram as Mermaid's <c>draw</c> does: the branch positions, the two passes
///         over the commits (positions, then bullets, labels and tags), the branch lines and labels, the arrows with their
///         rerouting lanes, the title and the view box of the browser's bounding box, with the git graph style sheet.
///     </para>
///     <para>
///         The namespace depends on the <c>Syntax</c> (scanning, sanitizing), <c>Configuration</c> (the gitGraph section
///         and style sheet), <c>Rendering</c> (text metrics), <c>Dagre</c> (the work meter) and <c>Scenegraph</c>
///         namespaces. It is internal to the package. A parsed diagram is never changed after parsing and is safe for
///         concurrent use.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
