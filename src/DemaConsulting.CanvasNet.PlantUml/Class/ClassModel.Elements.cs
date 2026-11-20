// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Svek;

namespace DemaConsulting.CanvasNet.PlantUml.Class;

/// <summary>
///     The kind of an entity of a class or object diagram (PlantUML's <c>LeafType</c>), which chooses its circled
///     character, its spot color, and whether its name is italic.
/// </summary>
internal enum ClassKind
{
    /// <summary>A class (<c>class</c>), spot C.</summary>
    Class,

    /// <summary>An abstract class (<c>abstract</c>, <c>abstract class</c>), spot A, italic name.</summary>
    AbstractClass,

    /// <summary>An interface (<c>interface</c>), spot I, italic name.</summary>
    Interface,

    /// <summary>An enumeration (<c>enum</c>), spot E.</summary>
    Enum,

    /// <summary>An annotation (<c>annotation</c>), spot @.</summary>
    Annotation,

    /// <summary>An entity (<c>entity</c>), spot E.</summary>
    Entity,

    /// <summary>An exception (<c>exception</c>), spot X.</summary>
    Exception,

    /// <summary>A structure (<c>struct</c>), spot S.</summary>
    Struct,

    /// <summary>A protocol (<c>protocol</c>), spot P.</summary>
    Protocol,

    /// <summary>A metaclass (<c>metaclass</c>), spot M.</summary>
    Metaclass,

    /// <summary>A stereotype (<c>stereotype</c>), spot S.</summary>
    Stereotype,

    /// <summary>A record (<c>record</c>), spot R.</summary>
    Record,

    /// <summary>A data class (<c>dataclass</c>), spot D.</summary>
    DataClass,

    /// <summary>An object (<c>object</c>): a centered name and fields, no spot.</summary>
    Object,

    /// <summary>A map (<c>map</c>): a centered name and a two-column table of keys and values.</summary>
    Map,
}

/// <summary>
///     The visibility of a member (PlantUML's <c>VisibilityModifier</c>), drawn as an icon before it.
/// </summary>
internal enum MemberVisibility
{
    /// <summary>No visibility character.</summary>
    None,

    /// <summary><c>-</c>: a square, red.</summary>
    Private,

    /// <summary><c>#</c>: a diamond, yellow.</summary>
    Protected,

    /// <summary><c>~</c>: a triangle, blue.</summary>
    Package,

    /// <summary><c>+</c>: a circle, green.</summary>
    Public,

    /// <summary><c>*</c>: a black circle (an IE mandatory attribute).</summary>
    Mandatory,
}

/// <summary>
///     What a <c>hide</c> or <c>show</c> statement of a class diagram hides or shows.
/// </summary>
internal enum ClassPortion
{
    /// <summary>The fields and the methods (<c>members</c>).</summary>
    Members,

    /// <summary>The fields (<c>fields</c>, <c>attributes</c>).</summary>
    Fields,

    /// <summary>The methods (<c>methods</c>).</summary>
    Methods,

    /// <summary>The circled character of the header (<c>circle</c>, <c>circled</c>).</summary>
    Circle,

    /// <summary>The stereotypes of the header (<c>stereotype</c>).</summary>
    Stereotype,
}

