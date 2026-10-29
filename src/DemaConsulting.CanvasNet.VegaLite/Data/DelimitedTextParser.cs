using System.Globalization;
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The type that a <c>format.parse</c> entry converts a field to.
/// </summary>
internal enum ParseType
{
    /// <summary>A number (<c>"number"</c>).</summary>
    Number,

    /// <summary>A boolean (<c>"boolean"</c>).</summary>
    Boolean,

    /// <summary>An ISO 8601 date or date-time in UTC (<c>"date"</c>).</summary>
    Date,

    /// <summary>The text of the value (<c>"string"</c>).</summary>
    String,

    /// <summary>A date with a custom format (<c>"date:'%d/%m/%Y'"</c>, <c>"utc:'...'"</c>); recognized but not applied.</summary>
    CustomDate,
}

/// <summary>
///     How the values of a data source are converted: not at all, by inferring the type of every text column, or by
///     the explicit type of named fields.
/// </summary>
internal enum ParseMode
{
    /// <summary>No conversion: every value keeps the type it was read with (<c>parse: null</c>).</summary>
    None,

    /// <summary>The type of each column of text is inferred from its values (<c>parse: "auto"</c>, the default for delimited text).</summary>
    Auto,

    /// <summary>The fields named in <see cref="ParseDirectives.Fields"/> are converted; all others keep their type.</summary>
    Fields,
}

/// <summary>
///     The conversions that a <c>format.parse</c> property asks for, and the rules that apply them.
/// </summary>
/// <remarks>
///     <para>
///         <b>Modes.</b> Delimited text (CSV, TSV, DSV) is parsed with <see cref="ParseMode.Auto"/> unless the
///         specification says otherwise; JSON data keeps its JSON types unless the specification asks for
///         <c>"auto"</c> or names fields. <c>parse: null</c> keeps every value as read (text stays text). An object
///         names the fields to convert (<c>number</c>, <c>boolean</c>, <c>date</c>, <c>string</c>); the keys are field
///         paths, compared in their canonical form like the fields of an encoding. All other fields keep their type.
///     </para>
///     <para>
///         <b>Auto inference</b> looks at a whole column, as Vega does: a column is a boolean column when every non-empty
///         text is <c>true</c> or <c>false</c> (any case), a number column when every non-empty text is a finite
///         invariant integer, decimal or exponent number, and a date column when every non-empty text is an ISO 8601
///         date or date-time (tested in that order); otherwise it stays text. In a typed column an empty text is null.
///         A column that holds a value that is not text (JSON) is left alone. Dates are read in UTC: a date without a
///         time zone is midnight UTC, and a date-time without a zone is taken as UTC.
///     </para>
///     <para>
///         <b>Explicit conversion</b> never invents a value: a text that cannot be converted becomes null and is counted
///         in a <see cref="ParseFailures"/> record, which the loader reports as the warning <c>vl-data-parse-failed</c>.
///         Null stays null and an empty text becomes null (except for <c>string</c>). Custom date formats are not
///         implemented and leave the field as read (the feature catalog reports <c>vl-data-parse-date-format</c>).
///     </para>
/// </remarks>
internal sealed class ParseDirectives
{
    /// <summary>The ISO 8601 date format: year, month and day.</summary>
    private const string DateFormat = "yyyy'-'MM'-'dd";

    /// <summary>The ISO 8601 date and time format down to the minute.</summary>
    private const string MinuteFormat = DateFormat + "'T'HH':'mm";

    /// <summary>The ISO 8601 date and time format down to the second.</summary>
    private const string SecondFormat = MinuteFormat + "':'ss";

    /// <summary>The ISO 8601 date and time format with up to seven digits of fraction.</summary>
    private const string FractionFormat = SecondFormat + "'.'FFFFFFF";

    /// <summary>The ISO 8601 formats of dates and date-times that are recognized, in the invariant culture; <c>K</c> adds the optional zone.</summary>
    private static readonly string[] IsoFormats =
    [
        DateFormat,
        MinuteFormat, MinuteFormat + "K",
        SecondFormat, SecondFormat + "K",
        FractionFormat, FractionFormat + "K",
    ];

