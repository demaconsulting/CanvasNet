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

    /// <summary>A date (<c>"date"</c>: the <c>Date.parse</c> rules; <c>"date:'%d/%m/%Y'"</c> and <c>"utc:'...'"</c>: a d3 time format, see <see cref="ParseSpec"/>).</summary>
    Date,

    /// <summary>The text of the value (<c>"string"</c>).</summary>
    String,
}

/// <summary>
///     One conversion of a <c>format.parse</c> entry: its type and, for a date, the optional d3 time format.
/// </summary>
/// <param name="Type">The type.</param>
/// <param name="Format">The d3 time format of <c>date:'%fmt'</c> and <c>utc:'%fmt'</c>; null for every other conversion (a plain <c>date</c> uses the <c>Date.parse</c> rules).</param>
/// <param name="Utc">Whether the format is read as UTC (<c>utc:'%fmt'</c>) instead of in the zone of the calendar (<c>date:'%fmt'</c>).</param>
internal readonly record struct ParseSpec(ParseType Type, string? Format = null, bool Utc = false)
{
    /// <summary>Gets a value indicating whether this is a date with a custom format.</summary>
    public bool IsCustomDate => Type == ParseType.Date && Format is not null;
}

/// <summary>
///     How the values of a data source are converted: not at all, by inferring the type of every text column, or by
///     the explicit type of named fields.
/// </summary>
internal enum ParseMode
{
    /// <summary>No conversion: every value keeps the type it was read with (<c>parse: null</c>).</summary>
    None,

    /// <summary>The type of each column of text is inferred from its values (<c>parse: "auto"</c>); always an explicit opt-in, never a default of the loader.</summary>
    Auto,

    /// <summary>The fields named in <see cref="ParseDirectives.Fields"/> are converted; all others keep their type.</summary>
    Fields,
}

/// <summary>
///     The conversions that a <c>format.parse</c> property asks for, and the rules that apply them.
/// </summary>
/// <remarks>
///     <para>
///         <b>Modes.</b> As in Vega, no data-based type inference happens unless the specification asks for it: delimited
///         text (CSV, TSV, DSV) and JSON data both default to <see cref="ParseMode.None"/>, so every delimited cell
///         stays a string (a leading zero or an ISO-looking date is kept as text) and JSON keeps its JSON types.
///         <c>parse: "auto"</c> is the explicit opt-in to <see cref="ParseMode.Auto"/> inference. <c>parse: null</c> keeps
///         every value as read. An object names the fields to convert (<c>number</c>, <c>boolean</c>, <c>date</c>,
///         <c>string</c>); the keys are field paths, compared in their canonical form like the fields of an encoding,
///         and only the named fields are converted. All other fields keep their type. Quantitative encodings still
///         coerce numeric text to numbers when they are drawn; that is not a parse mode.
///     </para>
///     <para>
///         <b>Auto inference</b> looks at a whole column, as Vega does: a column is a boolean column when every non-empty
///         text is <c>true</c> or <c>false</c> (any case), a number column when every non-empty text is a finite
///         invariant integer, decimal or exponent number, and a date column when every non-empty text is a date that
///         JavaScript's <c>Date.parse</c> reads (see <see cref="JsDateParser"/>), tested in that order; otherwise it stays
///         text. In a typed column an empty text is null. A column that holds a value that is not text (JSON) is left alone.
///         A date without a time is UTC and a date-time without an offset is in the zone of the calendar (UTC by default).
///     </para>
///     <para>
///         <b>Explicit conversion</b> never invents a value: a text that cannot be converted becomes null and is counted
///         in a <see cref="ParseFailures"/> record, which the loader reports as the warning <c>vl-data-parse-failed</c>.
///         Null stays null and an empty text becomes null (except for <c>string</c>). <c>date</c> uses the
///         <c>Date.parse</c> rules, and <c>date:'%fmt'</c> and <c>utc:'%fmt'</c> read the text with a d3 time format
///         (<see cref="TimeFormat.TryParse"/>) in the zone of the calendar or in UTC; a number is read through its
///         JavaScript text. A text of more than 128 characters (256 for a custom format) is never a date, so a huge cell
///         costs no more than a short one.
///     </para>
///     <para>
///         <b>Implicit dates.</b> A temporal field of the encoding is date-parsed even when <c>parse</c> does not name it
///         (Vega-Lite's implicit <c>parse</c>): the loader passes those fields for the main data only. An explicit entry
///         for the field wins, and <c>parse: null</c> switches the implicit parse off; with <c>parse: "auto"</c> the
///         inference runs first and an implicit field that is still text is date-parsed afterwards.
///     </para>
///     <para>
///         <b>Cancellation</b> is polled before every field and every 4,096 rows.
///     </para>
/// </remarks>
internal sealed class ParseDirectives
{
    /// <summary>The number of rows between two cancellation checks.</summary>
    private const int CancellationInterval = 4_096;

