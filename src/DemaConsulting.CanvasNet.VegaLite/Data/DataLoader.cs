using System.Globalization;
using System.Text.Json;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     Reads the data of a view from the <c>data</c> property of its specification: inline rows, a local file, a named
///     dataset or a generated sequence.
/// </summary>
/// <remarks>
///     <para>
///         <b>Sources.</b> The source is chosen by precedence, not by the order of the keys: inline <c>values</c>, then
///         <c>url</c>, <c>sequence</c> and <c>name</c> (the same precedence the feature catalog reports). Inline <c>values</c>
///         given as an array of objects give one row per object, and an array of primitives gives one row per value with
///         the value in the field <c>data</c> (the name Vega-Lite uses for it); a nested object or array is flattened
///         into fields named by the canonical path of each leaf (see <see cref="FieldPath"/>), and a field a row does
///         not have is null. A <c>url</c> that is a relative local path is read through <see cref="DataFileResolver"/>
///         as JSON (the default), CSV, TSV or DSV: the <c>format.type</c> decides, else the extension of the path
///         (<c>.csv</c>, <c>.tsv</c>, anything else is JSON). A <c>name</c> is looked up in the datasets that the caller
///         supplied (<see cref="VegaLiteRenderOptions.Datasets"/>), then in the top-level <c>datasets</c> of the
///         specification; the caller wins. A <c>sequence</c> generates the numbers from <c>start</c> up to, not
///         including, <c>stop</c> in steps of <c>step</c> (1 by default) in the field <c>as</c> (<c>data</c> by default).
///     </para>
///     <para>
///         <b>No network.</b> A <c>url</c> that has a scheme, or that is absolute, rooted, a drive path or a network path
///         (see <see cref="FeatureCatalog.IsSchemeBearingUrl"/>) is remote data: it is never given to the file resolver
///         or any other reader, and gives an empty table that the feature catalog reports as <c>vl-data-remote-url</c>
///         (strict mode throws there). A local file that is missing, blocked by the sandbox, not valid UTF-8, not valid
///         JSON or delimited text, or over a limit is not an empty dataset: it is a <see cref="VegaLiteRenderException"/>
///         with the location of the <c>url</c>, in lenient and strict mode alike. So is a <c>name</c> that nobody defines.
///     </para>
///     <para>
///         <b>Other sources.</b> Inline <c>values</c> given as text or as an object (GeoJSON), a <c>format.type</c> of
///         <c>topojson</c>, the geographic generators, and no source at all give an empty table and no error; the feature
///         catalog reports them (<c>vl-data-empty</c>, <c>vl-data-values-text</c>, <c>vl-data-geo</c>), so a lenient render
///         still draws the rest of the chart.
///     </para>
///     <para>
///         <b>Conversion.</b> <c>format.parse</c> converts values as described at <see cref="ParseDirectives"/>; as in
///         Vega, delimited text keeps every cell as a string unless <c>parse</c> asks for more (<c>"auto"</c> infers
///         booleans, numbers and dates per column; a field map converts the named fields only), and JSON keeps its
///         types. The temporal fields of the encoding of the main data are additionally date-parsed unless <c>parse</c>
///         names them or is null (the lookup sources are never parsed implicitly); dates without an offset and
///         <c>date:'%fmt'</c> formats are read in the zone of the calendar of the loader (UTC by default). Values that an
///         explicit conversion cannot read
///         become null and are reported once per field as the warning <c>vl-data-parse-failed</c> (see
///         <see cref="Warnings"/>). <c>format.property</c> selects the array of rows inside a JSON document by a dotted
///         path.
///     </para>
///     <para>
///         <b>Limits.</b> One instance serves one measure or render call. It counts the rows of every table it creates
///         against <see cref="VegaLiteLimits.MaxDataRows"/> in total, checks the number of rows before it allocates them
///         (also for a sequence), and enforces <see cref="VegaLiteLimits.MaxFieldCount"/> per row and
///         <see cref="VegaLiteLimits.MaxStringLength"/> per text; its file resolver enforces
///         <see cref="VegaLiteLimits.MaxDataFiles"/> and <see cref="VegaLiteLimits.MaxDataBytes"/> and caches every file
///         for the call. All of them are <see cref="VegaLiteRenderException"/>s that carry the location of the offending
///         data. The loader runs on the calling thread and is not thread-safe.
///     </para>
///     <para>
///         <b>Transforms.</b> The loader of the call is also the context of the row transforms: the data of a lookup is
///         loaded by <see cref="Load(SpecNode?, IReadOnlySet{string}?)"/> on the same instance (so with the sandbox, the file cache, the file and
///         byte limits and the row total of the main data), and the rows of a derived table are counted by
///         <see cref="ReserveDerivedRows"/> before they are allocated and given back by <see cref="ReleaseRows"/> when the table
///         they came from is no longer needed.
///     </para>
/// </remarks>
internal sealed class DataLoader
{
    /// <summary>The field that holds the value of a row when the values are primitives (or the default field of a sequence).</summary>
    public const string PrimitiveField = "data";

