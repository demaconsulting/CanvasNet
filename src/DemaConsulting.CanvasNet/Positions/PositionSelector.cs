using System.Globalization;
using System.Text;
using System.Text.Json;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Positions;

/// <summary>
///     One parsed position selector, such as <c>all</c>, <c>node:Send</c>, <c>commit:*fix*</c>,
///     <c>node:"my*odd id"</c>, <c>mark:x=Nov+color=Americas</c> or <c>edge:@from=A+@to=B</c>, which tells whether an
///     <see cref="ImagePosition"/> is wanted.
/// </summary>
/// <remarks>
///     <para>
///         <b>Grammar.</b>
///     </para>
///     <code>
///     selector    := "all" | kind [ ":" target ]
///     kind        := ALPHA *( ALPHA / DIGIT / "-" )
///     target      := json-string        ; when the target starts with '"' (it must then be the whole target)
///                  | predicates         ; else, when it has '='
///                  | id-pattern         ; otherwise
///     id-pattern  := 1*( any char except '"' and '=' )  ; '*' is any run (also empty), '?' exactly one char
///     json-string := '"' JSON string body '"'           ; an exact id; '*' and '?' are literal
///     predicates  := predicate *( "+" predicate )       ; '+' separates only outside JSON strings
///     predicate   := name "=" value                     ; the first '=' separates; a name once per selector
///     name        := "@" meta | word
///     meta        := "id" / "view" / "from" / "to" / "index"
///     word        := ALPHA *( ALPHA / DIGIT / "_" )   ; a channel or a data field
///     value       := json-string | bare
///     bare        := 1*( any char except '+' and '"' )
///     </code>
///     <para>
///         <b>Case.</b> The keywords are case-insensitive: <c>all</c>, the kind and the <c>@</c> names. Ids, channel
///         and field names and text values are case-sensitive.
///     </para>
///     <para>
///         <b>Values.</b> A bare channel or field value is read like a <c>--param</c> value
///         (<see cref="ImageParameterValues.TryParseParameter(string, out object?, out string?)"/>): <c>true</c>,
///         <c>false</c>, <c>null</c>, a finite invariant number (<c>3</c>, <c>-1.5</c>, <c>1e3</c>) is a number, and
///         anything else is text with one pair of surrounding single quotes removed; a JSON string is always text, so
///         <c>x=3</c> matches the number 3 (and 3.0) and <c>x="3"</c> matches the text <c>3</c>. A value that starts with
///         <c>[</c> or <c>{</c> is refused. A bare value of <c>@id</c>, <c>@view</c>, <c>@from</c> and <c>@to</c> is the
///         text as written (they hold ids); <c>@index</c> takes a whole number that is not negative.
///     </para>
///     <para>
///         <b>Matching.</b> <c>all</c> matches every position and a kind alone matches every position of that kind. An
///         id pattern globs against <see cref="ImagePosition.Id"/>; the matcher walks the pattern and the id once with a
///         single backtrack point, so it takes at most (pattern length) × (id length) steps. Predicates are ANDed. A
///         word name is a channel when it is in <see cref="ImagePosition.ChannelNames"/> or is a name
///         <see cref="ImagePosition.Values"/> holds, and then compares with
///         <see cref="ImagePosition.TryGetValue(string, out PositionValue)"/>; any other word is a data field and
///         compares with <see cref="ImagePosition.TryGetData(string, out PositionValue)"/> (so a data field whose name
///         is a channel is matched through that channel). Both use typed equality (a missing name never matches;
///         <c>x=null</c> matches a name that is present and holds null). <c>@id</c> compares exactly (no globs), and
///         <c>@view</c>, <c>@from</c>, <c>@to</c> and <c>@index</c> never match a position that does not have them.
///     </para>
///     <para>
///         <b>Limits.</b> A selector is at most <see cref="MaxSelectorLength"/> characters with at most
///         <see cref="MaxPredicates"/> predicates. The class is immutable and thread-safe.
///     </para>
/// </remarks>
public sealed class PositionSelector
{
    /// <summary>
    ///     The longest selector text: 256 UTF-16 characters.
    /// </summary>
    public const int MaxSelectorLength = 256;

