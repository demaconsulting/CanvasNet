// Portions derived from Vega (BSD-3-Clause); see third-party/README.md
using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.Expressions;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;

namespace DemaConsulting.CanvasNet.VegaLite.Transforms;

/// <summary>
///     One operation of a <c>window</c> transform: a window-only operation or an aggregate operation over the frame, the field it
///     reads, its parameter and the name of the result.
/// </summary>
/// <param name="Window">The window-only operation, or null when the operation is an aggregate.</param>
/// <param name="Aggregate">The aggregate operation (read only when <paramref name="Window"/> is null).</param>
/// <param name="Field">The field path the operation reads, or null.</param>
/// <param name="Param">The parameter: the tile count of <c>ntile</c>, the offset of <c>lag</c>/<c>lead</c>, the position of <c>nth_value</c> or the weight of the exponential operations.</param>
/// <param name="Name">The name of the output field as written in the specification.</param>
internal sealed record WindowSpec(WindowOp? Window, AggregateOp Aggregate, string? Field, double Param, string Name)
{
    /// <summary>
    ///     Gets the name of the output field when the specification gives no <c>as</c>: the operation, then an underscore and the field.
    /// </summary>
    /// <param name="window">The window-only operation, or null.</param>
    /// <param name="aggregate">The aggregate operation.</param>
    /// <param name="field">The field path, or null.</param>
    /// <returns>The default name (<c>rank</c>, <c>count</c>, <c>sum_x</c>, <c>lag_x</c>, ...).</returns>
    public static string DefaultName(WindowOp? window, AggregateOp aggregate, string? field)
    {
        var op = window is { } w ? AggregateMeasures.GetName(w) : AggregateMeasures.GetName(aggregate);
        return field is null ? op : $"{op}_{field}";
    }
}

/// <content>
///     The <c>window</c> transform (a partial file of the aggregate transform unit).
/// </content>
internal sealed partial class AggregateTransforms
{
    /// <summary>The number of elements the work of recomputing one row of a frame is charged for each row of the frame (one step per 16 rows).</summary>
    private const int FrameRowCost = 4;

    /// <summary>
    ///     Reads the operations of a <c>window</c> transform.
    /// </summary>
    /// <param name="list">The array of <c>{op, field, param, as}</c> objects (validated by the parser).</param>
    /// <returns>The operations in the order of the specification.</returns>
    internal static List<WindowSpec> ReadWindowSpecs(SpecNode list)
    {
        var specs = new List<WindowSpec>(list.Items.Count);
        foreach (var item in list.Items)
        {
            var name = item["op"]?.AsString();
            var field = item["field"]?.AsString();
            var number = item["param"]?.AsNumber();
            if (AggregateMeasures.TryParseWindow(name, out var window))
            {
                specs.Add(new WindowSpec(window, default, field, number ?? 1, item["as"]?.AsString() ?? WindowSpec.DefaultName(window, default, field)));
            }
            else if (AggregateMeasures.TryParse(name, out var aggregate))
            {
                specs.Add(new WindowSpec(null, aggregate, field, number ?? AggregateSpec.DefaultExponentialParameter, item["as"]?.AsString() ?? WindowSpec.DefaultName(null, aggregate, field)));
            }
        }

        return specs;
    }

    /// <summary>
    ///     Adds the window operations to every row, for a <c>window</c> transform of the specification.
    /// </summary>
    /// <param name="input">The input table.</param>
    /// <param name="entry">The transform object.</param>
    /// <param name="operation">The array of operations.</param>
    /// <returns>The input rows, in their order, with the results of the operations added.</returns>
    private DataTable Window(DataTable input, SpecNode entry, SpecNode operation)
    {
        var location = Locate(entry);
        var groupBy = ReadGroupBy(entry);
        var sort = ReadSortFields(entry["sort"]);
        var ops = ReadWindowSpecs(operation);
        var (frameStart, frameEnd) = ReadFrame(entry["frame"]);
        var ignorePeers = entry["ignorePeers"] is { IsBoolean: true } flag && flag.BoolValue;
        try
        {
            CheckTexts("A field of the window", location, groupBy, sort.Select(s => s.Field), ops.Select(o => o.Field), ops.Select(o => o.Name));
            var layout = new TableLayout(input, [.. ops.Select(o => TableLayout.Canonical(o.Name))], keepInput: true, _context.Limits, location);
            var width = layout.Fields.Length;
            var cells = layout.Allocate(input.RowCount);
            for (var row = 0; row < input.RowCount; row++)
            {
                _context.Tick(row);
                _meter.Add(1);
                layout.CopyRow(input, row, cells, row);
            }

            Window(
                input,
                groupBy,
                sort,
                frameStart,
                frameEnd,
                ignorePeers,
                ops,
                RowSampler.Seed(entry.Pointer),
                location,
                (row, op, value) => cells[((long)row * width) + layout.Added[op]] = value);
            return layout.Build(cells, input.RowCount);
        }
        catch (ExpressionRuntimeException ex)
        {
            throw Located(ex, location);
        }
    }

