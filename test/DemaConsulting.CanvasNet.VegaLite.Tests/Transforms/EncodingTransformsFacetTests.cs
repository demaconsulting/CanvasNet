using DemaConsulting.CanvasNet.VegaLite.Data;
using DemaConsulting.CanvasNet.VegaLite.SpecModel;
using DemaConsulting.CanvasNet.VegaLite.Transforms;
using static DemaConsulting.CanvasNet.VegaLite.Tests.Transforms.AggregateTestSupport;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Transforms;

/// <summary>
///     Unit tests for the facet part of <see cref="EncodingTransforms"/>: the ordered values of the facet fields (<c>FacetDomain</c>), the grid of cells (cross product or wrap),
///     the partition of the rows into one table per cell, and the checks on the number of cells, the row budget and the cancellation. The expected values are derived by hand from the
///     rows below, and the order rules (ascending with a null first, <c>sort: null</c> still ascending, an array first and then the values it does not list) were observed on
///     Vega-Lite 6 / Vega 6 once and are written here as numbers.
/// </summary>
public class EncodingTransformsFacetTests
{
    /// <summary>
    ///     The rows of most tests; the values of <c>c</c> are b, a, null, z, a, d (first appearance: b, a, null, z, d) and of <c>r</c> y, x, y, x, y, x.
    /// </summary>
    private const string Data =
        """[{"c":"b","r":"y","v":1},{"c":"a","r":"x","v":2},{"c":null,"r":"y","v":3},{"c":"z","r":"x","v":4},{"c":"a","r":"y","v":5},{"c":"d","r":"x","v":6}]""";

    /// <summary>The wrapped facet over <c>c</c>.</summary>
    private const string Wrap = """{"field":"c","type":"nominal"}""";

    /// <summary>The <c>column</c> facet over <c>c</c>.</summary>
    private const string Column = """{"column":{"field":"c","type":"nominal"}}""";

    /// <summary>The <c>row</c> and <c>column</c> facet over <c>r</c> and <c>c</c>.</summary>
    private const string RowColumn = """{"row":{"field":"r","type":"nominal"},"column":{"field":"c","type":"nominal"}}""";

    /// <summary>
    ///     Everything a test needs to run a facet.
    /// </summary>
    /// <param name="Facet">The facet definition.</param>
    /// <param name="Table">The rows of the facet.</param>
    /// <param name="Context">The context of the transform run.</param>
    /// <param name="Loader">The loader (its row budget).</param>
    /// <param name="Tree">The view tree.</param>
    /// <param name="Node">The facet node.</param>
    private sealed record Setup(FacetDefinition Facet, DataTable Table, TransformContext Context, DataLoader Loader, LayerTree Tree, LayerNode Node);

    /// <summary>
    ///     Builds a facet specification whose body is one point view.
    /// </summary>
    /// <param name="facet">The JSON of the <c>facet</c>.</param>
    /// <param name="rows">The JSON of the rows.</param>
    /// <returns>The specification text.</returns>
    private static string Spec(string facet, string rows) =>
        "{\"data\":{\"values\":" + rows + "},\"facet\":" + facet + ",\"spec\":{\"mark\":\"point\",\"encoding\":{\"x\":{\"field\":\"v\",\"type\":\"quantitative\"}}}}";

    /// <summary>
    ///     Parses a facet specification and prepares the run.
    /// </summary>
    /// <param name="facet">The JSON of the <c>facet</c>.</param>
    /// <param name="rows">The JSON of the rows.</param>
    /// <param name="limits">The limits, or null.</param>
    /// <param name="token">The token of the run, or null.</param>
    /// <returns>The setup.</returns>
    private static Setup Prepare(string facet, string rows = Data, VegaLiteLimits? limits = null, CancellationToken? token = null)
    {
        var (table, context, loader, tree) = AggregateTestSupport.Prepare(Spec(facet, rows), limits, token);
        var layers = SpecNormalizer.Layers(tree, limits);
        return new Setup(layers.Root.Facet!, table, context, loader, layers, layers.Root);
    }

    /// <summary>
    ///     Splits the rows of a facet into cells with the number of views of its tree.
    /// </summary>
    /// <param name="setup">The setup.</param>
    /// <returns>The partition.</returns>
    private static FacetPartition Split(Setup setup)
    {
        var (perCell, outside) = setup.Tree.FacetViews(setup.Node);
        return EncodingTransforms.Facet(setup.Facet, setup.Table, perCell, outside, setup.Context);
    }

    /// <summary>
    ///     Creates a string value.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The value.</returns>
    private static DataValue S(string text) => DataValue.FromString(text);

