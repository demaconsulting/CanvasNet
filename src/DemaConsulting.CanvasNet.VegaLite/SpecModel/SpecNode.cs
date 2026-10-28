// cspell:ignore hconcat vconcat ordinally
using System.Globalization;

namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     The kind of JSON value a <see cref="SpecNode"/> holds.
/// </summary>
internal enum SpecNodeKind
{
    /// <summary>A JSON object; its properties are in <see cref="SpecNode.Properties"/>.</summary>
    Object,

    /// <summary>A JSON array; its elements are in <see cref="SpecNode.Items"/>.</summary>
    Array,

    /// <summary>A JSON string; the text is <see cref="SpecNode.StringValue"/>.</summary>
    String,

    /// <summary>A JSON number; the value is <see cref="SpecNode.NumberValue"/>.</summary>
    Number,

    /// <summary>A JSON <c>true</c> or <c>false</c>; the value is <see cref="SpecNode.BoolValue"/>.</summary>
    Boolean,

    /// <summary>A JSON <c>null</c>.</summary>
    Null,
}

/// <summary>
///     One value of a parsed Vega-Lite specification: an immutable node of the tree produced by
///     <see cref="SpecParser"/>, remembering where it was written.
/// </summary>
/// <remarks>
///     <para>
///         Every later stage (feature catalog, configuration, data, scales, marks) reads the specification through this
///         tree instead of re-reading JSON, so each stage can point its diagnostics at the construct it is about:
///         <see cref="Pointer"/> is the RFC 6901 JSON pointer, and <see cref="Line"/> and <see cref="Column"/> are the
///         one-based text position of the first character of the value (a column counts UTF-16 code units, a tab is
///         one column). The position of the property key that introduced the value is kept too
///         (<see cref="KeyLine"/>, <see cref="KeyColumn"/>) for diagnostics that are about the property itself, such as
///         an unknown property.
///     </para>
///     <para>
///         The tree is immutable and safe for concurrent reading once <see cref="SpecParser"/> returns it. Object
///         properties are kept in document order; when a key is written twice the last value wins and keeps the
///         position of the first occurrence in <see cref="Properties"/> (the parser reports the duplicate). The pointer
///         is computed from the parent chain on demand, so a specification with a million data rows does not pay for a
///         million pointer strings.
///     </para>
/// </remarks>
internal sealed class SpecNode
{
    /// <summary>The number of properties above which an object builds a name index; fewer are searched linearly.</summary>
    private const int IndexThreshold = 8;

    /// <summary>An empty list shared by every node that has no children.</summary>
    private static readonly SpecNode[] NoChildren = [];

    /// <summary>The children of an object (property values, each with a <see cref="Name"/>) or an array (elements).</summary>
    private readonly SpecNode[] _children;

    /// <summary>A name index for objects with many properties, or null.</summary>
    private readonly Dictionary<string, SpecNode>? _index;

    /// <summary>The parent node; assigned once by the parent's constructor, or null for the root.</summary>
    private SpecNode? _parent;

    /// <summary>The cached pointer, computed on first use.</summary>
    private string? _pointer;

