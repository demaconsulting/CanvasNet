// Portions derived from Vega-Lite (BSD-3-Clause): field naming, titles and the stack plan; see third-party/README.md
using System.Text;
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scales;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     How a channel is binned.
/// </summary>
internal enum BinKind
{
    /// <summary>The channel is not binned.</summary>
    None,

    /// <summary>The encoding bins the field (<c>bin</c> is true or an object): the bins are computed from the data.</summary>
    Binning,

    /// <summary>The data is already binned (<c>bin: "binned"</c>): the field is the start of a bin and the secondary channel its end.</summary>
    Binned,
}

/// <summary>
///     The implicit bin of a field that one or more encoding channels share (Vega-Lite names a bin by its field and parameters, not by its channel).
/// </summary>
/// <param name="StartName">The literal name of the field that receives the lower boundary of the bin (<c>bin_maxbins_10_x</c>).</param>
/// <param name="EndName">The literal name of the field that receives the upper boundary (<c>bin_maxbins_10_x_end</c>).</param>
/// <param name="RangeName">The literal name of the field that receives the text of the bin (<c>bin_maxbins_10_x_range</c>) when a discrete channel shows the bin; otherwise null.</param>
/// <param name="SourceField">The field path that is binned, as written in the specification.</param>
/// <param name="Settings">The bin parameters.</param>
/// <param name="Node">The <c>bin</c> value of the first channel definition that asked for the bin (the location of failures).</param>
/// <param name="Format">The number format specifier of the boundaries in the text of the bin.</param>
internal sealed record BinPlan(
    string StartName,
    string EndName,
    string? RangeName,
    string SourceField,
    BinSettings Settings,
    SpecNode Node,
    string Format);

/// <summary>
///     The implicit time unit of a field that one or more encoding channels share (Vega-Lite names it by its unit and field, not by its channel).
/// </summary>
/// <param name="StartName">The literal name of the field that receives the start of the unit (<c>yearmonth_date</c>).</param>
/// <param name="EndName">The literal name of the field that receives the end of the unit (<c>yearmonth_date_end</c>).</param>
/// <param name="SourceField">The field path of the dates, as written in the specification.</param>
/// <param name="Spec">The time unit as written (the <c>maxbins</c> form is resolved against the data when the transform runs; see <see cref="EncodingData.TimeUnits"/>).</param>
/// <param name="Node">The <c>timeUnit</c> value of the first channel definition that asked for the unit (the location of failures).</param>
/// <param name="HasBandEnd">Whether a channel reads the end of the unit as the second end of its position (a mark that spans the unit).</param>
internal sealed record TimeUnitPlan(
    string StartName,
    string EndName,
    string SourceField,
    TimeUnitSpec Spec,
    SpecNode Node,
    bool HasBandEnd);

/// <summary>
///     One channel definition of an encoding and what the implicit pipeline makes of it.
/// </summary>
/// <param name="Channel">The channel name (<c>x</c>, <c>x2</c>, <c>color</c>, <c>detail</c>, ...).</param>
/// <param name="Definition">The definition object.</param>
/// <param name="SourceField">The field path as written in the specification, or null when the definition has none (a count, a datum or a value).</param>
/// <param name="ValueField">The field path the channel reads in the table before a stack: the (derived) field of a bin or an aggregate, else the source field; null when the channel reads no field.</param>
/// <param name="RawField">The field path of the channel in the Raw table (before the aggregate): the source field of an aggregate, else the value field; null for a count.</param>
/// <param name="FieldPath">The field path the channel reads in the Main table: the value field, or the upper end of its stack.</param>
/// <param name="Field2Path">The field path of the second end of the channel: the lower end of a stack, the end of a bin, the field of the secondary channel of a range; null when there is none.</param>
/// <param name="Bin">How the channel is binned.</param>
/// <param name="BinPlan">The bin of the channel when <paramref name="Bin"/> is <see cref="BinKind.Binning"/>; otherwise null.</param>
/// <param name="Measure">The aggregate measure of the channel, or null.</param>
/// <param name="IsStacked">Whether the channel carries the stack.</param>
/// <param name="Title">The default title of the channel (Vega-Lite's verbal, plain or functional title; two merged for a range), or null when the channel reads no field.</param>
/// <param name="FieldDefinition">The object that carries the field of the channel when it is not <paramref name="Definition"/>: the condition of a channel whose own definition is a value (Vega-Lite's <c>getFieldOrDatumDef</c>); otherwise null.</param>
/// <param name="TimeUnit">The time unit of the channel (its field is the start of the unit), or null.</param>
/// <param name="HasBandEnd">Whether the channel spans its time unit: its second end (<paramref name="Field2Path"/>) is the end of the unit.</param>
internal sealed record ChannelPlan(
    string Channel,
    SpecNode Definition,
    string? SourceField,
    string? ValueField,
    string? RawField,
    string? FieldPath,
    string? Field2Path,
    BinKind Bin,
    BinPlan? BinPlan,
    AggregateSpec? Measure,
    bool IsStacked,
    string? Title,
    SpecNode? FieldDefinition = null,
    TimeUnitPlan? TimeUnit = null,
    bool HasBandEnd = false)
{
    /// <summary>Gets the object whose <c>type</c>, <c>scale</c>, <c>sort</c>, <c>bin</c> and <c>legend</c> the channel reads: its field condition, or else its own definition.</summary>
    public SpecNode FieldDef => FieldDefinition ?? Definition;

    /// <summary>Gets a value indicating whether the channel reads a field of the table.</summary>
    public bool HasField => FieldPath is not null;

    /// <summary>Gets a value indicating whether the channel aggregates its field.</summary>
    public bool IsAggregate => Measure is not null;
}

/// <summary>
///     The implicit aggregate of an encoding: one row per distinct combination of the dimension fields, with the measures.
/// </summary>
/// <param name="GroupBy">The field paths of the dimensions (every field definition that does not aggregate).</param>
/// <param name="Measures">The measures, one per distinct output name.</param>
/// <param name="ArgFields">The fields that are read from the row an <c>argmax</c> or <c>argmin</c> measure returns.</param>
internal sealed record AggregatePlan(IReadOnlyList<string> GroupBy, IReadOnlyList<AggregateSpec> Measures, IReadOnlyList<ArgField> ArgFields);

/// <summary>
///     A field that an <c>argmax</c> or <c>argmin</c> channel reads from the row the aggregate returned.
/// </summary>
/// <param name="Measure">The literal name of the measure (the field that holds the row).</param>
/// <param name="Source">The field path inside the row.</param>
/// <param name="Output">The canonical path of the scalar field that receives the value.</param>
internal sealed record ArgField(string Measure, string Source, string Output);

