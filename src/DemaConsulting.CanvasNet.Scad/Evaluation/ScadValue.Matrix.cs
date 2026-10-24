namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     Dot, matrix and vector products of <see cref="ScadValue"/>.
/// </summary>
/// <remarks>
///     A <em>numeric vector</em> is a non-empty vector whose elements are all numbers. A <em>matrix</em> is a
///     non-empty vector of numeric vectors that all have the same length (rows of equal width). Products are
///     summed left to right in index order, so the result is bit-identical on every platform.
/// </remarks>
internal readonly partial struct ScadValue
{
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
    /// <returns>The product, or <c>undef</c> for invalid operands.</returns>
    private static ScadValue ProductOfVectors(ScadVector left, ScadVector right, ref bool typeError)
    {
        var leftIsVector = TryAsNumbers(left, out var leftNumbers);
        var rightIsVector = TryAsNumbers(right, out var rightNumbers);
        var leftIsMatrix = TryAsMatrix(left, out var leftRows);
        var rightIsMatrix = TryAsMatrix(right, out var rightRows);

        // Choose the product from the shapes of the two operands
        if (leftIsVector && rightIsVector && leftNumbers.Length == rightNumbers.Length)
        {
            return FromNumber(Dot(leftNumbers, rightNumbers));
        }

        if (leftIsMatrix && rightIsVector && leftRows[0].Length == rightNumbers.Length)
        {
            return FromNumbers(leftRows.Select(row => Dot(row, rightNumbers)));
        }

        if (leftIsVector && rightIsMatrix && rightRows.Length == leftNumbers.Length)
        {
            return FromNumbers(Enumerable.Range(0, rightRows[0].Length).Select(c => DotColumn(leftNumbers, rightRows, c)));
        }

        if (leftIsMatrix && rightIsMatrix && leftRows[0].Length == rightRows.Length)
        {
            return MatrixProduct(leftRows, rightRows);
        }

        return InvalidOperand(ref typeError);
    }

    /// <summary>
    ///     Multiplies two matrices whose inner dimensions agree.
    /// </summary>
    /// <param name="left">The left matrix (rows by inner).</param>
    /// <param name="right">The right matrix (inner by columns).</param>
    /// <returns>The product matrix as a vector of numeric vectors.</returns>
    private static ScadValue MatrixProduct(double[][] left, double[][] right)
    {
        var rows = new ScadValue[left.Length];
        for (var r = 0; r < rows.Length; r++)
        {
            rows[r] = FromNumbers(Enumerable.Range(0, right[0].Length).Select(c => DotColumn(left[r], right, c)));
        }

        return FromVector(rows);
    }

    /// <summary>
    ///     Computes the dot product of two numeric arrays of equal length.
    /// </summary>
    /// <param name="a">The first array.</param>
    /// <param name="b">The second array.</param>
    /// <returns>The sum of products, accumulated left to right.</returns>
    private static double Dot(double[] a, double[] b)
    {
        var sum = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

    /// <summary>
    ///     Computes the dot product of a row with one column of a matrix.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="matrix">The matrix; it has as many rows as the row has entries.</param>
    /// <param name="column">The zero-based column.</param>
    /// <returns>The sum of products, accumulated left to right.</returns>
    private static double DotColumn(double[] row, double[][] matrix, int column)
    {
        var sum = 0.0;
        for (var i = 0; i < row.Length; i++)
        {
            sum += row[i] * matrix[i][column];
        }

        return sum;
    }

    /// <summary>
    ///     Builds a vector value from numbers.
    /// </summary>
    /// <param name="numbers">The numbers.</param>
    /// <returns>The vector.</returns>
    private static ScadValue FromNumbers(IEnumerable<double> numbers) =>
        FromVector(numbers.Select(FromNumber).ToArray());

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
