using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     Options that control one call of <see cref="MermaidDocument.Measure(MermaidRenderOptions?, CancellationToken)"/>
///     or one of the <c>Render</c> methods of <see cref="MermaidDocument"/>.
/// </summary>
/// <remarks>
///     <para>
///         Every property is validated when it is set (fail-closed) and the instance is immutable after
///         initialization, so an options object can be shared between threads and calls.
///     </para>
///     <para>
///         <b>Configuration precedence.</b> As in Mermaid, the effective configuration is, from lowest to highest: the
///         built-in defaults, <see cref="Theme"/> and <see cref="Config"/> (the site configuration that
///         <c>mermaid.initialize</c> or <c>mmdc -c</c> gives), the <c>config:</c> of the frontmatter, and the
///         <c>%%{init: ...}%%</c> directives of the text. The text cannot change the <c>secure</c> keys.
///     </para>
///     <para>
///         <b>Reporting</b> is uniform: warnings go to <see cref="WarningHandler"/> and constructs that are recognized but
///         not supported go to <see cref="UnsupportedFeatureHandler"/>, once, in the order they were found, on the
///         thread that made the call, before the method returns or throws. <see cref="ThrowOnUnsupportedFeature"/> turns
///         the first unsupported construct into a <see cref="MermaidUnsupportedFeatureException"/>.
///     </para>
/// </remarks>
public sealed class MermaidRenderOptions
{
    /// <summary>The largest accepted <see cref="Config"/> text in UTF-16 characters.</summary>
    private const int MaxConfigLength = 1_048_576;

    /// <summary>The nesting depth that a <see cref="Config"/> text may have.</summary>
    private const int MaxConfigDepth = 64;

    /// <summary>The names of Mermaid's built-in themes, in Mermaid's documentation order.</summary>
    private static readonly ReadOnlyCollection<string> ThemeNameList = new(["default", "base", "dark", "forest", "neutral"]);

    /// <summary>The backing field of <see cref="Scale"/>.</summary>
    private readonly double _scale = 1;

    /// <summary>The backing field of <see cref="Config"/>.</summary>
    private readonly string? _config;

    /// <summary>The backing field of <see cref="Theme"/>.</summary>
    private readonly string? _theme;

    /// <summary>The backing field of <see cref="Limits"/>.</summary>
    private readonly MermaidLimits _limits = MermaidLimits.Default;

    /// <summary>
    ///     Gets the names of Mermaid's built-in themes that <see cref="Theme"/> accepts: <c>default</c>, <c>base</c>,
    ///     <c>dark</c>, <c>forest</c> and <c>neutral</c>.
    /// </summary>
    /// <remarks>
    ///     Hosts use the list to validate a theme that comes from a user (a command-line option, a link fragment) before
    ///     they build the options. The list is read-only and shared.
    /// </remarks>
    public static IReadOnlyList<string> ThemeNames => ThemeNameList;

