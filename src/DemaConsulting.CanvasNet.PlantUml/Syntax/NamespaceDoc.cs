namespace DemaConsulting.CanvasNet.PlantUml.Syntax;

/// <summary>
///     The <c>Syntax</c> namespace is the first stage of the pipeline: it turns a PlantUML text into the statements that
///     the diagram parsers read, with every position mapped back to the original text, and detects the diagram type.
/// </summary>
/// <remarks>
///     <para>
///         <c>Preprocessor</c> finds the first <c>@start</c> ... <c>@end</c> block, removes the comments, joins continued
///         lines and applies the supported subset of PlantUML's preprocessor (<c>!define</c> without parameters,
///         <c>!undef</c> and <c>!$variable</c>), reporting every other directive; <c>SourceLines</c> and
///         <c>SourceStatement</c> keep the original offset of every character; <c>DiagramDetector</c> names the type of
///         the diagram from its tag and its statements, in the order PlantUML's diagram factories try them.
///     </para>
///     <para>
///         No regular expression runs on diagram text, so the preprocessing and the detection are linear in the text;
///         nothing is read from the network, and a local file only for an include directive, through the core file
///         reference resolver under the caller's file-access policy. The namespace is internal to the package; every
///         type is immutable or used by one call on one thread.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
