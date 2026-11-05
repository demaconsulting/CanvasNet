using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     In-process helper that runs the tool and captures what it writes to the console.
/// </summary>
/// <remarks>
///     Console redirection is process-wide, so every test class that uses this helper belongs to the
///     "Sequential" collection. The original console writers are always restored.
/// </remarks>
internal static class ToolHost
{
    /// <summary>
    ///     Runs <see cref="Program.Execute"/> with the default command registry.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The exit code and the captured standard output and standard error.</returns>
    public static (int ExitCode, string StdOut, string StdErr) Invoke(params string[] args)
    {
        return Invoke(CommandRegistry.CreateDefault(), args);
    }

    /// <summary>
    ///     Runs <see cref="Program.Execute"/> with the given command registry.
    /// </summary>
    /// <param name="registry">The registry of commands.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The exit code and the captured standard output and standard error.</returns>
    public static (int ExitCode, string StdOut, string StdErr) Invoke(CommandRegistry registry, params string[] args)
    {
        var (exitCode, stdOut, stdErr) = Capture(() => Program.Execute(args, registry));
        return (exitCode, stdOut, stdErr);
    }

    /// <summary>
    ///     Runs <see cref="Program.Execute"/> and asserts that it throws, as it does for an unexpected exception.
    /// </summary>
    /// <typeparam name="TException">The exact type of the expected exception.</typeparam>
    /// <param name="registry">The registry of commands.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The thrown exception and the captured standard output and standard error.</returns>
    public static (TException Exception, string StdOut, string StdErr) InvokeExpectingThrow<TException>(
        CommandRegistry registry,
        params string[] args)
        where TException : Exception
    {
        // Capture the console while asserting that the entry point throws; the thrown exception is the result
        return Capture(() => Assert.Throws<TException>(() => Program.Execute(args, registry)));
    }

    /// <summary>
    ///     Runs an action that is expected to fail with an expected, user-correctable error, and asserts that it does.
    /// </summary>
    /// <param name="action">The action, typically <c>command.Run(context, args)</c>.</param>
    /// <returns>
    ///     The failure as a <see cref="ToolException"/>: the exception itself when the command line code threw it, and the
    ///     same code, message, and hint in a <see cref="ToolException"/> when the Viewer library threw a
    ///     <see cref="ViewerException"/> (which the command registry does for every command that runs through it).
    /// </returns>
    /// <remarks>
    ///     A command that is run directly (not through the registry) lets a library failure escape as it is, so the
    ///     tests of the commands accept either type and look at the stable code that both carry.
    /// </remarks>
    public static ToolException ExpectFailure(Action action)
    {
        var failure = Assert.ThrowsAny<ViewerException>(action);
        return failure as ToolException ?? new ToolException(failure.Code, failure.Message, failure.Hint, failure.InnerException);
    }

    /// <summary>
    ///     Runs an action while capturing standard output and standard error.
    /// </summary>
    /// <param name="action">The action to run.</param>
    /// <returns>The captured standard output and standard error.</returns>
    public static (string StdOut, string StdErr) Capture(Action action)
    {
        var (_, stdOut, stdErr) = Capture(() =>
        {
            action();
            return 0;
        });
        return (stdOut, stdErr);
    }

    /// <summary>
    ///     Runs a function while capturing standard output and standard error.
    /// </summary>
    /// <typeparam name="T">The function's result type.</typeparam>
    /// <param name="function">The function to run.</param>
    /// <returns>The function's result and the captured standard output and standard error.</returns>
    public static (T Result, string StdOut, string StdErr) Capture<T>(Func<T> function)
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        try
        {
            Console.SetOut(outWriter);
            Console.SetError(errWriter);
            var result = function();
            return (result, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
