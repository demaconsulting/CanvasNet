namespace DemaConsulting.CanvasNet.MathMl.Syntax;

/// <summary>
///     The <c>DemaConsulting.CanvasNet.MathMl.Syntax</c> namespace turns MathML text into the equation tree, with the
///     location of every element in the original text.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="CharacterReferences"/> rewrites the named character references (the full W3C HTML+MathML set) into
///         numeric ones and keeps the map of the positions; <see cref="MathMlReader"/> reads the result with the hardened
///         XML reader of the core into the immutable <see cref="MathElement"/> tree; <see cref="AttributeValues"/> and
///         <see cref="StyleDeclarations"/> parse the attribute values and the inline <c>style</c> subset that the layout
///         reads.
///     </para>
///     <para>
///         Everything here is internal. The equation tree holds no XML types, so another front end can produce it.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