/// <summary>
///     The implicit stack of an encoding.
/// </summary>
/// <param name="Channel">The stacked channel (<c>x</c>, <c>y</c>, <c>theta</c> or <c>radius</c>); its second end is <c>x2</c>, <c>y2</c>, <c>theta2</c> or <c>radius2</c>.</param>
/// <param name="Offset">The baseline.</param>
/// <param name="Field">The field path that is stacked (the value field of the channel).</param>
/// <param name="StartName">The literal name of the field that receives the lower end.</param>
/// <param name="EndName">The literal name of the field that receives the upper end.</param>
/// <param name="GroupBy">The field paths that split the rows into stacks (the fields of the dimension channel: <c>y</c> for <c>x</c>, <c>x</c> for <c>y</c>, <c>radius</c> for <c>theta</c>, <c>theta</c> for <c>radius</c>).</param>
/// <param name="Sort">The order of the rows inside a stack, bottom first.</param>
/// <param name="Definition">The definition of the stacked channel (the location of failures).</param>
internal sealed record StackPlan(
    string Channel,
    StackOffset Offset,
    string Field,
    string StartName,
    string EndName,
    IReadOnlyList<string> GroupBy,
    IReadOnlyList<SortField> Sort,
    SpecNode Definition);

/// <summary>
///     The impute that the <c>impute</c> property of the <c>x</c> or <c>y</c> channel asks for (Vega-Lite's <c>ImputeNode.makeFromEncoding</c>).
/// </summary>
/// <remarks>
///     The <c>method</c>, <c>value</c>, <c>frame</c> and <c>keyvals</c> are read from the <c>impute</c> object of <paramref name="Definition"/> when the transform runs.
/// </remarks>
/// <param name="Field">The field path that is imputed: the value field of the channel that has the <c>impute</c>.</param>
/// <param name="Key">The field path of the key: the value field of the other position channel.</param>
/// <param name="GroupBy">The field paths that split the rows into the series that are imputed (<see cref="EncodingPlan.PathGroupFields"/>).</param>
/// <param name="Definition">The definition of the channel that has the <c>impute</c> (its <c>impute</c> object is the source of the parameters, and the location of failures).</param>
internal sealed record ImputePlan(string Field, string Key, IReadOnlyList<string> GroupBy, SpecNode Definition);

/// <summary>
///     The impute that precedes the stack of a line or area mark: every layer of the stack gets a zero for each key of the dimension it lacks, so that the stacked paths have a point at every key.
/// </summary>
/// <param name="Field">The field path of the stacked measure (the same as <see cref="StackPlan.Field"/>).</param>
/// <param name="Key">The field path of the dimension channel (its start field for a time unit).</param>
/// <param name="GroupBy">The field paths of the stack-by definitions, in channel order (the layers of the stack).</param>
/// <param name="Definition">The definition of the stacked channel (the location of failures).</param>
internal sealed record StackImputePlan(string Field, string Key, IReadOnlyList<string> GroupBy, SpecNode Definition);