    /// <summary>The directives that convert nothing.</summary>
    private static readonly ParseDirectives NoneInstance = new(ParseMode.None, new Dictionary<string, ParseType>());

    /// <summary>The directives that infer the type of each column.</summary>
    private static readonly ParseDirectives AutoInstance = new(ParseMode.Auto, new Dictionary<string, ParseType>());

    /// <summary>
    ///     Initializes directives.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="fields">The explicit conversions by canonical field name.</param>
    private ParseDirectives(ParseMode mode, IReadOnlyDictionary<string, ParseType> fields)
    {
        Mode = mode;
        Fields = fields;
    }

    /// <summary>Gets the directives that convert nothing.</summary>
    public static ParseDirectives None => NoneInstance;

    /// <summary>Gets the directives that infer the type of each column.</summary>
    public static ParseDirectives Auto => AutoInstance;

    /// <summary>Gets the mode.</summary>
    public ParseMode Mode { get; }

    /// <summary>Gets the explicit conversions by canonical field name (empty unless <see cref="Mode"/> is <see cref="ParseMode.Fields"/>).</summary>
    public IReadOnlyDictionary<string, ParseType> Fields { get; }

    /// <summary>
    ///     Reads the directives of a data format.
    /// </summary>
    /// <param name="format">The <c>format</c> object, or null.</param>
    /// <param name="defaultAuto">Whether an absent <c>parse</c> means <see cref="ParseMode.Auto"/> (delimited text) or <see cref="ParseMode.None"/>.</param>
    /// <returns>The directives.</returns>
    public static ParseDirectives Read(SpecNode? format, bool defaultAuto)
    {
        var parse = format is { IsObject: true } ? format["parse"] : null;
        if (parse is null)
        {
            return defaultAuto ? Auto : None;
        }

        if (parse.IsObject)
        {
            var fields = new Dictionary<string, ParseType>(StringComparer.Ordinal);
            foreach (var entry in parse.Properties)
            {
                // Entries that validation would have rejected are skipped; the specification parser makes them errors
                if (entry.IsString && TryClassify(entry.StringValue!, out var type))
                {
                    fields[FieldPath.Canonicalize(entry.Name) ?? FieldPath.Escape(entry.Name!)] = type;
                }
            }

            return new ParseDirectives(ParseMode.Fields, fields);
        }

        return parse.IsString && string.Equals(parse.StringValue, "auto", StringComparison.Ordinal) ? Auto : None;
    }

