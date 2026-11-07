using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     Options that control one call of <see cref="VegaLiteDocument.Measure(VegaLiteRenderOptions?, CancellationToken)"/>
///     or one of the <c>Render</c> methods of <see cref="VegaLiteDocument"/>.
/// </summary>
/// <remarks>
///     <para>
///         Every property is validated when it is set (fail-closed) and the instance is immutable after
///         initialization, so an options object can be shared between threads and calls. Dictionaries and lists are
///         copied defensively and exposed read-only, so later changes to the caller's collections have no effect.
///     </para>
///     <para>
///         Reporting is uniform: warnings go to <see cref="WarningHandler"/> and constructs that are recognized but
///         not supported go to <see cref="UnsupportedFeatureHandler"/>, once, in the order they were found, on the
///         thread that made the call, before the method returns or throws. <see cref="ThrowOnUnsupportedFeature"/>
///         turns the first unsupported construct into a <see cref="VegaLiteUnsupportedFeatureException"/>.
///     </para>
/// </remarks>
public sealed class VegaLiteRenderOptions
{
    /// <summary>The largest accepted <see cref="Config"/> text in UTF-16 characters, matching the default source limit.</summary>
    private const int MaxConfigLength = 4_194_304;

    /// <summary>The nesting depth that a <see cref="Config"/> text may have.</summary>
    private const int MaxConfigDepth = 64;

    /// <summary>The nesting depth that a structured <see cref="Parameters"/> value may have.</summary>
    private const int MaxParameterDepth = 32;

    /// <summary>The number of values that one entry of <see cref="Parameters"/> may hold, nested values included.</summary>
    private const int MaxParameterItems = 65_536;

    /// <summary>The backing field of <see cref="Scale"/>.</summary>
    private readonly double _scale = 1;

    /// <summary>The backing field of <see cref="Config"/>.</summary>
    private readonly string? _config;

    /// <summary>The backing field of <see cref="Parameters"/>.</summary>
    private readonly IReadOnlyDictionary<string, object?>? _parameters;

    /// <summary>The backing field of <see cref="Datasets"/>.</summary>
    private readonly IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? _datasets;

    /// <summary>The backing field of <see cref="ContainerWidth"/>.</summary>
    private readonly int _containerWidth = 400;

    /// <summary>The backing field of <see cref="ContainerHeight"/>.</summary>
    private readonly int _containerHeight = 300;

    /// <summary>The backing field of <see cref="Limits"/>.</summary>
    private readonly VegaLiteLimits _limits = VegaLiteLimits.Default;

    /// <summary>
    ///     Gets the factor by which the natural size of the chart is multiplied; 1 by default.
    /// </summary>
    /// <remarks>
    ///     The scale is applied to the whole chart, text included, so a larger scale yields a larger, equally sharp
    ///     image rather than a magnified one. The size that results (the natural size times the scale, rounded to a
    ///     whole number of pixels) must lie within 1 and <see cref="Surface.MaxDimension"/> on both sides; this is
    ///     checked when the document is measured or rendered, before any other work. The scale is not used by the
    ///     <see cref="VegaLiteDocument.Render(int, int, VegaLiteRenderOptions?, CancellationToken)"/> overload, whose
    ///     explicit size decides the scale.
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
    ///     Gets the background color that replaces the background of the specification and of the configuration, or
    ///     <see langword="null"/> to use them (white when neither defines one).
    /// </summary>
    public Rgba32? Background { get; init; }

    /// <summary>
    ///     Gets a Vega-Lite configuration (a JSON object, such as the <c>config</c> property of a specification) that
    ///     is merged over the built-in defaults and the configuration of the specification, like the
    ///     <c>config</c> option of a Vega-Lite embedding; <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    ///     This is how a host applies a theme without editing the specification. The text is only checked for being a
    ///     JSON object when it is set; unknown keys are the business of the configuration stage.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown on initialization when the text is not a JSON object, is nested too deeply or is too long.</exception>
    public string? Config
    {
        get => _config;
        init => _config = ValidateConfig(value, nameof(Config));
    }