    /// <summary>
    ///     The most predicates one selector may have: 16.
    /// </summary>
    public const int MaxPredicates = 16;

    /// <summary>
    ///     The keyword that selects every position.
    /// </summary>
    private const string AllKeyword = "all";

    /// <summary>
    ///     The id glob pattern, or <see langword="null"/> when the selector has none.
    /// </summary>
    private readonly string? _idPattern;

    /// <summary>
    ///     The exact id, or <see langword="null"/> when the selector has none.
    /// </summary>
    private readonly string? _exactId;

    /// <summary>
    ///     The predicates, ANDed.
    /// </summary>
    private readonly Predicate[] _predicates;

    /// <summary>
    ///     Initializes a parsed selector.
    /// </summary>
    /// <param name="text">The selector text.</param>
    /// <param name="kind">The lowercase kind, or <see langword="null"/> for <c>all</c>.</param>
    /// <param name="idPattern">The id glob pattern.</param>
    /// <param name="exactId">The exact id.</param>
    /// <param name="predicates">The predicates.</param>
    private PositionSelector(string text, string? kind, string? idPattern, string? exactId, Predicate[] predicates)
    {
        Text = text;
        Kind = kind;
        _idPattern = idPattern;
        _exactId = exactId;
        _predicates = predicates;
    }

    /// <summary>
    ///     Gets the selector text as given.
    /// </summary>
    public string Text { get; }

    /// <summary>
    ///     Gets the lowercase kind the selector is limited to, or <see langword="null"/> for <c>all</c>.
    /// </summary>
    public string? Kind { get; }

    /// <summary>
    ///     Gets a value indicating whether the selector is <c>all</c>.
    /// </summary>
    public bool IsAll => Kind is null;

    /// <summary>
    ///     Parses a selector.
    /// </summary>
    /// <param name="text">The selector text.</param>
    /// <returns>The parsed selector.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    /// <exception cref="FormatException">
    ///     Thrown when the text does not follow the grammar or exceeds a limit; the message names the selector and the
    ///     problem.
    /// </exception>
    public static PositionSelector Parse(string text)
    {
        return TryParse(text, out var selector, out var problem)
            ? selector
            : throw new FormatException($"The position selector '{text}' is not valid: {problem}.");
    }

    /// <summary>
    ///     Parses a selector without throwing for a bad text.
    /// </summary>
    /// <param name="text">The selector text.</param>
    /// <param name="selector">The parsed selector when parsing succeeds.</param>
    /// <param name="problem">Why the text is not a selector when parsing fails; <see langword="null"/> otherwise.</param>
    /// <returns><see langword="true"/> when the text is a valid selector.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
    public static bool TryParse(string text, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out PositionSelector? selector, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(text);
        selector = null;

        // Bound the work before reading anything
        if (text.Length == 0)
        {
            problem = "a selector must not be empty (use 'all' to select everything)";
            return false;
        }

        if (text.Length > MaxSelectorLength)
        {
            problem = $"it is longer than {MaxSelectorLength.ToString(CultureInfo.InvariantCulture)} characters";
            return false;
        }

        // Split the kind from the target at the first ':'
        var colon = text.IndexOf(':');
        var kindText = colon < 0 ? text : text[..colon];
        if (!IsKindWord(kindText))
        {
            problem = $"'{kindText}' is not a kind (a kind is a word of letters, digits and '-' that starts with a letter)";
            return false;
        }

        var kind = kindText.ToLowerInvariant();
        if (kind == AllKeyword)
        {
            if (colon >= 0)
            {
                problem = "'all' takes no target";
                return false;
            }

            selector = new PositionSelector(text, null, null, null, []);
            problem = null;
            return true;
        }

        if (colon < 0)
        {
            selector = new PositionSelector(text, kind, null, null, []);
            problem = null;
            return true;
        }

        // Read the target as an exact id, predicates or an id pattern
        var target = text[(colon + 1)..];
        if (target.Length == 0)
        {
            problem = "the target after ':' is empty";
            return false;
        }

        if (target[0] == '"')
        {
            var end = FindStringEnd(target, 0);
            if (end < 0 || !TryReadJsonString(target[..(end + 1)], out var exact))
            {
                problem = "the quoted id is not a valid JSON string";
                return false;
            }

            if (end != target.Length - 1)
            {
                problem = "there is text after the closing quote of the id";
                return false;
            }

            selector = new PositionSelector(text, kind, null, exact, []);
            problem = null;
            return true;
        }

        if (target.Contains('='))
        {
            if (!TryParsePredicates(target, out var predicates, out problem))
            {
                return false;
            }

            selector = new PositionSelector(text, kind, null, null, predicates);
            return true;
        }

        if (target.Contains('"'))
        {
            problem = "a '\"' in an id pattern must start a quoted id (\"...\") that is the whole target";
            return false;
        }

        selector = new PositionSelector(text, kind, target, null, []);
        problem = null;
        return true;
    }

