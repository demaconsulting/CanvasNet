namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Runs the tool's self-validation once as a real process and shares the outcome with the tests that read it.
/// </summary>
/// <remarks>
///     A self-validation run executes every self-test of the tool, which makes it the slowest thing the system tests
///     do. The tests of the validate flag, of the TRX results file, and of the heading depth only read the outcome of
///     the same command line (<c>--validate --depth 3 --results &lt;file&gt;.trx</c>), so they share this one run
///     instead of starting one each. The run starts the first time a test reads it, so a filtered test run that
///     selects none of those tests never pays for it. The results file is deleted when the fixture is disposed.
/// </remarks>
public sealed class ValidateProcessFixture : IDisposable
{
    /// <summary>
    ///     The outcome of the run, computed on first use.
    /// </summary>
    private readonly Lazy<(int ExitCode, string Output)> _run;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ValidateProcessFixture"/> class without starting the run.
    /// </summary>
    public ValidateProcessFixture()
    {
        ResultsFile = Path.Combine(Path.GetTempPath(), $"canvasnet_system_test_{Guid.NewGuid()}.trx");
        _run = new Lazy<(int ExitCode, string Output)>(Run);
    }

    /// <summary>
    ///     Gets the path of the TRX results file that the run writes.
    /// </summary>
    public string ResultsFile { get; }

    /// <summary>
    ///     Gets the exit code of the run, running the self-validation first if it has not run yet.
    /// </summary>
    public int ExitCode => _run.Value.ExitCode;

    /// <summary>
    ///     Gets the combined standard output and standard error of the run, running the self-validation first if it has
    ///     not run yet.
    /// </summary>
    public string Output => _run.Value.Output;

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
    ///     Runs the tool assembly from the test output directory with the shared self-validation command line.
    /// </summary>
    /// <returns>The exit code and the combined output of the process.</returns>
    private (int ExitCode, string Output) Run()
    {
        // The tool assembly sits next to the test assembly because the test project references the tool project
        var dllPath = PathHelpers.SafePathCombine(AppContext.BaseDirectory, "DemaConsulting.CanvasNet.Tool.dll");

        // One process serves every reader: the validate flag, a heading depth of three, and a TRX results file
        var exitCode = Runner.Run(out var output, "dotnet", dllPath, "--validate", "--depth", "3", "--results", ResultsFile);
        return (exitCode, output);
    }
}