    /// <summary>
    ///     Validates that the distinct values are ordered ascending by default (a null first, numbers by value rather than by text), in the order the cells are laid out,
    ///     and that a value that repeats is one cell.
    /// </summary>
    [Fact]
    public void EncodingTransforms_FacetDomain_DistinctValuesAreSortedAscendingByDefault()
    {
        // Arrange: b, a, null, z, a, d are five distinct values; the numbers 10, 9, 100, 9 are three
        var text = Prepare(Column);
        var numbers = Prepare(Column, """[{"c":10},{"c":9},{"c":100},{"c":9}]""");

        // Act
        var byText = EncodingTransforms.FacetDomain(text.Facet, text.Table, text.Context);
        var byNumber = EncodingTransforms.FacetDomain(numbers.Facet, numbers.Table, numbers.Context);

        // Assert: null first, then a, b, d, z; numbers 9, 10, 100 (not the text order 10, 100, 9)
        Assert.Equal([default, S("a"), S("b"), S("d"), S("z")], byText.Columns);
        Assert.Empty(byText.Rows);
        Assert.Empty(byText.Wrap);
        Assert.Equal([DataValue.FromNumber(9), DataValue.FromNumber(10), DataValue.FromNumber(100)], byNumber.Columns);
    }

    /// <summary>
    ///     Validates the sorts of a facet field: <c>descending</c> reverses the order (a null last), <c>null</c> is still ascending (unlike a scale), and an array puts the
    ///     listed values first in the order of the array and then the values it does not list in the order they first appear; no value is dropped.
    /// </summary>
    [Fact]
    public void EncodingTransforms_FacetDomain_DescendingNullAndArraySort()
    {
        // Arrange
        static Setup With(string sort) => Prepare("{\"column\":{\"field\":\"c\",\"type\":\"nominal\",\"sort\":" + sort + "}}");
        var (descending, none, listed) = (With("\"descending\""), With("null"), With("[\"z\",\"a\",\"q\"]"));

        // Act
        var byDescending = EncodingTransforms.FacetDomain(descending.Facet, descending.Table, descending.Context).Columns;
        var byNull = EncodingTransforms.FacetDomain(none.Facet, none.Table, none.Context).Columns;
        var byArray = EncodingTransforms.FacetDomain(listed.Facet, listed.Table, listed.Context).Columns;

        // Assert: z, d, b, a, then the null last
        Assert.Equal([S("z"), S("d"), S("b"), S("a"), default], byDescending);

        // Assert: null is not "data order"; it is ascending
        Assert.Equal([default, S("a"), S("b"), S("d"), S("z")], byNull);

        // Assert: z and a as listed ('q' is not in the data); b, null and d are not listed and follow in the order they appear in the data (b, a, null, z, a, d)
        Assert.Equal([S("z"), S("a"), S("b"), default, S("d")], byArray);
    }

    /// <summary>
    ///     Validates that a sort by an aggregate (<c>{field, op, order}</c>) orders the cells by the operation over the rows of each value (Vega-Lite's facet sort): the mean of <c>v</c> by <c>c</c>
    ///     is b 1, a 3.5 (2 and 5), null 3, z 4 and d 6, so the descending order is d, z, a, null, b; the sum is b 1, a 7, null 3, z 4, d 6; the default operation is <c>min</c>, and equal results
    ///     keep the order in which the values first appear in the data (a stable sort), also when descending.
    /// </summary>
    [Fact]
    public void EncodingTransforms_FacetDomain_SortByAggregateOrdersTheCells()
    {
        // Arrange
        static Setup With(string sort) => Prepare("{\"column\":{\"field\":\"c\",\"type\":\"nominal\",\"sort\":" + sort + "}}");
        var (mean, sum, min, count) = (
            With("""{"field":"v","op":"mean","order":"descending"}"""),
            With("""{"field":"v","op":"sum"}"""),
            With("""{"field":"v"}"""),
            With("""{"op":"count","order":"descending"}"""));

        // Act
        var byMean = EncodingTransforms.FacetDomain(mean.Facet, mean.Table, mean.Context).Columns;
        var bySum = EncodingTransforms.FacetDomain(sum.Facet, sum.Table, sum.Context).Columns;
        var byMin = EncodingTransforms.FacetDomain(min.Facet, min.Table, min.Context).Columns;
        var byCount = EncodingTransforms.FacetDomain(count.Facet, count.Table, count.Context).Columns;

        // Assert
        Assert.Equal([S("d"), S("z"), S("a"), default, S("b")], byMean);
        Assert.Equal([S("b"), default, S("z"), S("d"), S("a")], bySum);

        // Assert: the minimum of v by c is b 1, a 2, null 3, z 4, d 6, which is the first appearance order
        Assert.Equal([S("b"), S("a"), default, S("z"), S("d")], byMin);

        // Assert: a has two rows, the others one: a first, then the ties in the order they appear (b, null, z, d)
        Assert.Equal([S("a"), S("b"), default, S("z"), S("d")], byCount);
    }