    /// <summary>
    ///     Tells whether the selector wants a position.
    /// </summary>
    /// <param name="position">The position.</param>
    /// <returns><see langword="true"/> when the position matches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="position"/> is null.</exception>
    public bool Matches(ImagePosition position)
    {
        ArgumentNullException.ThrowIfNull(position);

        // The kind gate first: it is the cheapest test
        if (Kind is not null && !string.Equals(Kind, position.Kind, StringComparison.Ordinal))
        {
            return false;
        }

        // Then the id, by exact text or by glob
        if (_exactId is not null && !string.Equals(_exactId, position.Id, StringComparison.Ordinal))
        {
            return false;
        }

        if (_idPattern is not null && !Glob(_idPattern, position.Id))
        {
            return false;
        }

        // Then every predicate
        return _predicates.All(predicate => predicate.Matches(position));
    }

    /// <summary>
    ///     Gets the selector text.
    /// </summary>
    /// <returns>The text as given.</returns>
    public override string ToString()
    {
        return Text;
    }

    /// <summary>
    ///     Tells whether a text is a kind word: an ASCII letter, then ASCII letters, digits and <c>-</c>.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for a kind word (in any case).</returns>
    internal static bool IsKindWord(string text)
    {
        return text.Length > 0 && char.IsAsciiLetter(text[0]) && text.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    }

    /// <summary>
    ///     Matches an id against a glob pattern: <c>*</c> is any run of characters (also empty) and <c>?</c> exactly one.
    /// </summary>
    /// <remarks>
    ///     The iterative two-pointer walk keeps only the last <c>*</c> as a backtrack point, which is enough for
    ///     <c>*</c>/<c>?</c> globs and bounds the work by (pattern length) × (id length).
    /// </remarks>
    /// <param name="pattern">The pattern.</param>
    /// <param name="id">The id.</param>
    /// <returns><see langword="true"/> when the whole id matches the whole pattern.</returns>
    internal static bool Glob(string pattern, string id)
    {
        var p = 0;
        var i = 0;
        var star = -1;
        var resume = 0;
        while (i < id.Length)
        {
            if (p < pattern.Length && (pattern[p] == '?' || (pattern[p] != '*' && pattern[p] == id[i])))
            {
                // One character matches: advance both
                p++;
                i++;
            }
            else if (p < pattern.Length && pattern[p] == '*')
            {
                // Remember the star and first try to match it with nothing
                star = p++;
                resume = i;
            }
            else if (star >= 0)
            {
                // Let the last star absorb one more character and retry after it
                p = star + 1;
                i = ++resume;
            }
            else
            {
                return false;
            }
        }

        // Trailing stars match the empty rest
        while (p < pattern.Length && pattern[p] == '*')
        {
            p++;
        }

        return p == pattern.Length;
    }