    /// <summary>
    ///     Initializes a node.
    /// </summary>
    /// <param name="kind">The kind of value.</param>
    /// <param name="name">The property name when the node is a property value; otherwise null.</param>
    /// <param name="index">The array index when the node is an array element; otherwise -1.</param>
    /// <param name="position">The positions of the value and of its property key.</param>
    /// <param name="stringValue">The text of a string node.</param>
    /// <param name="numberValue">The value of a number node.</param>
    /// <param name="boolValue">The value of a boolean node.</param>
    /// <param name="children">The property values or elements; adopted by this node.</param>
    internal SpecNode(
        SpecNodeKind kind,
        string? name,
        int index,
        NodePosition position,
        string? stringValue,
        double numberValue,
        bool boolValue,
        SpecNode[]? children)
    {
        Kind = kind;
        Name = name;
        Index = index;
        Line = position.Line;
        Column = position.Column;
        KeyLine = position.KeyLine;
        KeyColumn = position.KeyColumn;
        StringValue = stringValue;
        NumberValue = numberValue;
        BoolValue = boolValue;
        _children = children is { Length: > 0 } ? children : NoChildren;

        // The children are created before their parent, so the parent adopts them here; after construction the
        // tree is never modified again
        foreach (var child in _children)
        {
            child._parent = this;
        }

        // Objects with many properties get a name index so that lookups stay constant-time
        if (kind == SpecNodeKind.Object && _children.Length > IndexThreshold)
        {
            _index = new Dictionary<string, SpecNode>(_children.Length, StringComparer.Ordinal);
            foreach (var child in _children)
            {
                _index[child.Name!] = child;
            }
        }
    }

    /// <summary>Gets the kind of JSON value.</summary>
    public SpecNodeKind Kind { get; }

    /// <summary>Gets the parent node, or <see langword="null"/> for the root.</summary>
    public SpecNode? Parent => _parent;

    /// <summary>Gets the property name when this node is a property value of an object; otherwise null.</summary>
    public string? Name { get; }

    /// <summary>Gets the zero-based index when this node is an element of an array; otherwise -1.</summary>
    public int Index { get; }

    /// <summary>Gets the one-based line of the first character of the value.</summary>
    public int Line { get; }

    /// <summary>Gets the one-based UTF-16 column of the first character of the value.</summary>
    public int Column { get; }

    /// <summary>Gets the one-based line of the property key that introduced the value, or zero when this is not a property value.</summary>
    public int KeyLine { get; }

    /// <summary>Gets the one-based UTF-16 column of the property key that introduced the value, or zero when this is not a property value.</summary>
    public int KeyColumn { get; }

    /// <summary>Gets the text of a string node; null for other kinds.</summary>
    public string? StringValue { get; }

    /// <summary>Gets the value of a number node; zero for other kinds. Always finite.</summary>
    public double NumberValue { get; }

    /// <summary>Gets the value of a boolean node; false for other kinds.</summary>
    public bool BoolValue { get; }

    /// <summary>Gets the elements of an array node in document order; empty for other kinds.</summary>
    public IReadOnlyList<SpecNode> Items => Kind == SpecNodeKind.Array ? _children : NoChildren;

    /// <summary>
    ///     Gets the property values of an object node in document order (each carries its <see cref="Name"/>); empty for
    ///     other kinds.
    /// </summary>
    public IReadOnlyList<SpecNode> Properties => Kind == SpecNodeKind.Object ? _children : NoChildren;

    /// <summary>Gets a value indicating whether this node is an object.</summary>
    public bool IsObject => Kind == SpecNodeKind.Object;

    /// <summary>Gets a value indicating whether this node is an array.</summary>
    public bool IsArray => Kind == SpecNodeKind.Array;

    /// <summary>Gets a value indicating whether this node is a string.</summary>
    public bool IsString => Kind == SpecNodeKind.String;

    /// <summary>Gets a value indicating whether this node is a number.</summary>
    public bool IsNumber => Kind == SpecNodeKind.Number;

    /// <summary>Gets a value indicating whether this node is a boolean.</summary>
    public bool IsBoolean => Kind == SpecNodeKind.Boolean;

    /// <summary>Gets a value indicating whether this node is <c>null</c>.</summary>
    public bool IsNull => Kind == SpecNodeKind.Null;

    /// <summary>
    ///     Gets the RFC 6901 JSON pointer of this node; the empty string for the root.
    /// </summary>
    /// <remarks>
    ///     Property names and indexes are escaped (<c>~</c> as <c>~0</c>, <c>/</c> as <c>~1</c>). The value is
    ///     computed once and cached; computing it twice concurrently gives the same string.
    /// </remarks>
    public string Pointer => _pointer ??= ComputePointer();