    /// <summary>
    ///     Reads a <c>frame</c> value: two offsets, each an integer or null.
    /// </summary>
    /// <param name="frame">The array (validated by the parser), or null for the default.</param>
    /// <returns>The offsets of the first and the last row of the frame relative to the current row; null is unbounded.</returns>
    internal static (int? Start, int? End) ReadFrame(SpecNode? frame)
    {
        if (frame is not { IsArray: true, Items.Count: 2 })
        {
            return (null, 0);
        }

        int? Bound(SpecNode node) => node.AsNumber() is { } n ? (int)n : null;
        return (Bound(frame.Items[0]), Bound(frame.Items[1]));
    }

    /// <summary>
    ///     Computes window operations over the rows of a table and hands every result to a sink.
    /// </summary>
    /// <remarks>
    ///     This is the engine of the <c>window</c> transform; the <c>impute</c> transform calls it too. The sink receives each result
    ///     exactly once, for a row of the input table and an index into <paramref name="ops"/>.
    /// </remarks>
    /// <param name="input">The input table.</param>
    /// <param name="groupBy">The partition field paths (none: one partition).</param>
    /// <param name="sort">The sort fields (none: the input order).</param>
    /// <param name="frameStart">The offset of the first row of the frame from the current row; null is the start of the partition.</param>
    /// <param name="frameEnd">The offset of the last row of the frame from the current row; null is the end of the partition.</param>
    /// <param name="ignorePeers">Whether the frame is exactly the rows between the offsets even when it splits a group of peers.</param>
    /// <param name="ops">The operations.</param>
    /// <param name="seed">The seed of the bootstrap of <c>ci0</c> and <c>ci1</c>.</param>
    /// <param name="location">The location reported for a failure.</param>
    /// <param name="sink">Receives the row, the index of the operation and the result.</param>
    /// <exception cref="VegaLiteRenderException">Thrown, at <paramref name="location"/>, when a limit is exceeded.</exception>
    /// <exception cref="OperationCanceledException">Thrown when the token of the context is canceled.</exception>
    internal void Window(
        DataTable input,
        IReadOnlyList<string> groupBy,
        IReadOnlyList<SortField> sort,
        int? frameStart,
        int? frameEnd,
        bool ignorePeers,
        IReadOnlyList<WindowSpec> ops,
        ulong seed,
        VegaLiteLocation location,
        Action<int, int, DataValue> sink)
    {
        try
        {
            new WindowRun(this, input, groupBy, sort, frameStart, frameEnd, ignorePeers, ops, seed, location, sink).Run();
        }
        catch (ExpressionRuntimeException ex)
        {
            throw Located(ex, location);
        }
    }

    /// <summary>
    ///     One run of the window engine over one table.
    /// </summary>
    private sealed class WindowRun
    {
        /// <summary>The transform unit (the context, the meter and the grouping).</summary>
        private readonly AggregateTransforms _owner;

        /// <summary>The table.</summary>
        private readonly DataTable _input;

        /// <summary>The partition fields.</summary>
        private readonly IReadOnlyList<string> _groupBy;

        /// <summary>The sort fields.</summary>
        private readonly IReadOnlyList<SortField> _sort;

        /// <summary>The offset of the first frame row, or null.</summary>
        private readonly int? _frameStart;

        /// <summary>The offset of the last frame row, or null.</summary>
        private readonly int? _frameEnd;

        /// <summary>Whether the frame is widened to take in peers.</summary>
        private readonly bool _range;

        /// <summary>The operations.</summary>
        private readonly IReadOnlyList<WindowSpec> _ops;

        /// <summary>The bootstrap seed.</summary>
        private readonly ulong _seed;

        /// <summary>The location of the transform.</summary>
        private readonly VegaLiteLocation _location;

        /// <summary>Receives the results.</summary>
        private readonly Action<int, int, DataValue> _sink;

        /// <summary>The evaluation to charge.</summary>
        private readonly ExpressionFunctionContext _evaluation;

        /// <summary>The readers of the fields the operations read.</summary>
        private readonly List<FieldReader> _readers = [];

        /// <summary>For each operation the index of its reader, or -1.</summary>
        private readonly int[] _opReader;

