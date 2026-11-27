namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Viewer.Help"/> namespace holds the help about the drawing and file
///     formats as data, so a host can give it to a person or an agent: what to write in a file, how to make a picture
///     look good, how files compose, what is reported, the limits, and what the host controls.
/// </summary>
/// <remarks>
///     <para>
///         The cards are read and shown by DemaConsulting.HelpCards. <see cref="CanvasNetHelp.Catalog"/> is the catalog
///         of the library cards; each card has a Markdown body, tags from <see cref="HelpTags"/>, children, related
///         cards, and tiny example files. Its <c>Filter</c> gives a view with only some tags (a link to a hidden card
///         becomes plain text), <c>Find</c> resolves a path, and <c>Render</c> writes a card as text, Markdown, or JSON.
///         <see cref="CanvasNetHelp.Source"/> is the source a host imports to layer its own cards on the library's, and
///         <see cref="CanvasNetHelp.Configure"/> adds the library cards to a catalog builder.
///     </para>
///     <para>Every type is immutable and safe to use from several threads.</para>
/// </remarks>
/// <example>
///     Give an agent the Mermaid authoring and composition knowledge, without what the host controls:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Viewer.Help;
///     using DemaConsulting.HelpCards.Presentation;
///
///     var view = CanvasNetHelp.Catalog.Filter([HelpTags.Mermaid], [HelpTags.Hosting]);
///     foreach (var root in view.Roots)
///     {
///         Console.WriteLine($"{root.Id}: {root.Summary}");
///     }
///
///     var gantt = view.Find("mermaid/gantt");
///     if (gantt != null)
///     {
///         Console.WriteLine(view.Render(gantt, new HelpRenderOptions { Format = HelpFormat.Text }));
///     }
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
