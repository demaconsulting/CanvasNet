using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.PlantUml.Tests;

/// <summary>
///     Colors used by the tests.
/// </summary>
internal static class SkinColors
{
    /// <summary>Black.</summary>
    public static readonly Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>The participant fill of PlantUML's default look.</summary>
    public static readonly Rgba32 GreyBlue = new(0xE2, 0xE2, 0xF0, 255);

    /// <summary>The note fill of PlantUML's default look.</summary>
    public static readonly Rgba32 Note = new(0xFE, 0xFF, 0xDD, 255);
}
