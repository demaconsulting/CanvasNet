using System.Text;
using System.Text.RegularExpressions;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Hardening;

/// <summary>
///     Source-text guards of the Vega-Lite package: a tripwire for the two classes of defect that make a result depend on the runtime, the operating system or the machine.
///     (1) A narrowing cast of a <see cref="double"/> (<c>(int)x</c>, <c>(long)Math.Floor(x)</c>) is undefined in C# when the number is NaN or out of range: .NET 8 on x64 wraps to the minimum value, .NET 9 and
///     later saturate, and a checked context throws, so a specification that steers a number out of range must go through <c>NumericConversions</c>; every other cast of a double is audited here with the reason
///     that the value is in range. (2) Reads of the clock, the local time zone, the culture, the environment, random numbers or the order of a hash are the inputs that make a rendering differ between two machines.
/// </summary>
/// <remarks>
///     The scan is a heuristic over the text of <c>src/DemaConsulting.CanvasNet.VegaLite/**/*.cs</c> (comments and string literals are masked): a cast is flagged when its operand calls <c>Math</c>, names
///     <c>double</c>, or uses an identifier that the file declares as a <see cref="double"/> (or a <c>var</c> that is initialized from one). It cannot see every double; the exhaustive check is the structured fuzz suite
///     run with the project compiled with <c>CheckForOverflowUnderflow</c>, which turns every out-of-range conversion into an exception on every runtime (see <c>VegaLiteFuzzTests</c>).
///     To add a cast that the scan flags, route it through <c>NumericConversions</c> or add it to <see cref="AuditedCasts"/> with the reason that the value is always in range. An entry that no longer matches a cast fails
///     the test, so the list cannot go stale.
/// </remarks>
public partial class VegaLiteSourceGuardTests
{
    /// <summary>The casts that the scan flags and that are audited: <c>file|type|operand</c> to the reason that the value cannot be NaN or out of range.</summary>
    private static readonly Dictionary<string, string> AuditedCasts = new(StringComparer.Ordinal)
    {
        // The conversion helpers themselves: the range is tested on the lines above the cast
        ["Data/NumericConversions.cs|int|value"] = "The helper itself: NaN and the range are handled on the lines before the cast.",
        ["Data/NumericConversions.cs|long|value"] = "The helper itself: NaN and the range are handled on the lines before the cast.",

        // Calendar arithmetic on whole numbers (the wall time is saturated to a long and lies within the margin of the valid date range)
        ["Data/TimeCalendar.cs|int|(dayOfYear - (((153 * shiftedMonth) + 2) / 5) + 1)"] = "Integer civil-from-days arithmetic; the result is the day of the month, 1 to 31.",
        ["Data/TimeCalendar.cs|int|(days - DaysFromCivil(year, 1, 1))"] = "A difference of two day numbers of one year: 0 to 365.",
        ["Data/TimeCalendar.cs|int|(milliseconds - (days * 86_400_000L))"] = "The milliseconds of one day: 0 to 86,399,999.",
        ["Data/TimeCalendar.cs|int|(shiftedMonth < 10 ? shiftedMonth + 3 : shiftedMonth - 9)"] = "The calendar month: 1 to 12.",
        ["Data/TimeCalendar.cs|int|FloorMod(DaysFromCivil(NumericConversions.ToInt64Saturating(year), 1, 1) + 4, 7)"] = "A floor modulus by seven: 0 to 6.",
        ["Data/TimeCalendar.cs|int|FloorMod(days + 4, 7)"] = "A floor modulus by seven: 0 to 6.",
        ["Data/TimeCalendar.cs|int|year"] = "The year of a wall time within the margin around the valid date range (about 275,000 either side of 1970).",
        ["Data/TimeFormat.Parse.cs|int|Math.Min((long)text.Length, (long)i + 6)"] = "At most text.Length, which is an int.",
        ["Data/TimeFormat.Parse.cs|int|Math.Min((long)text.Length, (long)start + window)"] = "At most text.Length, which is an int.",
        ["Data/TimeFormat.Parse.cs|int|TimeCalendar.FloorMod(monthNumber, 12)"] = "A floor modulus by twelve: 0 to 11.",

        // JavaScript integer conversions
        ["Expressions/ExpressionEvaluator.Coercion.cs|int|(uint)wrapped"] = "The value is finite and was reduced modulo 2^32 into 0 to 2^32 - 1 just before.",
        ["Expressions/ExpressionEvaluator.Coercion.cs|uint|wrapped"] = "The value is finite and was reduced modulo 2^32 into 0 to 2^32 - 1 just before.",
        ["Expressions/ExpressionFunctions.Text.cs|int|Math.Max(length + integer, 0)"] = "ToInteger maps NaN to zero; the sum is clamped to 0 and below by the length (an int).",
        ["Expressions/ExpressionFunctions.Text.cs|int|Math.Min(Math.Max(ToInteger(value), 0), length)"] = "ToInteger maps NaN to zero; the result is clamped to 0 to length (an int).",
        ["Expressions/ExpressionFunctions.Text.cs|int|Math.Min(integer, length)"] = "ToInteger maps NaN to zero; the result is at most the length (an int) and not negative here.",
        ["Expressions/ExpressionFunctions.Time.cs|int|Math.Min(context.MaxArrayLength, affordable)"] = "At most MaxArrayLength, which is an int; the other operand is a non-negative budget.",

        // Counts of rows and columns computed from ints
        ["Guides/LegendBuilder.Grid.cs|int|Math.Ceiling(n / (double)columns)"] = "n is an int and columns at least one: the quotient is at most n.",
        ["Guides/LegendBuilder.Symbols.cs|int|Math.Ceiling(count / (double)columns)"] = "count is an int and columns at least one: the quotient is at most count.",
        ["Layout/CompositionLayout.Headers.cs|int|Math.Ceiling(n / (double)columnCount)"] = "n is an int and columnCount at least one: the quotient is at most n.",
        ["VegaLiteDocument.Composition.cs|int|Math.Ceiling(charts.Count / (double)columns)"] = "charts.Count is an int and columns at least one: the quotient is at most the count.",
        ["Scales/ScaleResolver.Color.cs|int|Math.Ceiling(Math.Log2(count))"] = "count is an int of at least two: the logarithm is at most 31.",
        ["Guides/TimeLabels.cs|int|Math.Min(options.MaxLength, remaining)"] = "At most options.MaxLength, an int.",
        ["Marks/MarkBuilder.Paths.cs|int|Math.Min(bound, 4096)"] = "At most 4,096.",
        ["Marks/MarkBuilder.Text.cs|int|Math.Max(0, Math.Min(_owner._maxStringLength, left))"] = "Clamped to 0 up to the string length limit (an int).",
        ["Marks/MarkBuilder.Text.cs|int|Math.Max(0, left)"] = "left is a long that starts at an int allowance and only decreases: 0 up to the allowance.",

        // Colors and pixels
        ["Scales/ColorInterpolator.cs|byte|Math.Clamp(Math.Round(value, MidpointRounding.AwayFromZero), 0, 255)"] = "NaN is handled by the conditional before the cast and the value is clamped to 0 to 255.",
        ["Scenegraph/CssPaint.cs|byte|alpha"] = "The factor is clamped to 0 to 1 (NaN counts as 1) and the alpha is clamped to 0 to 255.",
        ["Scenegraph/SceneImageSource.cs|byte|Math.Round(row[x].A * factor, MidpointRounding.AwayFromZero)"] = "The factor is an opacity from 0 to 1 (the painter clamps it) times a byte.",
        ["Scenegraph/ScenePainter.Paths.cs|long|boxWidth"] = "boxWidth is an int (a clipped pixel size); the cast only widens it before the product.",
        ["Scenegraph/ScenePainter.cs|long|boxWidth"] = "boxWidth is an int (a clipped pixel size); the cast only widens it before the product.",

        // Number formats and ticks
        ["Scales/NumberFormat.cs|int|((bits >> 52) & 0x7FF)"] = "The eleven bit exponent field of the bits of a double: 0 to 2047.",
        ["Scales/NumberFormat.cs|int|(Math.Max(-8, Math.Min(8, Math.Floor(exponent / 3.0))) * 3)"] = "exponent is an int (a decimal exponent); the prefix power is clamped to -24 to 24.",
        ["Scales/NumberFormat.cs|int|(e / 3)"] = "NaN is handled by the conditional before the cast; e is clamped to -24 to 24, so the quotient is -8 to 8.",
        ["Scales/Ticks.cs|int|n"] = "n is a count of ticks that is not NaN (it returns above) and at most maxTicks here.",
        ["Scales/Ticks.cs|long|x"] = "x is an integer value below 400 in magnitude (tested on the line before).",

        // Positions in sorted data and clamped integers
        ["SpecModel/SpecMetadata.cs|long|length"] = "A sequence length: not negative (zero is returned above) and below 9e18, and NaN fails the test and gives null.",
        ["SpecModel/SpecNormalizer.Composition.cs|int|Math.Clamp(Math.Truncate(number), 0, int.MaxValue)"] = "A number of a JSON specification is finite; the value is clamped to 0 up to int.MaxValue.",
        ["SpecModel/SpecParser.cs|int|Math.Clamp(offset, 0, _bytes.Length)"] = "A long offset clamped to the length of the byte buffer (an int).",
        ["Transforms/AggregateMeasures.cs|int|Math.Floor(position)"] = "A quantile position (n - 1) * p with p strictly between 0 and 1 (the other cases return earlier): 0 to n - 2.",
        ["Transforms/AggregateTransforms.Impute.cs|int|count"] = "count is not NaN and at most the row limit, which is capped at int.MaxValue above.",
        ["Transforms/AggregateTransforms.Window.cs|int|(i0 + (param - 1))"] = "Integer arithmetic on frame positions, tested against i1 (an int) in the same condition.",
        ["Transforms/AggregateTransforms.Window.cs|int|Math.Clamp((long)p + e + 1, 0, _n)"] = "A long sum clamped to 0 up to the partition size (an int).",
        ["Transforms/AggregateTransforms.Window.cs|int|Math.Clamp((long)p + s, 0, _n)"] = "A long sum clamped to 0 up to the partition size (an int).",
        ["Transforms/BinAlgorithm.cs|int|(-logStep / logBase)"] = "The step is finite and positive (tested above), so its logarithm is finite and at most 745 in magnitude.",
        ["Transforms/LoessFit.cs|long|n"] = "n is an int; the cast widens it before the product.",
        ["Transforms/StatTransforms.cs|int|Math.Min(Math.Max(needed, _cells.Length * 2L), TableLayout.MaxCells)"] = "A long clamped to TableLayout.MaxCells, which is an int.",
        ["Transforms/StatTransforms.cs|long|rows"] = "rows is an int; the cast widens it before the product.",
        ["VegaLiteDocument.Render.cs|int|height"] = "The size is rounded from a finite scale (validated) and tested against 1 and MaxDimension just before.",
        ["VegaLiteDocument.Render.cs|int|width"] = "The size is rounded from a finite scale (validated) and tested against 1 and MaxDimension just before.",
    };