    /// <summary>The directives that convert nothing.</summary>
    private static readonly ParseDirectives NoneInstance = new(ParseMode.None, new Dictionary<string, ParseSpec>(), [], suppressed: false);

    /// <summary>The directives that infer the type of each column.</summary>
    private static readonly ParseDirectives AutoInstance = new(ParseMode.Auto, new Dictionary<string, ParseSpec>(), [], suppressed: false);

    /// <summary>The fields that stay to be date-parsed after the inference of <see cref="ParseMode.Auto"/> when they are still text.</summary>
    private readonly IReadOnlyCollection<string> _implicitDates;

    /// <summary>
    ///     Initializes directives.
    /// </summary>
    /// <param name="mode">The mode.</param>
    /// <param name="fields">The explicit conversions by canonical field name.</param>
    /// <param name="implicitDates">The implicit date fields that <see cref="ParseMode.Auto"/> parses when inference left them as text.</param>
    /// <param name="suppressed">Whether the <c>parse</c> was JSON null.</param>
    private ParseDirectives(ParseMode mode, IReadOnlyDictionary<string, ParseSpec> fields, IReadOnlyCollection<string> implicitDates, bool suppressed)
    {
        Mode = mode;
        Fields = fields;
        _implicitDates = implicitDates;
        Suppressed = suppressed;
    }

    /// <summary>Gets the directives that convert nothing.</summary>
    public static ParseDirectives None => NoneInstance;

    /// <summary>Gets the directives that infer the type of each column.</summary>
    public static ParseDirectives Auto => AutoInstance;

    /// <summary>Gets the mode.</summary>
    public ParseMode Mode { get; }

    /// <summary>Gets the explicit conversions by canonical field name (empty unless <see cref="Mode"/> is <see cref="ParseMode.Fields"/>); the implicit date fields are included.</summary>
    public IReadOnlyDictionary<string, ParseSpec> Fields { get; }

    /// <summary>Gets a value indicating whether the <c>parse</c> property was JSON null, which converts nothing and also switches the implicit date parse off.</summary>
    public bool Suppressed { get; }

