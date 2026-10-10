namespace DemaConsulting.CanvasNet.Codecs;

public static partial class Jpeg2000Codec
{
    // ================================================================================================
    // Inverse discrete wavelet transforms (ITU-T T.800 Annex F): reversible 5/3 and irreversible 9/7
    // ================================================================================================

    private const float DwtAlpha = -1.586134342059924f;
    private const float DwtBeta = -0.052980118572961f;
    private const float DwtGamma = 0.882911075530934f;
    private const float DwtDelta = 0.443506852043971f;
    private const float DwtK = 1.230174104914001f;

    /// <summary>Runs the inverse 5/3 transform in place over a Mallat-layout plane.</summary>
    /// <param name="plane">The coefficient plane (row stride equals the full tile-component width).</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="res">The resolution levels, lowest first.</param>
    private static void InverseDwt53(int[] plane, int stride, Resolution[] res)
    {
        var maxDim = 1;
        foreach (var r in res)
        {
            maxDim = Math.Max(maxDim, Math.Max(r.Width, r.Height));
        }

        var line = new int[maxDim];
        for (var r = 1; r < res.Length; r++)
        {
            var cur = res[r];
            int w = cur.Width, h = cur.Height;
            var px = (int)(cur.X0 & 1);
            var py = (int)(cur.Y0 & 1);
            for (var y = 0; y < h; y++)
            {
                var rowBase = y * stride;
                Interleave(plane, rowBase, 1, w, res[r - 1].Width, px, line);
                Lift53(line, w, px);
                Array.Copy(line, 0, plane, rowBase, w);
            }

            for (var x = 0; x < w; x++)
            {
                Interleave(plane, x, stride, h, res[r - 1].Height, py, line);
                Lift53(line, h, py);
                for (var y = 0; y < h; y++)
                {
                    plane[(y * stride) + x] = line[y];
                }
            }
        }
    }

    /// <summary>Runs the inverse 9/7 transform in place over a Mallat-layout plane.</summary>
    /// <param name="plane">The coefficient plane.</param>
    /// <param name="stride">The row stride.</param>
    /// <param name="res">The resolution levels, lowest first.</param>
    private static void InverseDwt97(float[] plane, int stride, Resolution[] res)
    {
        var maxDim = 1;
        foreach (var r in res)
        {
            maxDim = Math.Max(maxDim, Math.Max(r.Width, r.Height));
        }

        var line = new float[maxDim];
        for (var r = 1; r < res.Length; r++)
        {
            var cur = res[r];
            int w = cur.Width, h = cur.Height;
            var px = (int)(cur.X0 & 1);
            var py = (int)(cur.Y0 & 1);
            for (var y = 0; y < h; y++)
            {
                var rowBase = y * stride;
                Interleave(plane, rowBase, 1, w, res[r - 1].Width, px, line);
                Lift97(line, w, px);
                Array.Copy(line, 0, plane, rowBase, w);
            }

            for (var x = 0; x < w; x++)
            {
                Interleave(plane, x, stride, h, res[r - 1].Height, py, line);
                Lift97(line, h, py);
                for (var y = 0; y < h; y++)
                {
                    plane[(y * stride) + x] = line[y];
                }
            }
        }
    }

    /// <summary>Interleaves a low-pass half followed by a high-pass half into alternating positions.</summary>
    /// <typeparam name="T">The sample type.</typeparam>
    /// <param name="src">The source plane.</param>
    /// <param name="start">The index of the first sample of the line.</param>
    /// <param name="step">The distance between consecutive samples of the line.</param>
    /// <param name="n">The line length.</param>
    /// <param name="lowCount">The number of low-pass samples.</param>
    /// <param name="parity">The parity of the first sample's absolute coordinate.</param>
    /// <param name="dest">The interleaved output.</param>
    private static void Interleave<T>(T[] src, int start, int step, int n, int lowCount, int parity, T[] dest)
    {
        var lo = 0;
        var hi = lowCount;
        for (var i = 0; i < n; i++)
        {
            if (((i + parity) & 1) == 0)
            {
                dest[i] = src[start + (lo * step)];
                lo++;
            }
            else
            {
                dest[i] = src[start + (hi * step)];
                hi++;
            }
        }
    }

    /// <summary>Inverse 5/3 lifting of one interleaved line.</summary>
    /// <param name="x">The line.</param>
    /// <param name="n">The line length.</param>
    /// <param name="parity">The parity of the first sample's absolute coordinate.</param>
    private static void Lift53(int[] x, int n, int parity)
    {
        if (n == 1)
        {
            if (parity == 1)
            {
                x[0] >>= 1;
            }

            return;
        }

        for (var i = parity; i < n; i += 2)
        {
            x[i] -= (x[i > 0 ? i - 1 : 1] + x[i < n - 1 ? i + 1 : n - 2] + 2) >> 2;
        }

        for (var i = 1 - parity; i < n; i += 2)
        {
            x[i] += (x[i > 0 ? i - 1 : 1] + x[i < n - 1 ? i + 1 : n - 2]) >> 1;
        }
    }

    /// <summary>Inverse 9/7 lifting of one interleaved line.</summary>
    /// <param name="x">The line.</param>
    /// <param name="n">The line length.</param>
    /// <param name="parity">The parity of the first sample's absolute coordinate.</param>
    private static void Lift97(float[] x, int n, int parity)
    {
        if (n == 1)
        {
            if (parity == 1)
            {
                x[0] *= 0.5f;
            }

            return;
        }

        for (var i = 0; i < n; i++)
        {
            x[i] *= ((i + parity) & 1) == 0 ? DwtK : 1f / DwtK;
        }

        LiftStep(x, n, parity, DwtDelta);
        LiftStep(x, n, 1 - parity, DwtGamma);
        LiftStep(x, n, parity, DwtBeta);
        LiftStep(x, n, 1 - parity, DwtAlpha);
    }

    private static void LiftStep(float[] x, int n, int first, float coef)
    {
        for (var i = first; i < n; i += 2)
        {
            x[i] -= coef * (x[i > 0 ? i - 1 : 1] + x[i < n - 1 ? i + 1 : n - 2]);
        }
    }
}