    /// <summary>
    ///     Gets the property of an object node by name.
    /// </summary>
    /// <param name="name">The property name (compared ordinally).</param>
    /// <returns>The property value, or <see langword="null"/> when this is not an object or has no such property.</returns>
    public SpecNode? this[string name]
    {
        get
        {
            if (Kind != SpecNodeKind.Object)
            {
                return null;
            }

            if (_index is not null)
            {
                return _index.GetValueOrDefault(name);
            }

            return _children.FirstOrDefault(child => string.Equals(child.Name, name, StringComparison.Ordinal));
        }
    }

    /// <summary>
    ///     Escapes a property name or array index as one RFC 6901 pointer segment.
    /// </summary>
    /// <param name="segment">The raw segment.</param>
    /// <returns>The escaped segment (<c>~</c> becomes <c>~0</c> and <c>/</c> becomes <c>~1</c>).</returns>
    public static string EscapePointerSegment(string segment) =>
        segment.Contains('~') || segment.Contains('/')
            ? segment.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal)
            : segment;

    /// <summary>
    ///     Gets the pointer of a child of this node without creating the child.
    /// </summary>
    /// <param name="segment">The raw (unescaped) property name or index text of the child.</param>
    /// <returns>The pointer of the child.</returns>
    public string ChildPointer(string segment) => Pointer + "/" + EscapePointerSegment(segment);

    /// <summary>
    ///     Gets the text of this node when it is a string.
    /// </summary>
    /// <returns>The text, or <see langword="null"/> for any other kind.</returns>
    public string? AsString() => Kind == SpecNodeKind.String ? StringValue : null;

    /// <summary>
    ///     Gets the value of this node when it is a number.
    /// </summary>
    /// <returns>The number, or <see langword="null"/> for any other kind.</returns>
    public double? AsNumber() => Kind == SpecNodeKind.Number ? NumberValue : null;

    /// <summary>
    ///     Gets the value of this node when it is a boolean.
    /// </summary>
    /// <returns>The boolean, or <see langword="null"/> for any other kind.</returns>
    public bool? AsBool() => Kind == SpecNodeKind.Boolean ? BoolValue : null;

    /// <summary>
    ///     Creates the location of the value for a diagnostic.
    /// </summary>
    /// <param name="file">The source name, or null.</param>
    /// <returns>The file, the pointer and the position of the first character of the value.</returns>
    public VegaLiteLocation GetLocation(string? file) => new(file, Pointer, Line, Column);

    /// <summary>
    ///     Creates the location of the property key for a diagnostic about the property itself.
    /// </summary>
    /// <param name="file">The source name, or null.</param>
    /// <returns>The file, the pointer and the position of the key; the position of the value when this is not a property.</returns>
    public VegaLiteLocation GetKeyLocation(string? file) =>
        KeyLine > 0 ? new VegaLiteLocation(file, Pointer, KeyLine, KeyColumn) : GetLocation(file);

    /// <summary>
    ///     Gets a short description of the node for debugging and test failure messages.
    /// </summary>
    /// <returns>The kind, the pointer and the position.</returns>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Kind} {(Pointer.Length == 0 ? "(root)" : Pointer)} @{Line}:{Column}");

    /// <summary>
    ///     Builds the pointer by walking up the parent chain.
    /// </summary>
    /// <returns>The pointer.</returns>
    private string ComputePointer()
    {
        // Collect the segments from this node up to the root, then join them in root-to-leaf order; iteration (not
        // recursion) keeps the cost independent of the stack even for the deepest accepted nesting
        var segments = new List<string>();
        for (var node = this; node._parent is not null; node = node._parent)
        {
            segments.Add(node.Name is not null
                ? EscapePointerSegment(node.Name)
                : node.Index.ToString(CultureInfo.InvariantCulture));
        }

        if (segments.Count == 0)
        {
            return string.Empty;
        }

        segments.Reverse();
        return "/" + string.Join('/', segments);
    }
}