        /// <summary>The indexes of the aggregate operations.</summary>
        private readonly int[] _aggregates;

        /// <summary>The reader of whole rows for <c>argmin</c> and <c>argmax</c>, or null.</summary>
        private readonly FieldReader? _whole;

        /// <summary>The sorted row numbers of the partition being processed (a slice of <see cref="_order"/>).</summary>
        private int[] _order = [];

        /// <summary>The start of the partition in <see cref="_order"/>.</summary>
        private int _base;

        /// <summary>The number of rows of the partition.</summary>
        private int _n;

        /// <summary>The first position of the peer group of every position.</summary>
        private int[] _peerStart = [];

        /// <summary>The position after the peer group of every position.</summary>
        private int[] _peerEnd = [];

        /// <summary>The 1-based number of the peer group of every position.</summary>
        private int[] _dense = [];

        /// <summary>For each operation the last non-null value seen in the partition (<c>prev_value</c>).</summary>
        private readonly DataValue[] _previous;

        /// <summary>For each operation the position of the next non-null value found (<c>next_value</c>).</summary>
        private readonly int[] _nextIndex;

        /// <summary>For each operation the next non-null value found.</summary>
        private readonly DataValue[] _nextValue;

        /// <summary>The number of elements of <c>values</c> results built so far.</summary>
        private long _materialized;

        /// <summary>The number of rows processed (for cancellation).</summary>
        private long _done;

        /// <summary>
        ///     Initializes a run.
        /// </summary>
        /// <param name="owner">The transform unit.</param>
        /// <param name="input">The table.</param>
        /// <param name="groupBy">The partition fields.</param>
        /// <param name="sort">The sort fields.</param>
        /// <param name="frameStart">The first frame offset.</param>
        /// <param name="frameEnd">The last frame offset.</param>
        /// <param name="ignorePeers">Whether peers are ignored.</param>
        /// <param name="ops">The operations.</param>
        /// <param name="seed">The bootstrap seed.</param>
        /// <param name="location">The location.</param>
        /// <param name="sink">Receives the results.</param>
        public WindowRun(
            AggregateTransforms owner,
            DataTable input,
            IReadOnlyList<string> groupBy,
            IReadOnlyList<SortField> sort,
            int? frameStart,
            int? frameEnd,
            bool ignorePeers,
            IReadOnlyList<WindowSpec> ops,
            ulong seed,
            VegaLiteLocation location,
            Action<int, int, DataValue> sink)
        {
            _owner = owner;
            _input = input;
            _groupBy = groupBy;
            _sort = sort;
            _frameStart = frameStart;
            _frameEnd = frameEnd;
            _range = sort.Count > 0 && !ignorePeers;
            _ops = ops;
            _seed = seed;
            _location = location;
            _sink = sink;
            _evaluation = owner._context.Evaluator.Context;
            var readerOf = new Dictionary<string, int>(StringComparer.Ordinal);
            _opReader = new int[ops.Count];
            _previous = new DataValue[ops.Count];
            _nextIndex = new int[ops.Count];
            _nextValue = new DataValue[ops.Count];
            for (var m = 0; m < ops.Count; m++)
            {
                var reader = -1;
                var needsField = ops[m].Window is { } w
                    ? AggregateMeasures.RequiresField(w)
                    : ops[m].Aggregate != AggregateOp.Count;
                if (needsField && ops[m].Field is { } field && !readerOf.TryGetValue(field, out reader))
                {
                    reader = _readers.Count;
                    readerOf[field] = reader;
                    _readers.Add(new FieldReader(input, field));
                }

                _opReader[m] = reader;
            }

            _aggregates = [.. Enumerable.Range(0, ops.Count).Where(m => ops[m].Window is null)];
            _whole = _aggregates.Any(m => AggregateMeasures.IsArgOp(ops[m].Aggregate)) ? new FieldReader(input, string.Empty) : null;
        }

