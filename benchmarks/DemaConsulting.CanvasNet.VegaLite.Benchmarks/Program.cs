using System.Diagnostics;
using System.Globalization;

namespace DemaConsulting.CanvasNet.VegaLite.Benchmarks;

/// <summary>
///     The Stopwatch harness for the Vega-Lite renderer: times a set of heavy charts, then runs calibration probes that check how long the default limits take to stop a runaway
///     specification. Developer tooling only: it is compiled with the solution and never run by the build. See the README of this folder.
/// </summary>
internal static class Program
{
    /// <summary>The seconds in which the default limits should stop a runaway specification.</summary>
    private const double TargetSeconds = 10;

    /// <summary>
    ///     Runs the harness.
    /// </summary>
    /// <param name="args">The arguments: <c>--smoke</c>, <c>--runs N</c>, <c>--filter TEXT</c>, <c>--no-calibration</c>, <c>--help</c>.</param>
    /// <returns>0 when every workload rendered and every calibration probe was stopped by the expected limit, else 1.</returns>
    public static int Main(string[] args)
    {
        var smoke = args.Contains("--smoke");
        var calibrate = !args.Contains("--no-calibration");
        var filter = ValueOf(args, "--filter");
        var defaultRuns = smoke ? 1 : 5;
        var runs = int.TryParse(ValueOf(args, "--runs"), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0 ? parsed : defaultRuns;
        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine("Usage: dotnet run --project benchmarks/DemaConsulting.CanvasNet.VegaLite.Benchmarks -c Release -- [--smoke] [--runs N] [--filter TEXT] [--no-calibration]");
            return 0;
        }

        Console.WriteLine($"CanvasNet Vega-Lite benchmarks: {Environment.ProcessorCount} logical processors, {Environment.Version}, {(smoke ? "smoke sizes" : "full sizes")}, {runs} run(s) each");
        Console.WriteLine("The numbers depend on the machine; compare runs on one machine only.");
        Console.WriteLine();

        var failures = 0;
        Console.WriteLine($"{"workload",-14} {"units",10} {"median ms",10} {"min ms",10} {"ns/unit",10} {"alloc MB",9}  what");
        foreach (var workload in Workloads.Make(smoke).Where(w => Matches(w.Name, filter)))
        {
            failures += RunWorkload(workload, runs);
        }

        if (calibrate)
        {
            Console.WriteLine();
            Console.WriteLine($"Calibration: a tightened limit stops each probe; the time is scaled to the default limit (target: about {TargetSeconds:0} s or less).");
            Console.WriteLine($"{"probe",-26} {"family",-10} {"stopped ms",11} {"x default",10} {"est. s at default",18}  stopped by");
            foreach (var probe in Workloads.MakeCalibration(smoke).Where(p => Matches(p.Name, filter)))
            {
                failures += RunProbe(probe, smoke);
            }
        }

        return failures == 0 ? 0 : 1;
    }

    /// <summary>
    ///     Gets the value that follows an option.
    /// </summary>
    /// <param name="args">The arguments.</param>
    /// <param name="name">The option.</param>
    /// <returns>The value, or null.</returns>
    private static string? ValueOf(string[] args, string name)
    {
        var index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
    }

    /// <summary>
    ///     Tests whether a name matches the filter.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="filter">The filter text, or null for everything.</param>
    /// <returns>True when the name contains the filter.</returns>
    private static bool Matches(string name, string? filter) => filter is null || name.Contains(filter, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    ///     Times a workload: one warm-up render, then the runs.
    /// </summary>
    /// <param name="workload">The workload.</param>
    /// <param name="runs">The number of timed runs.</param>
    /// <returns>1 when the workload failed, else 0.</returns>
    private static int RunWorkload(Workload workload, int runs)
    {
        try
        {
            var chart = VegaLiteDocument.Parse(workload.Spec);
            using (chart.Render(workload.Options()))
            {
                // The warm-up pays for the JIT, so that the timed runs measure the steady state
            }

            var times = new List<double>();
            var allocated = 0L;
            for (var run = 0; run < runs; run++)
            {
                var options = workload.Options();
                var before = GC.GetTotalAllocatedBytes(precise: true);
                var watch = Stopwatch.StartNew();
                using var surface = chart.Render(options);
                watch.Stop();
                allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
                times.Add(watch.Elapsed.TotalMilliseconds);
            }

            times.Sort();
            var median = times[times.Count / 2];
            var nsPerUnit = median * 1_000_000 / workload.Units;
            Console.WriteLine($"{workload.Name,-14} {workload.Units,10} {median,10:F1} {times[0],10:F1} {nsPerUnit,10:F0} {allocated / 1_048_576.0,9:F1}  {workload.Description} (ns per {workload.UnitName})");
            return 0;
        }
        catch (Exception ex) when (ex is VegaLiteRenderException or VegaLiteSpecException or VegaLiteUnsupportedFeatureException)
        {
            Console.WriteLine($"{workload.Name,-14} FAILED: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    ///     Renders a calibration probe until its tightened limit stops it, and reports the scaled time.
    /// </summary>
    /// <param name="probe">The probe.</param>
    /// <param name="smoke">True for the smoke run, where the probes are too small to reach the limit.</param>
    /// <returns>1 when the probe was not stopped by the expected limit (outside the smoke run), else 0.</returns>
    private static int RunProbe(CalibrationProbe probe, bool smoke)
    {
        var chart = VegaLiteDocument.Parse(probe.Spec);
        var options = new VegaLiteRenderOptions { ThrowOnUnsupportedFeature = true, Limits = probe.Limits };
        var watch = Stopwatch.StartNew();
        try
        {
            using var surface = chart.Render(options);
            watch.Stop();
            Console.WriteLine($"{probe.Name,-26} {probe.Family,-10} {watch.Elapsed.TotalMilliseconds,11:F0} {probe.Scale,10:F0} {"-",18}  not stopped (the probe is cheaper than the tightened limit{(smoke ? "; expected in the smoke run" : string.Empty)})");
            return smoke ? 0 : 1;
        }
        catch (VegaLiteRenderException ex)
        {
            watch.Stop();
            var stopped = ex.Message.Contains(probe.LimitName, StringComparison.Ordinal);
            var seconds = watch.Elapsed.TotalSeconds * probe.Scale;
            var verdict = string.Empty;
            if (!stopped)
            {
                verdict = $"  <-- another limit stopped it: {ex.Message}";
            }
            else if (seconds > TargetSeconds * 2)
            {
                verdict = "  <-- SLOW";
            }
            Console.WriteLine($"{probe.Name,-26} {probe.Family,-10} {watch.Elapsed.TotalMilliseconds,11:F0} {probe.Scale,10:F0} {seconds,18:F1}  {(stopped ? probe.LimitName : "?")}{verdict}");
            return stopped ? 0 : 1;
        }
    }
}