/// <summary>
///     The text positions of a node: where its value starts and where the property key that introduced it starts.
/// </summary>
/// <param name="Line">The one-based line of the first character of the value.</param>
/// <param name="Column">The one-based UTF-16 column of the first character of the value.</param>
/// <param name="KeyLine">The one-based line of the property key, or zero when the node is not a property value.</param>
/// <param name="KeyColumn">The one-based UTF-16 column of the property key, or zero when the node is not a property value.</param>
internal readonly record struct NodePosition(int Line, int Column, int KeyLine, int KeyColumn);

/// <summary>
///     A parsed specification: the tree, the source name used in locations, and the problems found while reading it.
/// </summary>
/// <remarks>
///     The tree is immutable and safe for concurrent use. <see cref="Warnings"/> holds the diagnostics that reading and
///     validating produce without failing (duplicate keys and unknown properties), in the order they were found; the
///     feature catalog adds its own reports for constructs that are recognized but not rendered.
/// </remarks>
internal sealed class SpecTree
{
    /// <summary>
    ///     Initializes a parsed specification.
    /// </summary>
    /// <param name="root">The root object.</param>
    /// <param name="sourceName">The source name used in locations, or null.</param>
    /// <param name="warnings">The warnings found while parsing.</param>
    public SpecTree(SpecNode root, string? sourceName, IReadOnlyList<FeatureReport> warnings)
    {
        Root = root;
        SourceName = sourceName;
        Warnings = warnings;
    }

    /// <summary>Gets the root object of the specification.</summary>
    public SpecNode Root { get; }

    /// <summary>Gets the source name used in locations, or <see langword="null"/> when the source is unnamed.</summary>
    public string? SourceName { get; }

    /// <summary>Gets the warnings found while parsing (<c>vl-duplicate-key</c>, <c>vl-unknown-property</c>), in the order found.</summary>
    public IReadOnlyList<FeatureReport> Warnings { get; }

    /// <summary>
    ///     Gets the location of a node's value.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The location with this tree's source name.</returns>
    public VegaLiteLocation Locate(SpecNode node) => node.GetLocation(SourceName);

    /// <summary>
    ///     Gets the location of a node's property key.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <returns>The location of the key with this tree's source name.</returns>
    public VegaLiteLocation LocateKey(SpecNode node) => node.GetKeyLocation(SourceName);

    /// <summary>
    ///     Enumerates the views of the specification: the root, then the views nested in <c>layer</c>,
    ///     <c>concat</c>, <c>hconcat</c>, <c>vconcat</c> and <c>spec</c> (the body of a facet or repeat), at any depth.
    /// </summary>
    /// <remarks>
    ///     The walk uses an explicit stack, so the nesting depth (bounded by the parse limit) cannot overflow the call
    ///     stack. A view is listed before the views nested in it, and nested views appear in the order their
    ///     composition properties are written. Callers that need strict text order sort the results by position.
    /// </remarks>
    /// <returns>The view objects, parents before children.</returns>
    public IReadOnlyList<SpecNode> EnumerateViews()
    {
        var views = new List<SpecNode>();
        var pending = new Stack<SpecNode>();
        pending.Push(Root);
        while (pending.Count > 0)
        {
            var view = pending.Pop();
            views.Add(view);

            // Push the nested views in reverse so that they pop in document order
            var nested = new List<SpecNode>();
            foreach (var property in view.Properties)
            {
                switch (property.Name)
                {
                    case "layer" or "concat" or "hconcat" or "vconcat":
                        nested.AddRange(property.Items.Where(item => item.IsObject));
                        break;
                    case "spec" when property.IsObject:
                        nested.Add(property);
                        break;
                    default:
                        break;
                }
            }

            for (var i = nested.Count - 1; i >= 0; i--)
            {
                pending.Push(nested[i]);
            }
        }

        return views;
    }
}