    /// <summary>
    ///     Reads the directives of a data format.
    /// </summary>
    /// <param name="format">The <c>format</c> object, or null.</param>
    /// <param name="defaultAuto">Whether an absent <c>parse</c> means <see cref="ParseMode.Auto"/> or <see cref="ParseMode.None"/>; the loader always passes <see langword="false"/> (Vega applies no inference unless <c>parse</c> is <c>"auto"</c>).</param>
    /// <param name="implicitDateFields">The canonical names of the fields that are date-parsed unless <c>parse</c> says otherwise (the temporal fields of the encoding); null or empty for none.</param>
    /// <returns>The directives.</returns>
    public static ParseDirectives Read(SpecNode? format, bool defaultAuto, IReadOnlyCollection<string>? implicitDateFields = null)
    {
        var parse = format is { IsObject: true } ? format["parse"] : null;
        IReadOnlyCollection<string> implicitDates = implicitDateFields ?? [];
        if (parse is null)
        {
            // An absent parse is the one case where the implicit dates are the whole conversion
            if (defaultAuto)
            {
                return implicitDates.Count == 0 ? Auto : new ParseDirectives(ParseMode.Auto, new Dictionary<string, ParseSpec>(), implicitDates, suppressed: false);
            }

            return implicitDates.Count == 0 ? None : new ParseDirectives(ParseMode.Fields, ImplicitOnly(implicitDates), [], suppressed: false);
        }

        if (parse.IsObject)
        {
            var fields = new Dictionary<string, ParseSpec>(StringComparer.Ordinal);
            foreach (var entry in parse.Properties)
            {
                // Entries that validation would have rejected are skipped; the specification parser makes them errors
                if (entry.IsString && TryClassify(entry.StringValue!, out var spec))
                {
                    fields[FieldPath.Canonicalize(entry.Name) ?? FieldPath.Escape(entry.Name!)] = spec;
                }
            }

            // An explicit entry of the field wins over the implicit date parse
            foreach (var name in implicitDates)
            {
                fields.TryAdd(name, new ParseSpec(ParseType.Date));
            }

            return new ParseDirectives(ParseMode.Fields, fields, [], suppressed: false);
        }

        if (parse.IsString && string.Equals(parse.StringValue, "auto", StringComparison.Ordinal))
        {
            return implicitDates.Count == 0 ? Auto : new ParseDirectives(ParseMode.Auto, new Dictionary<string, ParseSpec>(), implicitDates, suppressed: false);
        }

        return parse.IsNull ? new ParseDirectives(ParseMode.None, new Dictionary<string, ParseSpec>(), [], suppressed: true) : None;
    }

    /// <summary>
    ///     Classifies the text of a <c>format.parse</c> entry.
    /// </summary>
    /// <param name="text">The entry value, such as <c>number</c> or <c>date:'%Y'</c>.</param>
    /// <param name="spec">Receives the conversion.</param>
    /// <returns><see langword="true"/> when the text is a known conversion; a <c>date:</c> or <c>utc:</c> entry needs its format in single or double quotes.</returns>
    public static bool TryClassify(string text, out ParseSpec spec)
    {
        switch (text)
        {
            case "number":
                spec = new ParseSpec(ParseType.Number);
                return true;
            case "boolean":
                spec = new ParseSpec(ParseType.Boolean);
                return true;
            case "date":
                spec = new ParseSpec(ParseType.Date);
                return true;
            case "string":
                spec = new ParseSpec(ParseType.String);
                return true;
            default:
                // date:'%fmt' and utc:'%fmt' carry a custom time format in quotes
                var utc = text.StartsWith("utc:", StringComparison.Ordinal);
                spec = default;
                if (!utc && !text.StartsWith("date:", StringComparison.Ordinal))
                {
                    return false;
                }

                var quoted = text.AsSpan(utc ? 4 : 5);
                if (quoted.Length < 2 || quoted[0] is not ('\'' or '"') || quoted[^1] != quoted[0])
                {
                    return false;
                }

                spec = new ParseSpec(ParseType.Date, quoted[1..^1].ToString(), utc);
                return true;
        }
    }