    /// <summary>
    ///     Gets values that replace the default values of the named parameters (<c>params</c>) of the
    ///     specification, or <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         A value is <see langword="null"/>, a <see cref="bool"/>, a <see cref="string"/>, a finite number of any
    ///         built-in numeric type, a <see cref="DateTime"/> or a <see cref="DateTimeOffset"/>, or a structured value: a sequence (an array or a list) of values, or a
    ///         dictionary with string keys (<see cref="IReadOnlyDictionary{TKey, TValue}"/> or <see cref="IDictionary{TKey, TValue}"/> of <see langword="string"/> and
    ///         <see langword="object"/>) of values, nested up to 32 levels and holding at most 65,536 values in all. A structured value is how a caller gives a
    ///         parameter that holds an array (the domain of a scale, <c>[2, 6]</c>) or an object. The value replaces the whole definition of the parameter: its <c>value</c>
    ///         and its <c>expr</c> (the expression is not evaluated). The names are validated
    ///         against <see cref="VegaLiteDocument.ParameterNames"/> when the document is measured or rendered. The
    ///         dictionary, and every sequence and dictionary in it, is copied when it is set.
    ///     </para>
    ///     <para>
    ///         The value of a <b>selection</b> parameter (<c>"select"</c>) replaces its <c>value</c>, the initial state of the selection, and has the same shapes: an interval selection takes a
    ///         map from the encodings (or fields) to a range (<c>{"x": [2, 6]}</c>), a point selection a map from the fields to a value, a list of such maps (several values) or a single value
    ///         for every field; a <see cref="DateTime"/> is the instant of a date, read as given. A <see langword="null"/> value clears the initial state: the selection is empty, so a
    ///         condition or filter with the default <c>empty</c> rule selects everything. The selection is applied as the specification's own <c>value</c> would be: an interval selection
    ///         draws its brush from the replaced value (none for a <see langword="null"/> value), and a scale whose <c>domain</c> is the selection, a bin whose <c>extent</c> is, or an interval selection
    ///         bound to the scales takes the replaced range.
    ///     </para>
    /// </remarks>
    /// <example>
    ///     A caller that fixes the x domain of a chart whose scale reads the parameter <c>window</c> (<c>"domain": {"param": "window"}</c>):
    ///     <code language="csharp">
    ///     var options = new VegaLiteRenderOptions
    ///     {
    ///         Parameters = new Dictionary&lt;string, object?&gt; { ["window"] = new object[] { 2, 6 } },
    ///     };
    ///     using var surface = document.Render(options);
    ///     </code>
    ///     A caller that selects the range 2 to 6 of the interval selection <c>brush</c> (<c>"select": {"type": "interval", "encodings": ["x"]}</c>), whose conditions then
    ///     style the marks inside it and whose brush is drawn over that range:
    ///     <code language="csharp">
    ///     var options = new VegaLiteRenderOptions
    ///     {
    ///         Parameters = new Dictionary&lt;string, object?&gt;
    ///         {
    ///             ["brush"] = new Dictionary&lt;string, object?&gt; { ["x"] = new object[] { 2, 6 } },
    ///         },
    ///     };
    ///     using var surface = document.Render(options);
    ///     </code>
    /// </example>
    /// <exception cref="ArgumentException">Thrown on initialization when a key is empty, a value is not supported (the message names the key), or a structured value is nested too deeply or holds too many values.</exception>
    public IReadOnlyDictionary<string, object?>? Parameters
    {
        get => _parameters;
        init => _parameters = CopyParameters(value, nameof(Parameters));
    }

    /// <summary>
    ///     Gets named datasets that are supplied by the caller (the data a specification refers to with
    ///     <c>data.name</c>), or <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    ///     Each dataset is a list of rows and each row maps field names to values of the same kinds that
    ///     <see cref="Parameters"/> accepts; a <see cref="DateTime"/> or <see cref="DateTimeOffset"/> becomes a date
    ///     (UTC, a <see cref="DateTimeKind.Unspecified"/> time is taken as UTC). A caller dataset replaces a dataset of the
    ///     same name in the top-level <c>datasets</c> of the specification; a <c>data.name</c> that neither defines makes
    ///     the chart fail with a <see cref="VegaLiteRenderException"/>. The rows count against
    ///     <see cref="VegaLiteLimits.MaxDataRows"/> like any other data. The whole structure is copied when it is set.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown on initialization when a name or field is empty, a row is null, or a value is not supported.</exception>
    public IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? Datasets
    {
        get => _datasets;
        init => _datasets = CopyDatasets(value, nameof(Datasets));
    }

    /// <summary>
    ///     Gets the time zone in which dates are interpreted and time units are computed, or <see langword="null"/>
    ///     for UTC.
    /// </summary>
    /// <remarks>
    ///     UTC is the default so that output does not depend on the host's local time zone: the zone of the machine is
    ///     never used. The zone decides how a date-time text without an offset is read, which instant a local time unit
    ///     starts at, where the ticks of a time scale fall, and how date labels and the expression date functions
    ///     read the parts of a date. <c>utc</c> time units, <c>utc</c> scales, and date-only ISO texts always use UTC.
    ///     Offsets come from <see cref="TimeZoneInfo.GetUtcOffset(DateTime)"/> of the zone given here.
    /// </remarks>
    public TimeZoneInfo? TimeZone { get; init; }

    /// <summary>
    ///     Gets the width in pixels that a chart with a <c>"container"</c> width takes; 400 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    public int ContainerWidth
    {
        get => _containerWidth;
        init => _containerWidth = RequireDimension(value);
    }

