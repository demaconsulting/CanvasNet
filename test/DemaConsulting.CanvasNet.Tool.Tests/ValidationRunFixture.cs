namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Runs <see cref="Validation.Run"/> once in-process and shares the outcome with the tests that read it.
/// </summary>
/// <remarks>
///     A self-validation run executes every self-test of the tool, which makes it the slowest thing the unit tests of
///     <see cref="Validation"/> do. The tests of the summary, of each self-test's result and record, of the order of the
///     self-tests, and of the TRX results file only read the outcome of a run with a TRX results file and normal
///     (not silent) output, so they share this one run instead of starting one each. The run starts the first time a
///     test reads it, so a filtered test run that selects none of those tests never pays for it. Standard output is
///     captured while it runs, which is safe because every user of this fixture is in the "Sequential" collection.
///     The results file is deleted when the fixture is disposed.
/// </remarks>
public sealed class ValidationRunFixture : IDisposable
{
    /// <summary>
    ///     The outcome of the run, computed on first use.
    /// </summary>
    private readonly Lazy<(int ExitCode, string StdOut)> _run;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidationRunFixture"/> class without starting the run.
    /// </summary>
    public ValidationRunFixture()
    {
        ResultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_validation_test_{Guid.NewGuid()}.trx");
        _run = new Lazy<(int ExitCode, string StdOut)>(Run);
    }

    /// <summary>
    ///     Gets the path of the TRX results file that the run writes.
    /// </summary>
    public string ResultsFile { get; }

    /// <summary>
    ///     Gets the exit code of the context after the run, running the self-validation first if it has not run yet.
    /// </summary>
    public int ExitCode => _run.Value.ExitCode;

    /// <summary>
    ///     Gets the standard output of the run, running the self-validation first if it has not run yet.
    /// </summary>
    public string StdOut => _run.Value.StdOut;

    /// <summary>
    ///     Deletes the results file of the run, if it was written.
    /// </summary>
    public void Dispose()
    {
        if (File.Exists(ResultsFile))
        {
            File.Delete(ResultsFile);
        }
    }

    /// <summary>
    ///     Runs the self-validation with a TRX results file and normal output, capturing standard output.
    /// </summary>
    /// <returns>The exit code of the context and the captured standard output.</returns>
    private (int ExitCode, string StdOut) Run()
    {
        // A context that is not silent, so the summary and the self-test lines reach standard output
        using var context = Context.Create(["--results", ResultsFile]);

        // Run every self-test once and keep what the readers need
        var (stdOut, _) = ToolHost.Capture(() => Validation.Run(context));
        return (context.ExitCode, stdOut);
    }
}
