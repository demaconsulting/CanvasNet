namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Dot, matrix and vector products of <see cref="ScadValue"/>.
/// </summary>
/// <remarks>
///     <para>
///         A <em>numeric vector</em> is a non-empty vector whose elements are all numbers. A <em>matrix</em> is a
///         non-empty vector of numeric vectors that all have the same length (rows of equal width). Products are
///         summed left to right in index order, so the result is bit-identical on every platform.
///     </para>
///     <para>
///         A matrix product is the only product whose work and result exceed the size of its operands, so when
///         an evaluation host is supplied it is bounded: the result element count is checked against the list-length
///         limit and the multiply-add work is charged before anything is computed, and the host is polled for
///         cancellation every <see cref="PollIntervalMultiplyAdds"/> multiply-adds. Without a host (value-level
///         use) products are unbounded.
///     </para>
/// </remarks>
internal readonly partial struct ScadValue
{
    /// <summary>
    ///     The number of multiply-adds that cost one evaluation step when a matrix product is charged.
    /// </summary>
    /// <remarks>
    ///     Calibrated against the measured cost of a multiply-add on a contiguous array relative to the cost of
    ///     one step; chosen on the pessimistic side so a step budget bounds wall time.
    /// </remarks>
    internal const long MultiplyAddsPerStep = 16;

    /// <summary>
    ///     The number of multiply-adds between two cancellation polls inside a product.
    /// </summary>
    /// <remarks>The interval is counted, not timed, so the poll points are deterministic.</remarks>
    internal const long PollIntervalMultiplyAdds = 65_536;

    /// <summary>
    ///     Computes the number of multiply-adds in a matrix product, saturating instead of overflowing.
    /// </summary>
    /// <param name="rows">The rows of the left matrix.</param>
    /// <param name="inner">The shared inner dimension.</param>
    /// <param name="columns">The columns of the right matrix.</param>
    /// <returns><c>rows * inner * columns</c>, or <see cref="long.MaxValue"/> when that does not fit.</returns>
    internal static long ProductWork(long rows, long inner, long columns)
    {
        if (rows <= 0 || inner <= 0 || columns <= 0)
        {
            return 0;
        }

        if (rows > long.MaxValue / inner)
        {
            return long.MaxValue;
        }

        var pairs = rows * inner;
        return pairs > long.MaxValue / columns ? long.MaxValue : pairs * columns;
    }

    /// <summary>
    ///     Computes the evaluation steps a matrix product is charged: its result elements plus its scaled work.
    /// </summary>
    /// <param name="rows">The rows of the left matrix.</param>
    /// <param name="inner">The shared inner dimension.</param>
    /// <param name="columns">The columns of the right matrix.</param>
    /// <returns><c>rows * columns + ceil(work / MultiplyAddsPerStep)</c>, saturating at <see cref="long.MaxValue"/>.</returns>
    internal static long ProductSteps(long rows, long inner, long columns)
    {
        var work = ProductWork(rows, inner, columns);
        var scaled = (work / MultiplyAddsPerStep) + (work % MultiplyAddsPerStep == 0 ? 0 : 1);
        var elements = rows * columns;
        return scaled > long.MaxValue - elements ? long.MaxValue : scaled + elements;
    }

    /// <summary>
    ///     Multiplies two vectors according to their shapes.
    /// </summary>
    /// <remarks>
    ///     Numeric vector by numeric vector of equal length is the dot product (a number); matrix by numeric
    ///     vector, numeric vector by matrix and matrix by matrix give the usual linear-algebra products. Any other
    ///     combination, or a dimension mismatch, is invalid. The shapes are decided by looking at the operands
    ///     without copying them, so a product that a limit refuses never materializes its operands.
    /// </remarks>
    /// <param name="left">The left vector.</param>
    /// <param name="right">The right vector.</param>
    /// <param name="typeError">Receives the error flag.</param>
    /// <param name="host">The evaluation host that bounds the product, or <see langword="null"/> for none.</param>
    /// <returns>The product, or <c>undef</c> for invalid operands.</returns>
    private static ScadValue ProductOfVectors(ScadVector left, ScadVector right, ref bool typeError, IScadBuiltinHost? host)
    {
        var leftShape = ShapeOf(left, out var leftWidth);
        var rightShape = ShapeOf(right, out var rightWidth);
        long pending = 0;

        // Choose the product from the shapes of the two operands
        if (leftShape == ProductShape.Numbers && rightShape == ProductShape.Numbers && leftWidth == rightWidth)
        {
            return FromNumber(Dot(ReadNumbers(left), ReadNumbers(right), host, ref pending));
        }

        if (leftShape == ProductShape.Matrix && rightShape == ProductShape.Numbers && leftWidth == rightWidth)
        {
            var rows = ReadRows(left);
            var numbers = ReadNumbers(right);
            var products = new ScadValue[rows.Length];
            for (var r = 0; r < products.Length; r++)
            {
                products[r] = FromNumber(Dot(rows[r], numbers, host, ref pending));
            }

            return FromVector(products);
        }

        if (leftShape == ProductShape.Numbers && rightShape == ProductShape.Matrix && right.Count == leftWidth)
        {
            var numbers = ReadNumbers(left);
            var columns = ReadColumns(right, rightWidth);
            var products = new ScadValue[columns.Length];
            for (var c = 0; c < products.Length; c++)
            {
                products[c] = FromNumber(Dot(numbers, columns[c], host, ref pending));
            }

            return FromVector(products);
        }

        if (leftShape == ProductShape.Matrix && rightShape == ProductShape.Matrix && leftWidth == right.Count)
        {
            return MatrixProduct(left, right, rightWidth, host);
        }

        return InvalidOperand(ref typeError);
    }

    /// <summary>
    ///     Multiplies two matrices whose inner dimensions agree.
    /// </summary>
    /// <remarks>
    ///     With a host, the result element count is checked against the list-length limit first and the work is
    ///     charged second, both from the dimensions alone and before the operands are copied or anything is
    ///     multiplied, so one input always gives the same diagnostic and a refused product costs no allocation.
    /// </remarks>
    /// <param name="left">The left matrix (rows by inner).</param>
    /// <param name="right">The right matrix (inner by columns).</param>
    /// <param name="columnCount">The number of columns of the right matrix.</param>
    /// <param name="host">The evaluation host that bounds the product, or <see langword="null"/> for none.</param>
    /// <returns>The product matrix as a vector of numeric vectors.</returns>
    private static ScadValue MatrixProduct(ScadVector left, ScadVector right, int columnCount, IScadBuiltinHost? host)
    {
        if (host is not null)
        {
            host.CheckListLength((long)left.Count * columnCount);
            host.Charge(ProductSteps(left.Count, right.Count, columnCount));
        }

        // Summing along contiguous arrays keeps the accumulation order and is far kinder to the cache
        var leftRows = ReadRows(left);
        var columns = ReadColumns(right, columnCount);
        long pending = 0;
        var rows = new ScadValue[leftRows.Length];
        for (var r = 0; r < rows.Length; r++)
        {
            var cells = new ScadValue[columns.Length];
            for (var c = 0; c < cells.Length; c++)
            {
                cells[c] = FromNumber(Dot(leftRows[r], columns[c], host, ref pending));
            }

            rows[r] = FromVector(cells);
        }

        return FromVector(rows);
    }

    /// <summary>
    ///     Copies the rows of a matrix into arrays. The vector must have been recognized as a matrix by <see cref="ShapeOf"/>.
    /// </summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>One array per row.</returns>
    private static double[][] ReadRows(ScadVector matrix)
    {
        var rows = new double[matrix.Count][];
        for (var r = 0; r < rows.Length; r++)
        {
            rows[r] = ReadNumbers((ScadVector)matrix[r]._reference!);
        }

        return rows;
    }

    /// <summary>
    ///     Copies the columns of a matrix into arrays (each column from top to bottom), without an intermediate row copy.
    /// </summary>
    /// <param name="matrix">The matrix; it must have been recognized as a matrix by <see cref="ShapeOf"/>.</param>
    /// <param name="width">The number of columns.</param>
    /// <returns>One array per column.</returns>
    private static double[][] ReadColumns(ScadVector matrix, int width)
    {
        var columns = new double[width][];
        for (var c = 0; c < columns.Length; c++)
        {
            columns[c] = new double[matrix.Count];
        }

        for (var r = 0; r < matrix.Count; r++)
        {
            var row = (ScadVector)matrix[r]._reference!;
            for (var c = 0; c < columns.Length; c++)
            {
                columns[c][r] = row[c]._number;
            }
        }

        return columns;
    }

    /// <summary>
    ///     Computes the dot product of two numeric arrays of equal length, polling the host at fixed intervals.
    /// </summary>
    /// <param name="a">The first array.</param>
    /// <param name="b">The second array.</param>
    /// <param name="host">The host to poll, or <see langword="null"/> for none.</param>
    /// <param name="pending">The multiply-adds done since the last poll; carried across the dot products of one product.</param>
    /// <returns>The sum of products, accumulated left to right.</returns>
    private static double Dot(double[] a, double[] b, IScadBuiltinHost? host, ref long pending)
    {
        var sum = 0.0;
        var i = 0;
        while (i < a.Length)
        {
            // Run to the end, or to the next poll point when a host wants polling
            var end = host is null ? a.Length : (int)Math.Min(a.Length, i + (PollIntervalMultiplyAdds - pending));
            var chunk = end - i;
            for (; i < end; i++)
            {
                sum += a[i] * b[i];
            }

            if (host is not null)
            {
                pending += chunk;
                if (pending >= PollIntervalMultiplyAdds)
                {
                    pending = 0;
                    host.Poll();
                }
            }
        }

        return sum;
    }

    /// <summary>
    ///     Classifies a vector for a product without copying it.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <param name="width">
    ///     The element count of a numeric vector, or the common row length of a matrix; zero for any other vector.
    /// </param>
    /// <returns>
    ///     <see cref="ProductShape.Numbers"/> for a non-empty vector of numbers, <see cref="ProductShape.Matrix"/> for a
    ///     non-empty vector of numeric vectors of one common length, otherwise <see cref="ProductShape.Other"/>.
    /// </returns>
    private static ProductShape ShapeOf(ScadVector vector, out int width)
    {
        width = 0;
        if (vector.Count == 0)
        {
            return ProductShape.Other;
        }

        // The first element decides which shape is possible; the others must agree with it
        if (vector[0]._kind == ScadValueKind.Number)
        {
            if (!IsNumeric(vector))
            {
                return ProductShape.Other;
            }

            width = vector.Count;
            return ProductShape.Numbers;
        }

        if (vector[0]._kind != ScadValueKind.Vector)
        {
            return ProductShape.Other;
        }

        var first = (ScadVector)vector[0]._reference!;
        for (var i = 0; i < vector.Count; i++)
        {
            var element = vector[i];
            if (element._kind != ScadValueKind.Vector)
            {
                return ProductShape.Other;
            }

            var row = (ScadVector)element._reference!;
            if (row.Count != first.Count || !IsNumeric(row))
            {
                return ProductShape.Other;
            }
        }

        width = first.Count;
        return ProductShape.Matrix;
    }

    /// <summary>
    ///     Determines whether a vector is non-empty and holds only numbers.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <returns><see langword="true"/> for a numeric vector.</returns>
    private static bool IsNumeric(ScadVector vector)
    {
        if (vector.Count == 0)
        {
            return false;
        }

        foreach (var element in vector.AsSpan())
        {
            if (element._kind != ScadValueKind.Number)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    ///     Copies a numeric vector into an array.
    /// </summary>
    /// <param name="vector">The vector; it must have been recognized as numeric by <see cref="ShapeOf"/>.</param>
    /// <returns>The numbers.</returns>
    private static double[] ReadNumbers(ScadVector vector)
    {
        var values = new double[vector.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = vector[i]._number;
        }

        return values;
    }

    /// <summary>
    ///     The kinds of operand a product distinguishes.
    /// </summary>
    private enum ProductShape
    {
        /// <summary>Neither a numeric vector nor a rectangular matrix.</summary>
        Other,

        /// <summary>A non-empty vector of numbers.</summary>
        Numbers,

        /// <summary>A non-empty vector of numeric vectors of one common length.</summary>
        Matrix,
    }
}