    /// <summary>
    ///     Validates that the cells of a facet that is sorted by an aggregate are laid out in that order, with their rows, and that a sort object without a field or the operation
    ///     <c>count</c> (which Vega-Lite does not treat as a sort field) keeps the ascending order of the field.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_SortByAggregateOrdersTheCellsOfThePartition()
    {
        // Arrange
        var sorted = Prepare("""{"field":"c","type":"nominal","sort":{"field":"v","op":"max","order":"descending"}}""");
        var plain = Prepare("""{"field":"c","type":"nominal","sort":{"order":"descending"}}""");

        // Act
        var partition = Split(sorted);
        var unchanged = Split(plain);

        // Assert: the maximum of v by c is b 1, a 5, null 3, z 4, d 6: d, a, z, null, b
        Assert.Equal([S("d"), S("a"), S("z"), default, S("b")], partition.Domains.Wrap);
        Assert.Equal([[6.0], [2.0, 5.0], [4.0], [3.0], [1.0]], partition.Cells.Select(c => Numbers(c.Table, "v")));

        // Assert: without a field and an operation the sort is not an aggregate; the ascending order applies
        Assert.Equal([default, S("a"), S("b"), S("d"), S("z")], unchanged.Domains.Wrap);
    }

    /// <summary>
    ///     Validates that the <c>timeUnit</c> of a facet field makes the start of the unit the value of the cell (Vega-Lite's <c>TimeUnitNode</c> of a facet field): the rows of January 15 and 30
    ///     are one cell, February 20 another, the cells are dates in ascending order, and every cell keeps the fields of the table (a field that the unit derived is added).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_TimeUnitOfTheFieldIsTheCell()
    {
        // Arrange: 2024-01-15, 2024-02-20 and 2024-01-30 as milliseconds since the epoch
        const string rows = """[{"t":1705276800000,"v":1},{"t":1708387200000,"v":2},{"t":1706572800000,"v":3}]""";
        var setup = Prepare("""{"field":"t","type":"temporal","timeUnit":"yearmonth"}""", rows);

        // Act
        var partition = Split(setup);
        var domain = EncodingTransforms.FacetDomain(setup.Facet, setup.Table, setup.Context);

        // Assert: January 1 and February 1 of 2024 (UTC), as dates; the rows of January are v = 1 and 3
        var january = DataValue.FromDate(1704067200000);
        var february = DataValue.FromDate(1706745600000);
        Assert.Equal([january, february], partition.Domains.Wrap);
        Assert.Equal([january, february], domain.Wrap);
        Assert.Equal([[1.0, 3.0], [2.0]], partition.Cells.Select(c => Numbers(c.Table, "v")));
        Assert.Contains("yearmonth_t", partition.Cells[0].Table.Fields);
    }

    /// <summary>
    ///     Validates that a binned time unit of a facet field is not transformed (Vega-Lite's <c>TimeUnitNode.makeFromEncoding</c> adds nothing for a binned unit of a facet model): the raw value
    ///     of the field is the cell and the table gets no field.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_BinnedTimeUnitIsTheRawValue()
    {
        // Arrange
        const string rows = """[{"t":1705276800000,"v":1},{"t":1708387200000,"v":2},{"t":1705276800000,"v":3}]""";
        var binned = Prepare("""{"field":"t","type":"temporal","timeUnit":{"unit":"month","binned":true}}""", rows);

        // Act
        var partition = Split(binned);

        // Assert: the two distinct raw values, in the table of the cell as they were, and no field was added
        Assert.Equal([DataValue.FromNumber(1705276800000), DataValue.FromNumber(1708387200000)], partition.Domains.Wrap);
        Assert.Equal(binned.Table.Fields, partition.Cells[0].Table.Fields);
    }