    /// <summary>The number of rows between two cancellation checks.</summary>
    private const int CancellationInterval = 1024;

    /// <summary>The source name used in locations of errors, or null.</summary>
    private readonly string? _sourceName;

    /// <summary>The limits.</summary>
    private readonly VegaLiteLimits _limits;

    /// <summary>The token that stops the loading.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>The file resolver of this call.</summary>
    private readonly DataFileResolver _resolver;

    /// <summary>The top-level <c>datasets</c> object of the specification, or null.</summary>
    private readonly SpecNode? _specDatasets;

    /// <summary>The datasets supplied by the caller, or null.</summary>
    private readonly IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? _callerDatasets;

    /// <summary>The calendar of the date conversions.</summary>
    private readonly TimeCalendar _calendar;

    /// <summary>The warnings found while loading.</summary>
    private readonly List<FeatureReport> _warnings = [];

    /// <summary>The number of rows of the tables created so far, against <see cref="VegaLiteLimits.MaxDataRows"/>.</summary>
    private long _rowsLoaded;

    /// <summary>
    ///     Initializes a loader for one measure or render call.
    /// </summary>
    /// <param name="sourceName">The source name used in locations of errors, or null.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="openOptions">The effective open options that say where data files are (base directory, data paths, resolver callback); none when null, so a local data file cannot be found.</param>
    /// <param name="specDatasets">The top-level <c>datasets</c> object of the specification, or null.</param>
    /// <param name="callerDatasets">The named datasets supplied by the caller, or null.</param>
    /// <param name="cancellationToken">A token that stops the loading.</param>
    /// <param name="isLink">The link probe handed to the file resolver; <see langword="null"/> uses the file system. A seam for tests.</param>
    /// <param name="calendar">The calendar that reads dates without an offset and <c>date:'%fmt'</c> formats; UTC when null.</param>
    public DataLoader(
        string? sourceName,
        VegaLiteLimits? limits = null,
        VegaLiteOpenOptions? openOptions = null,
        SpecNode? specDatasets = null,
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyDictionary<string, object?>>>? callerDatasets = null,
        CancellationToken cancellationToken = default,
        Func<string, bool>? isLink = null,
        TimeCalendar? calendar = null)
    {
        _sourceName = sourceName;
        _limits = limits ?? VegaLiteLimits.Default;
        _cancellation = cancellationToken;
        _specDatasets = specDatasets;
        _callerDatasets = callerDatasets;
        _calendar = calendar ?? TimeCalendar.Utc;
        _resolver = new DataFileResolver(openOptions ?? new VegaLiteOpenOptions(), _limits, cancellationToken, isLink);
    }

    /// <summary>
    ///     Gets the warnings found while loading (<c>vl-data-parse-failed</c>), in the order found; empty when nothing went wrong.
    /// </summary>
    public IReadOnlyList<FeatureReport> Warnings => _warnings;

    /// <summary>
    ///     Gets the number of rows of the tables loaded so far.
    /// </summary>
    public long RowsLoaded => _rowsLoaded;

    /// <summary>
    ///     Counts the rows of a derived table (a transform's output) against <see cref="VegaLiteLimits.MaxDataRows"/>
    ///     before the table is allocated, so that the loaded sources, the lookup sources and the tables of the transform
    ///     stage share one row budget.
    /// </summary>
    /// <param name="rows">The number of rows about to be created.</param>
    /// <param name="what">What the rows are, for the message.</param>
    /// <param name="location">The location of the transform.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the rows would exceed <see cref="VegaLiteLimits.MaxDataRows"/>.</exception>
    public void ReserveDerivedRows(double rows, string what, VegaLiteLocation location)
    {
        Charge(rows, what, location);
        _rowsLoaded += (long)rows;
    }

    /// <summary>
    ///     Gives back the rows of a table that is no longer needed (the input of a transform once its output is complete).
    /// </summary>
    /// <param name="rows">The number of rows released.</param>
    public void ReleaseRows(long rows) => _rowsLoaded = Math.Max(0, _rowsLoaded - Math.Max(0, rows));

    /// <summary>
    ///     Loads the rows of a <c>data</c> property with no file source and no named datasets (inline values and sequences only).
    /// </summary>
    /// <param name="data">The <c>data</c> node of the view; null (or anything but an object) gives an empty table.</param>
    /// <param name="sourceName">The source name used in locations of errors, or null.</param>
    /// <param name="limits">The limits to enforce; the defaults when null.</param>
    /// <param name="cancellationToken">A token that stops the loading.</param>
    /// <returns>The table.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when a limit is exceeded or the source needs a file or a named dataset; carries the location of the data.</exception>
    /// <exception cref="OperationCanceledException">Thrown when <paramref name="cancellationToken"/> is canceled.</exception>
    public static DataTable Load(
        SpecNode? data,
        string? sourceName,
        VegaLiteLimits? limits = null,
        CancellationToken cancellationToken = default) =>
        new DataLoader(sourceName, limits, cancellationToken: cancellationToken).Load(data);