        /// <summary>
        ///     Runs the window operations over every partition.
        /// </summary>
        /// <exception cref="VegaLiteRenderException">Thrown when a limit is exceeded.</exception>
        /// <exception cref="ExpressionRuntimeException">Thrown when the step budget is exhausted.</exception>
        public void Run()
        {
            var rows = _input.RowCount;
            if (rows == 0)
            {
                return;
            }

            var storing = _aggregates.Count(m => MeasureCell.StoresValues(_ops[m].Aggregate));
            if ((long)rows * storing > TableLayout.MaxCells)
            {
                throw new VegaLiteRenderException(
                    $"The window would keep up to {(long)rows * storing} values in memory, more than the limit of {TableLayout.MaxCells} values (TableLayout.MaxCells).",
                    _location);
            }

            // Partition the rows (the groups are not output rows), keeping the input order inside a partition
            var (groupOf, groups) = _owner.GroupRows(_input, _groupBy, 2 + _groupBy.Count, _location, reserveRows: false);
            int[] starts;
            int largest;
            (_order, starts, largest) = Partition(groupOf, groups.Count);

            Func<int, int, int>? compare = null;
            DataValue[]? keys = null;
            int[]? buffer = null;
            if (_sort.Count > 0)
            {
                keys = _owner.ReadSortKeys(_input, _sort, _location);
                compare = RowComparison(keys, _sort);
                buffer = new int[largest];
            }

            _peerStart = new int[largest];
            _peerEnd = new int[largest];
            _dense = new int[largest];
            for (var g = 0; g < groups.Count; g++)
            {
                _base = starts[g];
                _n = starts[g + 1] - starts[g];
                if (compare is not null)
                {
                    _owner.SortRows(_order, _base, _n, buffer!, compare);
                }

                FindPeers(compare);
                Process();
            }
        }

        /// <summary>
        ///     Finds the groups of peers (rows with equal sort values) of the sorted partition.
        /// </summary>
        /// <param name="compare">The comparison of two rows by the sort fields, or null when there is no sort (no row has a peer).</param>
        private void FindPeers(Func<int, int, int>? compare)
        {
            for (var p = 0; p < _n; p++)
            {
                if (p > 0 && compare is not null && compare(_order[_base + p - 1], _order[_base + p]) == 0)
                {
                    _peerStart[p] = _peerStart[p - 1];
                    _dense[p] = _dense[p - 1];
                }
                else
                {
                    _peerStart[p] = p;
                    _dense[p] = p == 0 ? 1 : _dense[p - 1] + 1;
                }
            }

            for (var p = _n - 1; p >= 0; p--)
            {
                _peerEnd[p] = p < _n - 1 && _peerStart[p + 1] == _peerStart[p] ? _peerEnd[p + 1] : p + 1;
            }
        }

        /// <summary>
        ///     Computes the operations for every row of the sorted partition.
        /// </summary>
        private void Process()
        {
            var context = _owner._context;
            var prefix = _frameStart is null;
            var cells = new MeasureCell[_aggregates.Length];
            var results = new DataValue[_aggregates.Length];
            var added = 0;
            int lastStart = -1, lastEnd = -1;
            Array.Clear(_previous);
            Array.Fill(_nextIndex, -1);
            Array.Clear(_nextValue);
            for (var p = 0; p < _n; p++)
            {
                context.Tick(_done++);
                _owner._meter.Add(_ops.Count + 1);
                var row = _order[_base + p];

                var i0 = _frameStart is { } s ? (int)Math.Clamp((long)p + s, 0, _n) : 0;
                var i1 = _frameEnd is { } e ? (int)Math.Clamp((long)p + e + 1, 0, _n) : _n;
                if (_range)
                {
                    // The frame takes in the peers of its first and its last row
                    var r0 = i0;
                    var r1 = i1 - 1;
                    if (r0 > 0 && r0 <= _n - 1)
                    {
                        i0 = _peerStart[r0];
                    }

                    if (r1 >= 0 && r1 < _n - 1)
                    {
                        i1 = _peerEnd[r1];
                    }
                }

                if (_aggregates.Length > 0 && (i0 != lastStart || i1 != lastEnd))
                {
                    if (prefix)
                    {
                        if (i1 < added)
                        {
                            Array.Clear(cells);
                            added = 0;
                        }

                        for (; added < i1; added++)
                        {
                            AddRow(cells, _order[_base + added]);
                        }
                    }
                    else
                    {
                        Array.Clear(cells);
                        var length = Math.Max(0, i1 - i0);
                        _owner._meter.Add((long)FrameRowCost * length * _aggregates.Length);
                        for (var q = i0; q < i1; q++)
                        {
                            AddRow(cells, _order[_base + q]);
                        }
                    }

                    Finish(cells, results, Math.Max(0, i1 - i0), row);
                    lastStart = i0;
                    lastEnd = i1;
                }

                for (var a = 0; a < _aggregates.Length; a++)
                {
                    _sink(row, _aggregates[a], results[a]);
                }

                for (var m = 0; m < _ops.Count; m++)
                {
                    if (_ops[m].Window is { } window)
                    {
                        _sink(row, m, WindowValue(window, m, p, i0, i1));
                    }
                }
            }
        }

