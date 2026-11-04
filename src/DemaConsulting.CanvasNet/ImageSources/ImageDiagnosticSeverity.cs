namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     How serious an <see cref="ImageSourceDiagnostic"/> is.
/// </summary>
public enum ImageDiagnosticSeverity
{
    /// <summary>Information only; the picture is complete.</summary>
    Info,

    /// <summary>Something was wrong or ignored, but a picture was still drawn (or, for an unresolved link, nothing was drawn).</summary>
    Warning,

    /// <summary>The file uses a feature the source does not draw; the picture is drawn without it.</summary>
    Unsupported
}