    /// <summary>
    ///     Validates that the maxbins form of a facet field is applied (Vega-Lite's <c>TimeUnitNode</c> with <c>maxbins</c>): the extent of the two dates, 2024-01-15 and 2024-02-20, with
    ///     three bins gives Vega's <c>timeBin</c> the unit <c>yearweek</c> (computed with vega-time 2.x: the weeks starting Sunday 2024-01-14 and Sunday 2024-02-18), and the field
    ///     <c>timeunit_maxbins_3_t</c> is derived.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_MaxBinsTimeUnitIsResolvedAgainstTheData()
    {
        // Arrange
        const string rows = """[{"t":1705276800000,"v":1},{"t":1708387200000,"v":2},{"t":1705276800000,"v":3}]""";
        var maxBins = Prepare("""{"field":"t","type":"temporal","timeUnit":{"maxbins":3}}""", rows);

        // Act
        var partition = Split(maxBins);

        // Assert
        Assert.Equal([DataValue.FromDate(1705190400000), DataValue.FromDate(1708214400000)], partition.Domains.Wrap);
        Assert.Equal([[1.0, 3.0], [2.0]], partition.Cells.Select(c => Numbers(c.Table, "v")));
        Assert.Contains("timeunit_maxbins_3_t", partition.Cells[0].Table.Fields);
    }

    /// <summary>
    ///     Validates the <c>bin</c> of a facet field (Vega-Lite's <c>BinNode</c> of a facet model, 6 bins for a <c>column</c> by default): the cell is the start of the bin, the cells are in ascending order of the
    ///     start, the text of the cell is <c>start – end</c> (with the <c>format</c> of the header when the configuration gives one), a row without a number is the cell <c>null</c>, and the bin fields are added.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_BinOfTheFieldIsTheCellWithItsRangeAsText()
    {
        // Arrange: bins of 10 (maxbins 3 over 1..29), a text that is no number and a missing value
        const string rows = """[{"c":25,"v":1},{"c":2,"v":2},{"c":12,"v":3},{"c":"x","v":4},{"c":3,"v":5},{"v":6}]""";
        var setup = Prepare("""{"column":{"field":"c","type":"quantitative","bin":{"maxbins":3}}}""", rows);

        // Act
        var partition = Split(setup);
        var domain = EncodingTransforms.FacetDomain(setup.Facet, setup.Table, setup.Context);

        // Assert: the null cell first (a null sorts first), then 0 - 10, 10 - 20 and 20 - 30
        Assert.Equal([S("null"), S("0 \u2013 10"), S("10 \u2013 20"), S("20 \u2013 30")], partition.Domains.Columns);
        Assert.Equal(partition.Domains.Columns, domain.Columns);
        Assert.Equal([[4.0, 6.0], [2.0, 5.0], [3.0], [1.0]], partition.Cells.Select(c => Numbers(c.Table, "v")));
        Assert.Contains("bin_maxbins_3_c", partition.Cells[1].Table.Fields);
        Assert.Contains("bin_maxbins_3_c_end", partition.Cells[1].Table.Fields);
    }

    /// <summary>
    ///     Validates the default number of bins of a facet field (<c>autoMaxBins</c>: 6 for <c>row</c> and <c>column</c>, 10 for the wrapped <c>facet</c>) and the order of bins, which is numeric
    ///     (the bin <c>2 – 3</c> comes before the bin <c>10 – 11</c>, not after it as text).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_BinNamesFollowAutoMaxBinsAndOrderIsNumeric()
    {
        // Arrange: values 0 to 20 in steps of 1
        var rows = "[" + string.Join(",", Enumerable.Range(0, 21).Select(i => $"{{\"c\":{i},\"v\":{i}}}")) + "]";
        var column = Prepare("""{"column":{"field":"c","type":"quantitative","bin":true}}""", rows);
        var wrap = Prepare("""{"field":"c","type":"quantitative","bin":true}""", rows);

        // Act
        var byColumn = Split(column);
        var byWrap = Split(wrap);

        // Assert: four bins of 5 (0..20) for the column, ten bins of 2 (0..20) for the wrapped facet, in numeric order (the maximum belongs to the last bin)
        Assert.Equal(4, byColumn.Domains.Columns.Count);
        Assert.Equal(S("0 \u2013 5"), byColumn.Domains.Columns[0]);
        Assert.Equal(S("15 \u2013 20"), byColumn.Domains.Columns[3]);
        Assert.Contains("bin_maxbins_6_c", byColumn.Cells[0].Table.Fields);
        Assert.Equal(10, byWrap.Domains.Wrap.Count);
        Assert.Equal(S("2 \u2013 4"), byWrap.Domains.Wrap[1]);
        Assert.Equal(S("10 \u2013 12"), byWrap.Domains.Wrap[5]);
        Assert.Contains("bin_maxbins_10_c", byWrap.Cells[0].Table.Fields);
    }

