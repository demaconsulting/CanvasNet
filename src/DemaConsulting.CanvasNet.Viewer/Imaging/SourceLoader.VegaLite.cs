using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.VegaLite;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     Vega-Lite sources of <see cref="SourceLoader"/>: a chart drawn from its specification.
/// </summary>
/// <remarks>
///     <para>
///         A chart has no pages. Its natural size is the size the library lays the chart out at (<c>Measure</c>), and
///         <c>--scale</c>, <c>--width</c>, and <c>--height</c> are applied to it like they are to any vector source: the
///         chart is drawn sharply at the target size, fitted inside it and centered when both sides are given. The
///         background is the one of the chart (white when it has none) unless <c>--background</c> replaces it.
///     </para>
///     <para>
///         A render is static and offline: the specification is the only thing read from the file system apart from
///         the data files the library itself resolves beside it, no network is used, dates are in UTC, and only the
///         fonts bundled with the library are used, so the same chart gives the same pixels on every machine. A
///         construct the library does not draw (yet) is returned as a diagnostic (<c>vega-lite</c>, <c>unsupported</c>,
///         with its stable <c>vl-*</c> identifier as the code); with <see cref="SourceOptions.Strict"/> it is an
///         <c>unsupported-feature</c> error instead. A problem with the specification or with the chart is a
///         <c>vega-lite-error</c>; this includes a specification that nests deeper than the parser limit or has a
///         string longer than the string limit, as the SCAD parser's nesting limit is a <c>scad-error</c>.
///         A limit that the library applies while it lays out or draws the chart (rows, ticks, scene items, and so
///         on) is <c>budget-exceeded</c>; a specification file longer than the source limit is <c>corrupt-input</c>, in
///         parity with an overlong SCAD model; and a chart that lays out larger than the maximum image size is
///         <c>size-limit</c>.
///     </para>
///     <para>
///         <b>Pictures.</b> The <c>image</c> marks of a chart show files that lie beside it: the loader gives the library the same sandboxed resolver as it gives an SVG file
///         (<see cref="SvgImages"/>, <see cref="CanvasNetViewer.CreateImageResolver(string, FolderImageResolverOptions?)"/>), rooted at the folder of the chart, so a <c>url</c> may name a raster, an SVG
///         file, a model (<c>part.scad</c>), a page of a document (<c>report.pdf#page=2</c>) or another chart, and nothing outside the folder; a remote URL is never fetched. What
///         the resolver reports becomes the notes of the result (a link that cannot be drawn is the warning <c>svg-image-unresolved</c>, and the library's own
///         <c>vl-mark-image-unresolved</c> for the same link is left out), the files that were drawn are <c>embed</c> operations, and with <see cref="SourceOptions.Strict"/> a link that was not
///         drawn is an <c>unsupported-feature</c> error, exactly as for the pictures of an SVG file. A chart that is itself embedded (<see cref="LoadContext.UseScopedResolver"/>) uses the resolver
///         of the embedding document and leaves the notes to it.
///     </para>
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     The code of a warning of the Vega-Lite library in the diagnostics of a result.
    /// </summary>
    private const string VegaLiteWarningCode = "vega-lite-warning";

    /// <summary>
    ///     Reads what a chart declares and the size it is laid out at, by parsing and measuring it; the chart is not
    ///     drawn.
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <param name="bytes">The size of the file in bytes.</param>
    /// <returns>
    ///     The facts about the file: the natural size of the chart, 4 channels, and the marks, data references,
    ///     parameters, title, and schema version of the specification.
    /// </returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>vega-lite-error</c> when the specification or the chart is not valid, <c>budget-exceeded</c>
    ///     when a limit is exceeded, and <c>size-limit</c> when the chart is larger than <see cref="Surface.MaxDimension"/>.
    /// </exception>
    private static SourceInfo ProbeVegaLite(SourceInput input, long bytes)
    {
        var path = input.Name;
        var document = Read(path, SourceKind.VegaLite, () => OpenVegaLite(input));
        var size = Read(path, SourceKind.VegaLite, () => MeasureChart(() => document.Measure(CreateVegaLiteOptions(new SourceOptions(), null)), path));
        var chart = new ChartFacts(
            [.. document.MarkTypes],
            [.. document.DataReferences.Select(r => new ChartDataReference(r.Kind.ToString().ToLowerInvariant(), r.Url, r.RowCount, r.Location.Line))],
            [.. document.ParameterNames],
            document.Title,
            document.SchemaMajorVersion);
        return new SourceInfo(path, SourceKind.VegaLite, bytes, size.Width, size.Height, 4, true, true, 1, 1, null, [], [], [])
        {
            Chart = chart
        };
    }

    /// <summary>
    ///     Parses a chart from its source: the name of the source is the name of the library's messages, and the folder
    ///     of the source is where the data files of the chart are looked up (nowhere else).
    /// </summary>
    /// <param name="input">Where the bytes of the file come from.</param>
    /// <returns>The parsed chart.</returns>
    private static VegaLiteDocument OpenVegaLite(SourceInput input)
    {
        using var stream = input.Open();
        return VegaLiteDocument.Open(stream, new VegaLiteOpenOptions { BaseDirectory = input.Directory, SourceName = input.Name });
    }

    /// <summary>
    ///     Measures a chart.
    /// </summary>
    /// <param name="measure">The measure of the chart, which gives its natural size.</param>
    /// <param name="path">The path of the file, for messages.</param>
    /// <returns>The natural size of the chart.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>size-limit</c> when the chart lays out larger than the maximum image size, and
    ///     <c>vega-lite-error</c> when the library rejects the measure for any other argument failure.
    /// </exception>
    internal static VegaLiteSize MeasureChart(Func<VegaLiteSize> measure, string path)
    {
        try
        {
            return measure();
        }
        catch (ArgumentOutOfRangeException ex) when (ex.ParamName == "options")
        {
            // The library refuses a natural size outside 1 to Surface.MaxDimension on a side and reports it for the
            // options (their scale); this is the only argument failure that means "too large"
            throw new ViewerException(
                ErrorCodes.SizeLimit,
                $"'{path}' lays out larger than the maximum of {Surface.MaxDimension} pixels per side: {OneLine(ex.Message)}",
                "Reduce the width and height of the chart.",
                ex);
        }
        catch (ArgumentException ex)
        {
            // Any other argument failure is a chart the library cannot lay out, never a size problem
            throw new ViewerException(ErrorCodes.VegaLiteError, $"Vega-Lite measure error: {OneLine(ex.Message)}", null, ex);
        }
    }

    /// <summary>
    ///     Draws a chart, mapping the failures of the library to a <see cref="ViewerException"/>.
    /// </summary>
    /// <param name="render">The render of the chart.</param>
    /// <param name="path">The path of the file, for messages.</param>
    /// <returns>The surface; the caller owns it.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown for the allow-listed failures of <see cref="Read{T}"/>, and with code <c>vega-lite-error</c> for an
    ///     argument failure of the library: the target size was validated before the render, so such a failure is a
    ///     chart the library cannot draw and never a usage error.
    /// </exception>
    internal static Surface RenderChart(Func<Surface> render, string path)
    {
        try
        {
            return Read(path, SourceKind.VegaLite, render);
        }
        catch (ArgumentException ex)
        {
            throw new ViewerException(ErrorCodes.VegaLiteError, $"Vega-Lite render error: {OneLine(ex.Message)}", null, ex);
        }
    }

    /// <summary>
    ///     Creates the options of one measure or render of a chart: the fixed, deterministic ones (UTC, bundled fonts
    ///     only) and the ones the tool options say (background, strictness).
    /// </summary>
    /// <param name="options">The loading options.</param>
    /// <param name="notes">The list the warnings and unsupported features are added to, or <see langword="null"/> to drop them.</param>
    /// <param name="resolver">
    ///     The resolver that loads the pictures of the chart's <c>image</c> marks, or <see langword="null"/> for none: the chart then draws
    ///     no picture and reports <c>vl-mark-image</c>, and a measure never needs one.
    /// </param>
    /// <param name="parameters">The values of the chart's parameters (all of them declared by the chart), or <see langword="null"/> for the defaults.</param>
    /// <returns>The options.</returns>
    private static VegaLiteRenderOptions CreateVegaLiteOptions(
        SourceOptions options,
        List<ImageDiagnostic>? notes,
        IImageResolver? resolver = null,
        IReadOnlyDictionary<string, object?>? parameters = null)
    {
        return new VegaLiteRenderOptions
        {
            Background = options.Background,
            TimeZone = TimeZoneInfo.Utc,
            Fonts = new VegaLiteFontOptions { UseSystemFonts = false },
            Parameters = parameters,
            ImageResolver = resolver,
            WarningHandler = notes == null
                ? null
                : warning => notes.Add(
                    new ImageDiagnostic("vega-lite", "warning", VegaLiteWarningCode, OneLine(warning.ToString()))
                    {
                        Detail = ReadVegaLiteDetail(warning.Message),
                        Location = ToVegaLiteLocation(warning.Location)
                    }),
            UnsupportedFeatureHandler = notes == null
                ? null
                : unsupported => notes.Add(
                    new ImageDiagnostic(
                        "vega-lite",
                        "unsupported",
                        unsupported.Feature,
                        OneLine(unsupported.Location.IsKnown ? $"{unsupported.Location}: {unsupported.Message}" : unsupported.Message))
                    {
                        Location = ToVegaLiteLocation(unsupported.Location)
                    }),
            ThrowOnUnsupportedFeature = options.Strict
        };
    }

    /// <summary>
    ///     Reads the stable <c>vl-*</c> identifier a warning of the Vega-Lite library starts its message with.
    /// </summary>
    /// <param name="message">The message of the warning.</param>
    /// <returns>The identifier (for example <c>vl-text-missing-glyph</c>), or <see langword="null"/> when the message has none.</returns>
    private static string? ReadVegaLiteDetail(string message)
    {
        var colon = message.IndexOf(':', StringComparison.Ordinal);
        if (colon <= 3 || !message.StartsWith("vl-", StringComparison.Ordinal))
        {
            return null;
        }

        var identifier = message[..colon];
        return identifier.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-') ? identifier : null;
    }

    /// <summary>
    ///     Converts the position the Vega-Lite library reports to the location of a note.
    /// </summary>
    /// <param name="location">The position in the specification.</param>
    /// <returns>The location, or <see langword="null"/> when the library does not know it.</returns>
    private static ImageDiagnosticLocation? ToVegaLiteLocation(VegaLiteLocation location)
    {
        if (!location.IsKnown)
        {
            return null;
        }

        int? line = location.Line > 0 ? location.Line : null;
        int? column = location.Column > 0 ? location.Column : null;
        var pointer = string.IsNullOrEmpty(location.Pointer) ? null : location.Pointer;
        return new ImageDiagnosticLocation(location.File, line, column, pointer);
    }

    /// <summary>
    ///     Maps a failure to render a chart that was read successfully to a <see cref="ViewerException"/>.
    /// </summary>
    /// <param name="exception">The failure.</param>
    /// <returns>
    ///     The exception to throw: <c>budget-exceeded</c> when the chart exceeded a limit of the library (rows, fields,
    ///     ticks, scene items, and so on), otherwise <c>vega-lite-error</c> (missing or invalid data, an invalid property
    ///     value, and so on).
    /// </returns>
    /// <remarks>
    ///     The library names every limit it enforces in the message, either as <c>the limit of N</c>, as
    ///     <c>the limit (MaxViews)</c>, or with the name of the limit (<c>VegaLiteLimits.MaxSceneItems</c>); a test pins
    ///     the texts, because the library gives no other way to tell a limit from another failure.
    /// </remarks>
    internal static ViewerException MapVegaLiteRender(VegaLiteRenderException exception)
    {
        var message = OneLine(exception.Message);
        if (!IsVegaLiteLimit(message))
        {
            return new ViewerException(ErrorCodes.VegaLiteError, $"Vega-Lite render error: {message}", null, exception);
        }

        return new ViewerException(
            ErrorCodes.BudgetExceeded,
            $"The Vega-Lite chart exceeds a limit: {message}",
            "Simplify the chart or reduce its data; this limit cannot be changed from the command line.",
            exception);
    }

    /// <summary>
    ///     Determines whether a message of the Vega-Lite library reports that a limit was exceeded.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns><see langword="true"/> when the message names a limit.</returns>
    private static bool IsVegaLiteLimit(string message)
    {
        return message.Contains("the limit of ", StringComparison.Ordinal)
               || message.Contains("the limit (Max", StringComparison.Ordinal)
               || message.Contains("VegaLiteLimits.", StringComparison.Ordinal);
    }

    /// <summary>
    ///     A chart opened for drawing.
    /// </summary>
    private sealed class VegaLitePageSource : PageSource
    {
        /// <summary>
        ///     The parsed chart.
        /// </summary>
        private readonly VegaLiteDocument _document;

        /// <summary>
        ///     Initializes a new instance of the <see cref="VegaLitePageSource"/> class.
        /// </summary>
        /// <param name="document">The parsed chart.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="context">The hooks of the caller.</param>
        private VegaLitePageSource(VegaLiteDocument document, SourceInfo info, SourceOptions options, SourceInput input, LoadContext context)
            : base(info, options, input, context)
        {
            _document = document;
        }

        /// <summary>
        ///     Opens a chart.
        /// </summary>
        /// <param name="input">Where the bytes of the file come from.</param>
        /// <param name="info">The facts about the file.</param>
        /// <param name="options">The loading options.</param>
        /// <param name="context">The hooks of the caller.</param>
        /// <returns>The page source.</returns>
        /// <exception cref="ViewerException">Thrown when the file cannot be read or is not a valid specification.</exception>
        public static VegaLitePageSource Open(SourceInput input, SourceInfo info, SourceOptions options, LoadContext context)
        {
            var document = Read(info.Path, SourceKind.VegaLite, () => OpenVegaLite(input));
            return new VegaLitePageSource(document, info, options, input, context);
        }

        /// <inheritdoc />
        protected override LoadedSource LoadPageCore(int page)
        {
            var notes = new List<ImageDiagnostic>();
            var parameters = SelectParameters(notes);

            // A parameter can change the layout (a filter that changes a band scale's step), and the probe measured the chart with
            // the defaults, so a parameterized chart is measured again before the target size is worked out from its natural size
            var info = Info;
            if (parameters is not null)
            {
                var measured = Read(
                    Info.Path,
                    SourceKind.VegaLite,
                    () => MeasureChart(() => _document.Measure(CreateVegaLiteOptions(Options, null, null, parameters)), Info.Path));
                info = Info with { Width = measured.Width, Height = measured.Height };
            }

            var (width, height) = ComputeTargetSize(info.Path, info.Width, info.Height, Options);

            // The pictures of the chart's image marks: through the resolver of the embedding document when there is one, else through a resolver
            // confined to the folder of the chart, which is where the data files of the chart are looked up too
            var scoped = Context.UseScopedResolver;
            var images = scoped ? null : new SvgImages(Info.Path, Options);
            var renderOptions = CreateVegaLiteOptions(Options, notes, scoped ? Context.ScopedResolver : images!.Resolver, parameters);

            // The surface is charged before the chart is drawn, so a chart that cannot be paid for costs nothing
            Context.ChargeSurface(width, height);
            Surface surface;
            try
            {
                surface = RenderChart(() => _document.Render(width, height, renderOptions, Context.CancellationToken), Info.Path);
            }
            catch (ImageSourceException ex) when (images is not null)
            {
                throw images.Map(ex);
            }

            var fields = new List<(string Name, object Value)>
            {
                ("width", width),
                ("height", height),
                ("timeZone", "UTC")
            };
            if (Options.Background is { } background)
            {
                fields.Add(("background", FormatColor(background)));
            }

            if (parameters is not null)
            {
                fields.Add(("params", parameters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={FormatParameterValue(p.Value)}").ToList()));
            }

            var operations = new List<AppliedOperation> { Operation("render", [.. fields]) };
            if (images is null)
            {
                return new LoadedSource(surface, info, 1, true, notes, [], operations);
            }

            try
            {
                // A reference that was not drawn is an error in strict mode, as it is for the pictures of an SVG file
                if (Options.Strict)
                {
                    images.ThrowForUnresolved();
                }

                operations.AddRange(images.Operations());
                notes.AddRange(images.Notes());
                return new LoadedSource(surface, info, 1, true, DropDuplicateUnresolved(notes, images.Resolver.Diagnostics), [.. images.Resolver.Echo], operations);
            }
            catch
            {
                surface.Dispose();
                throw;
            }
        }

        /// <summary>
        ///     Chooses the parameters to draw the chart with: the ones the options set that the chart declares.
        /// </summary>
        /// <param name="notes">The list the warning for each parameter the chart does not declare is added to.</param>
        /// <returns>The values by name, or <see langword="null"/> when the chart is drawn with the defaults of its specification.</returns>
        /// <exception cref="ViewerException">
        ///     Thrown with code <c>vega-lite-error</c> when the options name a parameter the chart does not declare and
        ///     <see cref="ChartOptions.WarnOnUnknownParameters"/> is not set; the message lists the declared names.
        /// </exception>
        private IReadOnlyDictionary<string, object?>? SelectParameters(List<ImageDiagnostic> notes)
        {
            if (Options.VegaLite is not { Parameters.Count: > 0 } chart)
            {
                return null;
            }

            var declared = new HashSet<string>(_document.ParameterNames, StringComparer.Ordinal);
            var unknown = chart.Parameters.Keys.Where(name => !declared.Contains(name)).Order(StringComparer.Ordinal).ToList();
            if (unknown.Count == 0)
            {
                return chart.Parameters;
            }

            var known = declared.Count == 0 ? "the chart declares no parameters" : $"declared: {string.Join(", ", declared.Order(StringComparer.Ordinal))}";
            if (!chart.WarnOnUnknownParameters)
            {
                throw new ViewerException(
                    ErrorCodes.VegaLiteError,
                    $"The chart '{Info.Path}' declares no parameter named {ShowName(unknown[0])} ({known}).",
                    declared.Count == 0 ? "Add a params entry to the specification, or remove the parameter." : "Check the spelling of the parameter name.");
            }

            // A link is not a command line: the chart is drawn without the parameters it does not declare, and says so
            foreach (var name in unknown)
            {
                notes.Add(
                    new ImageDiagnostic(
                        "vega-lite",
                        "warning",
                        VegaLiteImageSource.UnknownParameterCode,
                        $"the fragment parameter {ShowName(VegaLiteImageSource.ParameterPrefix + name)} names no parameter of the chart and is ignored ({known})"));
            }

            var kept = chart.Parameters.Where(p => declared.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            return kept.Count == 0 ? null : kept;
        }
    }

    /// <summary>
    ///     Shows the name of a parameter in a message: quoted, without control characters, and cut at 40 characters.
    /// </summary>
    /// <param name="name">The name, which can come from the fragment of a link.</param>
    /// <returns>The quoted name.</returns>
    private static string ShowName(string name)
    {
        var shown = new string([.. name.Take(40).Select(c => char.IsControl(c) ? '?' : c)]);
        return name.Length > 40 ? $"'{shown}...'" : $"'{shown}'";
    }

    /// <summary>
    ///     Writes the value of a chart parameter for the <c>params</c> field of the render operation: JSON for a structured value.
    /// </summary>
    /// <param name="value">The value: <see langword="null"/>, a boolean, a number, a string, a list or a dictionary.</param>
    /// <returns>The text, for example <c>5</c>, <c>"EU"</c> or <c>{"x":[2,6]}</c>.</returns>
    private static string FormatParameterValue(object? value)
    {
        return value switch
        {
            null => "null",
            bool flag => flag ? "true" : "false",
            string text => System.Text.Json.JsonSerializer.Serialize(text, ParameterTextOptions),
            IEnumerable<KeyValuePair<string, object?>> map => "{" + string.Join(
                ",",
                map.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => System.Text.Json.JsonSerializer.Serialize(pair.Key, ParameterTextOptions) + ":" + FormatParameterValue(pair.Value))) + "}",
            System.Collections.IEnumerable list => "[" + string.Join(",", list.Cast<object?>().Select(FormatParameterValue)) + "]",
            IFormattable number => number.ToString("R", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    /// <summary>
    ///     The JSON options that write the text of a parameter without escaping what need not be escaped.
    /// </summary>
    private static readonly System.Text.Json.JsonSerializerOptions ParameterTextOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    ///     Removes the warning of the Vega-Lite library about a picture that the resolver did not give when the resolver has reported the same link itself.
    /// </summary>
    /// <param name="notes">The notes of the render: the library's, then the resolver's.</param>
    /// <param name="reported">The notes of the folder resolver.</param>
    /// <returns>The notes without the library's duplicates.</returns>
    /// <remarks>
    ///     The folder resolver says why it could not draw a link (<c>svg-image-unresolved</c> names the reason: missing file, a path outside the folder, a remote URL); the
    ///     library's <c>vl-mark-image-unresolved</c> only says that nothing came back. When both exist for the same link the resolver's is the one that helps the user.
    ///     A library warning for a link that the resolver did not report (a resolver that returns nothing without a note) stays.
    /// </remarks>
    private static List<ImageDiagnostic> DropDuplicateUnresolved(List<ImageDiagnostic> notes, IReadOnlyList<ImageSourceDiagnostic> reported)
    {
        var unresolvedLinks = reported
            .Where(d => d.Code is FolderImageResolver.UnresolvedCode or FolderImageResolver.TooLargeCode or FolderImageResolver.FragmentInvalidCode or FolderImageResolver.PageOutOfRangeCode)
            .Select(d => UnresolvedMessage.Match(d.Message))
            .Where(m => m.Success)
            .Select(m => m.Groups["href"].Value)
            .ToHashSet(StringComparer.Ordinal);
        return [.. notes.Where(n => !(n.Detail == "vl-mark-image-unresolved" && unresolvedLinks.Any(link => n.Message.Contains($"'{link}'", StringComparison.Ordinal))))];
    }
}
