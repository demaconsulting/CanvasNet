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
    ///     combination, or a dimension mismatch, is invalid.
    /// </remarks>
    /// <param name="left">The left vector.</param>
    /// <param name="right">The right vector.</param>
    /// <param name="typeError">Receives the error flag.</param>
    /// <param name="host">The evaluation host that bounds the product, or <see langword="null"/> for none.</param>
    /// <returns>The product, or <c>undef</c> for invalid operands.</returns>
    private static ScadValue ProductOfVectors(ScadVector left, ScadVector right, ref bool typeError, IScadBuiltinHost? host)
    {
        var leftIsVector = TryAsNumbers(left, out var leftNumbers);
        var rightIsVector = TryAsNumbers(right, out var rightNumbers);
        var leftIsMatrix = TryAsMatrix(left, out var leftRows);
        var rightIsMatrix = TryAsMatrix(right, out var rightRows);
        long pending = 0;

        // Choose the product from the shapes of the two operands
        if (leftIsVector && rightIsVector && leftNumbers.Length == rightNumbers.Length)
        {
            return FromNumber(Dot(leftNumbers, rightNumbers, host, ref pending));
        }

        if (leftIsMatrix && rightIsVector && leftRows[0].Length == rightNumbers.Length)
        {
            var products = new ScadValue[leftRows.Length];
            for (var r = 0; r < products.Length; r++)
            {
                products[r] = FromNumber(Dot(leftRows[r], rightNumbers, host, ref pending));
            }

            return FromVector(products);
        }

        if (leftIsVector && rightIsMatrix && rightRows.Length == leftNumbers.Length)
        {
            var columns = Transpose(rightRows);
            var products = new ScadValue[columns.Length];
            for (var c = 0; c < products.Length; c++)
            {
                products[c] = FromNumber(Dot(leftNumbers, columns[c], host, ref pending));
            }

            return FromVector(products);
        }

        if (leftIsMatrix && rightIsMatrix && leftRows[0].Length == rightRows.Length)
        {
            return MatrixProduct(leftRows, rightRows, host);
        }

        return InvalidOperand(ref typeError);
    }

    /// <summary>
    ///     Multiplies two matrices whose inner dimensions agree.
    /// </summary>
    /// <remarks>
    ///     With a host, the result element count is checked against the list-length limit first and the work is
    ///     charged second, both before any multiplication, so one input always gives the same diagnostic.
    /// </remarks>
    /// <param name="left">The left matrix (rows by inner).</param>
    /// <param name="right">The right matrix (inner by columns).</param>
    /// <param name="host">The evaluation host that bounds the product, or <see langword="null"/> for none.</param>
    /// <returns>The product matrix as a vector of numeric vectors.</returns>
    private static ScadValue MatrixProduct(double[][] left, double[][] right, IScadBuiltinHost? host)
    {
        var rowCount = (long)left.Length;
        var innerCount = (long)right.Length;
        var columnCount = (long)right[0].Length;
        if (host is not null)
        {
            host.CheckListLength(rowCount * columnCount);
            host.Charge(ProductSteps(rowCount, innerCount, columnCount));
        }

        // Summing along contiguous arrays keeps the accumulation order and is far kinder to the cache
        var columns = Transpose(right);
        long pending = 0;
        var rows = new ScadValue[left.Length];
        for (var r = 0; r < rows.Length; r++)
        {
            var cells = new ScadValue[columns.Length];
            for (var c = 0; c < cells.Length; c++)
            {
                cells[c] = FromNumber(Dot(left[r], columns[c], host, ref pending));
            }

            rows[r] = FromVector(cells);
        }

        return FromVector(rows);
    }

    /// <summary>
    ///     Builds the columns of a matrix as contiguous arrays.
    /// </summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>One array per column, each holding that column from top to bottom.</returns>
    private static double[][] Transpose(double[][] matrix)
    {
        var columns = new double[matrix[0].Length][];
        for (var c = 0; c < columns.Length; c++)
        {
            var column = new double[matrix.Length];
            for (var r = 0; r < column.Length; r++)
            {
                column[r] = matrix[r][c];
            }

            columns[c] = column;
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
    ///     Reads a numeric vector.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <param name="numbers">The numbers when the vector is numeric; otherwise an empty array.</param>
    /// <returns><see langword="true"/> when the vector is non-empty and every element is a number.</returns>
    private static bool TryAsNumbers(ScadVector vector, out double[] numbers)
    {
        numbers = [];
        if (vector.Count == 0)
        {
            return false;
        }

        var values = new double[vector.Count];
        for (var i = 0; i < values.Length; i++)
        {
            if (vector[i]._kind != ScadValueKind.Number)
            {
                return false;
            }

            values[i] = vector[i]._number;
        }

        numbers = values;
        return true;
    }

    /// <summary>
    ///     Reads a matrix: a non-empty vector of numeric vectors of one common length.
    /// </summary>
    /// <param name="vector">The vector.</param>
    /// <param name="rows">The rows when the vector is a matrix; otherwise an empty array.</param>
    /// <returns><see langword="true"/> when the vector is a rectangular matrix.</returns>
    private static bool TryAsMatrix(ScadVector vector, out double[][] rows)
    {
        rows = [];
        if (vector.Count == 0)
        {
            return false;
        }

        var result = new double[vector.Count][];
        for (var i = 0; i < result.Length; i++)
        {
            var element = vector[i];
            if (element._kind != ScadValueKind.Vector
                || !TryAsNumbers((ScadVector)element._reference!, out result[i])
                || result[i].Length != result[0].Length)
            {
                return false;
            }
        }

        rows = result;
        return true;
    }
}
