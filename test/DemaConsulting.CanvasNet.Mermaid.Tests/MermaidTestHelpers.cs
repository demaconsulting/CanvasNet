using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Mermaid.Syntax;

namespace DemaConsulting.CanvasNet.Mermaid.Tests;

/// <summary>
///     Shared helpers of the Mermaid tests: report collection, mapped texts and temporary files.
/// </summary>
internal static class MermaidTestHelpers
{
    /// <summary>A small pie chart, a known type that this version does not draw.</summary>
    public const string PieText = "pie\n    \"a\" : 1\n";

    /// <summary>A small flowchart, the supported type.</summary>
    public const string FlowchartText = "flowchart LR\n    A --> B\n";

    /// <summary>
    ///     Creates render options that collect every report.
    /// </summary>
    /// <param name="strict">Whether the first unsupported feature throws.</param>
    /// <param name="background">The background, or null.</param>
    /// <returns>The options and the lists the reports go to.</returns>
    public static (MermaidRenderOptions Options, List<MermaidDiagnostic> Warnings, List<MermaidUnsupportedFeature> Unsupported) Collecting(
        bool strict = false,
        Rgba32? background = null)
    {
        var warnings = new List<MermaidDiagnostic>();
        var unsupported = new List<MermaidUnsupportedFeature>();
        var options = new MermaidRenderOptions
        {
            WarningHandler = warnings.Add,
            UnsupportedFeatureHandler = unsupported.Add,
            ThrowOnUnsupportedFeature = strict,
            Background = background,
        };
        return (options, warnings, unsupported);
    }

    /// <summary>
    ///     Creates the identity mapped text of a string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="sourceName">The source name, or null.</param>
    /// <returns>The mapped text.</returns>
    public static MappedText Mapped(string text, string? sourceName = null) => MappedText.Identity(new SourceLines(text, sourceName));

    /// <summary>
    ///     Gets whether every pixel of a surface is fully transparent.
    /// </summary>
    /// <param name="surface">The surface.</param>
    /// <returns>Whether it is.</returns>
    public static bool IsFullyTransparent(Surface surface)
    {
        for (var y = 0; y < surface.Height; y++)
        {
            foreach (var pixel in surface.GetRowSpan(y))
            {
                if (pixel.A != 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    ///     Gets whether two surfaces have the same size and identical pixels.
    /// </summary>
    /// <param name="a">The first surface.</param>
    /// <param name="b">The second surface.</param>
    /// <returns>Whether they are identical.</returns>
    public static bool PixelsEqual(Surface a, Surface b)
    {
        if (a.Width != b.Width || a.Height != b.Height)
        {
            return false;
        }

        for (var y = 0; y < a.Height; y++)
        {
            if (!a.GetRowSpan(y).SequenceEqual(b.GetRowSpan(y)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Writes a temporary file and returns its path.
    /// </summary>
    /// <param name="content">The bytes.</param>
    /// <param name="extension">The extension, with the dot.</param>
    /// <returns>The path; the caller deletes it.</returns>
    public static string WriteTempFile(byte[] content, string extension = ".mmd")
    {
        var path = Path.Combine(Path.GetTempPath(), $"canvasnet-mermaid-{Guid.NewGuid():N}{extension}");
        File.WriteAllBytes(path, content);
        return path;
    }
}