    /// <summary>
    ///     Parses the predicates of a target.
    /// </summary>
    /// <param name="target">The target text.</param>
    /// <param name="predicates">The predicates when parsing succeeds.</param>
    /// <param name="problem">Why the target is not valid when parsing fails.</param>
    /// <returns><see langword="true"/> when every predicate is valid.</returns>
    private static bool TryParsePredicates(string target, out Predicate[] predicates, out string? problem)
    {
        predicates = [];
        var list = new List<Predicate>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var position = 0;
        while (true)
        {
            // Read the name up to the first '='
            var equals = target.IndexOf('=', position);
            var plus = target.IndexOf('+', position);
            if (equals < 0 || (plus >= 0 && plus < equals))
            {
                var end = plus < 0 ? target.Length : plus;
                problem = end == position
                    ? "a predicate is empty (check for a doubled or trailing '+')"
                    : $"the predicate '{target[position..end]}' has no '='";
                return false;
            }

            var name = target[position..equals];
            if (!TryReadName(name, out var normalized, out problem))
            {
                return false;
            }

            if (!names.Add(normalized))
            {
                problem = $"the name '{name}' occurs twice";
                return false;
            }

            // Read the value: a JSON string up to its closing quote, or bare text up to the next '+'
            var start = equals + 1;
            int next;
            PositionValue value;
            if (start < target.Length && target[start] == '"')
            {
                var close = FindStringEnd(target, start);
                if (close < 0 || !TryReadJsonString(target[start..(close + 1)], out var text))
                {
                    problem = $"the quoted value of '{name}' is not a valid JSON string";
                    return false;
                }

                next = close + 1;
                if (next < target.Length && target[next] != '+')
                {
                    problem = $"there is text after the closing quote of the value of '{name}'";
                    return false;
                }

                value = PositionValue.FromString(text);
                if (normalized == "@index")
                {
                    problem = "@index takes a whole number, not text";
                    return false;
                }
            }
            else
            {
                next = target.IndexOf('+', start);
                if (next < 0)
                {
                    next = target.Length;
                }

                var bare = target[start..next];
                if (!TryReadBareValue(normalized, bare, out value, out problem))
                {
                    return false;
                }
            }

            list.Add(new Predicate(normalized, value));
            if (list.Count > MaxPredicates)
            {
                problem = $"it has more than {MaxPredicates.ToString(CultureInfo.InvariantCulture)} predicates";
                return false;
            }

            // Continue after the '+', which must be followed by another predicate
            if (next >= target.Length)
            {
                break;
            }

            position = next + 1;
        }

        predicates = [.. list];
        problem = null;
        return true;
    }