    /// <summary>
    ///     Gets the factor by which the natural size of the diagram is multiplied; 1 by default.
    /// </summary>
    /// <remarks>
    ///     The natural size is the size of Mermaid's SVG view box (the bounds of the drawing plus the diagram padding). The
    ///     scale is applied to the whole diagram, text included, so a larger scale yields a larger, equally sharp image
    ///     rather than a magnified one. The size that results (the natural size times the scale, rounded to whole pixels)
    ///     must lie within 1 and <see cref="Surface.MaxDimension"/> on both sides; this is checked when the document is
    ///     measured or rendered. The scale is not used by the
    ///     <see cref="MermaidDocument.Render(int, int, MermaidRenderOptions?, CancellationToken)"/> overload, whose explicit
    ///     size decides the scale.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown on initialization when the value is not finite, is not greater than zero, or exceeds
    ///     <see cref="Surface.MaxDimension"/>.
    /// </exception>
    public double Scale
    {
        get => _scale;
        init
        {
            if (!double.IsFinite(value) || value <= 0 || value > Surface.MaxDimension)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Scale),
                    value,
                    string.Create(CultureInfo.InvariantCulture, $"The scale must be a finite number greater than zero and at most {Surface.MaxDimension}."));
            }

            _scale = value;
        }
    }

    /// <summary>
    ///     Gets the color the surface is filled with before the diagram is drawn, or <see langword="null"/> for a
    ///     transparent background (the default, as Mermaid's SVG is transparent).
    /// </summary>
    public Rgba32? Background { get; init; }

    /// <summary>
    ///     Gets the current time a gantt chart takes as now, or <see langword="null"/> (the default) for none.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Mermaid reads the clock of the viewer for a gantt chart's today marker, for an <c>after</c> or <c>until</c>
    ///         id that no task has (today at midnight) and for a date format without a day (today's date). This is the only
    ///         clock of the library, which never reads the system clock, so a render is the same every time: with a value,
    ///         its UTC date and time are today (gantt dates are in UTC); without one, the today marker is not drawn and
    ///         1970-01-01 is taken as today, reported as <see cref="MermaidDiagnosticCodes.GanttTodayUnavailable"/> where
    ///         it matters.
    ///     </para>
    /// </remarks>
    public DateTimeOffset? Now { get; init; }

    /// <summary>
    ///     Gets a Mermaid site configuration (a JSON object, such as the argument of <c>mermaid.initialize</c> or the file
    ///     of <c>mmdc -c</c>), or <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    ///     This is how a host sets a theme, fonts or flowchart spacing for every diagram without editing them. The text is
    ///     only checked for being a JSON object when it is set; unknown keys and values of the wrong type are reported as
    ///     warnings when the document is measured or rendered.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown on initialization when the text is not a JSON object, is nested too deeply or is too long.</exception>
    public string? Config
    {
        get => _config;
        init => _config = ValidateConfig(value, nameof(Config));
    }

    /// <summary>
    ///     Gets the site-level theme (one of <see cref="ThemeNames"/>, like <c>mmdc -t</c>), or <see langword="null"/>
    ///     for Mermaid's <c>default</c> theme.
    /// </summary>
    /// <remarks>
    ///     The theme is applied at site level, so a <c>theme</c> that the text sets in its frontmatter or a directive still
    ///     wins, exactly as with <c>mmdc -t</c>. A <c>theme</c> in <see cref="Config"/> overrides this property.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is not one of <see cref="ThemeNames"/>.</exception>
    public string? Theme
    {
        get => _theme;
        init
        {
            if (value is not null && !ThemeNameList.Contains(value, StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"The theme '{value}' is not a Mermaid theme; use one of {string.Join(", ", ThemeNameList)}.",
                    nameof(Theme));
            }

            _theme = value;
        }
    }

    /// <summary>
    ///     Gets the resolver that loads the pictures of the diagram (<c>@{ img: ... }</c> nodes and <c>&lt;img&gt;</c> in
    ///     labels), or <see langword="null"/> for none (the default).
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>No implicit file access.</b> The library never opens a file or contacts a network for a picture on its
    ///         own: the link is handed to this resolver, as text, and to nothing else. Without a resolver no picture is drawn
    ///         and the warning <see cref="MermaidDiagnosticCodes.ImageUnresolved"/> is reported. The policy of what a link may
    ///         refer to is the resolver's: <see cref="FolderImageResolver"/> confines it to a folder and understands every
    ///         format of the CanvasNet libraries. The text of the link is untrusted, so a resolver that touches the file
    ///         system or the network must confine it.
    ///     </para>
    ///     <para>
    ///         The resolver is called on the calling thread; <see cref="MermaidLimits.MaxImages"/> bounds the number of
    ///         calls of one rendering. The diagram disposes every surface it was given when the call ends.
    ///     </para>
    /// </remarks>
    public IImageResolver? ImageResolver { get; init; }

    /// <summary>
    ///     Gets the resource limits of the call; <see cref="MermaidLimits.Default"/> by default.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value is <see langword="null"/>.</exception>
    public MermaidLimits Limits
    {
        get => _limits;
        init => _limits = value ?? throw new ArgumentNullException(nameof(Limits));
    }

    /// <summary>
    ///     Gets the callback that receives warnings, or <see langword="null"/> to drop them.
    /// </summary>
    /// <remarks>
    ///     Invoked on the calling thread, in order, after the work finished and before the method returns. When the call
    ///     fails, the reports recorded before the failure are still delivered but an exception thrown by the handler is
    ///     ignored so that the original failure is the one reported.
    /// </remarks>
    public Action<MermaidDiagnostic>? WarningHandler { get; init; }

    /// <summary>
    ///     Gets the callback that receives constructs that are recognized but not supported, or <see langword="null"/> to
    ///     drop them.
    /// </summary>
    /// <remarks>Delivered like <see cref="WarningHandler"/>.</remarks>
    public Action<MermaidUnsupportedFeature>? UnsupportedFeatureHandler { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the first unsupported construct aborts the call with a
    ///     <see cref="MermaidUnsupportedFeatureException"/>; <see langword="false"/> by default.
    /// </summary>
    /// <remarks>
    ///     Set this for builds that must never accept an approximate diagram. The report that caused the exception is
    ///     delivered to <see cref="UnsupportedFeatureHandler"/> first.
    /// </remarks>
    public bool ThrowOnUnsupportedFeature { get; init; }

    /// <summary>
    ///     Validates a configuration text.
    /// </summary>
    /// <param name="value">The text, or null.</param>
    /// <param name="propertyName">The property name for exceptions.</param>
    /// <returns>The text, unchanged.</returns>
    /// <exception cref="ArgumentException">Thrown when the text is not a JSON object, is nested too deeply or is too long.</exception>
    private static string? ValidateConfig(string? value, string propertyName)
    {
        if (value is null)
        {
            return null;
        }

        // A configuration is merged over defaults, so only a JSON object makes sense; check it at the door
        if (value.Length > MaxConfigLength)
        {
            throw new ArgumentException("The configuration text is too long.", propertyName);
        }

        try
        {
            using var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = MaxConfigDepth });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException("The configuration must be a JSON object.", propertyName);
            }
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("The configuration is not valid JSON: " + ex.Message, propertyName, ex);
        }

        return value;
    }
}