/// <summary>
///     One member of a class or an object as PlantUML reads it (<c>cucadiagram/Member</c>): its display text without
///     the visibility character and the modifiers, its visibility, and its modifiers.
/// </summary>
/// <param name="Text">The display text (one space for an empty member).</param>
/// <param name="Visibility">The visibility.</param>
/// <param name="IsStatic">Whether the member is static (<c>{static}</c> or <c>{classifier}</c>): underlined.</param>
/// <param name="IsAbstract">Whether the member is abstract (<c>{abstract}</c>): italic.</param>
internal sealed record ClassMember(string Text, MemberVisibility Visibility, bool IsStatic, bool IsAbstract)
{
    /// <summary>
    ///     Reads a member line.
    /// </summary>
    /// <param name="raw">The line as written.</param>
    /// <returns>The member.</returns>
    public static ClassMember Parse(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var text = RemoveTag(raw, "{method}");
        text = RemoveTag(text, "{field}");

        // A URL at the end of the member is not drawn
        var url = text.LastIndexOf("[[", StringComparison.Ordinal);
        if (url >= 0 && text.TrimEnd().EndsWith("]]", StringComparison.Ordinal))
        {
            text = text[..url];
        }

        var lower = text.ToLowerInvariant();
        var isStatic = lower.Contains("{static}", StringComparison.Ordinal) || lower.Contains("{classifier}", StringComparison.Ordinal);
        var isAbstract = lower.Contains("{abstract}", StringComparison.Ordinal);
        foreach (var tag in new[] { "{static}", "{classifier}", "{abstract}" })
        {
            text = RemoveTag(text, tag);
        }

        var clean = text.Trim();
        if (clean.Length == 0)
        {
            return new ClassMember(" ", MemberVisibility.None, isStatic, isAbstract);
        }

        var visibility = VisibilityOf(clean);
        var display = visibility == MemberVisibility.None ? clean : clean[1..].Trim();
        return new ClassMember(Guillemets(display), visibility, isStatic, isAbstract);
    }

    /// <summary>
    ///     Gets the visibility character of a member line, as PlantUML's <c>isVisibilityCharacter</c> reads it: a line of
    ///     more than two characters whose first is <c>-</c>, <c>#</c>, <c>+</c>, <c>~</c>, or <c>*</c> and whose second
    ///     differs from it.
    /// </summary>
    /// <param name="text">The trimmed line.</param>
    /// <returns>The visibility; none when the line has no visibility character.</returns>
    public static MemberVisibility VisibilityOf(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length <= 2 || text[1] == text[0])
        {
            return MemberVisibility.None;
        }

