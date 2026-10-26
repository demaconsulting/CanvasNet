using System.Diagnostics;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Program runner class for out-of-process system testing.
/// </summary>
internal static class Runner
{
    /// <summary>
    ///     Runs the specified program and captures its output.
    /// </summary>
    /// <param name="output">Program output (stdout and stderr combined).</param>
    /// <param name="program">Program name or path.</param>
    /// <param name="arguments">Program arguments.</param>
    /// <returns>Program exit code.</returns>
    /// <exception cref="InvalidOperationException">Thrown when process fails to start.</exception>
    public static int Run(out string output, string program, params string[] arguments)
    {
        // Construct the start information
        var startInfo = new ProcessStartInfo(program)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // Add the arguments
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // Start the process
        using var process = Process.Start(startInfo) ??
                            throw new InvalidOperationException("Failed to start process");

        // Read output asynchronously to avoid buffer overflow
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

        // Wait for the process to exit
        process.WaitForExit();

        // Combine stdout and stderr, save the output and return the exit code
        var stdout = outputTask.GetAwaiter().GetResult();
        var stderr = errorTask.GetAwaiter().GetResult();
        output = stdout + stderr;
        return process.ExitCode;
    }
}