    /// <summary>The patterns of the inputs that make a result depend on the machine, with the files that may use them and why.</summary>
    private static readonly (string Pattern, string What, Dictionary<string, string> Allowed)[] MachineInputs =
    [
        (@"\bDateTime(Offset)?\.(Now|UtcNow|Today)\b", "the wall clock", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\bTimeZoneInfo\.(Local|ClearCachedData)\b", "the local time zone", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\.ToLocalTime\s*\(", "the local time zone", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\b(CurrentCulture|CurrentUICulture|DefaultThreadCurrentCulture|InstalledUICulture)\b", "the culture of the thread", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\bCultureInfo\.CurrentCulture\b", "the culture of the thread", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\.(ToLower|ToUpper)\s*\(\s*\)", "a culture-dependent case change", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\bstring\.Compare\s*\(|\.CompareTo\s*\(\s*\""", "a culture-dependent string comparison", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\bEnvironment\.", "the environment", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\b(new\s+Random\s*\(|Random\.Shared|RandomNumberGenerator|Guid\.NewGuid)", "a random source", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\bStopwatch\b|\bEnvironment\.TickCount|\bTimestamp\.GetTimestamp", "a clock", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\b(Parallel\.|AsParallel\s*\(|Task\.Run\s*\(|ThreadPool\.|ConcurrentDictionary|ConcurrentBag)", "a source of nondeterministic order", new Dictionary<string, string>(StringComparer.Ordinal)),
        (@"\bGetHashCode\s*\(|\bHashCode\.Combine\b|RuntimeHelpers\.GetHashCode", "a hash code (randomized per process for strings)", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Data/DataTable.cs"] = "The hash of a value that is the key of a dictionary (equality decides, and the dictionaries are enumerated in insertion order, never in bucket order).",
            ["Marks/MarkBuilder.Paths.cs"] = "The precomputed hash of a row key struct of a dictionary (insertion order is the enumeration order).",
            ["Transforms/AggregateTransforms.cs"] = "The precomputed hash of a group key struct of a dictionary (the groups are listed in first-seen order).",
            ["Transforms/RowTransforms.Shape.cs"] = "The precomputed hash of a group key struct of a dictionary (the groups are listed in first-seen order).",
        }),
        (@"\bnew\s+(Dictionary|HashSet|SortedDictionary|SortedSet)<string\b[^>]*>\s*\(\s*\)", "a string-keyed collection without an explicit comparer", new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Data/DelimitedTextParser.cs"] = "Empty placeholder dictionaries of the parse directives; they are never filled (the filled ones pass StringComparer.Ordinal).",
            ["SpecModel/ExpressionReferenceSites.cs"] = "Membership sets of property names that are never enumerated; the default string equality is ordinal.",
        }),
    ];

    /// <summary>
    ///     Validates that every narrowing cast of a double in the package is either routed through <c>NumericConversions</c> or audited with the reason that its value is in range.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_SourceGuard_EveryNarrowingCastOfADoubleIsAudited()
    {
        // Arrange
        var found = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (relative, text) in SourceFiles())
        {
            foreach (var cast in FlaggedCasts(text))
            {
                found.Add($"{relative}|{cast.Type}|{cast.Operand}");
            }
        }

        // Act
        var unaudited = found.Where(c => !AuditedCasts.ContainsKey(c)).ToList();
        var stale = AuditedCasts.Keys.Where(k => !found.Contains(k)).ToList();

        // Assert
        Assert.True(unaudited.Count == 0, "Narrowing casts of a double that are not audited (use NumericConversions, or add the cast to AuditedCasts with the reason that its value is in range):\n" + string.Join("\n", unaudited.Select(c => "    [\"" + c.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"] = \"...\",")));
        Assert.True(stale.Count == 0, "Audited casts that no longer exist in the source (remove them):\n" + string.Join("\n", stale));
    }

    /// <summary>
    ///     Validates that every audit has a reason.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_SourceGuard_EveryAuditedCastHasAReason()
    {
        // Arrange / Act
        var withoutReason = AuditedCasts.Where(a => a.Value.Length < 10).Select(a => a.Key).ToList();

        // Assert
        Assert.Empty(withoutReason);
    }

    /// <summary>
    ///     Validates that the package reads none of the inputs that make a rendering depend on the machine (the clock, the local time zone, the culture, the environment, random numbers, hash order), except
    ///     where the file is listed with the reason.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_SourceGuard_NoMachineInputsAreRead()
    {
        // Arrange
        var violations = new List<string>();

        // Act
        foreach (var (relative, text) in SourceFiles())
        {
            foreach (var (pattern, what, allowed) in MachineInputs)
            {
                if (Regex.IsMatch(text, pattern) && !allowed.ContainsKey(relative))
                {
                    violations.Add($"{relative}: {what} ({pattern})");
                }
            }
        }

        // Assert
        Assert.True(violations.Count == 0, "The package must not read inputs that differ between machines (add the file to the allowed list of the pattern, with the reason, when the use is harmless):\n" + string.Join("\n", violations));
    }

    /// <summary>
    ///     Validates the guard itself: the scan finds the casts of the cases that it is meant to find and skips the ones that it must not flag, so that an empty report means that the source is clean.
    /// </summary>
    [Fact]
    public void CanvasNetVegaLite_SystemIntegration_SourceGuard_ScanFlagsDoubleCastsOnly()
    {
        // Arrange
        const string source = """
            double value = 1;
            var n = Math.Floor(value);
            var count = 3;
            var a = (int)value;                 // flagged: a double identifier
            var b = (long)Math.Floor(value);    // flagged: Math
            var c = (int)n;                     // flagged: a var that is initialized from Math
            var d = (int)count;                 // not flagged: an int
            var e = (int)text.Length;           // not flagged
            var f = "(int)value";               // not flagged: in a string
            // var g = (int)value;              // not flagged: in a comment
            """;

        // Act
        var flagged = FlaggedCasts(source).Select(c => c.Type + " " + c.Operand).ToList();

        // Assert
        Assert.Equal(["int value", "long Math.Floor(value)", "int n"], flagged);
    }

    /// <summary>
    ///     Finds the narrowing casts in a source text that cast something that may be a double.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <returns>The type and the operand of each flagged cast.</returns>
    internal static IEnumerable<(string Type, string Operand)> FlaggedCasts(string source)
    {
        var text = Mask(source);
        var doubles = new HashSet<string>(Regex.Matches(text, @"\bdouble\??\s+(\w+)").Select(m => m.Groups[1].Value), StringComparer.Ordinal);
        for (var pass = 0; pass < 3; pass++)
        {
            foreach (Match declaration in Regex.Matches(text, @"\bvar\s+(\w+)\s*=\s*([^;]*);"))
            {
                var rhs = declaration.Groups[2].Value;
                if (rhs.Contains("Math.", StringComparison.Ordinal) || Regex.IsMatch(rhs, @"\bdouble\b|\d\.\d") || Identifiers(rhs).Any(doubles.Contains))
                {
                    doubles.Add(declaration.Groups[1].Value);
                }
            }
        }

        foreach (Match cast in Regex.Matches(text, @"(?<![\w>.])\((int|long|uint|ulong|short|ushort|byte|sbyte|char)\)(?=\s*[\w(\[\-!~])"))
        {
            var operand = Regex.Replace(Operand(text, cast.Index + cast.Length), @"\s+", " ");
            if (operand.Contains("Math.", StringComparison.Ordinal) || Regex.IsMatch(operand, @"\bdouble\b") || Identifiers(operand).Any(doubles.Contains))
            {
                yield return (cast.Groups[1].Value, operand);
            }
        }
    }

    /// <summary>
    ///     Lists the identifiers of an expression text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The identifiers.</returns>
    private static IEnumerable<string> Identifiers(string text) => Regex.Matches(text, @"(?<![\w.])[A-Za-z_]\w*").Select(m => m.Value);

    /// <summary>
    ///     Reads the operand of a cast: the primary expression that follows it, with its member accesses, calls and indexers.
    /// </summary>
    /// <param name="text">The masked source.</param>
    /// <param name="start">The index after the closing parenthesis of the cast.</param>
    /// <returns>The operand text.</returns>
    private static string Operand(string text, int start)
    {
        var i = start;
        while (i < text.Length && (text[i] is '-' or '!' or '~' || char.IsWhiteSpace(text[i])))
        {
            i++;
        }

        var begin = i;
        while (i < text.Length)
        {
            var c = text[i];
            if (char.IsLetterOrDigit(c) || c is '_' or '.')
            {
                i++;
            }
            else if (c is '(' or '[')
            {
                i = SkipBalanced(text, i);
            }
            else
            {
                break;
            }
        }

        return text[begin..i];
    }

    /// <summary>
    ///     Skips a balanced group of parentheses or brackets.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The index of the opening character.</param>
    /// <returns>The index after the closing character.</returns>
    private static int SkipBalanced(string text, int start)
    {
        var open = text[start];
        var close = open == '(' ? ')' : ']';
        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] == open)
            {
                depth++;
            }
            else if (text[i] == close && --depth == 0)
            {
                return i + 1;
            }
        }

        return text.Length;
    }

    /// <summary>
    ///     Blanks the comments and the contents of string and character literals, keeping the length and the line breaks.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>The masked source.</returns>
    private static string Mask(string source)
    {
        var result = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var end = i + 1;
            if (source.AsSpan(i).StartsWith("//", StringComparison.Ordinal))
            {
                end = source.IndexOf('\n', i);
                end = end < 0 ? source.Length : end;
                result.Append(' ', end - i);
            }
            else if (source.AsSpan(i).StartsWith("/*", StringComparison.Ordinal))
            {
                end = source.IndexOf("*/", i, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
                result.Append(Regex.Replace(source[i..end], @"[^\n]", " "));
            }
            else if (source[i] is '"' or '\'')
            {
                var quote = source[i];
                var j = i + 1;
                while (j < source.Length && source[j] != quote)
                {
                    j += source[j] == '\\' ? 2 : 1;
                }

                end = Math.Min(j + 1, source.Length);
                result.Append(quote).Append(Regex.Replace(source[(i + 1)..Math.Max(i + 1, end - 1)], @"[^\n]", " ")).Append(quote);
            }
            else
            {
                result.Append(source[i]);
            }

            i = end;
        }

        return result.ToString();
    }

    /// <summary>
    ///     Enumerates the source files of the package with their path relative to the package folder (with forward slashes).
    /// </summary>
    /// <returns>The files and their text.</returns>
    private static IEnumerable<(string Relative, string Text)> SourceFiles()
    {
        var root = FindPackageSourceRoot();
        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var relative = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (relative.StartsWith("obj/", StringComparison.Ordinal) || relative.StartsWith("bin/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return (relative, File.ReadAllText(file));
        }
    }

    /// <summary>
    ///     Finds the source folder of the package by walking up from the test assembly to the repository root.
    /// </summary>
    /// <returns>The folder.</returns>
    private static string FindPackageSourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "src", "DemaConsulting.CanvasNet.VegaLite");
            if (File.Exists(Path.Combine(directory.FullName, "CanvasNet.slnx")) && Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("The repository root (the folder with CanvasNet.slnx) was not found above " + AppContext.BaseDirectory + "; the source guards read the source files of the package.");
    }
}