    /// <summary>
    ///     Checks and normalizes a predicate name.
    /// </summary>
    /// <param name="name">The name as written.</param>
    /// <param name="normalized">The name with an <c>@</c> name lowercased.</param>
    /// <param name="problem">Why the name is not valid.</param>
    /// <returns><see langword="true"/> when the name is valid.</returns>
    private static bool TryReadName(string name, out string normalized, out string? problem)
    {
        normalized = name;
        problem = null;
        if (name.Length == 0)
        {
            problem = "a predicate has no name before '='";
            return false;
        }

        if (name[0] == '@')
        {
            normalized = name.ToLowerInvariant();
            if (normalized is "@id" or "@view" or "@from" or "@to" or "@index")
            {
                return true;
            }

            problem = $"'{name}' is not a metadata name (use @id, @view, @from, @to or @index)";
            return false;
        }

        // A channel or data field name: a letter, then letters, digits and '_'
        if (!char.IsAsciiLetter(name[0]) || !name.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            problem = $"'{name}' is not a channel or field name (a word of letters, digits and '_' that starts with a letter) or an @ name";
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Reads a bare predicate value.
    /// </summary>
    /// <param name="name">The normalized name.</param>
    /// <param name="bare">The bare text.</param>
    /// <param name="value">The value when reading succeeds.</param>
    /// <param name="problem">Why the value is not valid.</param>
    /// <returns><see langword="true"/> when the value is valid.</returns>
    private static bool TryReadBareValue(string name, string bare, out PositionValue value, out string? problem)
    {
        value = PositionValue.Null;
        problem = null;
        if (bare.Length == 0)
        {
            problem = $"the value of '{name}' is empty (write \"\" for empty text)";
            return false;
        }

        if (bare.Contains('"'))
        {
            problem = $"a '\"' in the value of '{name}' must start a quoted value that ends before the next '+'";
            return false;
        }

        // A metadata name holds an id or a path: take the text as written, except @index, which is a number
        switch (name)
        {
            case "@index":
                if (!int.TryParse(bare, NumberStyles.None, CultureInfo.InvariantCulture, out var index))
                {
                    problem = $"@index takes a whole number that is not negative, not '{bare}'";
                    return false;
                }

                value = PositionValue.FromNumber(index);
                return true;
            case "@id" or "@view" or "@from" or "@to":
                value = PositionValue.FromString(bare);
                return true;
        }

        // A channel value: the --param grammar, scalars only
        if (bare[0] is '[' or '{')
        {
            problem = $"the value of '{name}' is an array or object; only scalars can be compared";
            return false;
        }

        if (!ImageParameterValues.TryParseParameter(bare, out var parsed, out var parseProblem) ||
            !PositionValue.TryFromObject(parsed, out value))
        {
            problem = $"the value of '{name}' is not valid ({parseProblem ?? "not a scalar"})";
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Finds the closing quote of a JSON string.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="start">The index of the opening quote.</param>
    /// <returns>The index of the closing quote, or -1 when the string is not terminated.</returns>
    private static int FindStringEnd(string text, int start)
    {
        var index = start + 1;
        while (index < text.Length)
        {
            if (text[index] == '"')
            {
                return index;
            }

            // Skip an escaped character, so that \" does not close the string
            index += text[index] == '\\' ? 2 : 1;
        }

        return -1;
    }

    /// <summary>
    ///     Reads a JSON string literal.
    /// </summary>
    /// <param name="literal">The literal, quotes included.</param>
    /// <param name="value">The decoded text.</param>
    /// <returns><see langword="true"/> when the literal is a valid JSON string.</returns>
    private static bool TryReadJsonString(string literal, out string value)
    {
        value = string.Empty;
        try
        {
            var reader = new Utf8JsonReader(Encoding.UTF8.GetBytes(literal));
            if (!reader.Read() || reader.TokenType != JsonTokenType.String)
            {
                return false;
            }

            value = reader.GetString() ?? string.Empty;
            return !reader.Read();
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>
    ///     One predicate: a normalized name and the value it must equal.
    /// </summary>
    /// <param name="Name">The name: an <c>@</c> name in lowercase or a channel or field name as written.</param>
    /// <param name="Value">The value.</param>
    private sealed record Predicate(string Name, PositionValue Value)
    {
        /// <summary>
        ///     Tells whether a position satisfies the predicate.
        /// </summary>
        /// <param name="position">The position.</param>
        /// <returns><see langword="true"/> when it does.</returns>
        public bool Matches(ImagePosition position)
        {
            return Name switch
            {
                "@id" => Equal(position.Id),
                "@view" => Equal(position.View),
                "@from" => Equal(position.From),
                "@to" => Equal(position.To),
                "@index" => position.Index is { } index && Value == PositionValue.FromNumber(index),
                _ => Lookup(position, out var actual) && actual == Value
            };
        }

        /// <summary>
        ///     Looks up the word name of the predicate: in the values when it is a channel, otherwise in the data.
        /// </summary>
        /// <remarks>
        ///     The renderer names its channels (<see cref="ImagePosition.ChannelNames"/>), so the rule holds even for a
        ///     channel the feature does not have (<c>x=1</c> never reads a data field <c>x</c>); a name the values hold
        ///     is a channel too, so a renderer that gives no channel names (a pie slice's <c>value</c>) still works.
        /// </remarks>
        /// <param name="position">The position.</param>
        /// <param name="actual">The value when the name is present.</param>
        /// <returns><see langword="true"/> when the name is present.</returns>
        private bool Lookup(ImagePosition position, out PositionValue actual)
        {
            if (position.ChannelNames?.Contains(Name) == true)
            {
                return position.TryGetValue(Name, out actual);
            }

            return position.TryGetValue(Name, out actual) || position.TryGetData(Name, out actual);
        }

        /// <summary>
        ///     Compares a text property with the value; a missing property never matches.
        /// </summary>
        /// <param name="actual">The property.</param>
        /// <returns><see langword="true"/> when they are equal.</returns>
        private bool Equal(string? actual)
        {
            return actual is not null && Value == PositionValue.FromString(actual);
        }
    }
}
