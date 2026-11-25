namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     An annotation of an equation: the text of an <c>annotation</c> element, or the markup of an
///     <c>annotation-xml</c> element, inside <c>semantics</c>, such as the TeX source of the equation.
/// </summary>
/// <remarks>
///     Annotations are never drawn (MathML Core draws only the first child of <c>semantics</c>); they are listed by
///     <see cref="MathMlDocument.Annotations"/> so that a tool can show or reuse them. The record is immutable and safe
///     for concurrent use.
/// </remarks>
/// <param name="Encoding">The value of the <c>encoding</c> attribute (for example <c>application/x-tex</c>), or <see langword="null"/> when it has none.</param>
/// <param name="Text">
///     The text content of an <c>annotation</c> (character references decoded, whitespace kept), or the inner markup of an
///     <c>annotation-xml</c> as XML text.
/// </param>
public sealed record MathMlAnnotation(string? Encoding, string Text);
