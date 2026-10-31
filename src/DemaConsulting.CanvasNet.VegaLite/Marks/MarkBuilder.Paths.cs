// Portions derived from d3-shape (ISC), d3-path (ISC), Vega (BSD-3-Clause) and Vega-Lite (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Scenegraph;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;

namespace DemaConsulting.CanvasNet.VegaLite.Marks;

/// <content>
///     The path marks of <see cref="MarkBuilder"/>: <c>line</c>, <c>area</c> and <c>trail</c>.
/// </content>
internal sealed partial class MarkBuilder
{
    /// <summary>The state of a row of a path mark: it is dropped (the <c>filter</c> mode), it breaks the path, or it is a vertex of the path.</summary>
    private enum RowState : byte
    {
        /// <summary>The row has no valid value for a continuous channel: it breaks the path.</summary>
        Undefined = 0,

        /// <summary>The row is a vertex of the path.</summary>
        Defined = 1,

        /// <summary>The row is not part of any path (<c>invalid: "filter"</c>).</summary>
        Dropped = 2,
    }

    /// <summary>
    ///     The values of the group fields of a row as a dictionary key.
    /// </summary>
    /// <remarks>
    ///     Values compare as <see cref="DataValue"/> does (an array or an object compares by identity, so the rows of a table that holds composite group values are their own groups).
    /// </remarks>
    private sealed class RowKey : IEquatable<RowKey>
    {
        /// <summary>The values.</summary>
        private readonly DataValue[] _values;

        /// <summary>The hash code, computed once.</summary>
        private readonly int _hash;

        /// <summary>
        ///     Initializes a key.
        /// </summary>
        /// <param name="values">The values of the group fields; ownership passes to the key.</param>
        public RowKey(DataValue[] values)
        {
            _values = values;
            var hash = new HashCode();
            foreach (var value in values)
            {
                hash.Add(value);
            }

            _hash = hash.ToHashCode();
        }

        /// <inheritdoc/>
        public bool Equals(RowKey? other) => other is not null && _values.AsSpan().SequenceEqual(other._values);

        /// <inheritdoc/>
        public override bool Equals(object? obj) => Equals(obj as RowKey);

        /// <inheritdoc/>
        public override int GetHashCode() => _hash;
    }

    /// <content>
    ///     The path marks of a run.
    /// </content>
    private sealed partial class Run
    {
        /// <summary>The size of a trail point that nothing else decides (the Vega style of the trail).</summary>
        private const double DefaultTrailSize = 2;

        /// <summary>
        ///     Reads the curve of a line or an area: the <c>interpolate</c> of the mark (linear by default).
        /// </summary>
        /// <remarks>
        ///     A name that Vega-Lite does not know (it can come from the configuration, which the parser does not check) is the linear curve, and so is <c>bundle</c> for an
        ///     area, which d3-shape cannot draw (the feature catalog reports <c>vl-mark-interpolate-bundle-area</c>). A trail has no curve.
        /// </remarks>
        /// <param name="areaHorizontal">Whether the mark is a horizontal area (<c>monotone</c> follows y then).</param>
        /// <returns>The curve.</returns>
        private CurveKind ResolveCurve(bool areaHorizontal)
        {
            if (_type == "trail")
            {
                return CurveKind.Linear;
            }

            var kind = CurveKind.Linear;
            if (Prop("interpolate")?.AsString() is { } name && CurveBuilder.TryParse(name, areaHorizontal, out var parsed))
            {
                kind = parsed;
            }

            return _type == "area" && !CurveBuilder.SupportsArea(kind) ? CurveKind.Linear : kind;
        }

        /// <summary>
        ///     Gets the paint of a path row: the paint of the mark, with the stroke width that the <c>size</c> of a line gives.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="widthDefault">The stroke width of the mark; null for the default of one pixel.</param>
        /// <param name="paint">Receives the paint.</param>
        /// <returns><see langword="false"/> when the row is invalid (a continuous channel, the size or an opacity has no number for it).</returns>
        private bool TryPathPaint(int row, double? widthDefault, out Paint paint)
        {
            var width = widthDefault;
            if (_type == "line")
            {
                // The size of a line is its stroke width
                if (Get("size") is { } sizeChannel)
                {
                    width = Encoded(sizeChannel, row, widthDefault);
                    if (width is null)
                    {
                        paint = default;
                        return false;
                    }
                }
                else if (ExpressionNumber(_sizeExpression, row) is { } computed)
                {
                    width = computed;
                }
            }

            return TryPaint(row, width, out paint);
        }

