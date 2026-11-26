namespace DemaConsulting.CanvasNet.MathMl;

/// <summary>
///     How an equation is laid out: in a line of text or as a display on its own.
/// </summary>
/// <remarks>
///     The value of the <c>display</c> attribute of the <c>math</c> element (<see cref="MathMlDocument.Display"/>), which
///     <see cref="MathMlRenderOptions.Display"/> can override. A block equation is laid out in display style (MathML Core
///     <c>math-style: normal</c>); an inline equation in the more compact text style (<c>math-style: compact</c>).
/// </remarks>
public enum MathMlDisplay
{
    /// <summary>
    ///     An equation inside a line of text (<c>display="inline"</c>, the default): compact math style.
    /// </summary>
    Inline,

    /// <summary>
    ///     A display equation on its own (<c>display="block"</c>): normal math style, the display style.
    /// </summary>
    Block,
}
