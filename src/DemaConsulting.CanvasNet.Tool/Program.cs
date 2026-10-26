// cspell:ignore canvasnet
using System.Reflection;
using DemaConsulting.CanvasNet.Tool.Commands;

namespace DemaConsulting.CanvasNet.Tool;

/// <summary>
///     Main program entry point for the CanvasNet Tool.
/// </summary>
internal static class Program
{
    /// <summary>
    ///     Gets the application version string.
    /// </summary>
    /// <remarks>
    ///     The version is read from the <see cref="AssemblyInformationalVersionAttribute"/> via
    ///     reflection on every access. There is no caching; callers that need the value more than
    ///     once should store the result locally.
    /// </remarks>
    public static string Version
    {
        get
        {
            // Get the assembly containing this program
            var assembly = typeof(Program).Assembly;

            // Try to get version from assembly attributes, fallback to AssemblyVersion, or default to 0.0.0
            return assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                   ?? assembly.GetName().Version?.ToString()
                   ?? "0.0.0";
        }
    }

    /// <summary>
    ///     Main entry point for the CanvasNet Tool.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    /// <exception cref="Exception">Thrown when an unexpected error occurs; re-thrown after writing to stderr.</exception>
    /// <remarks>
    ///     See <see cref="Execute"/> for the error-handling contract.
    /// </remarks>
    public static int Main(string[] args)
    {
        return Execute(args, CommandRegistry.CreateDefault());
    }

    /// <summary>
    ///     Runs the tool against a given command registry, with the tool's error handling.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <param name="registry">The registry of commands the tool offers; a test seam for <see cref="Main"/>.</param>
    /// <returns>Exit code: 0 for success, 1 for failure.</returns>
    /// <exception cref="Exception">Thrown when an unexpected error occurs; re-thrown after writing to stderr.</exception>
    /// <remarks>
    ///     <see cref="ArgumentException"/> and <see cref="InvalidOperationException"/> (which includes
    ///     <see cref="ToolException"/>) are treated as expected errors: a concise message is written to
    ///     stderr and exit code 1 is returned without a stack trace; when the command line contains
    ///     <c>--json</c> and not <c>--silent</c> the failure is also written to stdout as a JSON document.
    ///     The report goes through the <see cref="Context"/> (so <c>--silent</c> and <c>--log</c> apply) whenever one
    ///     exists, and to the console only when none could be created.
    ///     Any other exception is a defect: it
    ///     is written to stderr and then re-thrown so that the runtime can record it in event logs.
    /// </remarks>
    internal static int Execute(string[] args, CommandRegistry registry)
    {
        // The context, once created, carries --silent and --log, so errors are reported through it; the command
        // name identifies the command in the JSON error document
        Context? context = null;
        string? commandName = null;
        try
        {
            // Create context from command-line arguments
            context = Context.Create(args);
            commandName = context.Command;

            // Run the program logic
            Run(context, registry);

            // Return the exit code from the context
            return context.ExitCode;
        }
        catch (ArgumentException ex)
        {
            // Print expected argument exceptions and return error code
            ReportExpected(context, args, commandName, ex);
            return 1;
        }
        catch (InvalidOperationException ex)
        {
            // Print expected operation exceptions (including ToolException) and return error code
            ReportExpected(context, args, commandName, ex);
            return 1;
        }
        catch (Exception ex)
        {
            // Print unexpected exceptions and re-throw to generate event logs
            WriteError(context, $"Unexpected error: {ex.Message}");
            throw;
        }
        finally
        {
            // The context is disposed only now, so that the error reports above reach the log file
            context?.Dispose();
        }
    }

    /// <summary>
    ///     Runs the program logic based on the provided context.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <remarks>
    ///     Dispatch is priority-ordered: version query first, then help, then self-validation, then
    ///     the selected command. Only the highest-priority matching action is executed per invocation.
    ///     The banner is printed only for help, self-validation, and an empty command line, so that the
    ///     output of a command is exactly the command's own output.
    /// </remarks>
    public static void Run(Context context)
    {
        Run(context, CommandRegistry.CreateDefault());
    }