        /// <summary>
        ///     Adds a row to the cells of the aggregate operations.
        /// </summary>
        /// <param name="cells">The cells, one per aggregate operation.</param>
        /// <param name="row">The row of the input table.</param>
        private void AddRow(MeasureCell[] cells, int row)
        {
            for (var a = 0; a < _aggregates.Length; a++)
            {
                var op = _ops[_aggregates[a]];
                var reader = _opReader[_aggregates[a]];
                cells[a].Add(op.Aggregate, op.Param, reader < 0 ? default : _readers[reader].Read(row, _evaluation), row, _owner._environment);
            }
        }

        /// <summary>
        ///     Takes the results of the aggregate operations over a frame.
        /// </summary>
        /// <param name="cells">The cells, one per aggregate operation.</param>
        /// <param name="results">Receives the results.</param>
        /// <param name="length">The number of rows of the frame.</param>
        /// <param name="row">The row the frame belongs to (seeds the bootstrap).</param>
        /// <exception cref="VegaLiteRenderException">Thrown when the arrays of <c>values</c> would exceed the cell limit.</exception>
        private void Finish(MeasureCell[] cells, DataValue[] results, int length, int row)
        {
            Func<int, DataValue>? rowOf = _whole is null ? null : r => _whole.Read(r, _evaluation);
            var seed = MeasureCell.GroupSeed(_seed, row);
            for (var a = 0; a < _aggregates.Length; a++)
            {
                var op = _ops[_aggregates[a]];
                if (op.Aggregate == AggregateOp.Values)
                {
                    _materialized += length;
                    if (_materialized > _owner._context.Limits.MaxDataRows)
                    {
                        throw new VegaLiteRenderException(
                            $"The window would build arrays of more than the limit of {_owner._context.Limits.MaxDataRows} values (MaxDataRows).",
                            _location);
                    }

                    _owner._meter.Add(length);
                }

                results[a] = cells[a].Result(op.Aggregate, op.Param, seed, _owner._environment, rowOf);
            }
        }

        /// <summary>
        ///     Reads a field of a row of the sorted partition.
        /// </summary>
        /// <param name="op">The index of the operation that reads the field.</param>
        /// <param name="position">The position in the sorted partition.</param>
        /// <returns>The value.</returns>
        private DataValue Value(int op, int position) => _readers[_opReader[op]].Read(_order[_base + position], _evaluation);

        /// <summary>
        ///     Computes a window-only operation for a row.
        /// </summary>
        /// <param name="window">The operation.</param>
        /// <param name="op">The index of the operation.</param>
        /// <param name="p">The position of the row in the sorted partition.</param>
        /// <param name="i0">The first position of the frame.</param>
        /// <param name="i1">The position after the frame.</param>
        /// <returns>The result.</returns>
        private DataValue WindowValue(WindowOp window, int op, int p, int i0, int i1)
        {
            var param = (long)_ops[op].Param;
            switch (window)
            {
                case WindowOp.RowNumber:
                    return DataValue.FromNumber(p + 1);
                case WindowOp.Rank:
                    return DataValue.FromNumber(_peerStart[p] + 1);
                case WindowOp.DenseRank:
                    return DataValue.FromNumber(_dense[p]);
                case WindowOp.PercentRank:
                    // One row in the partition divides zero by zero, which is NaN as in Vega
                    return DataValue.FromNumber(_peerStart[p] / (_n - 1.0));
                case WindowOp.CumeDist:
                    return DataValue.FromNumber(_peerEnd[p] / (double)_n);
                case WindowOp.Ntile:
                    return DataValue.FromNumber(Math.Ceiling(param * (_peerEnd[p] / (double)_n)));
                case WindowOp.Lag:
                    return p - param >= 0 ? Value(op, (int)(p - param)) : default;
                case WindowOp.Lead:
                    return p + param < _n ? Value(op, (int)(p + param)) : default;
                case WindowOp.FirstValue:
                    return i1 > i0 ? Value(op, i0) : default;
                case WindowOp.LastValue:
                    return i1 > i0 ? Value(op, i1 - 1) : default;
                case WindowOp.NthValue:
                    return i1 > i0 && i0 + (param - 1) < i1 ? Value(op, (int)(i0 + (param - 1))) : default;
                case WindowOp.PrevValue:
                    var current = Value(op, p);
                    if (!current.IsNull)
                    {
                        _previous[op] = current;
                    }

                    return _previous[op];
                default:
                    if (p > _nextIndex[op])
                    {
                        var j = p;
                        while (j < _n && Value(op, j).IsNull)
                        {
                            j++;
                        }

                        _nextIndex[op] = j < _n ? j : _n;
                        _nextValue[op] = j < _n ? Value(op, j) : default;
                    }

                    return _nextValue[op];
            }
        }
    }
}