    /// <summary>
    ///     Gets the height in pixels that a chart with a <c>"container"</c> height takes; 300 by default.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown on initialization when the value is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    public int ContainerHeight
    {
        get => _containerHeight;
        init => _containerHeight = RequireDimension(value);
    }

    /// <summary>
    ///     Gets the font options, or <see langword="null"/> for the defaults of <see cref="VegaLiteFontOptions"/>
    ///     (bundled fonts only).
    /// </summary>
    public VegaLiteFontOptions? Fonts { get; init; }

    /// <summary>
    ///     Gets the resource limits of the call; <see cref="VegaLiteLimits.Default"/> by default.
    /// </summary>
    /// <exception cref="ArgumentNullException">Thrown on initialization when the value is <see langword="null"/>.</exception>
    public VegaLiteLimits Limits
    {
        get => _limits;
        init
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(Limits));
            }

            _limits = value;
        }
    }

    /// <summary>
    ///     Gets the callback that receives warnings, or <see langword="null"/> to drop them.
    /// </summary>
    /// <remarks>
    ///     Invoked on the calling thread, in order, after the work finished and before the method returns. When the
    ///     call fails, the reports recorded before the failure are still delivered but an exception thrown by the
    ///     handler is ignored so that the original failure is the one reported.
    /// </remarks>
    public Action<VegaLiteDiagnostic>? WarningHandler { get; init; }

    /// <summary>
    ///     Gets the callback that receives constructs that are recognized but not supported, or
    ///     <see langword="null"/> to drop them.
    /// </summary>
    /// <remarks>Delivered like <see cref="WarningHandler"/>.</remarks>
    public Action<VegaLiteUnsupportedFeature>? UnsupportedFeatureHandler { get; init; }

    /// <summary>
    ///     Gets a value indicating whether the first unsupported construct aborts the call with a
    ///     <see cref="VegaLiteUnsupportedFeatureException"/>; <see langword="false"/> by default.
    /// </summary>
    /// <remarks>
    ///     Set this for builds that must never accept an approximate chart. The report that caused the exception is
    ///     delivered to <see cref="UnsupportedFeatureHandler"/> first.
    /// </remarks>
    public bool ThrowOnUnsupportedFeature { get; init; }

    /// <summary>
    ///     Gets the font options that are in effect: <see cref="Fonts"/>, or the defaults when none were given.
    /// </summary>
    internal VegaLiteFontOptions EffectiveFonts => Fonts ?? new VegaLiteFontOptions();

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

    /// <summary>
    ///     Validates a pixel dimension.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="propertyName">The property name for the exception.</param>
    /// <returns>The value, unchanged.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is outside 1 to <see cref="Surface.MaxDimension"/>.</exception>
    private static int RequireDimension(int value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (value < 1 || value > Surface.MaxDimension)
        {
            throw new ArgumentOutOfRangeException(propertyName, value, $"The value must be between 1 and {Surface.MaxDimension}.");
        }

        return value;
    }

    /// <summary>
    ///     Validates and deep-copies the parameter values: the scalars of <see cref="CopyValues"/> and the sequences and string-keyed dictionaries of values.
    /// </summary>
    /// <param name="values">The values, or null.</param>
    /// <param name="propertyName">The property name for exceptions.</param>
    /// <returns>A read-only copy, or null.</returns>
    /// <exception cref="ArgumentException">Thrown when a key is empty, a value is not supported, or a structured value is nested too deeply or holds too many values.</exception>
    private static IReadOnlyDictionary<string, object?>? CopyParameters(IReadOnlyDictionary<string, object?>? values, string propertyName)
    {
        if (values is null)
        {
            return null;
        }

        var copy = new Dictionary<string, object?>(values.Count, StringComparer.Ordinal);
        foreach (var pair in values)
        {
            RequireName(pair.Key, propertyName);
            var remaining = MaxParameterItems;
            copy[pair.Key] = CopyParameterValue(pair.Value, pair.Key, propertyName, 0, ref remaining);
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }

    /// <summary>
    ///     Validates and copies one parameter value.
    /// </summary>
    /// <remarks>
    ///     The recursion is bounded by <see cref="MaxParameterDepth"/>, so a value that contains itself is rejected, not followed. A sequence becomes a
    ///     <see cref="ReadOnlyCollection{T}"/> and a dictionary a <see cref="ReadOnlyDictionary{TKey, TValue}"/>, so the caller's later changes do not reach the call.
    /// </remarks>
    /// <param name="value">The value.</param>
    /// <param name="key">The name of the parameter, for the message.</param>
    /// <param name="propertyName">The property name for the exception.</param>
    /// <param name="depth">The nesting depth of the value.</param>
    /// <param name="remaining">The number of values that may still be copied; reduced by each value.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException">Thrown when the value is not supported, is nested too deeply or the parameter holds too many values.</exception>
    private static object? CopyParameterValue(object? value, string key, string propertyName, int depth, ref int remaining)
    {
        if (--remaining < 0)
        {
            throw new ArgumentException($"The value of '{key}' holds more than {MaxParameterItems} values.", propertyName);
        }

        if (value is null || value is string || !IsStructured(value))
        {
            RequireSupportedValue(value, key, propertyName);
            return value;
        }

        if (depth >= MaxParameterDepth)
        {
            throw new ArgumentException($"The value of '{key}' is nested deeper than {MaxParameterDepth} levels.", propertyName);
        }

        if (value is IEnumerable<KeyValuePair<string, object?>> map)
        {
            var members = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var pair in map)
            {
                if (pair.Key is null)
                {
                    throw new ArgumentException($"The value of '{key}' has a member without a name.", propertyName);
                }

                members[pair.Key] = CopyParameterValue(pair.Value, key, propertyName, depth + 1, ref remaining);
            }

            return new ReadOnlyDictionary<string, object?>(members);
        }

        var items = new List<object?>();
        foreach (var item in (System.Collections.IEnumerable)value)
        {
            items.Add(CopyParameterValue(item, key, propertyName, depth + 1, ref remaining));
        }

        return items.AsReadOnly();
    }

    /// <summary>
    ///     Tells whether a parameter value is a sequence or a dictionary (anything enumerable that is not a string).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for a structured value.</returns>
    private static bool IsStructured(object value) => value is System.Collections.IEnumerable;

    /// <summary>
    ///     Validates and copies a dictionary of named values.
    /// </summary>
    /// <param name="values">The values, or null.</param>
    /// <param name="propertyName">The property name for exceptions.</param>
    /// <returns>A read-only copy, or null.</returns>
    /// <exception cref="ArgumentException">Thrown when a key is empty or a value is not supported.</exception>
    private static IReadOnlyDictionary<string, object?>? CopyValues(IReadOnlyDictionary<string, object?>? values, string propertyName)
    {
        if (values is null)
        {
            return null;
        }

        var copy = new Dictionary<string, object?>(values.Count, StringComparer.Ordinal);
        foreach (var pair in values)
        {
            RequireName(pair.Key, propertyName);
            RequireSupportedValue(pair.Value, pair.Key, propertyName);
            copy[pair.Key] = pair.Value;
        }

        return new ReadOnlyDictionary<string, object?>(copy);
    }

    /// <summary>
    ///     Validates and deep-copies the named datasets.
    /// </summary>
    /// <param name="datasets">The datasets, or null.</param>
    /// <param name="propertyName">The property name for exceptions.</param>
    /// <returns>A read-only copy, or null.</returns>
    /// <exception cref="ArgumentException">Thrown when a name or field is empty, a row is null, or a value is not supported.</exception>
    private static IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? CopyDatasets(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? datasets,
        string propertyName)
    {
        if (datasets is null)
        {
            return null;
        }

        var copy = new Dictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(datasets.Count, StringComparer.Ordinal);
        foreach (var dataset in datasets)
        {
            RequireName(dataset.Key, propertyName);
            if (dataset.Value is null)
            {
                throw new ArgumentException($"The dataset '{dataset.Key}' must not be null.", propertyName);
            }

            var rows = new List<IReadOnlyDictionary<string, object?>>(dataset.Value.Count);
            foreach (var row in dataset.Value)
            {
                if (row is null)
                {
                    throw new ArgumentException($"The dataset '{dataset.Key}' contains a null row.", propertyName);
                }

                rows.Add(CopyValues(row, propertyName)!);
            }

            copy[dataset.Key] = rows.AsReadOnly();
        }

        return new ReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>(copy);
    }

    /// <summary>
    ///     Requires a non-empty name.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="propertyName">The property name for the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the name is null, empty or white space.</exception>
    private static void RequireName(string name, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Names must not be null, empty or white space.", propertyName);
        }
    }

    /// <summary>
    ///     Requires a value of one of the supported kinds.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="key">The key the value belongs to, for the message.</param>
    /// <param name="propertyName">The property name for the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the value is not supported (or is a non-finite number).</exception>
    private static void RequireSupportedValue(object? value, string key, string propertyName)
    {
        var supported = value switch
        {
            null => true,
            bool or string or sbyte or byte or short or ushort or int or uint or long or ulong or decimal => true,
            DateTime or DateTimeOffset => true,
            float single => float.IsFinite(single),
            double number => double.IsFinite(number),
            _ => false,
        };

        if (!supported)
        {
            throw new ArgumentException($"The value of '{key}' is not supported: use null, bool, string, a finite number, DateTime or DateTimeOffset.", propertyName);
        }
    }
}