    /// <summary>
    ///     Validates that the <c>aggregate</c> of a facet field is not computed (Vega-Lite's <c>AggregateNode.makeFromEncoding</c> takes a unit model only): the cell reads the field of the name that
    ///     <c>vgField</c> gives (<c>sum_v</c>), which a table that an aggregate transform wrote has, and a table that has no such field is the one null cell.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_AggregateOfTheFieldReadsTheFieldOfItsName()
    {
        // Arrange
        const string rows = """[{"c":"a","sum_v":10,"v":1},{"c":"b","sum_v":20,"v":2},{"c":"a","sum_v":10,"v":3}]""";
        var named = Prepare("""{"field":"v","type":"quantitative","aggregate":"sum"}""", rows);
        var missing = Prepare("""{"field":"c","type":"nominal","aggregate":"max"}""", rows);
        var counted = Prepare("""{"field":"c","type":"nominal","aggregate":"count"}""", """[{"c":"a","__count":2},{"c":"b","__count":1}]""");

        // Act
        var byName = Split(named);
        var byMissing = Split(missing);
        var byCount = Split(counted);

        // Assert
        Assert.Equal([DataValue.FromNumber(10), DataValue.FromNumber(20)], byName.Domains.Wrap);
        Assert.Equal([default], byMissing.Domains.Wrap);
        Assert.Equal([DataValue.FromNumber(1), DataValue.FromNumber(2)], byCount.Domains.Wrap);
    }

    /// <summary>
    ///     Validates that a null value and a missing value form a cell of their own, the first one in ascending order, and that a field the table does not have is one null cell.
    /// </summary>
    [Fact]
    public void EncodingTransforms_FacetDomain_NullValuesFormTheirOwnFirstCell()
    {
        // Arrange: the row {"v":2} has no c at all; the rows with null and the missing field are the same null value
        var present = Prepare(Wrap, """[{"c":"b","v":1},{"c":null,"v":2},{"v":3}]""");
        var absent = Prepare("""{"field":"nope","type":"nominal"}""");

        // Act
        var withNulls = EncodingTransforms.Facet(present.Facet, present.Table, 1, 1, present.Context);
        var withoutField = EncodingTransforms.Facet(absent.Facet, absent.Table, 1, 1, absent.Context);

        // Assert: cell 0 is the null cell with the rows v = 2 and 3, then b with v = 1
        Assert.Equal([default, S("b")], withNulls.Domains.Wrap);
        Assert.Equal([2.0, 3.0], Numbers(withNulls.Cells[0].Table, "v"));
        Assert.Equal([1.0], Numbers(withNulls.Cells[1].Table, "v"));

        // Assert: a field that no row has is one cell of nulls with every row
        var only = Assert.Single(withoutField.Cells);
        Assert.Equal(default, only.Cell.WrapValue);
        Assert.Equal(6, only.Table.RowCount);
    }