    /// <summary>
    ///     Loads the rows of a <c>data</c> property.
    /// </summary>
    /// <param name="data">The <c>data</c> node of the view; null (or anything but an object) gives an empty table.</param>
    /// <param name="implicitDateFields">The canonical names of the fields that are date-parsed unless <c>format.parse</c> says otherwise (the temporal fields of the encoding); only the main data of a view passes them, never a lookup source.</param>
    /// <returns>The table: the rows of the source, or an empty table for a source that is only reported.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when a local file or a named dataset cannot be used, the data is malformed or a limit is exceeded; carries the location of the data.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is canceled.</exception>
    public DataTable Load(SpecNode? data, IReadOnlySet<string>? implicitDateFields = null)
    {
        if (data is not { IsObject: true })
        {
            return DataTable.Empty;
        }

        _cancellation.ThrowIfCancellationRequested();
        var format = data["format"] is { IsObject: true } given ? given : null;

        // The source is chosen by precedence, exactly as the feature catalog and the data references do
        if (data["values"] is { } values)
        {
            return values.IsArray ? LoadSpecRows(values, "inline data", format, data, implicitDateFields) : DataTable.Empty;
        }

        if (data["url"] is { IsString: true } url)
        {
            return LoadUrl(data, url, format, implicitDateFields);
        }

        if (data["sequence"] is { IsObject: true } sequence)
        {
            return LoadSequence(sequence);
        }

        return data["name"] is { IsString: true } name ? LoadNamed(data, name, format, implicitDateFields) : DataTable.Empty;
    }