        /// <summary>
        ///     Gets the size of the trail at a row: its <c>size</c> channel, else the mark property, else the Vega style.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <param name="defaultSize">The size of the mark.</param>
        /// <returns>The diameter in pixels; null when the row has no valid size.</returns>
        private double? TrailSize(int row, double defaultSize) =>
            Get("size") is { } channel ? Encoded(channel, row, defaultSize) : ExpressionNumber(_sizeExpression, row) ?? defaultSize;

        /// <summary>
        ///     Reads the fields that group the rows into paths.
        /// </summary>
        /// <returns>The positions of the fields in the table (-1 for a field the table does not have, which reads as null).</returns>
        private int[] GroupIndexes()
        {
            if (_plan is null)
            {
                return [];
            }

            // An overlay line groups as a line does, whatever mark it overlays
            IReadOnlyList<string> fields = _overlay is not null ? EncodingPlan.PlanPathGroupFields(_type, _plan.Channels) : _plan.PathGroupFields;
            var indexes = new int[fields.Count];
            for (var i = 0; i < indexes.Length; i++)
            {
                indexes[i] = FieldIndexOf(fields[i]);
            }

            return indexes;
        }

        /// <summary>
        ///     Creates the comparison of the rows of a path: by the <c>order</c> fields, else by the position along the dimension, or none when the sort is off.
        /// </summary>
        /// <param name="horizontal">Whether the mark is horizontal (its dimension is y).</param>
        /// <param name="sortKey">The pixel position of every row along the dimension.</param>
        /// <returns>The comparison; null when the rows keep the order of the data.</returns>
        private Func<int, int, int>? PathComparison(bool horizontal, double[] sortKey)
        {
            if (IsOrderOff())
            {
                return null;
            }

            var fields = RowOrderFields();
            if (fields.Count > 0)
            {
                return OrderComparison(fields);
            }

            // Both a line and an area are sorted by their position along the dimension when it is a field
            if (Get(horizontal ? "y" : "x") is not { FieldIndex: >= 0 })
            {
                return null;
            }

            return (a, b) =>
            {
                // Like Vega's ascending comparison: an undefined position sorts first
                double left = sortKey[a], right = sortKey[b];
                var leftNaN = double.IsNaN(left);
                var rightNaN = double.IsNaN(right);
                if (leftNaN || rightNaN)
                {
                    if (leftNaN == rightNaN)
                    {
                        return 0;
                    }

                    return leftNaN ? -1 : 1;
                }

                return left.CompareTo(right);
            };
        }

        /// <summary>
        ///     Counts one scene budget charge before the items it pays for are created.
        /// </summary>
        /// <param name="items">The number of items.</param>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when the budget is exhausted.</exception>
        private void Reserve(long items) => _owner._budget.Add(items, _location);