    /// <summary>
    ///     Validates the grid of a <c>row</c> and <c>column</c> facet: every combination of a row value (x, y) and a column value (null, a, b, d, z) is a cell, row-major, including the
    ///     combinations the data has no row for, which have an empty table with the same fields.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_CrossProductIncludesEmptyCombinations()
    {
        // Arrange
        var setup = Prepare(RowColumn);

        // Act
        var partition = Split(setup);

        // Assert: 2 rows x 5 columns, row-major: row x = cells 0 to 4, row y = cells 5 to 9
        Assert.Equal((2, 5), (partition.RowCount, partition.ColumnCount));
        Assert.Equal([S("x"), S("y")], partition.Domains.Rows);
        Assert.Equal([default, S("a"), S("b"), S("d"), S("z")], partition.Domains.Columns);
        Assert.Equal(Enumerable.Range(0, 10), partition.Cells.Select(c => c.Cell.Index));
        Assert.Equal(new[] { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 }, partition.Cells.Select(c => c.Cell.RowIndex));
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 0, 1, 2, 3, 4 }, partition.Cells.Select(c => c.Cell.ColumnIndex));

        // Assert: the rows of each cell: (x, a) = v 2, (x, d) = 6, (x, z) = 4, (y, null) = 3, (y, a) = 5, (y, b) = 1; the others have none
        Assert.Equal(new[] { 0, 1, 0, 1, 1, 1, 1, 1, 0, 0 }, partition.Cells.Select(c => c.Table.RowCount));
        Assert.Equal([2.0], Numbers(partition.Cells[1].Table, "v"));
        Assert.Equal([6.0], Numbers(partition.Cells[3].Table, "v"));
        Assert.Equal([4.0], Numbers(partition.Cells[4].Table, "v"));
        Assert.Equal([3.0], Numbers(partition.Cells[5].Table, "v"));
        Assert.Equal([5.0], Numbers(partition.Cells[6].Table, "v"));
        Assert.Equal([1.0], Numbers(partition.Cells[7].Table, "v"));

        // Assert: the cell knows its values
        var yb = partition.Cells[7].Cell;
        Assert.Equal((S("y"), S("b"), default), (yb.RowValue, yb.ColumnValue, yb.WrapValue));
    }

    /// <summary>
    ///     Validates that a facet with only a <c>row</c> field has one column, and one with only a <c>column</c> field has one row.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_RowOnlyHasOneColumnAndColumnOnlyOneRow()
    {
        // Arrange
        var rowOnly = Prepare("""{"row":{"field":"r","type":"nominal"}}""");
        var columnOnly = Prepare(Column);

        // Act
        var byRow = Split(rowOnly);
        var byColumn = Split(columnOnly);

        // Assert: r has x (v 2, 4, 6) and y (v 1, 3, 5), in the order of the rows
        Assert.Equal((2, 1), (byRow.RowCount, byRow.ColumnCount));
        Assert.Equal([2.0, 4.0, 6.0], Numbers(byRow.Cells[0].Table, "v"));
        Assert.Equal([1.0, 3.0, 5.0], Numbers(byRow.Cells[1].Table, "v"));
        Assert.Equal((1, 0), (byRow.Cells[1].Cell.RowIndex, byRow.Cells[1].Cell.ColumnIndex));
        Assert.Equal(default, byRow.Cells[1].Cell.ColumnValue);

        // Assert: five columns in one row
        Assert.Equal((1, 5), (byColumn.RowCount, byColumn.ColumnCount));
        Assert.Equal(Enumerable.Range(0, 5), byColumn.Cells.Select(c => c.Cell.ColumnIndex));
        Assert.All(byColumn.Cells, c => Assert.Equal(0, c.Cell.RowIndex));
    }

    /// <summary>
    ///     Validates that the cells of a wrapped facet are the distinct values in order, with no row or column index, and with the rows of each value.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_WrapCellsAreTheDistinctList()
    {
        // Arrange
        var setup = Prepare(Wrap);

        // Act
        var partition = Split(setup);

        // Assert: null, a, b, d, z
        Assert.Equal([default, S("a"), S("b"), S("d"), S("z")], partition.Domains.Wrap);
        Assert.Equal((1, 5), (partition.RowCount, partition.ColumnCount));
        Assert.All(partition.Cells, c => Assert.Equal((-1, -1), (c.Cell.RowIndex, c.Cell.ColumnIndex)));
        Assert.Equal(partition.Domains.Wrap, partition.Cells.Select(c => c.Cell.WrapValue));
        Assert.Equal(new[] { 1, 2, 1, 1, 1 }, partition.Cells.Select(c => c.Table.RowCount));
        Assert.Equal([2.0, 5.0], Numbers(partition.Cells[1].Table, "v"));
    }

    /// <summary>
    ///     Validates that the table of a cell has the fields of the table of the facet and the rows in their original order, that the cells together hold every row once, and that
    ///     the cells without rows share one empty table with the same fields.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_PartitionKeepsRowOrderAndFields()
    {
        // Arrange
        var setup = Prepare(RowColumn);

        // Act
        var partition = Split(setup);

        // Assert: every cell has the fields c, r, v
        Assert.All(partition.Cells, c => Assert.Equal(["c", "r", "v"], c.Table.Fields));

        // Assert: the rows are kept once; a cell of two rows keeps their order (c = a: v 2 in row x, v 5 in row y, so each is in its own cell here; the wrap facet has both)
        Assert.Equal(6, partition.Cells.Sum(c => c.Table.RowCount));
        var wrapped = Split(Prepare(Wrap));
        Assert.Equal(["a|x|2", "a|y|5"], Rows(wrapped.Cells[1].Table, "c", "r", "v"));

        // Assert: the empty cells are the same table
        var empty = partition.Cells.Where(c => c.Table.RowCount == 0).Select(c => c.Table).ToList();
        Assert.Equal(4, empty.Count);
        Assert.All(empty, t => Assert.Same(empty[0], t));
    }

    /// <summary>
    ///     Validates that the rows of the cells are charged to the row budget once: the six rows of the table are counted again, not once for each cell.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_PartitionChargesDerivedRowsOnce()
    {
        // Arrange: the six rows of the data are loaded
        var setup = Prepare(RowColumn);
        var before = setup.Loader.RowsLoaded;

        // Act
        _ = Split(setup);

        // Assert: ten cells, six rows
        Assert.Equal(6, before);
        Assert.Equal(12, setup.Loader.RowsLoaded);
    }

    /// <summary>
    ///     Validates that the rows of the cells count against the row limit with the rows that were loaded: six loaded and six more are twelve, over a limit of ten.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_RowsOfTheCellsAreLimitedByMaxDataRows()
    {
        // Arrange
        var setup = Prepare(RowColumn, Data, new VegaLiteLimits { MaxDataRows = 10 });

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Split(setup));

        // Assert
        Assert.Contains("MaxDataRows", ex.Message, StringComparison.Ordinal);
        Assert.Equal("/facet/row", ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates the limit on the number of views: a thousand row values and a thousand column values are a million cells and fail with <c>MaxViews</c> before a row is charged or
    ///     a table is made; the boundary is exact (two views in each of six cells and one view outside are thirteen).
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_CellCountIsLimitedByMaxViews()
    {
        // Arrange: 1000 rows with 1000 different row values and 1000 different column values
        var many = "[" + string.Join(",", Enumerable.Range(0, 1000).Select(i => "{\"r\":" + i + ",\"c\":" + i + ",\"v\":1}")) + "]";
        var huge = Prepare("""{"row":{"field":"r","type":"quantitative"},"column":{"field":"c","type":"quantitative"}}""", many);
        var before = huge.Loader.RowsLoaded;

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => Split(huge));

        // Assert: located at the facet, nothing charged
        Assert.Contains("1000000 cells", ex.Message, StringComparison.Ordinal);
        Assert.Contains("MaxViews", ex.Message, StringComparison.Ordinal);
        Assert.Equal("/facet/row", ex.Location.Pointer);
        Assert.Equal(before, huge.Loader.RowsLoaded);

        // Arrange / Act / Assert: 2 rows x 3 columns = 6 cells of 2 views and 1 view outside make 13 views
        const string Product = """[{"r":"x","c":"a"},{"r":"x","c":"b"},{"r":"x","c":"c"},{"r":"y","c":"a"},{"r":"y","c":"b"},{"r":"y","c":"c"}]""";
        var exact = Prepare(RowColumn, Product, new VegaLiteLimits { MaxViews = 13 });
        var over = Prepare(RowColumn, Product, new VegaLiteLimits { MaxViews = 12 });
        Assert.Equal(6, EncodingTransforms.Facet(exact.Facet, exact.Table, 2, 1, exact.Context).Cells.Count);
        Assert.Throws<VegaLiteRenderException>(() => EncodingTransforms.Facet(over.Facet, over.Table, 2, 1, over.Context));
    }

    /// <summary>
    ///     Validates that the count of views does not overflow: a cell count that no multiplication could hold fails as one over the limit.
    /// </summary>
    [Fact]
    public void EncodingTransforms_CheckFacetViews_DoesNotOverflow()
    {
        // Arrange
        var setup = Prepare(Wrap);

        // Act / Assert
        Assert.Throws<VegaLiteRenderException>(() => EncodingTransforms.CheckFacetViews(long.MaxValue / 2, int.MaxValue, int.MaxValue, setup.Facet.Wrap!, setup.Context));
        EncodingTransforms.CheckFacetViews(1000, 1, 0, setup.Facet.Wrap!, setup.Context);
        Assert.Throws<VegaLiteRenderException>(() => EncodingTransforms.CheckFacetViews(1000, 1, 1, setup.Facet.Wrap!, setup.Context));
    }

    /// <summary>
    ///     Validates that a number of views per cell that would wrap the product with the cells is still refused: 2 cells of 2^62 views are 2^63, which wraps to a negative number
    ///     that a product compared with the limit would accept.
    /// </summary>
    [Fact]
    public void EncodingTransforms_CheckFacetViews_ProductThatWrapsIsRefused()
    {
        // Arrange
        var setup = Prepare(Wrap);

        // Act / Assert: 2 * 2^62 wraps to long.MinValue; also the largest per-cell count with one cell and no cells at all
        Assert.Throws<VegaLiteRenderException>(() => EncodingTransforms.CheckFacetViews(2, 1L << 62, 0, setup.Facet.Wrap!, setup.Context));
        Assert.Throws<VegaLiteRenderException>(() => EncodingTransforms.CheckFacetViews(1, long.MaxValue, 1, setup.Facet.Wrap!, setup.Context));
        EncodingTransforms.CheckFacetViews(0, long.MaxValue, 0, setup.Facet.Wrap!, setup.Context);
    }

    /// <summary>
    ///     Validates that a sort array longer than the limit on the categories of a scale is refused, located at the array.
    /// </summary>
    [Fact]
    public void EncodingTransforms_FacetDomain_SortArrayIsLimited()
    {
        // Arrange
        var setup = Prepare("""{"column":{"field":"c","type":"nominal","sort":["a","b","c"]}}""", Data, new VegaLiteLimits { MaxScaleDomainSize = 2 });

        // Act
        var ex = Assert.Throws<VegaLiteRenderException>(() => EncodingTransforms.FacetDomain(setup.Facet, setup.Table, setup.Context));

        // Assert
        Assert.Contains("MaxScaleDomainSize", ex.Message, StringComparison.Ordinal);
        Assert.Equal("/facet/column/sort", ex.Location.Pointer);
    }

    /// <summary>
    ///     Validates that a sort array of a temporal facet field lists dates as date-time objects (a local one in the active calendar, and a UTC one): they match the dates of the
    ///     rows, so the listed dates come first in the order of the array, and the dates it does not list follow in the order they first appear.
    /// </summary>
    [Fact]
    public void EncodingTransforms_FacetDomain_SortArrayOfDateTimeObjectsOrdersTemporalValues()
    {
        // Arrange: the rows hold 2022, 2020, 2021 and 2019; the array lists 2021 and then 2022 (the dates of 2020 and 2019 are not listed)
        const string json = """
            {"data":{"values":[{"t":"2022-01-01"},{"t":"2020-01-01"},{"t":"2021-01-01"},{"t":"2019-01-01"}],"format":{"parse":{"t":"date:'%Y-%m-%d'"}}},
             "facet":{"column":{"field":"t","type":"temporal","sort":[{"year":2021,"month":"jan","date":1,"utc":true},{"year":2022,"month":1,"date":1}]}},
             "spec":{"mark":"point","encoding":{"x":{"field":"t","type":"temporal"}}}}
            """;
        var (table, context, _, tree) = AggregateTestSupport.Prepare(json);
        var facet = SpecNormalizer.Layers(tree).Root.Facet!;

        // Act
        var columns = EncodingTransforms.FacetDomain(facet, table, context).Columns;

        // Assert: 2021, 2022, then 2020 and 2019 in the order they appear
        Assert.Equal(4, columns.Count);
        Assert.Equal(
            [Utc(2021), Utc(2022), Utc(2020), Utc(2019)],
            columns);
    }

    /// <summary>
    ///     Gets the date value of the first of January of a year, in UTC.
    /// </summary>
    /// <param name="year">The year.</param>
    /// <returns>The date.</returns>
    private static DataValue Utc(int year) => DataValue.FromDate(TimeCalendar.Utc.FromParts(year, 0, 1, 0, 0, 0, 0));

    /// <summary>
    ///     Validates that a table without rows has no cell and is not an error.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_EmptyTableHasNoCells()
    {
        // Arrange
        var setup = Prepare(RowColumn, "[]");

        // Act
        var partition = Split(setup);

        // Assert
        Assert.Empty(partition.Cells);
        Assert.Equal((0, 0), (partition.RowCount, partition.ColumnCount));
        Assert.Empty(partition.Domains.Rows);
    }

    /// <summary>
    ///     Validates that the cancellation token is honored while the values are read.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_CancellationIsHonored()
    {
        // Arrange
        using var source = new CancellationTokenSource();
        var setup = Prepare(RowColumn, Data, null, source.Token);
        source.Cancel();

        // Act / Assert
        Assert.Throws<OperationCanceledException>(() => Split(setup));
        Assert.Throws<OperationCanceledException>(() => EncodingTransforms.FacetDomain(setup.Facet, setup.Table, setup.Context));
    }

    /// <summary>
    ///     Validates the arguments.
    /// </summary>
    [Fact]
    public void EncodingTransforms_Facet_ChecksItsArguments()
    {
        // Arrange
        var setup = Prepare(Wrap);

        // Act / Assert
        Assert.Throws<ArgumentNullException>(() => EncodingTransforms.Facet(null!, setup.Table, 1, 1, setup.Context));
        Assert.Throws<ArgumentNullException>(() => EncodingTransforms.Facet(setup.Facet, null!, 1, 1, setup.Context));
        Assert.Throws<ArgumentNullException>(() => EncodingTransforms.Facet(setup.Facet, setup.Table, 1, 1, null!));
        Assert.Throws<ArgumentNullException>(() => EncodingTransforms.FacetDomain(null!, setup.Table, setup.Context));
        Assert.Throws<ArgumentException>(() => EncodingTransforms.Facet(new FacetDefinition(null, null, null), setup.Table, 1, 1, setup.Context));
    }
}
