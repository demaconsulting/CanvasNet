namespace DemaConsulting.CanvasNet.Viewer.Help;

/// <summary>
///     The <see cref="DemaConsulting.CanvasNet.Viewer.Help"/> namespace holds the help about the drawing and file
///     formats as data, so a host can give it to a person or an agent: what to write in a file, how to make a picture
///     look good, how files compose, what is reported, the limits, and what the host controls.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="HelpCatalog.Default"/> is the catalog of the library cards. Each <see cref="HelpCard"/> has a
///         Markdown body, tags from <see cref="HelpTags"/>, children, related cards, and tiny
///         <see cref="HelpExample"/> files. <see cref="HelpCatalog.Filter"/> gives a view with only some tags (a link to
///         a hidden card becomes plain text), <see cref="HelpCatalog.Find"/> resolves a path, and
///         <see cref="HelpCatalog.RenderText"/> and <see cref="HelpCatalog.RenderJson"/> write a card.
///         <see cref="HelpCatalog.FormatMarkdown"/> writes the in-text links (<see cref="HelpLink"/>) in a host's own
///         words, and <see cref="HelpCatalog.Extend"/> layers a host's cards on the library's.
///     </para>
///     <para>Every type is immutable and safe to use from several threads.</para>
/// </remarks>
/// <example>
///     Give an agent the Mermaid authoring and composition knowledge, without what the host controls:
///     <code language="csharp">
///     using DemaConsulting.CanvasNet.Viewer.Help;
///
///     var view = HelpCatalog.Default.Filter([HelpTags.Mermaid], [HelpTags.Hosting]);
///     foreach (var root in view.Roots)
///     {
///         Console.WriteLine($"{root.Id}: {root.Summary}");
///     }
///
///     var gantt = view.Find("mermaid/gantt");
///     if (gantt != null)
///     {
///         Console.WriteLine(view.RenderText(gantt));
///     }
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