    /// <summary>
    ///     Classifies the text of a <c>format.parse</c> entry.
    /// </summary>
    /// <param name="text">The entry value, such as <c>number</c> or <c>date:'%Y'</c>.</param>
    /// <param name="type">Receives the type.</param>
    /// <returns><see langword="true"/> when the text is a known conversion.</returns>
    public static bool TryClassify(string text, out ParseType type)
    {
        switch (text)
        {
            case "number":
                type = ParseType.Number;
                return true;
            case "boolean":
                type = ParseType.Boolean;
                return true;
            case "date":
                type = ParseType.Date;
                return true;
            case "string":
                type = ParseType.String;
                return true;
            default:
                // date:'%fmt' and utc:'%fmt' carry a custom time format
                type = ParseType.CustomDate;
                return text.StartsWith("date:", StringComparison.Ordinal) || text.StartsWith("utc:", StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Applies the directives to the rows of one data source, in place.
    /// </summary>
    /// <param name="records">The rows; each maps canonical field names to values.</param>
    /// <param name="failures">Collects the values that could not be converted.</param>
    /// <param name="cancellation">The token polled between fields.</param>
    public void Apply(IReadOnlyList<Dictionary<string, DataValue>> records, ParseFailures failures, CancellationToken cancellation)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(failures);
        switch (Mode)
        {
            case ParseMode.Auto:
                ApplyAuto(records, cancellation);
                break;
            case ParseMode.Fields:
                foreach (var (field, type) in Fields.OrderBy(f => f.Key, StringComparer.Ordinal))
                {
                    cancellation.ThrowIfCancellationRequested();
                    ApplyField(records, field, type, failures);
                }

                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Tries to read a boolean from text.
    /// </summary>
    /// <param name="text">The text; surrounding white space is ignored.</param>
    /// <param name="value">Receives the boolean.</param>
    /// <returns><see langword="true"/> for <c>true</c> and <c>false</c> in any case.</returns>
    public static bool TryParseBoolean(string text, out bool value)
    {
        var trimmed = text.AsSpan().Trim();
        if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        value = false;
        return trimmed.Equals("false", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Tries to read a finite number from text.
    /// </summary>
    /// <remarks>
    ///     Integers, decimals with a point and exponents are accepted in the invariant culture, with an optional sign and
    ///     surrounding white space. Hexadecimal, thousands separators, <c>NaN</c>, infinities and numbers outside the range
    ///     of a double are not numbers.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="value">Receives the number.</param>
    /// <returns><see langword="true"/> when the text is a finite number.</returns>
    public static bool TryParseNumber(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>
    ///     Tries to read an ISO 8601 date or date-time as milliseconds since the epoch in UTC.
    /// </summary>
    /// <remarks>
    ///     The forms are <c>yyyy-MM-dd</c> (midnight UTC), and <c>yyyy-MM-ddTHH:mm</c>, <c>...:ss</c> and <c>...:ss.f</c> (up
    ///     to seven fraction digits), each with an optional <c>Z</c> or <c>+HH:mm</c> offset; without an offset the time is
    ///     taken as UTC, so the result never depends on the host time zone or culture.
    /// </remarks>
    /// <param name="text">The text; surrounding white space is ignored.</param>
    /// <param name="milliseconds">Receives the milliseconds since 1970-01-01T00:00:00Z.</param>
    /// <returns><see langword="true"/> when the text is a valid date or date-time.</returns>
    public static bool TryParseIsoDate(string text, out double milliseconds)
    {
        milliseconds = 0;
        var trimmed = text.Trim();

        // A cheap shape test first: four digits, dash, two digits, dash, two digits
        if (trimmed.Length < 10
            || !char.IsAsciiDigit(trimmed[0]) || !char.IsAsciiDigit(trimmed[1]) || !char.IsAsciiDigit(trimmed[2]) || !char.IsAsciiDigit(trimmed[3])
            || trimmed[4] != '-' || !char.IsAsciiDigit(trimmed[5]) || !char.IsAsciiDigit(trimmed[6])
            || trimmed[7] != '-' || !char.IsAsciiDigit(trimmed[8]) || !char.IsAsciiDigit(trimmed[9]))
        {
            return false;
        }

        if (!DateTime.TryParseExact(trimmed, IsoFormats, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return false;
        }

        milliseconds = (parsed - DateTime.UnixEpoch).TotalMilliseconds;
        return true;
    }

    /// <summary>
    ///     Gets the name of a conversion for messages.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The name used in a specification (<c>number</c>, <c>boolean</c>, <c>date</c>, <c>string</c>).</returns>
    public static string NameOf(ParseType type) => type switch
    {
        ParseType.Number => "number",
        ParseType.Boolean => "boolean",
        ParseType.Date => "date",
        ParseType.String => "string",
        _ => "date with a custom format",
    };

    /// <summary>
    ///     Converts one value to a type.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The target type.</param>
    /// <param name="result">Receives the converted value (null when the value is null, empty or cannot be converted).</param>
    /// <returns><see langword="false"/> when a value that is present could not be converted; otherwise <see langword="true"/>.</returns>
    public static bool TryConvert(DataValue value, ParseType type, out DataValue result)
    {
        result = DataValue.Null;
        if (value.IsNull)
        {
            return true;
        }

        return type switch
        {
            ParseType.Number => TryToNumber(value, out result),
            ParseType.Boolean => TryToBoolean(value, out result),
            ParseType.Date => TryToDate(value, out result),
            ParseType.String => TryToString(value, out result),
            _ => KeepAsRead(value, out result),
        };
    }

    /// <summary>
    ///     Leaves a value as read (the custom date formats, which are not implemented).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="result">Receives the same value.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool KeepAsRead(DataValue value, out DataValue result)
    {
        result = value;
        return true;
    }

    /// <summary>
    ///     Converts a value to a number.
    /// </summary>
    /// <param name="value">The non-null value.</param>
    /// <param name="result">Receives the result.</param>
    /// <returns><see langword="false"/> when a text is not a number.</returns>
    private static bool TryToNumber(DataValue value, out DataValue result)
    {
        result = DataValue.Null;
        switch (value.Kind)
        {
            case DataValueKind.Number:
                result = value;
                return true;
            case DataValueKind.Boolean:
                result = DataValue.FromNumber(value.Boolean ? 1 : 0);
                return true;
            case DataValueKind.Date:
                result = DataValue.FromNumber(value.Milliseconds);
                return true;
            default:
                if (string.IsNullOrWhiteSpace(value.Text))
                {
                    return true;
                }

                if (TryParseNumber(value.Text, out var number))
                {
                    result = DataValue.FromNumber(number);
                    return true;
                }

                return false;
        }
    }

    /// <summary>
    ///     Converts a value to a boolean.
    /// </summary>
    /// <param name="value">The non-null value.</param>
    /// <param name="result">Receives the result.</param>
    /// <returns><see langword="false"/> when the value has no boolean meaning.</returns>
    private static bool TryToBoolean(DataValue value, out DataValue result)
    {
        result = DataValue.Null;
        switch (value.Kind)
        {
            case DataValueKind.Boolean:
                result = value;
                return true;
            case DataValueKind.Number:
                result = DataValue.FromBoolean(value.Number != 0);
                return true;
            case DataValueKind.Date:
                return false;
            default:
                var text = value.Text!.Trim();
                if (text.Length == 0)
                {
                    return true;
                }

                if (TryParseBoolean(text, out var flag))
                {
                    result = DataValue.FromBoolean(flag);
                    return true;
                }

                // The digits 1 and 0 are accepted as the CSV spelling of true and false
                if (text is "1" or "0")
                {
                    result = DataValue.FromBoolean(text == "1");
                    return true;
                }

                return false;
        }
    }

    /// <summary>
    ///     Converts a value to a date.
    /// </summary>
    /// <param name="value">The non-null value.</param>
    /// <param name="result">Receives the result.</param>
    /// <returns><see langword="false"/> when the value is not a date.</returns>
    private static bool TryToDate(DataValue value, out DataValue result)
    {
        result = DataValue.Null;
        switch (value.Kind)
        {
            case DataValueKind.Date:
                result = value;
                return true;
            case DataValueKind.Number:
                // A number is taken as milliseconds since the epoch, like JavaScript's new Date(number)
                try
                {
                    result = DataValue.FromDate(value.Number);
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    return false;
                }

            case DataValueKind.Boolean:
                return false;
            default:
                if (string.IsNullOrWhiteSpace(value.Text))
                {
                    return true;
                }

                if (TryParseIsoDate(value.Text, out var milliseconds))
                {
                    result = DataValue.FromDate(milliseconds);
                    return true;
                }

                return false;
        }
    }

    /// <summary>
    ///     Converts a value to its text.
    /// </summary>
    /// <param name="value">The non-null value.</param>
    /// <param name="result">Receives the text value.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private static bool TryToString(DataValue value, out DataValue result)
    {
        result = DataValue.FromString(value.ToText());
        return true;
    }

    /// <summary>
    ///     Converts one field of every row to an explicit type.
    /// </summary>
    /// <param name="records">The rows.</param>
    /// <param name="field">The canonical field name.</param>
    /// <param name="type">The target type.</param>
    /// <param name="failures">Collects the failures.</param>
    private static void ApplyField(IReadOnlyList<Dictionary<string, DataValue>> records, string field, ParseType type, ParseFailures failures)
    {
        // A custom date format is reported by the feature catalog and leaves the field as read
        if (type == ParseType.CustomDate)
        {
            return;
        }

        for (var row = 0; row < records.Count; row++)
        {
            if (!records[row].TryGetValue(field, out var value))
            {
                continue;
            }

            if (!TryConvert(value, type, out var converted))
            {
                failures.Add(field, type, row, value.ToText());
            }

            records[row][field] = converted;
        }
    }

    /// <summary>
    ///     Infers the type of every text column and converts the columns that have one.
    /// </summary>
    /// <param name="records">The rows.</param>
    /// <param name="cancellation">The token polled between fields.</param>
    private static void ApplyAuto(IReadOnlyList<Dictionary<string, DataValue>> records, CancellationToken cancellation)
    {
        var fields = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            fields.UnionWith(record.Keys);
        }

        foreach (var field in fields)
        {
            cancellation.ThrowIfCancellationRequested();
            var type = InferColumn(records, field);
            if (type is null)
            {
                continue;
            }

            // Every non-empty text of the column converts by construction, so no failure can occur here
            foreach (var record in records)
            {
                if (record.TryGetValue(field, out var value) && value.IsString && TryConvert(value, type.Value, out var converted))
                {
                    record[field] = converted;
                }
            }
        }
    }

    /// <summary>
    ///     Infers the type of one column from its texts.
    /// </summary>
    /// <param name="records">The rows.</param>
    /// <param name="field">The canonical field name.</param>
    /// <returns>The type, or <see langword="null"/> when the column stays as it is.</returns>
    private static ParseType? InferColumn(IReadOnlyList<Dictionary<string, DataValue>> records, string field)
    {
        var canBeBoolean = true;
        var canBeNumber = true;
        var canBeDate = true;
        var seen = false;
        foreach (var record in records)
        {
            if (!record.TryGetValue(field, out var value) || value.IsNull)
            {
                continue;
            }

            // A column that holds anything but text (JSON numbers, booleans) already has a type
            if (!value.IsString)
            {
                return null;
            }

            var text = value.Text!;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            seen = true;
            canBeBoolean = canBeBoolean && TryParseBoolean(text, out _);
            canBeNumber = canBeNumber && TryParseNumber(text, out _);
            canBeDate = canBeDate && TryParseIsoDate(text, out _);
            if (!canBeBoolean && !canBeNumber && !canBeDate)
            {
                return null;
            }
        }

        if (!seen)
        {
            return null;
        }

        if (canBeBoolean)
        {
            return ParseType.Boolean;
        }

        return canBeNumber ? ParseType.Number : ParseType.Date;
    }
}

/// <summary>
///     Collects the values of one data source that an explicit conversion could not read, so that one warning per field
///     and type can report them instead of one per value.
/// </summary>
internal sealed class ParseFailures
{
    /// <summary>The longest part of a value that is quoted in a message.</summary>
    private const int MaxQuoted = 40;

    /// <summary>The failures by field and type: how many, and the first row and text.</summary>
    private readonly SortedDictionary<(string Field, ParseType Type), (long Count, long FirstRow, string FirstText)> _failures =
        new(Comparer<(string Field, ParseType Type)>.Create((a, b) =>
        {
            var byField = string.CompareOrdinal(a.Field, b.Field);
            return byField != 0 ? byField : a.Type.CompareTo(b.Type);
        }));

    /// <summary>Gets a value indicating whether no failure was recorded.</summary>
    public bool IsEmpty => _failures.Count == 0;

    /// <summary>
    ///     Records one value that could not be converted.
    /// </summary>
    /// <param name="field">The canonical field name.</param>
    /// <param name="type">The type the value was converted to.</param>
    /// <param name="row">The zero-based row of the value.</param>
    /// <param name="text">The text of the value.</param>
    public void Add(string field, ParseType type, long row, string text)
    {
        var key = (field, type);
        _failures[key] = _failures.TryGetValue(key, out var existing)
            ? (existing.Count + 1, existing.FirstRow, existing.FirstText)
            : (1, row, text);
    }

    /// <summary>
    ///     Creates one warning per field and type that had failures.
    /// </summary>
    /// <param name="location">The location of the data.</param>
    /// <returns>The warnings, in field order.</returns>
    public IEnumerable<FeatureReport> ToReports(VegaLiteLocation location)
    {
        foreach (var ((field, type), (count, firstRow, firstText)) in _failures)
        {
            var quoted = new string(firstText.Take(MaxQuoted).Select(c => c < ' ' || c == '\u007F' ? '?' : c).ToArray());
            var more = firstText.Length > MaxQuoted ? "..." : string.Empty;
            yield return new FeatureReport(
                "vl-data-parse-failed",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{count} value(s) of the field '{field}' could not be read as {ParseDirectives.NameOf(type)} and are null (the first is in row {firstRow + 1}: '{quoted}{more}')."),
                location,
                FeatureReportKind.Warning);
        }
    }
}

/// <summary>
///     What the delimited text parser needs to know about one read.
/// </summary>
/// <param name="Delimiter">The delimiter: a comma for CSV, a tab for TSV, or the character of a DSV format.</param>
/// <param name="Directives">The conversions to apply after reading.</param>
/// <param name="Limits">The limits (field count and string length).</param>
/// <param name="RowBudget">The number of rows the read may still produce under <see cref="VegaLiteLimits.MaxDataRows"/>.</param>
/// <param name="Display">The name of the data in messages.</param>
/// <param name="Location">The location of the data in the specification, for errors.</param>
/// <param name="Cancellation">The token that stops the read.</param>
internal sealed record DelimitedOptions(
    char Delimiter,
    ParseDirectives Directives,
    VegaLiteLimits Limits,
    long RowBudget,
    string Display,
    VegaLiteLocation Location,
    CancellationToken Cancellation);

/// <summary>
///     The rows that a delimited text yields, and the values that conversion could not read.
/// </summary>
/// <param name="Records">The rows: one dictionary per row from canonical field name to value.</param>
/// <param name="Failures">The values that an explicit conversion could not read.</param>
internal sealed record DelimitedResult(List<Dictionary<string, DataValue>> Records, ParseFailures Failures);

/// <summary>
///     Reads delimiter-separated text (CSV, TSV, DSV) into rows of typed values.
/// </summary>
/// <remarks>
///     <para>
///         <b>Grammar.</b> The first record is the header; its entries name the fields (a name is stored as a canonical
///         field name, so a header <c>a.b</c> is addressed as <c>a\.b</c>, like a JSON key with a dot). Records end at a
///         line feed, a carriage return followed by a line feed, or a lone carriage return. A field is either unquoted
///         (it runs to the next delimiter or line end; a quote inside it is an ordinary character) or quoted with a
///         double quote (it may hold delimiters and line breaks, and a doubled quote stands for one quote); anything
///         between a closing quote and the next delimiter or line end is an error, as is a quote that is never closed.
///         A line without any character is skipped. A leading U+FEFF is ignored. The text is already decoded (the
///         file resolver decodes strict UTF-8).
///     </para>
///     <para>
///         <b>Ragged rows.</b> A row with fewer entries than the header has null in the missing fields; entries beyond
///         the header are ignored. A repeated header name or more header names than
///         <see cref="VegaLiteLimits.MaxFieldCount"/> is an error, as is a field text longer than
///         <see cref="VegaLiteLimits.MaxStringLength"/> and more rows than the budget allows. Every error is a
///         <see cref="VegaLiteRenderException"/> with the location of the data and, where it helps, the line of the text.
///     </para>
///     <para>
///         <b>Types.</b> The cells are read as text and then converted by the <see cref="ParseDirectives"/>; the default
///         for delimited text is inference per column (see <see cref="ParseDirectives"/>).
///     </para>
/// </remarks>
internal static class DelimitedTextParser
{
    /// <summary>The number of records between two cancellation checks.</summary>
    private const int CancellationInterval = 1024;

    /// <summary>
    ///     Determines whether a character can be a delimiter: any single UTF-16 unit except a quote, a line break and a surrogate.
    /// </summary>
    /// <param name="delimiter">The character.</param>
    /// <returns><see langword="true"/> when it can separate fields.</returns>
    public static bool IsValidDelimiter(char delimiter) =>
        delimiter is not ('"' or '\r' or '\n') && !char.IsSurrogate(delimiter);

    /// <summary>
    ///     Reads delimited text into rows and applies the conversions.
    /// </summary>
    /// <param name="text">The decoded text.</param>
    /// <param name="options">The delimiter, conversions, limits and error context.</param>
    /// <returns>The rows and the values that could not be converted.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    /// <exception cref="VegaLiteRenderException">Thrown when the text is malformed or exceeds a limit, or the delimiter is not valid.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the cancellation token is canceled.</exception>
    public static DelimitedResult Parse(string text, DelimitedOptions options)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);
        if (!IsValidDelimiter(options.Delimiter))
        {
            throw new VegaLiteRenderException("The delimiter must be a single character other than a quote or a line break.", options.Location);
        }

        var reader = new Reader(text, options);
        var header = reader.ReadRecord();
        var records = new List<Dictionary<string, DataValue>>();
        var failures = new ParseFailures();
        if (header is null)
        {
            return new DelimitedResult(records, failures);
        }

        var names = ReadHeader(header, options);
        var count = 0;
        while (reader.ReadRecord() is { } cells)
        {
            if (++count % CancellationInterval == 0)
            {
                options.Cancellation.ThrowIfCancellationRequested();
            }

            if (count > options.RowBudget)
            {
                throw new VegaLiteRenderException(
                    $"The data '{options.Display}' exceeds the limit of {options.Limits.MaxDataRows} rows in total (MaxDataRows).", options.Location);
            }

            // Missing entries are null and extra entries are ignored
            var record = new Dictionary<string, DataValue>(names.Length, StringComparer.Ordinal);
            for (var i = 0; i < names.Length; i++)
            {
                record[names[i]] = i < cells.Count ? DataValue.FromString(cells[i]) : DataValue.Null;
            }

            records.Add(record);
        }

        options.Directives.Apply(records, failures, options.Cancellation);
        return new DelimitedResult(records, failures);
    }

    /// <summary>
    ///     Turns the header record into canonical field names.
    /// </summary>
    /// <param name="header">The entries of the header record.</param>
    /// <param name="options">The options.</param>
    /// <returns>The canonical names, in column order.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when there are too many columns or a name is repeated.</exception>
    private static string[] ReadHeader(List<string> header, DelimitedOptions options)
    {
        if (header.Count > options.Limits.MaxFieldCount)
        {
            throw new VegaLiteRenderException(
                $"The data '{options.Display}' has {header.Count} columns, more than the limit of {options.Limits.MaxFieldCount} (MaxFieldCount).", options.Location);
        }

        var names = new string[header.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < names.Length; i++)
        {
            names[i] = FieldPath.Escape(header[i]);
            if (!seen.Add(names[i]))
            {
                throw new VegaLiteRenderException($"The data '{options.Display}' has the column '{header[i]}' more than once.", options.Location);
            }
        }

        return names;
    }

    /// <summary>
    ///     A forward reader of the records of delimited text.
    /// </summary>
    private sealed class Reader
    {
        /// <summary>The text.</summary>
        private readonly string _text;

        /// <summary>The options.</summary>
        private readonly DelimitedOptions _options;

        /// <summary>The index of the next character.</summary>
        private int _position;

        /// <summary>The one-based line of the next character.</summary>
        private int _line = 1;

        /// <summary>
        ///     Initializes a reader.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <param name="options">The options.</param>
        public Reader(string text, DelimitedOptions options)
        {
            _text = text;
            _options = options;

            // A byte-order mark that survived decoding is not part of the first field
            _position = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
        }

        /// <summary>
        ///     Reads the next record, skipping lines that have no character.
        /// </summary>
        /// <returns>The entries of the record, or <see langword="null"/> at the end of the text.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown for a malformed record.</exception>
        public List<string>? ReadRecord()
        {
            // Skip empty lines (and the line break at the very end of the text)
            while (_position < _text.Length && IsLineBreak(_text[_position]))
            {
                ConsumeLineBreak();
            }

            if (_position >= _text.Length)
            {
                return null;
            }

            var cells = new List<string>();
            while (true)
            {
                cells.Add(ReadField());
                if (_position >= _text.Length)
                {
                    return cells;
                }

                var c = _text[_position];
                if (c == _options.Delimiter)
                {
                    _position++;
                    continue;
                }

                // Only a line break can follow a field now
                ConsumeLineBreak();
                return cells;
            }
        }

        /// <summary>
        ///     Reads one field.
        /// </summary>
        /// <returns>The text of the field.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown for an unterminated quote, text after a closing quote, or a field that is too long.</exception>
        private string ReadField() =>
            _position < _text.Length && _text[_position] == '"' ? ReadQuoted() : ReadUnquoted();

        /// <summary>
        ///     Reads an unquoted field: everything up to the next delimiter or line break.
        /// </summary>
        /// <returns>The text.</returns>
        private string ReadUnquoted()
        {
            var start = _position;
            while (_position < _text.Length && _text[_position] != _options.Delimiter && !IsLineBreak(_text[_position]))
            {
                _position++;
            }

            CheckLength(_position - start);
            return _text.Substring(start, _position - start);
        }

        /// <summary>
        ///     Reads a quoted field: a doubled quote is one quote, and delimiters and line breaks are text.
        /// </summary>
        /// <returns>The text between the quotes.</returns>
        private string ReadQuoted()
        {
            var startLine = _line;
            var builder = new StringBuilder();
            _position++;
            while (true)
            {
                if (_position >= _text.Length)
                {
                    throw Error($"a quoted field that starts on line {startLine} is not closed");
                }

                var c = _text[_position++];
                if (c == '"')
                {
                    if (_position < _text.Length && _text[_position] == '"')
                    {
                        builder.Append('"');
                        _position++;
                        CheckLength(builder.Length);
                        continue;
                    }

                    break;
                }

                if (c == '\n')
                {
                    _line++;
                }

                builder.Append(c);
                CheckLength(builder.Length);
            }

            // Nothing may stand between the closing quote and the next delimiter or line break
            if (_position < _text.Length && _text[_position] != _options.Delimiter && !IsLineBreak(_text[_position]))
            {
                throw Error("unexpected text after the closing quote of a field");
            }

            return builder.ToString();
        }

        /// <summary>
        ///     Consumes one line break: a line feed, a carriage return and a line feed, or a lone carriage return.
        /// </summary>
        private void ConsumeLineBreak()
        {
            if (_text[_position] == '\r' && _position + 1 < _text.Length && _text[_position + 1] == '\n')
            {
                _position++;
            }

            _position++;
            _line++;
        }

        /// <summary>
        ///     Checks the length of a field against <see cref="VegaLiteLimits.MaxStringLength"/>.
        /// </summary>
        /// <param name="length">The length so far.</param>
        private void CheckLength(int length)
        {
            if (length > _options.Limits.MaxStringLength)
            {
                throw Error($"a field on line {_line} is longer than the limit of {_options.Limits.MaxStringLength} characters (MaxStringLength)");
            }
        }

        /// <summary>
        ///     Creates the exception for a malformed text.
        /// </summary>
        /// <param name="description">What is wrong.</param>
        /// <returns>The exception, ready to throw.</returns>
        private VegaLiteRenderException Error(string description) =>
            new($"The data '{_options.Display}' is not valid delimited text: {description}.", _options.Location);

        /// <summary>
        ///     Determines whether a character ends a line.
        /// </summary>
        /// <param name="c">The character.</param>
        /// <returns><see langword="true"/> for a line feed or a carriage return.</returns>
        private static bool IsLineBreak(char c) => c is '\n' or '\r';
    }
}
