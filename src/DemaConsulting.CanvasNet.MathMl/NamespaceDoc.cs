namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     The <c>DemaConsulting.CanvasNet.MathMl</c> namespace renders equations written in MathML Core onto a CanvasNet
///     <c>Surface</c>, statically, offline and deterministically.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="MathMlDocument"/> parses a text (<c>Parse</c>, <c>Open</c>), describes it without drawing it
///         (<see cref="MathMlDocument.Display"/>, <see cref="MathMlDocument.ElementCount"/>,
///         <see cref="MathMlDocument.AltText"/>, <see cref="MathMlDocument.Annotations"/>), measures it with its baseline
///         and renders it. The token elements, <c>mrow</c> and the grouping elements are drawn, with the automatic italic
///         of a single-character <c>mi</c> and every <c>mathvariant</c>; every construct that is recognized but not drawn
///         is reported with a stable <c>mathml-*</c> code and a location (<see cref="MathMlUnsupportedFeature"/>), and
///         strict mode (<see cref="MathMlRenderOptions.ThrowOnUnsupportedFeature"/>) turns the first one into an
///         exception. Untrusted text is bounded by <see cref="MathMlLimits"/>.
///     </para>
///     <para>
///         The layout follows MathML Core over the OpenType MATH table of the bundled Noto Sans Math font, so a rendering is
///         the same on every machine. The library never uses the network. The package is not affiliated with the W3C; its
///         tables of named character references and of the italic mappings are derived from W3C documents, and its
///         <c>mathvariant</c> tables from the Unicode Character Database (see the package's third-party notices).
///     </para>
/// </remarks>
/// <example>
///     <code>
///     using DemaConsulting.CanvasNet.Codecs;
///     using DemaConsulting.CanvasNet.MathMl;
///
///     var equation = MathMlDocument.Parse("""
///         &lt;math display="block"&gt;
///           &lt;mi&gt;E&lt;/mi&gt;&lt;mo&gt;=&lt;/mo&gt;&lt;mi&gt;m&lt;/mi&gt;&lt;mo&gt;&amp;InvisibleTimes;&lt;/mo&gt;&lt;mi&gt;c&lt;/mi&gt;
///         &lt;/math&gt;
///         """);
///     using var image = equation.Render(new MathMlRenderOptions { FontSize = 32 });
///     PngCodec.Save(image, "equation.png");
///     </code>
/// </example>
internal static class NamespaceDoc
{
}