    /// <summary>
    ///     Guesses the type of a data file from the extension of its path.
    /// </summary>
    /// <param name="path">The path as written in the specification.</param>
    /// <returns><c>csv</c> or <c>tsv</c> for those extensions (in any case), otherwise <c>json</c>.</returns>
    public static string InferFormatType(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var extension = Path.GetExtension(path.AsSpan());
        if (extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            return "csv";
        }

        return extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase) ? "tsv" : "json";
    }

    /// <summary>
    ///     Appends a property name or array index to a canonical path name.
    /// </summary>
    /// <param name="prefix">The canonical name so far; empty at the top.</param>
    /// <param name="segment">The raw property name or index text.</param>
    /// <returns>The canonical name of the child.</returns>
    private static string Join(string prefix, string segment) =>
        prefix.Length == 0 ? FieldPath.Escape(segment) : prefix + "." + FieldPath.Escape(segment);

    /// <summary>
    ///     Converts a value of a caller's dataset.
    /// </summary>
    /// <param name="value">The value: null, a bool, a string, a finite number of any built-in type, a <see cref="DateTime"/> or a <see cref="DateTimeOffset"/>.</param>
    /// <param name="location">The location of the data, for errors.</param>
    /// <returns>The data value.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when a date cannot be represented.</exception>
    internal static DataValue FromObject(object? value, VegaLiteLocation location) => value switch
    {
        null => DataValue.Null,
        bool flag => DataValue.FromBoolean(flag),
        string text => DataValue.FromString(text),
        DateTime date => FromDateTime(date, location),
        DateTimeOffset offset => FromDateTime(offset.UtcDateTime, location),
        _ => DataValue.FromNumber(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
    };

    /// <summary>
    ///     Converts a date of a caller's dataset, clamping the largest value of the .NET types to the largest supported date.
    /// </summary>
    /// <remarks>
    ///     <see cref="DateTime.MaxValue"/> and <see cref="DateTimeOffset.MaxValue"/> are the usual "no end" sentinels. They
    ///     carry sub-millisecond ticks that the whole-millisecond date range cannot hold, so a point in time past the last
    ///     whole millisecond of year 9999 is represented by that millisecond. Kind handling is that of
    ///     <see cref="DataValue.FromDateTime(DateTime)"/> (local converted to UTC, unspecified taken as UTC). Any other
    ///     failure to represent the date is a located failure, never a raw exception.
    /// </remarks>
    /// <param name="value">The point in time.</param>
    /// <param name="location">The location of the data, for errors.</param>
    /// <returns>The date value.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the date cannot be represented.</exception>
    private static DataValue FromDateTime(DateTime value, VegaLiteLocation location)
    {
        try
        {
            var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
            var milliseconds = DataValue.MillisecondsSinceEpoch(utc);
            return DataValue.FromDate(Math.Min(milliseconds, DataValue.MaxDateMilliseconds));
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new VegaLiteRenderException("A date in the data cannot be represented (the supported dates are the years 1 to 9999).", location);
        }
    }

    /// <summary>
    ///     Reads the <c>delimiter</c> of a DSV format.
    /// </summary>
    /// <param name="format">The <c>format</c> object, or null.</param>
    /// <param name="location">The location of the data, for errors.</param>
    /// <returns>The delimiter; a comma when the format names none.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the delimiter is not a single valid character.</exception>
    private static char ReadDelimiter(SpecNode? format, VegaLiteLocation location)
    {
        if (format?["delimiter"] is not { IsString: true } node)
        {
            return ',';
        }

        var text = node.StringValue!;
        return text.Length == 1 && DelimitedTextParser.IsValidDelimiter(text[0])
            ? text[0]
            : throw new VegaLiteRenderException("The format delimiter must be a single character other than a quote or a line break.", location);
    }

    /// <summary>
    ///     Follows a dotted field path (<c>format.property</c>) from the root of a JSON document.
    /// </summary>
    /// <param name="root">The root element.</param>
    /// <param name="path">The path text.</param>
    /// <param name="display">The name of the data, for messages.</param>
    /// <param name="location">The location of the <c>url</c>.</param>
    /// <returns>The selected element.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the path is malformed or names a property that is not there.</exception>
    private static JsonElement SelectProperty(JsonElement root, string path, string display, VegaLiteLocation location)
    {
        var segments = FieldPath.Parse(path)
            ?? throw new VegaLiteRenderException($"The format property '{path}' is not a valid field path.", location);
        var current = root;
        foreach (var segment in segments)
        {
            if (current.ValueKind == JsonValueKind.Object && current.TryGetProperty(segment, out var child))
            {
                current = child;
            }
            else if (current.ValueKind == JsonValueKind.Array
                && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
                && index < current.GetArrayLength())
            {
                current = current[index];
            }
            else
            {
                throw new VegaLiteRenderException($"The data '{display}' has no property '{path}' (format.property).", location);
            }
        }

        return current;
    }

    /// <summary>
    ///     Counts rows against the total row limit before they are created.
    /// </summary>
    /// <param name="rows">The number of rows about to be created.</param>
    /// <param name="what">What the rows are, for the message.</param>
    /// <param name="location">The location of the data.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the rows would exceed <see cref="VegaLiteLimits.MaxDataRows"/>.</exception>
    private void Charge(double rows, string what, VegaLiteLocation location)
    {
        if (rows <= _limits.MaxDataRows - _rowsLoaded)
        {
            return;
        }

        var total = _rowsLoaded == 0
            ? string.Empty
            : string.Create(CultureInfo.InvariantCulture, $"; with the {_rowsLoaded} rows read before, that is");

        // A count that overflowed or is beyond any exact integer is described, not printed digit by digit
        var count = double.IsFinite(rows) && rows < 1e18
            ? string.Create(CultureInfo.InvariantCulture, $"{rows:0}")
            : "an unrepresentable number of";
        throw new VegaLiteRenderException(
            string.Create(CultureInfo.InvariantCulture, $"The {what} has {count} rows{total}, more than the limit of {_limits.MaxDataRows} (MaxDataRows)."),
            location);
    }

    /// <summary>
    ///     Completes a table: counts its rows and reports the values that could not be converted.
    /// </summary>
    /// <param name="records">The rows.</param>
    /// <param name="failures">The values that could not be converted.</param>
    /// <param name="location">The location of the data, for the table's limit errors.</param>
    /// <param name="dataLocation">The location of the <c>data</c> object, for the warnings.</param>
    /// <returns>The table.</returns>
    private DataTable Complete(List<Dictionary<string, DataValue>> records, ParseFailures failures, VegaLiteLocation location, VegaLiteLocation dataLocation)
    {
        var table = DataTable.FromRecords(records, _limits, location);
        _rowsLoaded += table.RowCount;
        _warnings.AddRange(failures.ToReports(dataLocation));
        return table;
    }

    /// <summary>
    ///     Loads rows given as an array in the specification (inline <c>values</c> or a top-level dataset).
    /// </summary>
    /// <param name="rows">The array node.</param>
    /// <param name="what">What the rows are, for messages.</param>
    /// <param name="format">The <c>format</c> object of the data, or null.</param>
    /// <param name="data">The <c>data</c> object, for the location of warnings.</param>
    /// <param name="implicitDateFields">The implicit date fields of the main data, or null.</param>
    /// <returns>The table.</returns>
    private DataTable LoadSpecRows(SpecNode rows, string what, SpecNode? format, SpecNode data, IReadOnlySet<string>? implicitDateFields)
    {
        var location = rows.GetLocation(_sourceName);
        Charge(rows.Items.Count, what, location);

        // Turn every row into a record of its leaf values
        var records = new List<Dictionary<string, DataValue>>(rows.Items.Count);
        var counter = 0;
        foreach (var row in rows.Items)
        {
            if (++counter % CancellationInterval == 0)
            {
                _cancellation.ThrowIfCancellationRequested();
            }

            var record = new Dictionary<string, DataValue>(StringComparer.Ordinal);
            if (row.IsObject)
            {
                FlattenProperties(row, string.Empty, record);
            }
            else
            {
                // A primitive (or an array) row becomes a record with the one field "data"
                Flatten(row, PrimitiveField, record);
            }

            records.Add(record);
        }

        var failures = new ParseFailures();
        ParseDirectives.Read(format, defaultAuto: false, implicitDateFields).Apply(records, failures, _cancellation, _calendar);
        return Complete(records, failures, location, data.GetLocation(_sourceName));
    }

    /// <summary>
    ///     Loads the rows of a named dataset.
    /// </summary>
    /// <param name="data">The <c>data</c> object.</param>
    /// <param name="name">The <c>name</c> value.</param>
    /// <param name="format">The <c>format</c> object, or null.</param>
    /// <param name="implicitDateFields">The implicit date fields of the main data, or null.</param>
    /// <returns>The table.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when no dataset of that name is defined.</exception>
    private DataTable LoadNamed(SpecNode data, SpecNode name, SpecNode? format, IReadOnlySet<string>? implicitDateFields)
    {
        var key = name.StringValue!;

        // The caller's dataset wins over the one of the specification
        if (_callerDatasets is not null && _callerDatasets.TryGetValue(key, out var callerRows))
        {
            var dataLocation = data.GetLocation(_sourceName);
            return LoadCallerRows(callerRows, key, format, dataLocation, implicitDateFields);
        }

        if (_specDatasets?[key] is { } dataset)
        {
            // A dataset given as text or as an object is reported by the feature catalog and is empty here
            return dataset.IsArray ? LoadSpecRows(dataset, $"dataset '{key}'", format, data, implicitDateFields) : DataTable.Empty;
        }

        throw new VegaLiteRenderException(
            $"The named data '{key}' is not defined: supply it in VegaLiteRenderOptions.Datasets or in the top-level 'datasets' of the specification.",
            name.GetLocation(_sourceName));
    }

    /// <summary>
    ///     Loads the rows of a dataset that the caller supplied.
    /// </summary>
    /// <param name="rows">The rows.</param>
    /// <param name="name">The name of the dataset.</param>
    /// <param name="format">The <c>format</c> object, or null.</param>
    /// <param name="location">The location of the <c>data</c> object.</param>
    /// <param name="implicitDateFields">The implicit date fields of the main data, or null.</param>
    /// <returns>The table.</returns>
    private DataTable LoadCallerRows(IReadOnlyList<IReadOnlyDictionary<string, object?>> rows, string name, SpecNode? format, VegaLiteLocation location, IReadOnlySet<string>? implicitDateFields)
    {
        Charge(rows.Count, $"dataset '{name}'", location);
        var records = new List<Dictionary<string, DataValue>>(rows.Count);
        foreach (var row in rows)
        {
            if (records.Count % CancellationInterval == CancellationInterval - 1)
            {
                _cancellation.ThrowIfCancellationRequested();
            }

            var record = new Dictionary<string, DataValue>(row.Count, StringComparer.Ordinal);
            foreach (var (field, value) in row)
            {
                var cell = FromObject(value, location);
                if (cell.IsString && cell.Text!.Length > _limits.MaxStringLength)
                {
                    throw new VegaLiteRenderException(
                        $"A text in the data is longer than the limit of {_limits.MaxStringLength} characters (MaxStringLength).", location);
                }

                record[FieldPath.Escape(field)] = cell;
                CheckFieldCount(record, location);
            }

            records.Add(record);
        }

        var failures = new ParseFailures();
        ParseDirectives.Read(format, defaultAuto: false, implicitDateFields).Apply(records, failures, _cancellation, _calendar);
        return Complete(records, failures, location, location);
    }

    /// <summary>
    ///     Loads a generated sequence.
    /// </summary>
    /// <param name="sequence">The <c>sequence</c> object.</param>
    /// <returns>The table with one field and one row per generated number.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the sequence is not valid or has more rows than the limit allows.</exception>
    private DataTable LoadSequence(SpecNode sequence)
    {
        var location = sequence.GetLocation(_sourceName);

        // The endpoints must be present, numeric and finite; a missing or wrongly typed one is never a default
        if (sequence["start"]?.AsNumber() is not { } start || sequence["stop"]?.AsNumber() is not { } stop
            || !double.IsFinite(start) || !double.IsFinite(stop))
        {
            throw new VegaLiteRenderException("The sequence needs a finite numeric 'start' and 'stop'.", location);
        }

        // Only an absent step defaults to 1; a step that is present must be a finite number other than zero
        var step = 1.0;
        if (sequence["step"] is { } stepNode)
        {
            if (stepNode.AsNumber() is not { } given || given == 0 || !double.IsFinite(given))
            {
                throw new VegaLiteRenderException("The sequence step must be a finite number other than zero.", location);
            }

            step = given;
        }

        // The length is computed arithmetically and charged before anything is allocated: start inclusive, stop
        // exclusive. The difference and the quotient may overflow to infinity, which the charge then refuses, and a
        // step that points away from the stop (a quotient that is not positive) gives an empty sequence.
        var length = (stop - start) / step;
        length = double.IsNaN(length) || length <= 0 ? 0 : Math.Ceiling(length);
        length = TrimSequenceCount(start, stop, step, length);
        Charge(length, "generated sequence", location);
        var field = FieldPath.Escape(sequence["as"]?.AsString() ?? PrimitiveField);
        var table = DataTable.Create([field], Generate(start, step, (long)length, location), _limits, location);
        _rowsLoaded += table.RowCount;
        return table;
    }

    /// <summary>
    ///     Corrects the rounded count of a sequence so that exactly the generated values before the stop are counted.
    /// </summary>
    /// <param name="start">The first value.</param>
    /// <param name="stop">The exclusive end.</param>
    /// <param name="step">The step.</param>
    /// <param name="count">The count from the rounded quotient.</param>
    /// <returns>The count whose last value <c>start + (i * step)</c> is strictly before <paramref name="stop"/> and whose next value is not.</returns>
    /// <remarks>
    ///     The quotient and the indexed values round differently, so the count may be one too high (the last value
    ///     equals or passes the stop) or one too low. A count too large to index exactly is left to the limit.
    /// </remarks>
    private static double TrimSequenceCount(double start, double stop, double step, double count)
    {
        const double ExactLimit = 9007199254740992.0;
        if (!double.IsFinite(count) || count >= ExactLimit)
        {
            return count;
        }

        bool Before(double index) => step > 0 ? start + (index * step) < stop : start + (index * step) > stop;

        while (count > 0 && !Before(count - 1))
        {
            count--;
        }

        // Rounding is off by at most a step or two; the bound keeps a degenerate step from looping
        for (var extra = 0; extra < 2 && count < ExactLimit && Before(count); extra++)
        {
            count++;
        }

        return count;
    }

    /// <summary>
    ///     Generates the rows of a sequence lazily.
    /// </summary>
    /// <param name="start">The first value.</param>
    /// <param name="step">The step.</param>
    /// <param name="count">The number of values.</param>
    /// <param name="location">The location of the sequence, for errors.</param>
    /// <returns>The rows, each with one number.</returns>
    private IEnumerable<IReadOnlyList<DataValue>> Generate(double start, double step, long count, VegaLiteLocation location)
    {
        for (long i = 0; i < count; i++)
        {
            if (i % CancellationInterval == CancellationInterval - 1)
            {
                _cancellation.ThrowIfCancellationRequested();
            }

            // Each value is computed from its index (not by accumulating the step), so rounding does not drift
            var value = start + (i * step);
            if (!double.IsFinite(value))
            {
                throw new VegaLiteRenderException("The sequence leaves the range of a number.", location);
            }

            yield return [DataValue.FromNumber(value)];
        }
    }

    /// <summary>
    ///     Loads the rows of a <c>url</c>: a local data file, or nothing for remote data.
    /// </summary>
    /// <param name="data">The <c>data</c> object.</param>
    /// <param name="url">The <c>url</c> value.</param>
    /// <param name="format">The <c>format</c> object, or null.</param>
    /// <param name="implicitDateFields">The implicit date fields of the main data, or null.</param>
    /// <returns>The table; empty (and never read) when the URL is remote or the format is geographic.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the file cannot be used.</exception>
    private DataTable LoadUrl(SpecNode data, SpecNode url, SpecNode? format, IReadOnlySet<string>? implicitDateFields)
    {
        var text = url.StringValue!;

        // Remote data never reaches the file resolver: the catalog reports it and the dataset is empty
        if (FeatureCatalog.IsSchemeBearingUrl(text))
        {
            return DataTable.Empty;
        }

        var type = format?["type"]?.AsString() ?? InferFormatType(text);
        if (type == "topojson")
        {
            return DataTable.Empty;
        }

        var location = url.GetLocation(_sourceName);
        var file = _resolver.Load(text, location);
        var dataLocation = data.GetLocation(_sourceName);
        return type switch
        {
            "csv" => LoadDelimited(file, ',', format, location, dataLocation, implicitDateFields),
            "tsv" => LoadDelimited(file, '\t', format, location, dataLocation, implicitDateFields),
            "dsv" => LoadDelimited(file, ReadDelimiter(format, location), format, location, dataLocation, implicitDateFields),
            _ => LoadJson(file, format, location, dataLocation, implicitDateFields),
        };
    }

    /// <summary>
    ///     Loads a delimited text file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="delimiter">The delimiter.</param>
    /// <param name="format">The <c>format</c> object, or null.</param>
    /// <param name="location">The location of the <c>url</c>.</param>
    /// <param name="dataLocation">The location of the <c>data</c> object.</param>
    /// <param name="implicitDateFields">The implicit date fields of the main data, or null.</param>
    /// <returns>The table.</returns>
    private DataTable LoadDelimited(DataFile file, char delimiter, SpecNode? format, VegaLiteLocation location, VegaLiteLocation dataLocation, IReadOnlySet<string>? implicitDateFields)
    {
        var options = new DelimitedOptions(
            delimiter,
            ParseDirectives.Read(format, defaultAuto: false, implicitDateFields),
            _limits,
            _limits.MaxDataRows - _rowsLoaded,
            file.Display,
            location,
            _cancellation,
            _calendar);
        var result = DelimitedTextParser.Parse(file.Text, options);
        return Complete(result.Records, result.Failures, location, dataLocation);
    }

    /// <summary>
    ///     Loads a JSON file: an array of rows, or the array that <c>format.property</c> selects.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="format">The <c>format</c> object, or null.</param>
    /// <param name="location">The location of the <c>url</c>.</param>
    /// <param name="dataLocation">The location of the <c>data</c> object.</param>
    /// <param name="implicitDateFields">The implicit date fields of the main data, or null.</param>
    /// <returns>The table.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the text is not JSON, the property is missing, or the selected value is not an array.</exception>
    private DataTable LoadJson(DataFile file, SpecNode? format, VegaLiteLocation location, VegaLiteLocation dataLocation, IReadOnlySet<string>? implicitDateFields)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(file.Text, new JsonDocumentOptions { MaxDepth = _limits.MaxJsonDepth });
        }
        catch (JsonException ex)
        {
            throw new VegaLiteRenderException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The data '{file.Display}' is not valid JSON (line {(ex.LineNumber ?? 0) + 1}, position {(ex.BytePositionInLine ?? 0) + 1}) or is nested deeper than MaxJsonDepth."),
                location);
        }

        using (document)
        {
            var records = ReadJsonRecords(document.RootElement, format, file, location);
            var failures = new ParseFailures();
            ParseDirectives.Read(format, defaultAuto: false, implicitDateFields).Apply(records, failures, _cancellation, _calendar);
            return Complete(records, failures, location, dataLocation);
        }
    }

    /// <summary>
    ///     Reads the rows of a parsed JSON document, turning text that is not valid Unicode into a located failure.
    /// </summary>
    /// <remarks>
    ///     The JSON parser accepts an escaped lone surrogate (such as <c>\ud800</c>) and only fails when the string or the
    ///     property name is read, with a raw <see cref="InvalidOperationException"/>. Every element access of the rows (the
    ///     <c>format.property</c> lookup, the property names and the strings) is inside one boundary that reports it as a
    ///     <see cref="VegaLiteRenderException"/> at the data location. A failure that is already located passes through.
    /// </remarks>
    /// <param name="root">The root element of the document.</param>
    /// <param name="format">The <c>format</c> object, or null.</param>
    /// <param name="file">The file.</param>
    /// <param name="location">The location of the <c>url</c>.</param>
    /// <returns>The rows.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when the property is missing, the selected value is not an array, a limit is exceeded or the text is not valid Unicode.</exception>
    private List<Dictionary<string, DataValue>> ReadJsonRecords(JsonElement root, SpecNode? format, DataFile file, VegaLiteLocation location)
    {
        try
        {
            if (format?["property"] is { IsString: true } property)
            {
                root = SelectProperty(root, property.StringValue!, file.Display, location);
            }

            if (root.ValueKind != JsonValueKind.Array)
            {
                throw new VegaLiteRenderException(
                    $"The data '{file.Display}' must be an array of rows; use format.property to select the array inside an object.", location);
            }

            Charge(root.GetArrayLength(), $"data '{file.Display}'", location);
            var records = new List<Dictionary<string, DataValue>>(root.GetArrayLength());
            foreach (var row in root.EnumerateArray())
            {
                if (records.Count % CancellationInterval == CancellationInterval - 1)
                {
                    _cancellation.ThrowIfCancellationRequested();
                }

                var record = new Dictionary<string, DataValue>(StringComparer.Ordinal);
                if (row.ValueKind == JsonValueKind.Object)
                {
                    FlattenJsonProperties(row, string.Empty, record, location);
                }
                else
                {
                    FlattenJson(row, PrimitiveField, record, location);
                }

                records.Add(record);
            }

            return records;
        }
        catch (InvalidOperationException ex) when (ex is not VegaLiteRenderException)
        {
            throw new VegaLiteRenderException($"The data '{file.Display}' contains text that is not valid Unicode (an unpaired surrogate escape).", location);
        }
    }

    /// <summary>
    ///     Adds the leaves of one JSON value to a record.
    /// </summary>
    /// <remarks>
    ///     Recursion is bounded by <see cref="VegaLiteLimits.MaxJsonDepth"/>, which the JSON reader enforced.
    /// </remarks>
    /// <param name="element">The value.</param>
    /// <param name="key">The canonical name of the value's path.</param>
    /// <param name="record">The record being built.</param>
    /// <param name="location">The location of the data, for errors.</param>
    private void FlattenJson(JsonElement element, string key, Dictionary<string, DataValue> record, VegaLiteLocation location)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                FlattenJsonProperties(element, key, record, location);
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenJson(item, Join(key, index.ToString(CultureInfo.InvariantCulture)), record, location);
                    index++;
                }

                break;
            case JsonValueKind.String:
                var text = element.GetString()!;
                if (text.Length > _limits.MaxStringLength)
                {
                    throw new VegaLiteRenderException(
                        $"A text in the data is longer than the limit of {_limits.MaxStringLength} characters (MaxStringLength).", location);
                }

                Add(key, DataValue.FromString(text), record, location);
                break;
            case JsonValueKind.Number:
                if (!element.TryGetDouble(out var number) || !double.IsFinite(number))
                {
                    throw new VegaLiteRenderException("A number in the data is outside the range of a double.", location);
                }

                Add(key, DataValue.FromNumber(number), record, location);
                break;
            case JsonValueKind.True or JsonValueKind.False:
                Add(key, DataValue.FromBoolean(element.GetBoolean()), record, location);
                break;
            default:
                Add(key, DataValue.Null, record, location);
                break;
        }
    }

    /// <summary>
    ///     Adds the leaves of the properties of a JSON object to a record.
    /// </summary>
    /// <param name="element">The object.</param>
    /// <param name="prefix">The canonical name of the object's path; empty for the row itself.</param>
    /// <param name="record">The record being built.</param>
    /// <param name="location">The location of the data, for errors.</param>
    private void FlattenJsonProperties(JsonElement element, string prefix, Dictionary<string, DataValue> record, VegaLiteLocation location)
    {
        foreach (var property in element.EnumerateObject())
        {
            FlattenJson(property.Value, Join(prefix, property.Name), record, location);
        }
    }

    /// <summary>
    ///     Adds the leaves of one specification value to a record.
    /// </summary>
    /// <remarks>
    ///     Recursion is bounded by the nesting depth the specification parser accepted (at most 256 levels).
    /// </remarks>
    /// <param name="node">The value.</param>
    /// <param name="key">The canonical name of the value's path.</param>
    /// <param name="record">The record being built.</param>
    private void Flatten(SpecNode node, string key, Dictionary<string, DataValue> record)
    {
        switch (node.Kind)
        {
            case SpecNodeKind.Object:
                FlattenProperties(node, key, record);
                break;
            case SpecNodeKind.Array:
                for (var i = 0; i < node.Items.Count; i++)
                {
                    Flatten(node.Items[i], Join(key, i.ToString(CultureInfo.InvariantCulture)), record);
                }

                break;
            case SpecNodeKind.String:
                // The limit of the call can be lower than the one the specification was parsed with
                if (node.StringValue!.Length > _limits.MaxStringLength)
                {
                    throw new VegaLiteRenderException(
                        $"A text in the data is longer than the limit of {_limits.MaxStringLength} characters (MaxStringLength).",
                        node.GetLocation(_sourceName));
                }

                Add(node, key, DataValue.FromString(node.StringValue), record);
                break;
            case SpecNodeKind.Number:
                Add(node, key, DataValue.FromNumber(node.NumberValue), record);
                break;
            case SpecNodeKind.Boolean:
                Add(node, key, DataValue.FromBoolean(node.BoolValue), record);
                break;
            default:
                Add(node, key, DataValue.Null, record);
                break;
        }
    }

    /// <summary>
    ///     Adds the leaves of the properties of a specification object to a record.
    /// </summary>
    /// <param name="node">The object.</param>
    /// <param name="prefix">The canonical name of the object's path; empty for the row itself.</param>
    /// <param name="record">The record being built.</param>
    private void FlattenProperties(SpecNode node, string prefix, Dictionary<string, DataValue> record)
    {
        foreach (var property in node.Properties)
        {
            Flatten(property, Join(prefix, property.Name!), record);
        }
    }

    /// <summary>
    ///     Stores one leaf of a specification, enforcing the field limit; the location is only computed on failure.
    /// </summary>
    /// <param name="node">The leaf node (for the location).</param>
    /// <param name="key">The canonical field name.</param>
    /// <param name="value">The value.</param>
    /// <param name="record">The record being built.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the row has too many fields.</exception>
    private void Add(SpecNode node, string key, DataValue value, Dictionary<string, DataValue> record)
    {
        record[key] = value;
        if (record.Count > _limits.MaxFieldCount)
        {
            throw FieldLimit(node.GetLocation(_sourceName));
        }
    }

    /// <summary>
    ///     Stores one leaf of a file, enforcing the field limit.
    /// </summary>
    /// <param name="key">The canonical field name.</param>
    /// <param name="value">The value.</param>
    /// <param name="record">The record being built.</param>
    /// <param name="location">The location of the data.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the row has too many fields.</exception>
    private void Add(string key, DataValue value, Dictionary<string, DataValue> record, VegaLiteLocation location)
    {
        record[key] = value;
        CheckFieldCount(record, location);
    }

    /// <summary>
    ///     Enforces <see cref="VegaLiteLimits.MaxFieldCount"/> on a row.
    /// </summary>
    /// <param name="record">The record being built.</param>
    /// <param name="location">The location reported.</param>
    /// <exception cref="VegaLiteRenderException">Thrown when the row has too many fields.</exception>
    private void CheckFieldCount(Dictionary<string, DataValue> record, VegaLiteLocation location)
    {
        if (record.Count > _limits.MaxFieldCount)
        {
            throw FieldLimit(location);
        }
    }

    /// <summary>
    ///     Creates the exception for a row with too many fields.
    /// </summary>
    /// <param name="location">The location of the offending data.</param>
    /// <returns>The exception, ready to throw.</returns>
    private VegaLiteRenderException FieldLimit(VegaLiteLocation location) =>
        new($"A data row has more than {_limits.MaxFieldCount} fields, the limit (MaxFieldCount).", location);
}