    /// <summary>
    ///     Applies the directives to the rows of one data source, in place.
    /// </summary>
    /// <param name="records">The rows; each maps canonical field names to values.</param>
    /// <param name="failures">Collects the values that could not be converted.</param>
    /// <param name="cancellation">The token polled before every field and every 4,096 rows.</param>
    /// <param name="calendar">The calendar of dates without an offset and of <c>date:'%fmt'</c> formats; UTC when null.</param>
    public void Apply(IReadOnlyList<Dictionary<string, DataValue>> records, ParseFailures failures, CancellationToken cancellation, TimeCalendar? calendar = null)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(failures);
        var effective = calendar ?? TimeCalendar.Utc;
        switch (Mode)
        {
            case ParseMode.Auto:
                ApplyAuto(records, failures, cancellation, effective);
                break;
            case ParseMode.Fields:
                foreach (var (field, spec) in Fields.OrderBy(f => f.Key, StringComparer.Ordinal))
                {
                    cancellation.ThrowIfCancellationRequested();
                    ApplyField(records, field, spec, failures, cancellation, effective);
                }

                break;
            default:
                break;
        }
    }

    /// <summary>
    ///     Creates the conversions of the implicit date fields alone.
    /// </summary>
    /// <param name="implicitDates">The canonical field names.</param>
    /// <returns>The conversions, a plain date each.</returns>
    private static Dictionary<string, ParseSpec> ImplicitOnly(IReadOnlyCollection<string> implicitDates)
    {
        var fields = new Dictionary<string, ParseSpec>(StringComparer.Ordinal);
        foreach (var name in implicitDates)
        {
            fields[name] = new ParseSpec(ParseType.Date);
        }

        return fields;
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
    ///     Tries to read a date from text with the rules of JavaScript's <c>Date.parse</c>.
    /// </summary>
    /// <remarks>
    ///     Surrounding white space is ignored; the trimmed text is handed to <see cref="JsDateParser"/> only when it is short
    ///     enough to be a date, so a huge cell is not copied or scanned beyond its white space.
    /// </remarks>
    /// <param name="text">The text.</param>
    /// <param name="calendar">The calendar whose zone reads a date-time without an offset.</param>
    /// <param name="milliseconds">Receives the milliseconds since 1970-01-01T00:00:00Z.</param>
    /// <returns><see langword="true"/> when the text is a date in the supported range.</returns>
    public static bool TryParseDate(string text, TimeCalendar calendar, out double milliseconds)
    {
        var trimmed = text.AsSpan().Trim();
        if (trimmed.Length == 0 || trimmed.Length > JsDateParser.MaxLength)
        {
            milliseconds = double.NaN;
            return false;
        }

        return JsDateParser.TryParse(trimmed.Length == text.Length ? text : trimmed.ToString(), calendar, out milliseconds);
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
        _ => "string",
    };

    /// <summary>
    ///     Gets the name of a conversion for messages, telling a date with a custom format from a plain date.
    /// </summary>
    /// <param name="spec">The conversion.</param>
    /// <returns>The name; <c>date with a custom format</c> for <c>date:'%fmt'</c> and <c>utc:'%fmt'</c>.</returns>
    public static string NameOf(ParseSpec spec) => spec.IsCustomDate ? "date with a custom format" : NameOf(spec.Type);

    /// <summary>
    ///     Converts one value to a type; a date is read in UTC.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="type">The target type.</param>
    /// <param name="result">Receives the converted value (null when the value is null, empty or cannot be converted).</param>
    /// <returns><see langword="false"/> when a value that is present could not be converted; otherwise <see langword="true"/>.</returns>
    public static bool TryConvert(DataValue value, ParseType type, out DataValue result) =>
        TryConvert(value, new ParseSpec(type), TimeCalendar.Utc, out result);

    /// <summary>
    ///     Converts one value to a type.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="spec">The conversion.</param>
    /// <param name="calendar">The calendar of dates without an offset and of <c>date:'%fmt'</c> formats.</param>
    /// <param name="result">Receives the converted value (null when the value is null, empty or cannot be converted).</param>
    /// <returns><see langword="false"/> when a value that is present could not be converted; otherwise <see langword="true"/>.</returns>
    public static bool TryConvert(DataValue value, ParseSpec spec, TimeCalendar calendar, out DataValue result)
    {
        result = DataValue.Null;
        if (value.IsNull)
        {
            return true;
        }

        return spec.Type switch
        {
            ParseType.Number => TryToNumber(value, out result),
            ParseType.Boolean => TryToBoolean(value, out result),
            ParseType.Date => TryToDate(value, spec, calendar, out result),
            _ => TryToString(value, out result),
        };
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
    /// <param name="spec">The conversion: a plain date or a date with a custom format.</param>
    /// <param name="calendar">The calendar of dates without an offset and of <c>date:'%fmt'</c> formats.</param>
    /// <param name="result">Receives the result.</param>
    /// <returns><see langword="false"/> when the value is not a date.</returns>
    private static bool TryToDate(DataValue value, ParseSpec spec, TimeCalendar calendar, out DataValue result)
    {
        result = DataValue.Null;
        switch (value.Kind)
        {
            case DataValueKind.Date:
                result = value;
                return true;
            case DataValueKind.Number when !spec.IsCustomDate:
                // A number is taken as milliseconds since the epoch, like JavaScript's new Date(number)
                return TryMakeDate(value.Number, out result);
            case DataValueKind.Boolean:
                return false;
            default:
                // A number with a custom format is read through its JavaScript text, as d3's parser coerces its input to a string
                var text = value.Kind == DataValueKind.Number ? value.ToText() : value.Text ?? string.Empty;
                if (string.IsNullOrWhiteSpace(text))
                {
                    return true;
                }

                // Both readers only return milliseconds inside the supported range; a failure to convert is the ordinary
                // "not a date" outcome (null with a warning, or text under auto inference), never a raw exception
                bool parsed;
                double milliseconds;
                if (spec.IsCustomDate)
                {
                    parsed = TimeFormat.TryParse(spec.Format!, text, spec.Utc ? TimeCalendar.Utc : calendar, out milliseconds);
                }
                else
                {
                    parsed = TryParseDate(text, calendar, out milliseconds);
                }

                return parsed && TryMakeDate(milliseconds, out result);
        }
    }

    /// <summary>
    ///     Makes a date value from milliseconds.
    /// </summary>
    /// <param name="milliseconds">The milliseconds since the epoch.</param>
    /// <param name="result">Receives the date.</param>
    /// <returns><see langword="false"/> when the milliseconds are not a supported date.</returns>
    private static bool TryMakeDate(double milliseconds, out DataValue result)
    {
        try
        {
            result = DataValue.FromDate(milliseconds);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = DataValue.Null;
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
    /// <param name="spec">The target conversion.</param>
    /// <param name="failures">Collects the failures.</param>
    /// <param name="cancellation">The token polled every 4,096 rows.</param>
    /// <param name="calendar">The calendar of dates.</param>
    private static void ApplyField(
        IReadOnlyList<Dictionary<string, DataValue>> records,
        string field,
        ParseSpec spec,
        ParseFailures failures,
        CancellationToken cancellation,
        TimeCalendar calendar)
    {
        for (var row = 0; row < records.Count; row++)
        {
            if (row % CancellationInterval == CancellationInterval - 1)
            {
                cancellation.ThrowIfCancellationRequested();
            }

            if (!records[row].TryGetValue(field, out var value))
            {
                continue;
            }

            if (!TryConvert(value, spec, calendar, out var converted))
            {
                failures.Add(field, spec, row, value.ToText());
            }

            records[row][field] = converted;
        }
    }

    /// <summary>
    ///     Infers the type of every text column and converts the columns that have one; the implicit date fields that
    ///     inference leaves alone are date-parsed.
    /// </summary>
    /// <param name="records">The rows.</param>
    /// <param name="failures">Collects the values of an implicit date field that are not dates.</param>
    /// <param name="cancellation">The token polled before every field and every 4,096 rows.</param>
    /// <param name="calendar">The calendar of dates.</param>
    private void ApplyAuto(IReadOnlyList<Dictionary<string, DataValue>> records, ParseFailures failures, CancellationToken cancellation, TimeCalendar calendar)
    {
        var fields = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            fields.UnionWith(record.Keys);
        }

        foreach (var field in fields)
        {
            cancellation.ThrowIfCancellationRequested();
            var type = InferColumn(records, field, cancellation, calendar);
            if (type is null)
            {
                // A temporal field of the encoding that is still text (or a mixed column) is a date column all the same
                if (_implicitDates.Contains(field))
                {
                    ApplyField(records, field, new ParseSpec(ParseType.Date), failures, cancellation, calendar);
                }

                continue;
            }

            // Every non-empty text of the column converts by construction, so no failure can occur here
            var spec = new ParseSpec(type.Value);
            for (var row = 0; row < records.Count; row++)
            {
                if (row % CancellationInterval == CancellationInterval - 1)
                {
                    cancellation.ThrowIfCancellationRequested();
                }

                var record = records[row];
                if (record.TryGetValue(field, out var value) && value.IsString && TryConvert(value, spec, calendar, out var converted))
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
    /// <param name="cancellation">The token polled every 4,096 rows.</param>
    /// <param name="calendar">The calendar of dates.</param>
    /// <returns>The type, or <see langword="null"/> when the column stays as it is.</returns>
    private static ParseType? InferColumn(IReadOnlyList<Dictionary<string, DataValue>> records, string field, CancellationToken cancellation, TimeCalendar calendar)
    {
        var canBeBoolean = true;
        var canBeNumber = true;
        var canBeDate = true;
        var seen = false;
        for (var row = 0; row < records.Count; row++)
        {
            if (row % CancellationInterval == CancellationInterval - 1)
            {
                cancellation.ThrowIfCancellationRequested();
            }

            if (!records[row].TryGetValue(field, out var value) || value.IsNull)
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
            canBeDate = canBeDate && TryParseDate(text, calendar, out _);
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

    /// <summary>The failures by field and conversion name: how many, and the first row and text.</summary>
    private readonly SortedDictionary<(string Field, string Type), (long Count, long FirstRow, string FirstText)> _failures =
        new(Comparer<(string Field, string Type)>.Create((a, b) =>
        {
            var byField = string.CompareOrdinal(a.Field, b.Field);
            return byField != 0 ? byField : string.CompareOrdinal(a.Type, b.Type);
        }));

    /// <summary>Gets a value indicating whether no failure was recorded.</summary>
    public bool IsEmpty => _failures.Count == 0;

    /// <summary>
    ///     Records one value that could not be converted.
    /// </summary>
    /// <param name="field">The canonical field name.</param>
    /// <param name="spec">The conversion the value was converted with.</param>
    /// <param name="row">The zero-based row of the value.</param>
    /// <param name="text">The text of the value.</param>
    public void Add(string field, ParseSpec spec, long row, string text)
    {
        var key = (field, ParseDirectives.NameOf(spec));
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
        foreach (var ((field, typeName), (count, firstRow, firstText)) in _failures)
        {
            var quoted = new string(firstText.Take(MaxQuoted).Select(c => c < ' ' || c == '\u007F' ? '?' : c).ToArray());
            var more = firstText.Length > MaxQuoted ? "..." : string.Empty;
            yield return new FeatureReport(
                "vl-data-parse-failed",
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{count} value(s) of the field '{field}' could not be read as {typeName} and are null (the first is in row {firstRow + 1}: '{quoted}{more}')."),
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
/// <param name="Calendar">The calendar of the date conversions; UTC when null.</param>
internal sealed record DelimitedOptions(
    char Delimiter,
    ParseDirectives Directives,
    VegaLiteLimits Limits,
    long RowBudget,
    string Display,
    VegaLiteLocation Location,
    CancellationToken Cancellation,
    TimeCalendar? Calendar = null);

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
///         the header are ignored (they are still read and validated, but never stored). A repeated header name or more
///         header names than <see cref="VegaLiteLimits.MaxFieldCount"/> (refused as soon as the header exceeds it) is an
///         error, as is a field text longer than
///         <see cref="VegaLiteLimits.MaxStringLength"/> and more rows than the budget allows. Every error is a
///         <see cref="VegaLiteRenderException"/> with the location of the data and, where it helps, the line of the text.
///     </para>
///     <para>
///         <b>Types.</b> The cells are read as text and then converted by the <see cref="ParseDirectives"/>; the default
///         is no conversion, so cells stay text unless <c>parse</c> asks for inference or names fields.
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
        var header = reader.ReadRecord(options.Limits.MaxFieldCount, rejectOverflow: true);
        var records = new List<Dictionary<string, DataValue>>();
        var failures = new ParseFailures();
        if (header is null)
        {
            return new DelimitedResult(records, failures);
        }

        var names = ReadHeader(header, options);
        var count = 0;

        // The header width is known now: a data row keeps at most that many entries and only validates the rest
        while (reader.ReadRecord(names.Length, rejectOverflow: false) is { } cells)
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

        options.Directives.Apply(records, failures, options.Cancellation, options.Calendar);
        return new DelimitedResult(records, failures);
    }

    /// <summary>
    ///     Turns the header record into canonical field names.
    /// </summary>
    /// <param name="header">The entries of the header record.</param>
    /// <param name="options">The options.</param>
    /// <returns>The canonical names, in column order.</returns>
    /// <exception cref="VegaLiteRenderException">Thrown when a name is repeated.</exception>
    private static string[] ReadHeader(List<string> header, DelimitedOptions options)
    {
        // The reader has already refused a header with more than MaxFieldCount entries, before storing them
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
        /// <remarks>
        ///     Storage is bounded by <paramref name="storeLimit"/>: entries beyond it are still consumed and fully
        ///     validated (quote closure, text after a closing quote, field length) but are not kept, so a record with an
        ///     enormous number of entries never materializes them. When <paramref name="rejectOverflow"/> is set (the
        ///     header), the first entry beyond the limit is an error instead, before that entry is read.
        /// </remarks>
        /// <param name="storeLimit">The most entries that are kept.</param>
        /// <param name="rejectOverflow">Whether an entry beyond <paramref name="storeLimit"/> is an error (the header) or ignored (a data row).</param>
        /// <returns>The kept entries of the record, or <see langword="null"/> at the end of the text.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown for a malformed record, or a header with too many entries.</exception>
        public List<string>? ReadRecord(int storeLimit, bool rejectOverflow)
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
                if (cells.Count < storeLimit)
                {
                    cells.Add(ReadField(store: true)!);
                }
                else if (rejectOverflow)
                {
                    throw new VegaLiteRenderException(
                        $"The data '{_options.Display}' has more columns than the limit of {storeLimit} (MaxFieldCount).", _options.Location);
                }
                else
                {
                    // An ignored entry is read for its validation only: nothing is stored for it
                    ReadField(store: false);
                }

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
        /// <param name="store">Whether the text is kept; when not, the field is only validated and consumed.</param>
        /// <returns>The text of the field, or <see langword="null"/> when it is not kept.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown for an unterminated quote, text after a closing quote, or a field that is too long.</exception>
        private string? ReadField(bool store) =>
            _position < _text.Length && _text[_position] == '"' ? ReadQuoted(store) : ReadUnquoted(store);

        /// <summary>
        ///     Reads an unquoted field: everything up to the next delimiter or line break.
        /// </summary>
        /// <param name="store">Whether the text is kept.</param>
        /// <returns>The text, or <see langword="null"/> when it is not kept.</returns>
        private string? ReadUnquoted(bool store)
        {
            var start = _position;
            while (_position < _text.Length && _text[_position] != _options.Delimiter && !IsLineBreak(_text[_position]))
            {
                _position++;
            }

            CheckLength(_position - start);
            return store ? _text.Substring(start, _position - start) : null;
        }

        /// <summary>
        ///     Reads a quoted field: a doubled quote is one quote, and delimiters and line breaks are text.
        /// </summary>
        /// <param name="store">Whether the text is kept.</param>
        /// <returns>The text between the quotes, or <see langword="null"/> when it is not kept.</returns>
        private string? ReadQuoted(bool store)
        {
            var startLine = _line;
            var builder = store ? new StringBuilder() : null;
            var length = 0;
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
                        builder?.Append('"');
                        _position++;
                        CheckLength(++length);
                        continue;
                    }

                    break;
                }

                if (c == '\n')
                {
                    _line++;
                }

                builder?.Append(c);
                CheckLength(++length);
            }

            // Nothing may stand between the closing quote and the next delimiter or line break
            if (_position < _text.Length && _text[_position] != _options.Delimiter && !IsLineBreak(_text[_position]))
            {
                throw Error("unexpected text after the closing quote of a field");
            }

            return builder?.ToString();
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