/// <summary>
///     What a view's encoding asks the data pipeline to do beyond the explicit transforms: bin the binned fields, compute the time units, aggregate the
///     aggregated ones and stack the stacked channel; and the field every channel then reads.
/// </summary>
/// <remarks>
///     <para>
///         The plan is a pure function of the specification and the configuration (it reads no data), and is the one place that
///         knows the derived field names and titles, so the scales, the marks and the axes agree on them. The rules are Vega-Lite's
///         (public algorithms, re-implemented): a bin is named by its parameters and field (<c>bin_maxbins_10_x</c>, with
///         <c>_end</c> for the upper boundary and <c>_range</c> for the text of a discrete channel); an aggregate is named
///         <c>sum_x</c> (a count <c>__count</c>); a stack writes <c>&lt;field&gt;_start</c> and <c>&lt;field&gt;_end</c> where the field is the
///         (derived) measure. When any definition aggregates, the rows are grouped by every other field definition of every
///         channel (a binned field by its start and end); the stack follows <see cref="StackRules"/> (on <c>x</c>/<c>y</c>, else on <c>theta</c>/<c>radius</c>), groups by the fields of the dimension channel and orders the layers by the <c>order</c> channel or by the stack-by fields (descending when
///         the stacked channel is <c>y</c>, ascending otherwise). Titles are Vega-Lite's defaults: <c>Sum of x</c>,
///         <c>Count of Records</c> (<c>countTitle</c>), <c>x (binned)</c>, <c>y for max x</c>, two titles of a range merged
///         as <c>a, b</c>; the configuration <c>fieldTitle</c> selects <c>verbal</c> (the default), <c>functional</c>
///         (<c>SUM(x)</c>) or <c>plain</c> (the field). A <c>timeUnit</c> (bin wins over it) is named by its unit and field the same way
///         (<c>yearmonth_date</c>, <c>month_step_2_date</c>, <c>timeunit_maxbins_10_date</c>, with <c>_end</c> for the end of the unit), is computed before the
///         aggregate, titled <c>date (year-month)</c> (verbal), <c>YEARMONTH(date)</c> (functional) or <c>date</c> (plain), and the dimension of an aggregate is its start
///         (and its end for a channel that spans the unit: a <c>temporal</c> <c>x</c> or <c>y</c> without a secondary channel whose mark has a band position).
///     </para>
///     <para>
///         For the path marks (line, area, trail) the plan also lists the fields that group the rows into paths (<see cref="PathGroupFields"/>) and the fields of the
///         <c>order</c> channel (<see cref="OrderFields"/>), and the imputes that run between the aggregate and the stack: the one of an <c>impute</c> property of <c>x</c> or <c>y</c>
///         (<see cref="Impute"/>) and the zeros that a stack of lines or areas adds for the keys a layer lacks (<see cref="StackImpute"/>).
///     </para>
///     <para>
///         <b>Differences from Vega-Lite.</b> <c>bin</c> is planned on <c>x</c> and <c>y</c> and on the channels that have a scale (<c>color</c>, <c>fill</c>,
///         <c>stroke</c>, <c>size</c>, the opacities, <c>strokeWidth</c>, <c>strokeDash</c>, <c>shape</c> and <c>angle</c>; the default <c>maxbins</c> of those channels is
///         Vega-Lite's <c>autoMaxBins</c>) and on <c>text</c> (the text mark shows the bin), while <c>bin: "binned"</c> is read on <c>x</c> and <c>y</c> only (the feature catalog reports it elsewhere); a count is titled by <c>countTitle</c> in every <c>fieldTitle</c> mode; the aggregate name keeps the
///         operation as written (<c>average_x</c>). The class is immutable and safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class EncodingPlan
{
    /// <summary>The number of bins Vega-Lite asks for on a position channel.</summary>
    internal const double DefaultPositionMaxBins = 10;

    /// <summary>The name of the field that holds the count of rows.</summary>
    internal const string CountName = "__count";

    /// <summary>An empty plan (no mark or no encoding).</summary>
    private static readonly IReadOnlyList<ChannelPlan> NoChannels = [];

    /// <summary>The channels in order of the encoding; array channels contribute every element.</summary>
    private readonly IReadOnlyList<ChannelPlan> _channels;

    /// <summary>
    ///     Initializes a plan.
    /// </summary>
    /// <param name="markType">The mark type.</param>
    /// <param name="orient">The inferred orientation of the mark, or null.</param>
    /// <param name="channels">The channel plans.</param>
    /// <param name="bins">The distinct bins.</param>
    /// <param name="timeUnits">The distinct time units.</param>
    /// <param name="aggregate">The aggregate, or null.</param>
    /// <param name="stack">The stack, or null.</param>
    /// <param name="needsRaw">Whether a scale reads the table as it was before the aggregate.</param>
    /// <param name="pathGroupFields">The fields that split the rows of a path mark into paths.</param>
    /// <param name="orderFields">The fields of the <c>order</c> channel.</param>
    /// <param name="impute">The impute of the <c>impute</c> property of <c>x</c> or <c>y</c>, or null.</param>
    /// <param name="stackImpute">The impute of the stack of a path mark, or null.</param>
    private EncodingPlan(
        string markType,
        string? orient,
        IReadOnlyList<ChannelPlan> channels,
        IReadOnlyList<BinPlan> bins,
        IReadOnlyList<TimeUnitPlan> timeUnits,
        AggregatePlan? aggregate,
        StackPlan? stack,
        bool needsRaw,
        IReadOnlyList<string>? pathGroupFields = null,
        IReadOnlyList<SortField>? orderFields = null,
        ImputePlan? impute = null,
        StackImputePlan? stackImpute = null)
    {
        MarkType = markType;
        Orient = orient;
        _channels = channels;
        Bins = bins;
        TimeUnits = timeUnits;
        Aggregate = aggregate;
        Stack = stack;
        NeedsRaw = needsRaw;
        PathGroupFields = pathGroupFields ?? [];
        OrderFields = orderFields ?? [];
        Impute = impute;
        StackImpute = stackImpute;
    }

    /// <summary>Gets the mark type; empty when the view has none.</summary>
    public string MarkType { get; }

    /// <summary>Gets the orientation of the mark as Vega-Lite infers it, or null.</summary>
    public string? Orient { get; }

    /// <summary>Gets every channel definition of the encoding, in order.</summary>
    public IReadOnlyList<ChannelPlan> Channels => _channels;

    /// <summary>Gets the distinct implicit bins.</summary>
    public IReadOnlyList<BinPlan> Bins { get; }

    /// <summary>Gets the distinct implicit time units.</summary>
    public IReadOnlyList<TimeUnitPlan> TimeUnits { get; }

    /// <summary>Gets the implicit aggregate, or null when no definition aggregates.</summary>
    public AggregatePlan? Aggregate { get; }

    /// <summary>Gets the implicit stack, or null when the marks are not stacked.</summary>
    public StackPlan? Stack { get; }

    /// <summary>Gets a value indicating whether a scale reads the Raw table (a sort by field, or an unaggregated domain).</summary>
    public bool NeedsRaw { get; }

    /// <summary>
    ///     Gets the canonical field paths that split the rows of a path mark (line, area, trail) into paths, in channel order and without duplicates:
    ///     the fields of the <c>detail</c> and <c>key</c> channels, of the <c>order</c> channel (except for a line and a trail), of <c>size</c> (except for a trail)
    ///     and of the paint channels; a definition that aggregates, and the position, polar, <c>text</c>, <c>shape</c>, <c>angle</c> and link channels, never split.
    /// </summary>
    public IReadOnlyList<string> PathGroupFields { get; }

    /// <summary>Gets the fields of the <c>order</c> channel with their direction (the order of the stack layers, of the points of a path or of the marks); empty when the encoding has no <c>order</c> field.</summary>
    public IReadOnlyList<SortField> OrderFields { get; }

    /// <summary>Gets the impute that the <c>impute</c> property of <c>x</c> or <c>y</c> asks for, or null (it needs a field definition on both channels).</summary>
    public ImputePlan? Impute { get; }

    /// <summary>Gets the impute that precedes the stack of a line or area mark, or null.</summary>
    public StackImputePlan? StackImpute { get; }

    /// <summary>Gets a value indicating whether the encoding asks for no implicit bin, time unit, aggregate, impute or stack.</summary>
    public bool IsIdentity => Bins.Count == 0 && TimeUnits.Count == 0 && Aggregate is null && Impute is null && StackImpute is null && Stack is null;

    /// <summary>
    ///     Gets the plan of the first definition of a channel.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <returns>The plan, or <see langword="null"/> when the encoding has no definition for the channel.</returns>
    public ChannelPlan? Channel(string channel) => _channels.FirstOrDefault(c => string.Equals(c.Channel, channel, StringComparison.Ordinal));

    /// <summary>
    ///     Creates the plan of a view.
    /// </summary>
    /// <param name="view">The view object (its <c>mark</c> and <c>encoding</c> are read).</param>
    /// <param name="config">The effective configuration (titles, number format).</param>
    /// <returns>The plan; an identity plan for a view without a mark or an encoding.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is null.</exception>
    public static EncodingPlan Create(SpecNode view, ResolvedConfig config)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(config);
        var (markType, specified) = ReadMark(view["mark"]);
        if (markType.Length == 0 || view["encoding"] is not { IsObject: true } encoding)
        {
            return new EncodingPlan(markType, null, NoChannels, [], [], null, null, false);
        }

        var orient = ScaleResolver.InferOrient(markType, specified, encoding);
        var bins = new Dictionary<string, BinBuilder>(StringComparer.Ordinal);
        var units = new Dictionary<string, TimeUnitBuilder>(StringComparer.Ordinal);
        var time = new TimeContext(encoding, markType, view["mark"] is { IsObject: true } markDefinition ? markDefinition : null, units);
        var channels = new List<ChannelPlan>();
        foreach (var property in encoding.Properties)
        {
            foreach (var definition in property.IsArray ? property.Items : [property])
            {
                if (definition.IsObject)
                {
                    channels.Add(PlanDefinition(property.Name!, definition, config, bins, time));
                }
            }
        }

        // A bin that a discrete channel shows as text has the range field for every channel that shares it
        var binPlans = bins.Values.Select(b => b.Build()).ToList();
        var finalBins = binPlans.ToDictionary(b => b.StartName, StringComparer.Ordinal);
        var unitPlans = units.Values.Select(u => u.Build()).ToList();
        var finalUnits = unitPlans.ToDictionary(u => u.StartName, StringComparer.Ordinal);
        channels = [.. channels.Select(c => c with
        {
            BinPlan = c.BinPlan is { } shared ? finalBins[shared.StartName] : null,
            TimeUnit = c.TimeUnit is { } sharedUnit ? finalUnits[sharedUnit.StartName] : null,
        })];
        var decision = StackRules.Decide(markType, orient, encoding);
        var stack = decision is null ? null : PlanStack(decision, encoding, channels);
        if (stack is not null)
        {
            channels = [.. channels.Select(c => c.Definition == stack.Definition ? c with { IsStacked = true, FieldPath = EscapeName(stack.EndName), Field2Path = EscapeName(stack.StartName) } : c)];
        }

        channels = LinkRanges(channels);
        var aggregate = PlanAggregate(channels);
        var needsRaw = channels.Any(c => c.Channel is not ("x2" or "y2" or "theta2" or "radius2") && (UsesFieldSort(c.FieldDef) || c.FieldDef["scale"]?["domain"]?.AsString() == "unaggregated"));
        var groupFields = PlanPathGroupFields(markType, channels);
        var orderFields = PlanOrderFields(encoding, channels);
        var impute = PlanImpute(channels, groupFields);
        var stackImpute = PlanStackImpute(markType, decision, stack, channels);
        return new EncodingPlan(markType, orient, channels, binPlans, unitPlans, aggregate, stack, needsRaw, groupFields, orderFields, impute, stackImpute);
    }

    /// <summary>
    ///     Gets the literal name of the bin of a field, the way Vega-Lite spells it: <c>bin</c>, then the parameters as
    ///     <c>_name_value</c> in the order written (<c>maxbins</c> last when the specification gives neither it nor a <c>step</c>), then the field.
    /// </summary>
    /// <param name="bin">The <c>bin</c> value: true or an object of parameters.</param>
    /// <param name="field">The field path as written.</param>
    /// <param name="defaultMaxBins">The <c>maxbins</c> that is added when the parameters give neither it nor a step.</param>
    /// <returns>The literal name of the start field, for example <c>bin_maxbins_10_x</c>.</returns>
    internal static string BinStartName(SpecNode bin, string field, double defaultMaxBins)
    {
        var name = new StringBuilder("bin");
        var hasSize = false;
        if (bin is { IsObject: true })
        {
            foreach (var parameter in bin.Properties)
            {
                hasSize |= parameter.Name is "maxbins" or "step";
                name.Append('_').Append(VarName(parameter.Name!)).Append('_').Append(VarName(Describe(parameter)));
            }
        }

        if (!hasSize)
        {
            name.Append("_maxbins_").Append(JsNumber.ToString(defaultMaxBins));
        }

        return name.Append('_').Append(field).ToString();
    }

    /// <summary>
    ///     Makes a text a legal variable name the way Vega-Lite does: every character that is not a letter, a digit or an underscore becomes an underscore.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The name.</returns>
    private static string VarName(string text)
    {
        var name = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            name.Append(c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9') or '_' ? c : '_');
        }

        return name.ToString();
    }

    /// <summary>
    ///     Writes a bin parameter value as the text JavaScript's template string would give.
    /// </summary>
    /// <param name="node">The value.</param>
    /// <returns>The text.</returns>
    private static string Describe(SpecNode node) => node.Kind switch
    {
        SpecNodeKind.Number => JsNumber.ToString(node.NumberValue),
        SpecNodeKind.Boolean => node.BoolValue ? "true" : "false",
        SpecNodeKind.String => node.StringValue!,
        SpecNodeKind.Array => string.Join(',', node.Items.Select(Describe)),
        SpecNodeKind.Object => "param_" + string.Join('_', node.Properties.Select(p => p.Name + "_" + Describe(p))),
        _ => "null",
    };

    /// <summary>
    ///     Escapes a literal field name as a field path.
    /// </summary>
    /// <param name="name">The literal name.</param>
    /// <returns>The path.</returns>
    private static string EscapeName(string name) => FieldPath.Escape(name);

    /// <summary>
    ///     Reads the mark type and the orient of a <c>mark</c> property.
    /// </summary>
    /// <param name="mark">The <c>mark</c> node.</param>
    /// <returns>The type (empty when there is none) and the written orient.</returns>
    private static (string Type, string? Orient) ReadMark(SpecNode? mark) =>
        mark switch
        {
            { IsString: true } => (mark.StringValue!, null),
            { IsObject: true } => (mark["type"]?.AsString() ?? string.Empty, mark["orient"]?.AsString()),
            _ => (string.Empty, null),
        };

    /// <summary>
    ///     Plans one channel definition: its bin, its aggregate, the fields it reads and its title.
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <param name="definition">The definition object.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="bins">The bins found so far, by start name.</param>
    /// <param name="time">What the time units of the encoding need: the encoding, the mark and the units found so far.</param>
    /// <returns>The plan of the definition (without the stack and the secondary channel).</returns>
    private static ChannelPlan PlanDefinition(string channel, SpecNode definition, ResolvedConfig config, Dictionary<string, BinBuilder> bins, TimeContext time)
    {
        var fieldDefinition = ChannelDefinitions.FieldDefinitionOf(definition);
        var source = fieldDefinition["field"] is { IsString: true } field && FieldPath.Canonicalize(field.StringValue) is not null ? field.StringValue : null;
        var aggregate = ParseAggregate(fieldDefinition["aggregate"], source);
        var bin = BinKind.None;
        BinPlan? binPlan = null;
        string? field2 = null;
        string? valueField = aggregate is null ? source : (aggregate.ArgOutput ?? EscapeName(aggregate.Spec.Name));
        var isPositionChannel = channel is "x" or "y";
        if ((isPositionChannel || CanBin(channel)) && StackRules.IsEnabled(fieldDefinition["bin"]))
        {
            var node = fieldDefinition["bin"]!;
            var alreadyBinned = node is { IsString: true } || node["binned"] is { IsBoolean: true, BoolValue: true };
            if (alreadyBinned)
            {
                // Data that is binned already has its bin ends in the data: only a position channel reads them
                bin = source is null || !isPositionChannel ? BinKind.None : BinKind.Binned;
            }
            else if (source is not null && aggregate is null)
            {
                bin = BinKind.Binning;
                var discrete = StackRules.TypeOf(fieldDefinition) is "nominal" or "ordinal";
                var maxBins = AutoMaxBins(channel);
                var start = BinStartName(node, source, maxBins);
                if (!bins.TryGetValue(start, out var builder))
                {
                    builder = new BinBuilder(start, source, BinSettings.Read(node, maxBins), node);
                    bins[start] = builder;
                }

                if (discrete && !builder.Discrete)
                {
                    // The text of a bin is formatted as the guide of the channel is: the axis of a position, the legend of any other channel
                    builder.Discrete = true;
                    var guide = isPositionChannel ? fieldDefinition["axis"] : fieldDefinition["legend"];
                    builder.Format = guide?["format"]?.AsString() ?? config.GetString("numberFormat") ?? string.Empty;
                }

                valueField = EscapeName(discrete ? start + "_range" : start);
                field2 = discrete ? null : EscapeName(start + "_end");
                binPlan = builder.Build();
            }
        }

        var rawField = RawFieldOf(aggregate, source, valueField);
        TimeUnitPlan? timeUnitPlan = null;
        var hasBandEnd = false;
        if (bin == BinKind.None && aggregate is null && source is not null && !StackRules.IsEnabled(fieldDefinition["bin"])
            && fieldDefinition["timeUnit"] is { IsNull: false } unitNode && TimeUnitSpec.TryRead(unitNode, out var unit) && !unit.IsBinned)
        {
            // The field the channel reads is the start of its time unit; a bar or rect also reads the end, as the second end of its position
            var start = unit.FieldName(source);
            hasBandEnd = SpansUnit(channel, fieldDefinition, time, config);
            if (!time.Units.TryGetValue(start, out var unitBuilder))
            {
                unitBuilder = new TimeUnitBuilder(start, source, unit, unitNode);
                time.Units[start] = unitBuilder;
            }

            unitBuilder.HasBandEnd |= hasBandEnd;
            valueField = EscapeName(start);
            rawField = valueField;
            field2 = hasBandEnd ? EscapeName(start + "_end") : null;
            timeUnitPlan = unitBuilder.Build();
        }

        var title = valueField is null ? null : DefaultTitle(source, aggregate, bin == BinKind.Binning, timeUnitPlan?.Spec, config);
        return new ChannelPlan(
            channel,
            definition,
            source,
            valueField,
            rawField,
            valueField,
            field2,
            bin,
            binPlan,
            aggregate?.Spec,
            false,
            title,
            ReferenceEquals(fieldDefinition, definition) ? null : fieldDefinition,
            timeUnitPlan,
            hasBandEnd);
    }

    /// <summary>
    ///     Tells whether a channel spans the time unit of its field: Vega-Lite's <c>hasBandEnd</c> for a temporal field with a time unit.
    /// </summary>
    /// <remarks>
    ///     The channel is a position channel (<c>x</c> or <c>y</c>) of a field of the type <c>temporal</c> (the type a time unit implies when none is given) that has no
    ///     secondary channel, and a band position is defined: the <c>bandPosition</c> of the definition, or the <c>timeUnitBandPosition</c> of the mark
    ///     (the mark definition, then <c>config.&lt;mark&gt;</c> and <c>config.mark</c>; the bar, rect and tick have 0.5 by default, a point has none and sits at the start of the unit).
    ///     A value that is not a number is not a band position (the configuration is read as it is everywhere: what is not of the right kind is ignored).
    /// </remarks>
    /// <param name="channel">The channel name.</param>
    /// <param name="fieldDefinition">The object that holds the field, the type and the band position.</param>
    /// <param name="time">The encoding and mark of the view.</param>
    /// <param name="config">The configuration.</param>
    /// <returns><see langword="true"/> when the channel reads the end of the unit as its second end.</returns>
    private static bool SpansUnit(string channel, SpecNode fieldDefinition, TimeContext time, ResolvedConfig config)
    {
        if (channel is not ("x" or "y")
            || (fieldDefinition["type"]?.AsString() ?? "temporal") != "temporal"
            || time.Encoding[channel + "2"] is { IsObject: true })
        {
            return false;
        }

        if (fieldDefinition["bandPosition"] is { IsNumber: true })
        {
            return true;
        }

        if (time.MarkDefinition?["timeUnitBandPosition"] is { IsNumber: true })
        {
            return true;
        }

        List<string>? styles = null;
        if (time.MarkDefinition?["style"] is { } style)
        {
            styles = [.. (style.IsArray ? style.Items : [style]).Select(s => s.AsString()).OfType<string>()];
        }

        return config.ResolveMarkProperty(time.MarkType, styles, "timeUnitBandPosition")?.AsNumber() is not null;
    }

    /// <summary>
    ///     Tells whether a channel other than <c>x</c> and <c>y</c> can bin its field: every channel the renderer draws through a scale, and the <c>text</c> channel (which shows the bin).
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <returns><see langword="true"/> for <c>color</c>, <c>fill</c>, <c>stroke</c>, <c>size</c>, the opacities, <c>strokeWidth</c>, <c>strokeDash</c>, <c>shape</c>, <c>angle</c> and <c>text</c>.</returns>
    internal static bool CanBin(string channel) =>
        channel is "color" or "fill" or "stroke" or "size" or "opacity" or "fillOpacity" or "strokeOpacity" or "strokeWidth" or "strokeDash" or "shape" or "angle" or "text";

    /// <summary>
    ///     Gets the number of bins Vega-Lite asks for on a channel when the specification gives neither <c>maxbins</c> nor <c>step</c>
    ///     (its <c>autoMaxBins</c>).
    /// </summary>
    /// <param name="channel">The channel name.</param>
    /// <returns>6 for the color, size, opacity, stroke width and shape channels (a facet and a size should not have too many bins), 4 for the stroke dash
    /// (there are five dash patterns), else 10.</returns>
    internal static double AutoMaxBins(string channel) => channel switch
    {
        "color" or "fill" or "stroke" or "size" or "opacity" or "fillOpacity" or "strokeOpacity" or "strokeWidth" or "shape" => 6,
        "strokeDash" => 4,
        _ => DefaultPositionMaxBins,
    };

    /// <summary>
    ///     Gets the field a channel reads in the Raw table.
    /// </summary>
    /// <param name="aggregate">The aggregate of the definition, or null.</param>
    /// <param name="source">The field path of the definition, or null.</param>
    /// <param name="valueField">The field the channel reads before a stack.</param>
    /// <returns>The source field of an aggregate (none for a count), otherwise the value field.</returns>
    private static string? RawFieldOf(MeasureInfo? aggregate, string? source, string? valueField)
    {
        if (aggregate is null)
        {
            return valueField;
        }

        return aggregate.Spec.Op == AggregateOp.Count ? null : source;
    }

    /// <summary>
    ///     Gives the range channels (<c>x2</c>, <c>y2</c>, <c>theta2</c>, <c>radius2</c>) to the position or polar channel they extend: the second field, the merged title.
    /// </summary>
    /// <param name="channels">The channel plans.</param>
    /// <returns>The plans with the secondary fields linked.</returns>
    private static List<ChannelPlan> LinkRanges(List<ChannelPlan> channels)
    {
        var linked = new List<ChannelPlan>(channels.Count);
        foreach (var plan in channels)
        {
            var secondary = plan.Channel is "x" or "y" or "theta" or "radius" && !plan.IsStacked && plan.Bin != BinKind.Binning
                ? channels.FirstOrDefault(c => c.Channel == plan.Channel + "2" && c.ValueField is not null)
                : null;
            if (secondary is null)
            {
                linked.Add(plan);
                continue;
            }

            var titles = new[] { plan.Title, secondary.Title }.OfType<string>().Distinct(StringComparer.Ordinal).ToList();
            linked.Add(plan with { Field2Path = secondary.ValueField, Title = titles.Count == 0 ? null : string.Join(", ", titles) });
        }

        return linked;
    }

    /// <summary>
    ///     Plans the implicit aggregate: the measures of every aggregating definition and the dimensions of every other field definition.
    /// </summary>
    /// <param name="channels">The channel plans.</param>
    /// <returns>The plan, or null when no definition aggregates.</returns>
    private static AggregatePlan? PlanAggregate(List<ChannelPlan> channels)
    {
        var measures = new List<AggregateSpec>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var arg = new List<ArgField>();
        foreach (var plan in channels.Where(c => c.Measure is not null))
        {
            var measure = plan.Measure!;
            if (names.Add(measure.Name))
            {
                measures.Add(measure);
                if (AggregateMeasures.IsArgOp(measure.Op))
                {
                    arg.Add(new ArgField(measure.Name, plan.SourceField!, plan.ValueField!));
                }
            }
        }

        if (measures.Count == 0)
        {
            return null;
        }

        var groupBy = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plan in channels.Where(c => c.Measure is null))
        {
            foreach (var path in DimensionFields(plan))
            {
                if (FieldPath.Canonicalize(path) is { } canonical && seen.Add(canonical))
                {
                    groupBy.Add(path);
                }
            }
        }

        return new AggregatePlan(groupBy, measures, arg);
    }

    /// <summary>
    ///     Lists the dimension fields a definition adds to the aggregate: the start, end and text of its bins, or its field.
    /// </summary>
    /// <param name="plan">The definition that does not aggregate.</param>
    /// <returns>The field paths.</returns>
    private static IEnumerable<string> DimensionFields(ChannelPlan plan)
    {
        if (plan.Bin == BinKind.Binning)
        {
            yield return EscapeName(plan.BinPlan!.StartName);
            yield return EscapeName(plan.BinPlan.EndName);
            if (plan.BinPlan.RangeName is { } range)
            {
                yield return EscapeName(range);
            }
        }
        else if (plan.TimeUnit is { } unit)
        {
            // The end of a unit follows from its start, so it adds no group; the aggregate keeps it for the marks that span the unit
            yield return EscapeName(unit.StartName);
            if (plan.HasBandEnd)
            {
                yield return EscapeName(unit.EndName);
            }
        }
        else if (plan.SourceField is { } field)
        {
            yield return field;
        }
    }

    /// <summary>
    ///     Plans the stack: the stacked field, the stacks and the order of the layers.
    /// </summary>
    /// <param name="decision">The stack decision of <see cref="StackRules"/>.</param>
    /// <param name="encoding">The encoding object.</param>
    /// <param name="channels">The channel plans.</param>
    /// <returns>The plan, or null when the stacked channel reads no field.</returns>
    private static StackPlan? PlanStack(StackDecision decision, SpecNode encoding, List<ChannelPlan> channels)
    {
        var stacked = channels.FirstOrDefault(c => c.Channel == decision.Channel && !c.Definition.Parent!.IsArray);
        if (stacked?.ValueField is not { } field)
        {
            return null;
        }

        // The literal base of the two output names: the derived name of a measure, else the field as written
        var baseName = stacked.Measure?.Name ?? stacked.SourceField!;
        var other = StackRules.DimensionChannel(decision.Channel);
        var groupBy = new List<string>();
        foreach (var plan in channels.Where(c => c.Channel == other || c.Channel == other + "2"))
        {
            if (plan.ValueField is null)
            {
                continue;
            }

            groupBy.Add(plan.ValueField);
            if (plan.Bin == BinKind.Binning)
            {
                groupBy.Add(EscapeName(plan.BinPlan!.EndName));
            }
            else if (plan.HasBandEnd)
            {
                groupBy.Add(EscapeName(plan.TimeUnit!.EndName));
            }
        }

        var sort = OrderSort(encoding, channels);
        if (sort.Count == 0 && OrderDefinitions(encoding).Count == 0)
        {
            // The layers are ordered by the stack-by fields, descending when the stacked channel is y (as Vega-Lite does) and ascending otherwise
            var descending = decision.Channel == "y";
            foreach (var definition in decision.StackBy)
            {
                if (channels.FirstOrDefault(c => c.Definition == definition)?.ValueField is { } stackByField)
                {
                    sort.Add(new SortField(stackByField, descending));
                }
            }
        }

        var offset = decision.Offset switch
        {
            "center" => StackOffset.Center,
            "normalize" => StackOffset.Normalize,
            _ => StackOffset.Zero,
        };
        return new StackPlan(decision.Channel, offset, field, baseName + "_start", baseName + "_end", groupBy, sort, stacked.Definition);
    }

    /// <summary>
    ///     Lists the definitions of the <c>order</c> channel.
    /// </summary>
    /// <param name="encoding">The encoding object.</param>
    /// <returns>The definitions in order; empty when the encoding has no <c>order</c>.</returns>
    private static List<SpecNode> OrderDefinitions(SpecNode encoding)
    {
        if (encoding["order"] is not { } order)
        {
            return [];
        }

        return [.. (order.IsArray ? order.Items : [order]).Where(i => i.IsObject)];
    }

    /// <summary>
    ///     Lists the fields of the <c>order</c> channel with their direction.
    /// </summary>
    /// <param name="encoding">The encoding object.</param>
    /// <param name="channels">The channel plans.</param>
    /// <returns>One entry for every definition of the channel that reads a field, in order (a definition without a field, such as <c>{"value": null}</c>, adds none); descending when the definition says so.</returns>
    private static List<SortField> OrderSort(SpecNode encoding, List<ChannelPlan> channels)
    {
        var sort = new List<SortField>();
        foreach (var definition in OrderDefinitions(encoding))
        {
            if (channels.FirstOrDefault(c => c.Definition == definition)?.ValueField is { } orderField)
            {
                sort.Add(new SortField(orderField, definition["sort"]?.AsString() == "descending"));
            }
        }

        return sort;
    }

    /// <summary>
    ///     Plans the order fields: the <c>order</c> channel with its directions.
    /// </summary>
    /// <param name="encoding">The encoding object.</param>
    /// <param name="channels">The channel plans.</param>
    /// <returns>The fields in order of significance; empty when the encoding has no <c>order</c> field.</returns>
    private static List<SortField> PlanOrderFields(SpecNode encoding, List<ChannelPlan> channels) => OrderSort(encoding, channels);

    /// <summary>
    ///     Lists the fields that split the rows of a path mark into paths (Vega-Lite's <c>pathGroupingFields</c>).
    /// </summary>
    /// <remarks>
    ///     <c>detail</c> and <c>key</c> always group; <c>order</c> groups every mark but a line and a trail (a stack of areas is ordered by it); <c>size</c> groups
    ///     every mark but a trail (a line's width is per path, a trail's is per point); <c>color</c>, <c>fill</c>, <c>stroke</c>, the opacities, <c>strokeDash</c> and
    ///     <c>strokeWidth</c> group. A definition that aggregates never groups, and every other channel (positions, polar channels, <c>text</c>, <c>shape</c>,
    ///     <c>angle</c>, <c>tooltip</c>, <c>href</c>, <c>url</c>, <c>description</c>) does not either. The fields are those of the table after the bin and the time unit
    ///     (a bin is its start field, or its range text on a discrete channel).
    /// </remarks>
    /// <param name="markType">The mark type.</param>
    /// <param name="channels">The channel plans.</param>
    /// <returns>The canonical field paths in channel order, without duplicates.</returns>
    internal static List<string> PlanPathGroupFields(string markType, IEnumerable<ChannelPlan> channels)
    {
        var fields = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var plan in channels)
        {
            var groups = plan.Channel switch
            {
                "order" => markType is not ("line" or "trail"),
                "detail" or "key" => true,
                "size" => markType != "trail",
                "color" or "fill" or "stroke" or "opacity" or "fillOpacity" or "strokeOpacity" or "strokeDash" or "strokeWidth" => true,
                _ => false,
            };
            if (groups && plan.Measure is null && FieldPath.Canonicalize(plan.ValueField) is { } canonical && seen.Add(canonical))
            {
                fields.Add(canonical);
            }
        }

        return fields;
    }

    /// <summary>
    ///     Plans the impute of the <c>impute</c> property of <c>x</c> or <c>y</c> (Vega-Lite's <c>ImputeNode.makeFromEncoding</c>).
    /// </summary>
    /// <param name="channels">The channel plans.</param>
    /// <param name="groupFields">The path grouping fields.</param>
    /// <returns>The plan, or null when neither channel has an <c>impute</c> object, or when one of the two is not a field definition (Vega-Lite needs both: the key is the other one).</returns>
    private static ImputePlan? PlanImpute(List<ChannelPlan> channels, List<string> groupFields)
    {
        var x = channels.FirstOrDefault(c => c.Channel == "x" && !c.Definition.Parent!.IsArray);
        var y = channels.FirstOrDefault(c => c.Channel == "y" && !c.Definition.Parent!.IsArray);
        if (x?.ValueField is null || y?.ValueField is null || !StackRules.IsFieldDef(x.Definition) || !StackRules.IsFieldDef(y.Definition))
        {
            return null;
        }

        // The x channel wins when both ask for an impute; the other channel is the key
        var imputed = x.Definition["impute"] is { IsObject: true } ? x : null;
        imputed ??= y.Definition["impute"] is { IsObject: true } ? y : null;
        if (imputed is null)
        {
            return null;
        }

        var keyPlan = ReferenceEquals(imputed, x) ? y : x;
        return new ImputePlan(imputed.ValueField!, keyPlan.ValueField!, groupFields, imputed.Definition);
    }

    /// <summary>
    ///     Plans the impute that precedes the stack of a line or area mark (Vega-Lite's <c>StackNode</c> with <c>impute</c>).
    /// </summary>
    /// <remarks>
    ///     The impute exists when the mark is a path mark (line, area or trail), the marks are stacked, the stacked definition does not say <c>impute: null</c>, the
    ///     dimension channel is an unbinned field and there is at least one stack-by field (without one there is a single layer, which has every key already).
    ///     Vega-Lite imputes a binned dimension through a computed mid-point field; this renderer does not, so a binned dimension is not imputed.
    /// </remarks>
    /// <param name="markType">The mark type.</param>
    /// <param name="decision">The stack decision, or null.</param>
    /// <param name="stack">The stack plan, or null.</param>
    /// <param name="channels">The channel plans.</param>
    /// <returns>The plan, or null when no impute is needed.</returns>
    private static StackImputePlan? PlanStackImpute(string markType, StackDecision? decision, StackPlan? stack, List<ChannelPlan> channels)
    {
        if (decision is null || stack is null || markType is not ("line" or "area" or "trail") || stack.Definition["impute"] is { IsNull: true })
        {
            return null;
        }

        var dimension = channels.FirstOrDefault(c => c.Channel == StackRules.DimensionChannel(decision.Channel) && !c.Definition.Parent!.IsArray);
        if (dimension?.ValueField is not { } key || !StackRules.IsFieldDef(dimension.Definition) || dimension.HasBandEnd || StackRules.IsEnabled(dimension.FieldDef["bin"]))
        {
            return null;
        }

        var groupBy = new List<string>();
        foreach (var definition in decision.StackBy)
        {
            if (channels.FirstOrDefault(c => c.Definition == definition)?.ValueField is { } field && FieldPath.Canonicalize(field) is { } canonical && !groupBy.Contains(canonical))
            {
                groupBy.Add(canonical);
            }
        }

        return groupBy.Count == 0 || FieldPath.Canonicalize(key) is null ? null : new StackImputePlan(stack.Field, key, groupBy, stack.Definition);
    }

    /// <summary>
    ///     Determines whether a channel definition sorts its categories by a field, an aggregate or another channel.
    /// </summary>
    /// <param name="definition">The channel definition.</param>
    /// <returns><see langword="true"/> for an object or a channel shorthand (not <c>ascending</c>, <c>descending</c>, null or an array).</returns>
    private static bool UsesFieldSort(SpecNode definition) =>
        definition["sort"] is { } sort && (sort.IsObject || (sort.IsString && sort.StringValue is not ("ascending" or "descending")));

    /// <summary>
    ///     Parses the <c>aggregate</c> of a definition.
    /// </summary>
    /// <param name="aggregate">The <c>aggregate</c> value: an operation name, or an object with <c>argmax</c>, <c>argmin</c>, <c>exponential</c> or <c>exponentialb</c>.</param>
    /// <param name="source">The field path of the definition, or null.</param>
    /// <returns>The measure, or null when the definition does not aggregate a field (an operation without a field is not a measure).</returns>
    private static MeasureInfo? ParseAggregate(SpecNode? aggregate, string? source)
    {
        if (aggregate is { IsString: true } && AggregateMeasures.TryParse(aggregate.StringValue, out var op))
        {
            if (op == AggregateOp.Count)
            {
                return new MeasureInfo(new AggregateSpec(op, null, AggregateSpec.DefaultExponentialParameter, CountName), aggregate.StringValue!, null, null);
            }

            return source is null
                ? null
                : new MeasureInfo(new AggregateSpec(op, source, AggregateSpec.DefaultExponentialParameter, aggregate.StringValue + "_" + source), aggregate.StringValue!, null, null);
        }

        if (aggregate is not { IsObject: true, Properties.Count: 1 } || source is null)
        {
            return null;
        }

        var member = aggregate.Properties[0];
        if (member.Name is "argmax" or "argmin" && member.IsString && FieldPath.Canonicalize(member.StringValue) is not null)
        {
            var argOp = member.Name == "argmax" ? AggregateOp.ArgMax : AggregateOp.ArgMin;
            var name = member.Name + "_" + member.StringValue;
            var output = FieldPath.Canonicalize(EscapeName(name) + "." + source);
            return output is null ? null : new MeasureInfo(new AggregateSpec(argOp, member.StringValue, AggregateSpec.DefaultExponentialParameter, name), member.Name, member.StringValue, output);
        }

        if (member.Name is "exponential" or "exponentialb" && member.IsNumber)
        {
            var exponentialOp = member.Name == "exponential" ? AggregateOp.Exponential : AggregateOp.ExponentialB;
            return new MeasureInfo(new AggregateSpec(exponentialOp, source, member.NumberValue, member.Name + "_" + source), member.Name, null, null);
        }

        return null;
    }

    /// <summary>
    ///     Builds the default title of a channel definition (Vega-Lite's <c>defaultTitle</c> for a field definition).
    /// </summary>
    /// <param name="source">The field path, or null.</param>
    /// <param name="aggregate">The aggregate, or null.</param>
    /// <param name="binning">Whether the field is binned by the encoding.</param>
    /// <param name="unit">The time unit of the field, or null.</param>
    /// <param name="config">The configuration (<c>fieldTitle</c>, <c>countTitle</c>).</param>
    /// <returns>The title.</returns>
    private static string? DefaultTitle(string? source, MeasureInfo? aggregate, bool binning, TimeUnitSpec? unit, ResolvedConfig config)
    {
        var mode = config.GetString("fieldTitle") ?? "verbal";
        if (aggregate is { Spec.Op: AggregateOp.Count })
        {
            return config.GetString("countTitle") ?? "Count of Records";
        }

        if (source is null)
        {
            return null;
        }

        if (unit is not null && aggregate is null && !binning)
        {
            return TimeUnitTitle(source, unit, mode);
        }

        if (aggregate is not null)
        {
            if (mode == "plain")
            {
                return source;
            }

            if (mode == "functional")
            {
                return aggregate.Text.ToUpperInvariant() + "(" + source + ")";
            }

            if (aggregate.ArgField is { } arg)
            {
                return source + " for " + (aggregate.Text == "argmax" ? "max " : "min ") + arg;
            }

            return TitleCase(aggregate.Text) + " of " + source;
        }

        if (binning)
        {
            return mode switch
            {
                "plain" => source,
                "functional" => "BIN(" + source + ")",
                _ => source + " (binned)",
            };
        }

        return source;
    }

    /// <summary>
    ///     Gets the default title of a field with a time unit (Vega-Lite's verbal, functional and plain formatters).
    /// </summary>
    /// <param name="source">The field path.</param>
    /// <param name="unit">The time unit.</param>
    /// <param name="mode">The <c>fieldTitle</c> mode.</param>
    /// <returns><c>date (year-month)</c> (verbal, the parts of the unit; the field alone for the <c>maxbins</c> form), <c>YEARMONTH(date)</c> (functional) or the field (plain).</returns>
    private static string TimeUnitTitle(string source, TimeUnitSpec unit, string mode)
    {
        if (mode == "plain")
        {
            return source;
        }

        if (mode == "functional")
        {
            return (unit.Utc ? "UTC" : string.Empty) + (unit.Unit ?? "timeunit").ToUpperInvariant() + "(" + source + ")";
        }

        return unit.Parts.Count == 0 ? source : source + " (" + string.Join('-', unit.Parts) + ")";
    }

    /// <summary>
    ///     Upper-cases the first character of an operation name.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with its first character in upper case.</returns>
    private static string TitleCase(string text) =>
        text.Length == 0 ? text : char.ToUpperInvariant(text[0]) + text[1..];

    /// <summary>
    ///     What the time units of an encoding need to know about their view.
    /// </summary>
    /// <param name="Encoding">The encoding object (a channel with a secondary channel has no band end).</param>
    /// <param name="MarkType">The mark type.</param>
    /// <param name="MarkDefinition">The mark definition object, or null when the mark is a name.</param>
    /// <param name="Units">The units found so far, by start name.</param>
    private sealed record TimeContext(SpecNode Encoding, string MarkType, SpecNode? MarkDefinition, Dictionary<string, TimeUnitBuilder> Units);

    /// <summary>
    ///     The time unit of a field while the plan collects the channels that share it.
    /// </summary>
    private sealed class TimeUnitBuilder
    {
        /// <summary>The literal name of the start field.</summary>
        private readonly string _start;

        /// <summary>The field of the dates.</summary>
        private readonly string _field;

        /// <summary>The unit.</summary>
        private readonly TimeUnitSpec _spec;

        /// <summary>The <c>timeUnit</c> value.</summary>
        private readonly SpecNode _node;

        /// <summary>
        ///     Initializes a builder.
        /// </summary>
        /// <param name="start">The literal name of the start field.</param>
        /// <param name="field">The field of the dates.</param>
        /// <param name="spec">The unit.</param>
        /// <param name="node">The <c>timeUnit</c> value.</param>
        public TimeUnitBuilder(string start, string field, TimeUnitSpec spec, SpecNode node)
        {
            _start = start;
            _field = field;
            _spec = spec;
            _node = node;
        }

        /// <summary>Gets or sets a value indicating whether a channel that shares the unit reads its end.</summary>
        public bool HasBandEnd { get; set; }

        /// <summary>
        ///     Builds the plan of the unit.
        /// </summary>
        /// <returns>The plan.</returns>
        public TimeUnitPlan Build() => new(_start, _start + "_end", _field, _spec, _node, HasBandEnd);
    }

    /// <summary>
    ///     The measure of a definition with the facts the plan needs about it.
    /// </summary>
    /// <param name="Spec">The measure.</param>
    /// <param name="Text">The operation as written (the name of the member of an object).</param>
    /// <param name="ArgField">The field that is compared by an <c>argmax</c> or <c>argmin</c>, or null.</param>
    /// <param name="ArgOutput">The canonical path of the scalar field that receives the value of an <c>argmax</c> or <c>argmin</c>, or null.</param>
    private sealed record MeasureInfo(AggregateSpec Spec, string Text, string? ArgField, string? ArgOutput);

    /// <summary>
    ///     The bin of a field while the plan collects the channels that share it.
    /// </summary>
    private sealed class BinBuilder
    {
        /// <summary>The literal name of the start field.</summary>
        private readonly string _start;

        /// <summary>The field that is binned.</summary>
        private readonly string _field;

        /// <summary>The parameters.</summary>
        private readonly BinSettings _settings;

        /// <summary>The <c>bin</c> value.</summary>
        private readonly SpecNode _node;

        /// <summary>
        ///     Initializes a builder.
        /// </summary>
        /// <param name="start">The literal name of the start field.</param>
        /// <param name="field">The field that is binned.</param>
        /// <param name="settings">The parameters.</param>
        /// <param name="node">The <c>bin</c> value.</param>
        public BinBuilder(string start, string field, BinSettings settings, SpecNode node)
        {
            _start = start;
            _field = field;
            _settings = settings;
            _node = node;
        }

        /// <summary>Gets or sets a value indicating whether a discrete channel shows the bin as text.</summary>
        public bool Discrete { get; set; }

        /// <summary>Gets or sets the number format of the boundaries in the text.</summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        ///     Builds the plan of the bin.
        /// </summary>
        /// <returns>The plan.</returns>
        public BinPlan Build() => new(_start, _start + "_end", Discrete ? _start + "_range" : null, _field, _settings, _node, Format);
    }
}