    /// <summary>
    ///     Runs the program logic against a given command registry.
    /// </summary>
    /// <param name="context">The context containing command line arguments and program state.</param>
    /// <param name="registry">The registry of commands the tool offers.</param>
    /// <exception cref="ToolException">Thrown for an expected failure of the selected command.</exception>
    internal static void Run(Context context, CommandRegistry registry)
    {
        // Priority 1: Version query
        if (context.Version)
        {
            context.WriteLine(Version);
            return;
        }

        // Priority 2: Help (the command's own help when a command was named)
        if (context.Help)
        {
            PrintBanner(context);
            context.WriteLine(registry.RenderHelp(context.Command));
            return;
        }

        // Priority 3: Self-Validation
        if (context.Validate)
        {
            PrintBanner(context);
            Validation.Run(context);
            return;
        }

        // Priority 4: The selected command
        if (context.Command != null)
        {
            registry.Run(context);
            return;
        }

        // Nothing was asked for: identify the tool and point at the help
        PrintBanner(context);
        context.WriteLine("Usage: canvasnet <command> [options] (try --help)");
    }

    /// <summary>
    ///     Writes an expected error to standard error and, when requested, as a JSON document to standard output.
    /// </summary>
    /// <param name="context">
    ///     The active context, or <see langword="null"/> when none could be created (a malformed global option, or a
    ///     log file that cannot be opened).
    /// </param>
    /// <param name="args">The command-line arguments, scanned for <c>--json</c> and <c>--silent</c> when there is no context.</param>
    /// <param name="commandName">The command named on the command line, or <see langword="null"/> when unknown.</param>
    /// <param name="ex">The expected exception.</param>
    /// <remarks>
    ///     A <see cref="ToolException"/> is reported as <c>Error: {code}: {message}</c> followed by an
    ///     optional <c>Hint:</c> line; any other expected exception as <c>Error: {message}</c>. When the
    ///     failure is reported through the context, <c>--silent</c> suppresses the console output and the
    ///     <c>--log</c> file receives the report, exactly as for any other output; when the context asks for
    ///     <c>--json</c> the failure is also written as the JSON failure document (see <see cref="ResultWriter"/>)
    ///     through the context, so <c>--silent</c> suppresses it too. Without a context nothing can be honored, so the
    ///     report goes to the console, and the command line is scanned directly for <c>--json</c> and <c>--silent</c>.
    /// </remarks>
    private static void ReportExpected(Context? context, string[]? args, string? commandName, Exception ex)
    {
        if (ex is ToolException toolException)
        {
            WriteError(context, $"Error: {toolException.Code}: {toolException.Message}");
            if (toolException.Hint != null)
            {
                WriteError(context, $"Hint: {toolException.Hint}");
            }
        }
        else
        {
            WriteError(context, $"Error: {ex.Message}");
        }

        var failedCommand = (ex as ToolException)?.Command ?? commandName;
        if (context != null)
        {
            if (context.Json)
            {
                context.WriteLine(ResultWriter.Failure(failedCommand, ex));
            }
        }
        else if (args != null && args.Contains("--json") && !args.Contains("--silent"))
        {
            Console.Out.WriteLine(ResultWriter.Failure(failedCommand, ex));
        }
    }

    /// <summary>
    ///     Writes an error line through the context, or to standard error when there is no context.
    /// </summary>
    /// <param name="context">The active context, or <see langword="null"/> when none could be created.</param>
    /// <param name="message">The line to write.</param>
    private static void WriteError(Context? context, string message)
    {
        if (context != null)
        {
            context.WriteError(message);
        }
        else
        {
            Console.Error.WriteLine(message);
        }
    }

    /// <summary>
    ///     Prints the application banner.
    /// </summary>
    /// <param name="context">The context for output.</param>
    private static void PrintBanner(Context context)
    {
        context.WriteLine($"CanvasNet Tool version {Version}");
        context.WriteLine("Copyright (c) DEMA Consulting");
        context.WriteLine("");
    }
}