        return text[0] switch
        {
            '-' => MemberVisibility.Private,
            '#' => MemberVisibility.Protected,
            '~' => MemberVisibility.Package,
            '+' => MemberVisibility.Public,
            '*' => MemberVisibility.Mandatory,
            _ => MemberVisibility.None,
        };
    }

    /// <summary>
    ///     Replaces the double angle brackets of a text with guillemets, as PlantUML does in members and stereotypes.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with guillemets.</returns>
    public static string Guillemets(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace("<<", "\u00AB", StringComparison.Ordinal).Replace(">>", "\u00BB", StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets the visibility character of a visibility.
    /// </summary>
    /// <param name="visibility">The visibility.</param>
    /// <returns>The character, or an empty text for none.</returns>
    public static string CharacterOf(MemberVisibility visibility) => visibility switch
    {
        MemberVisibility.Private => "-",
        MemberVisibility.Protected => "#",
        MemberVisibility.Package => "~",
        MemberVisibility.Public => "+",
        MemberVisibility.Mandatory => "*",
        _ => string.Empty,
    };

    /// <summary>
    ///     Removes every occurrence of a modifier tag and the white space after it, without regard to case.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="tag">The tag (<c>{static}</c>, ...).</param>
    /// <returns>The text without the tag.</returns>
    private static string RemoveTag(string text, string tag)
    {
        var index = text.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
        while (index >= 0)
        {
            var end = index + tag.Length;
            while (end < text.Length && char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            text = string.Concat(text.AsSpan(0, index), text.AsSpan(end));
            index = text.IndexOf(tag, index, StringComparison.OrdinalIgnoreCase);
        }

        return text;
    }
}

/// <summary>
///     The body lines of a class or an object: PlantUML's rules for separators and for telling methods from fields
///     (<c>BodierLikeClassOrObject</c>, <c>BodyEnhancedAbstract</c>).
/// </summary>
internal static class ClassBody
{
    /// <summary>
    ///     Decides whether a body line is a separator: it starts and ends with <c>--</c>, <c>==</c>, <c>__</c>, or
    ///     <c>..</c> (but is not <c>...</c>).
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>True when it is.</returns>
    public static bool IsSeparator(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var mark = new[] { "--", "==", "__", ".." }
            .FirstOrDefault(m => line.StartsWith(m, StringComparison.Ordinal) && line.EndsWith(m, StringComparison.Ordinal));
        return mark is not null && (mark != ".." || line != "...");
    }

    /// <summary>
    ///     Gets the title of a separator (the text between its marks), or null when it has none.
    /// </summary>
    /// <param name="line">The separator line.</param>
    /// <returns>The trimmed title, or null.</returns>
    public static string? SeparatorTitle(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Length <= 4)
        {
            return null;
        }

        var title = line[2..^2].Trim();
        return title.Length == 0 ? null : title;
    }

    /// <summary>
    ///     Decides whether a member line is a method: it holds <c>{method}</c>, or it holds no <c>{field}</c> and has a
    ///     parenthesis.
    /// </summary>
    /// <param name="line">The line.</param>
    /// <returns>True for a method.</returns>
    public static bool IsMethod(string line)
    {
        ArgumentNullException.ThrowIfNull(line);
        var url = line.IndexOf("[[", StringComparison.Ordinal);
        var purged = url >= 0 ? line[..url] : line;
        if (purged.Contains("{method}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !purged.Contains("{field}", StringComparison.OrdinalIgnoreCase) && (purged.Contains('(') || purged.Contains(')'));
    }

    /// <summary>
    ///     Splits the body of a class without separators into its fields and its methods, as PlantUML does: an empty line
    ///     between two methods belongs to the methods, leading empty lines are dropped, and trailing ones removed.
    /// </summary>
    /// <param name="lines">The body lines.</param>
    /// <returns>The field lines and the method lines, in order.</returns>
    public static (IReadOnlyList<string> Fields, IReadOnlyList<string> Methods) Split(IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var fields = new List<string>();
        var methods = new List<string>();
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var betweenMethods = i > 0 && i < lines.Count - 1 && line.Length == 0 && IsMethod(lines[i - 1]) && IsMethod(lines[i + 1]);
            if ((betweenMethods || IsMethod(line)) && !(line.Length == 0 && methods.Count == 0))
            {
                methods.Add(line);
            }

            if (!IsMethod(line) && !(line.Length == 0 && fields.Count == 0))
            {
                fields.Add(line);
            }
        }

        TrimEnd(fields);
        TrimEnd(methods);
        return (fields, methods);
    }

    /// <summary>
    ///     Removes the trailing empty lines.
    /// </summary>
    /// <param name="lines">The lines.</param>
    private static void TrimEnd(List<string> lines)
    {
        while (lines.Count > 0 && lines[^1].Trim().Length == 0)
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }
}

/// <summary>
///     An element of a class diagram that links connect and the layout places: an entity, a package, or a note.
/// </summary>
/// <param name="id">The id: the qualified name of an entity or a package, the alias of a floating note.</param>
/// <param name="location">The location of the statement that declared it.</param>
internal abstract class ClassNode(string id, PlantUmlLocation location)
{
    /// <summary>
    ///     Gets the id.
    /// </summary>
    public string Id { get; } = id ?? throw new ArgumentNullException(nameof(id));

    /// <summary>
    ///     Gets the location of the statement that declared it.
    /// </summary>
    public PlantUmlLocation Location { get; } = location;

    /// <summary>
    ///     Gets or sets the package that contains the node, or null at the top of the diagram.
    /// </summary>
    public ClassPackage? Package { get; set; }
}

/// <summary>
///     A class, an interface, an enumeration, ..., an object, or a map, with what its statements gave it.
/// </summary>
/// <param name="id">The qualified name.</param>
/// <param name="display">The name drawn in the header.</param>
/// <param name="kind">The kind.</param>
/// <param name="location">The location of the statement that declared it (or first named it).</param>
internal sealed class ClassEntity(string id, string display, ClassKind kind, PlantUmlLocation location) : ClassNode(id, location)
{
    /// <summary>
    ///     Gets or sets the name drawn in the header.
    /// </summary>
    public string Display { get; set; } = display ?? throw new ArgumentNullException(nameof(display));

    /// <summary>
    ///     Gets or sets the kind.
    /// </summary>
    public ClassKind Kind { get; set; } = kind;

    /// <summary>
    ///     Gets the stereotype labels, in order (without the brackets).
    /// </summary>
    public List<string> Stereotypes { get; } = [];

    /// <summary>
    ///     Gets or sets the character of the spot written in a stereotype (<c>&lt;&lt; (S,#color) &gt;&gt;</c>), or null
    ///     for the kind's.
    /// </summary>
    public char? SpotCharacter { get; set; }

    /// <summary>
    ///     Gets or sets the color of the spot written in a stereotype, or null for the kind's.
    /// </summary>
    public Rgba32? SpotColor { get; set; }

    /// <summary>
    ///     Gets or sets the generic parameters (<c>&lt;T&gt;</c>), or null.
    /// </summary>
    public string? Generic { get; set; }

    /// <summary>
    ///     Gets the body lines (members and separators) in order.
    /// </summary>
    public List<string> Body { get; } = [];

    /// <summary>
    ///     Gets the entries of a map, in order.
    /// </summary>
    public List<(string Key, string Value)> Entries { get; } = [];

    /// <summary>
    ///     Gets or sets the fill (<c>#color</c>), or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; set; }

    /// <summary>
    ///     Gets or sets the line color (<c>##color</c>, <c>#back;line:color</c>), or null for the theme's.
    /// </summary>
    public Rgba32? Line { get; set; }

    /// <summary>
    ///     Gets or sets the text color (<c>#back;text:color</c>), or null for the theme's.
    /// </summary>
    public Rgba32? TextColor { get; set; }

    /// <summary>
    ///     Gets a value indicating whether the entity is an object or a map (drawn without a spot).
    /// </summary>
    public bool IsObjectLike => Kind is ClassKind.Object or ClassKind.Map;
}

/// <summary>
///     A package or a namespace: a cluster drawn as a folder (or another symbol chosen by its stereotype).
/// </summary>
/// <param name="id">The qualified name.</param>
/// <param name="title">The title drawn in the tab.</param>
/// <param name="location">The location of the statement that declared it (or first named it).</param>
internal sealed class ClassPackage(string id, string title, PlantUmlLocation location) : ClassNode(id, location)
{
    /// <summary>
    ///     Gets or sets the title.
    /// </summary>
    public string Title { get; set; } = title ?? throw new ArgumentNullException(nameof(title));

    /// <summary>
    ///     Gets the stereotype labels that are drawn (a stereotype that names a symbol chooses the outline instead).
    /// </summary>
    public List<string> Stereotypes { get; } = [];

    /// <summary>
    ///     Gets or sets the outline; a folder by default.
    /// </summary>
    public ClusterSymbol Symbol { get; set; } = ClusterSymbol.Folder;

    /// <summary>
    ///     Gets or sets the fill (<c>#color</c>), or null for none.
    /// </summary>
    public Rgba32? Fill { get; set; }
}

/// <summary>
///     A note: attached to a side of an entity, on a link, or floating (and then linked like an entity).
/// </summary>
/// <param name="id">The id: the alias of a floating note, a generated id otherwise.</param>
/// <param name="text">The text.</param>
/// <param name="location">The location of the statement.</param>
internal sealed class ClassNote(string id, string text, PlantUmlLocation location) : ClassNode(id, location)
{
    /// <summary>
    ///     Gets the text.
    /// </summary>
    public string Text { get; } = text ?? throw new ArgumentNullException(nameof(text));

    /// <summary>
    ///     Gets or sets the fill, or null for the theme's.
    /// </summary>
    public Rgba32? Fill { get; set; }

    /// <summary>
    ///     Gets or sets the entity the note is attached to, or null.
    /// </summary>
    public ClassEntity? Target { get; set; }

    /// <summary>
    ///     Gets or sets the link the note is on, or null.
    /// </summary>
    public ClassLink? OnLink { get; set; }

    /// <summary>
    ///     Gets or sets the side of the target or of the link label.
    /// </summary>
    public NotePosition Position { get; set; } = NotePosition.Right;

    /// <summary>
    ///     Gets a value indicating whether the note is floating (neither attached nor on a link).
    /// </summary>
    public bool IsFloating => Target is null && OnLink is null;
}

/// <summary>
///     A relationship between two nodes, with its decorations, style, labels, length, and direction.
/// </summary>
/// <param name="id">The id.</param>
/// <param name="from">The first end (written first).</param>
/// <param name="to">The second end.</param>
/// <param name="location">The location of the statement.</param>
internal sealed class ClassLink(string id, ClassNode from, ClassNode to, PlantUmlLocation location)
{
    /// <summary>Gets the id.</summary>
    public string Id { get; } = id ?? throw new ArgumentNullException(nameof(id));

    /// <summary>Gets the first end.</summary>
    public ClassNode From { get; } = from ?? throw new ArgumentNullException(nameof(from));

    /// <summary>Gets the second end.</summary>
    public ClassNode To { get; } = to ?? throw new ArgumentNullException(nameof(to));

    /// <summary>Gets the location of the statement.</summary>
    public PlantUmlLocation Location { get; } = location;

    /// <summary>Gets or sets the decoration at the first end.</summary>
    public LinkDecor TailDecor { get; set; }

    /// <summary>Gets or sets the decoration at the second end.</summary>
    public LinkDecor HeadDecor { get; set; }

    /// <summary>Gets or sets the line style.</summary>
    public LinkStyle Style { get; set; }

    /// <summary>Gets or sets the line width, or null for the style's.</summary>
    public double? Thickness { get; set; }

    /// <summary>Gets or sets the line color, or null for the theme's.</summary>
    public Rgba32? Color { get; set; }

    /// <summary>Gets or sets the length of the arrow body; 2 by default.</summary>
    public int Length { get; set; } = 2;

    /// <summary>Gets or sets the direction hint.</summary>
    public LinkDirection Direction { get; set; }

    /// <summary>Gets or sets the middle label, or null.</summary>
    public string? Label { get; set; }

    /// <summary>Gets or sets the direction mark of the label.</summary>
    public LinkLabelArrow LabelArrow { get; set; }

    /// <summary>Gets or sets the label near the first end, or null.</summary>
    public string? TailLabel { get; set; }

    /// <summary>Gets or sets the label near the second end, or null.</summary>
    public string? HeadLabel { get; set; }

    /// <summary>Gets or sets the weight; 1 by default.</summary>
    public double Weight { get; set; } = 1;

    /// <summary>Gets a value indicating whether the link touches a note.</summary>
    public bool TouchesNote => From is ClassNote || To is ClassNote;
}

/// <summary>
///     A <c>hide</c> or <c>show</c> rule of a class diagram, applied in text order.
/// </summary>
/// <param name="Show">True for <c>show</c>.</param>
/// <param name="Gender">What the rule applies to: null for every entity, a kind keyword (<c>class</c>, <c>interface</c>, ...), a stereotype (<c>&lt;&lt;S&gt;&gt;</c>), or an entity name.</param>
/// <param name="EmptyOnly">Whether the rule applies only to empty compartments (<c>empty</c>).</param>
/// <param name="Portion">What it hides or shows.</param>
internal sealed record ClassHideRule(bool Show, string? Gender, bool EmptyOnly, ClassPortion Portion);

/// <summary>
///     What is shown of one entity once the <c>hide</c> and <c>show</c> rules are applied.
/// </summary>
/// <param name="Fields">Whether the fields compartment is shown.</param>
/// <param name="Methods">Whether the methods compartment is shown.</param>
/// <param name="Circle">Whether the circled character is shown.</param>
/// <param name="Stereotype">Whether the stereotypes are shown.</param>
internal readonly record struct ClassPortions(bool Fields, bool Methods, bool Circle, bool Stereotype)
{
    /// <summary>
    ///     Gets the portions of an entity when no rule applies: everything shown.
    /// </summary>
    public static ClassPortions All { get; } = new(true, true, true, true);
}
