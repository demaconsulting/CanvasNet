namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     A size in whole pixels, used to report how large a diagram renders.
/// </summary>
/// <remarks>
///     The value is what <see cref="MermaidDocument.Measure(MermaidRenderOptions?, CancellationToken)"/> returns so a
///     caller can decide on a destination (a page, a slide, a tool's output size) before it renders. The type is an
///     immutable value type with value equality and is safe for concurrent use.
/// </remarks>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
public readonly record struct MermaidSize(int Width, int Height);