        /// <summary>
        ///     Builds the paths of a line, area or trail mark: one item for every group of rows that has points.
        /// </summary>
        /// <remarks>
        ///     The rows are read once (their validity and their pixel position), grouped in one pass (a counting sort into one slice of a row array for every group, in order of first
        ///     appearance), sorted group by group and turned into one path each. The work is charged to the step budget before it is done and the segments of a path to the scene
        ///     budget, as an upper bound, before the list of segments exists.
        /// </remarks>
        /// <returns>The path items in the order of their groups.</returns>
        /// <exception cref="VegaLiteRenderException">Thrown, with the location of the mark, when a limit is exceeded.</exception>
        /// <exception cref="OperationCanceledException">Thrown when the token is canceled.</exception>
        private List<SceneItem> Paths()
        {
            var items = new List<SceneItem>();
            var count = _table.RowCount;
            var isArea = _type == "area";
            var isTrail = _type == "trail";
            var horizontal = _orient == "horizontal";
            var vertical = !horizontal;
            var x = Get("x");
            var y = Get("y");

            // An area is a range along its measure axis: without the measure channel there is nothing to span
            var measure = vertical ? y : x;
            if (count == 0 || (isArea && measure is null))
            {
                return items;
            }

            var kind = ResolveCurve(isArea && horizontal);
            var tension = Prop("tension")?.AsNumber();
            var filter = Prop("invalid")?.AsString() == "filter";
            var widthDefault = Prop("strokeWidth", vega: true)?.AsNumber();
            if (_type == "line")
            {
                widthDefault = Def("strokeWidth")?.AsNumber() ?? Def("size")?.AsNumber() ?? widthDefault;
            }
            else if (isTrail)
            {
                widthDefault = null;
            }

            var trailSize = isTrail ? Prop("size", vega: true)?.AsNumber() ?? DefaultTrailSize : 0;

            // Pass one: the validity and the pixel position of every row
            Charge(count);
            var along = new double[count];
            var top = new double[count];
            var bottom = isArea ? new double[count] : [];
            var sizes = isTrail ? new double[count] : [];
            var state = new RowState[count];
            for (var row = 0; row < count; row++)
            {
                Batch(row);

                // The positions are kept for every row that has them (the sort uses the position of a row whose other channel is invalid)
                double a, t, b = double.NaN, s = double.NaN;
                bool defined;
                if (isArea)
                {
                    a = PointPosition(vertical ? x : y, vertical ? _width : _height, row) ?? double.NaN;
                    var pair = RangePair(isX: horizontal, measure!, row);
                    t = pair?.Main ?? double.NaN;
                    b = pair?.Other ?? double.NaN;
                    defined = double.IsFinite(a) && double.IsFinite(t) && double.IsFinite(b);
                }
                else
                {
                    a = PointPosition(x, _width, row) ?? double.NaN;
                    t = PointPosition(y, _height, row) ?? double.NaN;
                    defined = double.IsFinite(a) && double.IsFinite(t);
                    if (defined && isTrail)
                    {
                        s = TrailSize(row, trailSize) ?? double.NaN;
                        defined = double.IsFinite(s);
                    }
                }

                defined = defined && TryPathPaint(row, widthDefault, out _);
                along[row] = a;
                top[row] = t;
                if (isArea)
                {
                    bottom[row] = b;
                }

                if (isTrail)
                {
                    sizes[row] = s;
                }

                if (defined)
                {
                    state[row] = RowState.Defined;
                }
                else
                {
                    state[row] = filter ? RowState.Dropped : RowState.Undefined;
                }
            }

            // Pass two: the group of every row, in order of first appearance (a counting sort keeps the work linear)
            var groupIndexes = GroupIndexes();
            Charge((long)count * (1 + groupIndexes.Length));
            var groupOf = new int[count];
            var groupCounts = new List<int>();
            var groupDefined = new List<int>();
            var keys = groupIndexes.Length == 0 ? null : new Dictionary<RowKey, int>();
            for (var row = 0; row < count; row++)
            {
                Batch(row);
                if (state[row] == RowState.Dropped)
                {
                    groupOf[row] = -1;
                    continue;
                }

                var group = 0;
                if (keys is null)
                {
                    if (groupCounts.Count == 0)
                    {
                        groupCounts.Add(0);
                        groupDefined.Add(0);
                    }
                }
                else
                {
                    var values = new DataValue[groupIndexes.Length];
                    for (var i = 0; i < values.Length; i++)
                    {
                        values[i] = groupIndexes[i] >= 0 ? _table.GetValue(row, groupIndexes[i]) : DataValue.Null;
                    }

                    var key = new RowKey(values);
                    if (!keys.TryGetValue(key, out group))
                    {
                        group = groupCounts.Count;
                        keys[key] = group;
                        groupCounts.Add(0);
                        groupDefined.Add(0);
                    }
                }

                groupOf[row] = group;
                groupCounts[group]++;
                if (state[row] == RowState.Defined)
                {
                    groupDefined[group]++;
                }
            }

            var starts = new int[groupCounts.Count + 1];
            var largest = 0;
            for (var g = 0; g < groupCounts.Count; g++)
            {
                starts[g + 1] = starts[g] + groupCounts[g];
                largest = Math.Max(largest, groupCounts[g]);
            }

            var rows = new int[starts[^1]];
            var fill = new int[groupCounts.Count];
            for (var row = 0; row < count; row++)
            {
                if (groupOf[row] is var g and >= 0)
                {
                    rows[starts[g] + fill[g]++] = row;
                }
            }

            var sortKey = isArea || vertical ? along : top;
            var compare = PathComparison(horizontal, sortKey);
            var buffer = compare is null ? [] : new int[largest];
            for (var g = 0; g < groupCounts.Count; g++)
            {
                _cancellationToken.ThrowIfCancellationRequested();

                // A group without two points draws nothing and costs nothing
                if (groupDefined[g] < 2)
                {
                    continue;
                }

                var start = starts[g];
                var length = groupCounts[g];
                if (compare is not null)
                {
                    Charge(SortCost(length));
                    StableSorter.Sort(rows, start, length, buffer, compare, _cancellationToken);
                }

                var item = isTrail
                    ? BuildTrail(rows, start, length, state, along, top, sizes, widthDefault)
                    : BuildCurve(rows, start, length, state, along, top, bottom, kind, tension, isArea, horizontal, widthDefault);
                if (item is not null)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        /// <summary>
        ///     Builds the path of a line or an area for one group of sorted rows.
        /// </summary>
        /// <param name="rows">The rows of all groups.</param>
        /// <param name="start">The position of the first row of the group in <paramref name="rows"/>.</param>
        /// <param name="length">The number of rows of the group.</param>
        /// <param name="state">The state of every row.</param>
        /// <param name="along">The position along the dimension of every row.</param>
        /// <param name="top">The position of the value of every row (the y of a line).</param>
        /// <param name="bottom">The position of the baseline of every row of an area.</param>
        /// <param name="kind">The curve.</param>
        /// <param name="tension">The <c>tension</c>, or null.</param>
        /// <param name="isArea">Whether the mark is an area.</param>
        /// <param name="horizontal">Whether the mark is horizontal.</param>
        /// <param name="widthDefault">The stroke width of the mark.</param>
        /// <returns>The item; null when the group has no point that can be drawn.</returns>
        private SceneItem? BuildCurve(int[] rows, int start, int length, RowState[] state, double[] along, double[] top, double[] bottom, CurveKind kind, double? tension, bool isArea, bool horizontal, double? widthDefault)
        {
            // The number of defined points and of runs decides the bound that is charged before anything is generated
            long defined = 0, runs = 0;
            var previous = false;
            var first = -1;
            for (var k = start; k < start + length; k++)
            {
                var isDefined = state[rows[k]] == RowState.Defined;
                if (isDefined)
                {
                    defined++;
                    first = first < 0 ? rows[k] : first;
                    runs += previous ? 0 : 1;
                }

                previous = isDefined;
            }

            if (defined < 2 || !TryPathPaint(first, widthDefault, out var paint))
            {
                return null;
            }

            var bound = CurveBuilder.MaxSegments(kind, defined, runs, isArea);
            Reserve(1 + bound);
            Charge(defined + bound);
            var segments = new List<ScenePathSegment>((int)Math.Min(bound, 4096));
            if (isArea)
            {
                var points = new AreaPoint[length];
                for (var k = 0; k < length; k++)
                {
                    var row = rows[start + k];
                    points[k] = new AreaPoint(along[row], top[row], bottom[row], state[row] == RowState.Defined);
                }

                CurveBuilder.Area(kind, tension, horizontal, points, segments, _cancellationToken);
            }
            else
            {
                var points = new CurvePoint[length];
                for (var k = 0; k < length; k++)
                {
                    var row = rows[start + k];
                    points[k] = new CurvePoint(along[row], top[row], state[row] == RowState.Defined);
                }

                CurveBuilder.Line(kind, tension, points, segments, _cancellationToken);
            }

            return Style(new ScenePath("mark-" + _type, segments), paint);
        }

        /// <summary>
        ///     Builds the trail of one group of sorted rows: one run of points for every stretch of defined rows that has two points or more.
        /// </summary>
        /// <param name="rows">The rows of all groups.</param>
        /// <param name="start">The position of the first row of the group in <paramref name="rows"/>.</param>
        /// <param name="length">The number of rows of the group.</param>
        /// <param name="state">The state of every row.</param>
        /// <param name="x">The x of every row.</param>
        /// <param name="y">The y of every row.</param>
        /// <param name="sizes">The size of every row.</param>
        /// <param name="widthDefault">The stroke width of the mark.</param>
        /// <returns>The item; null when no run has two points.</returns>
        private SceneItem? BuildTrail(int[] rows, int start, int length, RowState[] state, double[] x, double[] y, double[] sizes, double? widthDefault)
        {
            // Runs of one point draw nothing; the others are charged (one item and one point each) before they are copied
            long kept = 0;
            long run = 0;
            var first = -1;
            for (var k = start; k <= start + length; k++)
            {
                var isDefined = k < start + length && state[rows[k]] == RowState.Defined;
                if (isDefined)
                {
                    run++;
                    first = first < 0 ? rows[k] : first;
                    continue;
                }

                kept += run >= 2 ? run : 0;
                run = 0;
            }

            if (kept == 0 || !TryPathPaint(first, widthDefault, out var paint))
            {
                return null;
            }

            Reserve(1 + kept);
            Charge(kept);
            var runs = new List<IReadOnlyList<SceneTrailPoint>>();
            var current = new List<SceneTrailPoint>();
            for (var k = start; k <= start + length; k++)
            {
                if (k < start + length && state[rows[k]] == RowState.Defined)
                {
                    var row = rows[k];
                    current.Add(new SceneTrailPoint(x[row], y[row], sizes[row]));
                    continue;
                }

                if (current.Count >= 2)
                {
                    runs.Add(current);
                }

                current = [];
            }

            return Style(new SceneTrail("mark-trail", runs), paint);
        }
    }
}
