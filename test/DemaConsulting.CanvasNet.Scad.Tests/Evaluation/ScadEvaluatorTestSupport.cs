using System.Text;
using DemaConsulting.CanvasNet.Scad.Evaluation;
using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Tests.Evaluation;

/// <summary>
///     Shared helpers for the evaluator tests: running source text and describing the results as plain text.
/// </summary>
internal static class ScadEvaluatorTestSupport
{
    /// <summary>
    ///     Cancels the source after a delay from a dedicated thread.
    /// </summary>
    /// <remarks>
    ///     <see cref="CancellationTokenSource.CancelAfter(TimeSpan)"/> fires its timer on the thread pool. The
    ///     evaluator blocks pool threads (every evaluation joins a dedicated thread), so with many test classes
    ///     running in parallel on a loaded machine the pool is starved and the timer callback can be delayed by
    ///     tens of seconds, which is what made cancellation tests exceed their bound on a loaded CI runner. A
    ///     dedicated thread does not depend on the pool.
    /// </remarks>
    /// <param name="cts">The source to cancel.</param>
    /// <param name="delay">The delay before canceling.</param>
    public static void CancelAfterOnDedicatedThread(CancellationTokenSource cts, TimeSpan delay) =>
        CancelAfterOnDedicatedThread(cts, delay, null);

    /// <summary>
    ///     Cancels the source from a dedicated thread once a readiness signal is set, after a further delay.
    /// </summary>
    /// <remarks>
    ///     A fixed delay alone can fire before a cold, slow runner (macOS CI) has even started evaluating; waiting for
    ///     the signal (for example "the first echo was produced", set from <see cref="ScadEvaluationSink.EchoRecorded"/>)
    ///     and only then delaying keeps the test deterministic. The signal is thread-safe, unlike polling the sink's
    ///     buffers. The wait is bounded so a signal that is never set still cancels eventually.
    /// </remarks>
    /// <param name="cts">The source to cancel.</param>
    /// <param name="delay">The delay, after the signal is set, before canceling.</param>
    /// <param name="ready">The signal to wait for, or <see langword="null"/> to cancel after the delay alone.</param>
    public static void CancelAfterOnDedicatedThread(CancellationTokenSource cts, TimeSpan delay, ManualResetEventSlim? ready)
    {
        var canceler = new Thread(() =>
        {
            // The wait is bounded and deliberately not tied to a token (the source may be disposed by then)
            ready?.Wait(TimeSpan.FromSeconds(60), CancellationToken.None);
            Thread.Sleep(delay);
            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The test already finished and disposed the source; nothing is left to cancel
            }
        })
        {
            IsBackground = true,
            Name = "ScadCancellationTestCanceler",
        };
        canceler.Start();
    }

    /// <summary>
    ///     Parses and evaluates source text.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="settings">The settings, or <see langword="null"/> for the defaults.</param>
    /// <param name="files">The virtual files that answer <c>include</c>/<c>use</c>, or <see langword="null"/> for none.</param>
    /// <param name="sink">The sink, or <see langword="null"/> to create one (pass one to inspect events after a failure).</param>
    /// <returns>The evaluation output.</returns>
    public static ScadEvaluationOutput Run(
        string source,
        ScadEvaluationSettings? settings = null,
        ScadVirtualFiles? files = null,
        ScadEvaluationSink? sink = null)
    {
        var effective = settings ?? new ScadEvaluationSettings();
        var options = files is null
            ? new ScadOpenOptions()
            : new ScadOpenOptions { FileResolver = files.Resolve, SourceName = "main.scad" };
        var document = ScadDocument.Parse(source, options);
        return ScadEvaluator.Evaluate(document, effective, sink ?? new ScadEvaluationSink(effective.Limits));
    }

    /// <summary>
    ///     Creates settings with the given limits.
    /// </summary>
    /// <param name="limits">The limits.</param>
    /// <returns>The settings.</returns>
    public static ScadEvaluationSettings With(ScadLimits limits) => new() { Limits = limits };

    /// <summary>
    ///     Evaluates source text and returns its echo lines.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <returns>The echo lines including the <c>ECHO: </c> prefix.</returns>
    public static string[] Echo(string source) => [.. Run(source).Echo];

    /// <summary>
    ///     Evaluates source text and returns its echo lines without the <c>ECHO: </c> prefix, joined with <c>|</c>.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <returns>The joined text.</returns>
    public static string EchoText(string source) =>
        string.Join('|', Run(source).Echo.Select(line => line["ECHO: ".Length..]));

    /// <summary>
    ///     Evaluates source text and returns its warnings as <c>line:column: message</c>.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <returns>The warnings.</returns>
    public static string[] Warnings(string source) =>
        [.. Run(source).Diagnostics.Select(d => $"{d.Location}: {d.Message}")];

    /// <summary>
    ///     Evaluates source text and describes the instantiation tree, see <see cref="ScadInstanceDumper"/>.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <returns>The dump.</returns>
    public static string Tree(string source) => ScadInstanceDumper.Dump(Run(source).Roots);

    /// <summary>
    ///     Evaluates source text that must fail and returns the evaluation exception.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="settings">The settings, or <see langword="null"/>.</param>
    /// <returns>The exception.</returns>
    public static ScadEvaluationException Fails(string source, ScadEvaluationSettings? settings = null) =>
        Assert.Throws<ScadEvaluationException>(() => Run(source, settings));
}

/// <summary>
///     Describes an instantiation tree as location-free text so tests can compare whole shapes.
/// </summary>
/// <remarks>
///     A built-in module is <c>name(arg, name=value)</c>, a group is <c>group&lt;name&gt;</c>, children follow in
///     braces separated by <c>;</c>, modifiers lead as <c>!</c>, <c>#</c> and <c>%</c>, and roots are joined by <c>;</c>.
/// </remarks>
internal static class ScadInstanceDumper
{
    /// <summary>
    ///     Describes a list of sibling instances.
    /// </summary>
    /// <param name="instances">The instances.</param>
    /// <returns>The dump.</returns>
    public static string Dump(IReadOnlyList<ScadInstance> instances)
    {
        // The walk is iterative on purpose: trees can be thousands of levels deep and tests dump them on small stacks
        var text = new StringBuilder();
        var work = new Stack<object>();
        for (var i = instances.Count - 1; i >= 0; i--)
        {
            work.Push(instances[i]);
            if (i > 0)
            {
                work.Push(";");
            }
        }

        while (work.Count > 0)
        {
            var item = work.Pop();
            if (item is string literal)
            {
                text.Append(literal);
            }
            else
            {
                AppendHead(text, (ScadInstance)item, work);
            }
        }

        return text.ToString();
    }

    /// <summary>
    ///     Describes one instance with its subtree.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The dump.</returns>
    public static string Dump(ScadInstance instance) => Dump([instance]);

    /// <summary>
    ///     Describes a list of sibling instances one per line, children indented by two spaces, with the fragment
    ///     settings that differ from the defaults appended as <c>$fn=…</c>, <c>$fa=…</c>, <c>$fs=…</c>.
    /// </summary>
    /// <param name="instances">The instances.</param>
    /// <returns>The lines joined by <c>\n</c> (no trailing line break).</returns>
    public static string DumpIndented(IReadOnlyList<ScadInstance> instances)
    {
        // Iterative for the same reason as Dump: the tree can be very deep
        var lines = new List<string>();
        var work = new Stack<(ScadInstance Instance, int Depth)>();
        for (var i = instances.Count - 1; i >= 0; i--)
        {
            work.Push((instances[i], 0));
        }

        while (work.Count > 0)
        {
            var (instance, depth) = work.Pop();
            lines.Add(new string(' ', depth * 2) + Head(instance) + Fragments(instance));
            for (var i = instance.Children.Count - 1; i >= 0; i--)
            {
                work.Push((instance.Children[i], depth + 1));
            }
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    ///     Describes an instance without its children: modifiers, then <c>name(arguments)</c> or <c>group&lt;name&gt;</c>.
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The description.</returns>
    private static string Head(ScadInstance instance)
    {
        var text = new StringBuilder();
        if ((instance.Modifiers & ScadModifiers.ShowOnly) != 0)
        {
            text.Append('!');
        }

        if ((instance.Modifiers & ScadModifiers.Highlight) != 0)
        {
            text.Append('#');
        }

        if ((instance.Modifiers & ScadModifiers.Background) != 0)
        {
            text.Append('%');
        }

        if (instance.Kind == ScadInstanceKind.Group)
        {
            text.Append("group<").Append(instance.Name).Append('>');
        }
        else
        {
            text.Append(instance.Name).Append('(').Append(string.Join(", ", instance.Arguments.Select(Describe))).Append(')');
        }

        return text.ToString();
    }

    /// <summary>
    ///     Describes the fragment settings of a built-in instance that differ from the defaults (0, 12, 2).
    /// </summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The text, empty when nothing differs or the instance is a group.</returns>
    private static string Fragments(ScadInstance instance)
    {
        if (instance.Kind == ScadInstanceKind.Group)
        {
            return string.Empty;
        }

        var text = new StringBuilder();
        if (Differs(instance.Fragments.Fn, 0))
        {
            text.Append(" $fn=").Append(ScadNumberFormatter.Format(instance.Fragments.Fn));
        }

        if (Differs(instance.Fragments.Fa, 12))
        {
            text.Append(" $fa=").Append(ScadNumberFormatter.Format(instance.Fragments.Fa));
        }

        if (Differs(instance.Fragments.Fs, 2))
        {
            text.Append(" $fs=").Append(ScadNumberFormatter.Format(instance.Fragments.Fs));
        }

        return text.ToString();
    }

    /// <summary>
    ///     Determines whether a fragment setting differs from its default.
    /// </summary>
    /// <param name="value">The setting.</param>
    /// <param name="defaultValue">The default.</param>
    /// <returns><see langword="true"/> when they differ.</returns>
    private static bool Differs(double value, double defaultValue) => Math.Abs(value - defaultValue) > 0;

    /// <summary>
    ///     Appends the description of an instance without its children and schedules the children.
    /// </summary>
    /// <param name="text">The output.</param>
    /// <param name="instance">The instance.</param>
    /// <param name="work">The work stack; the braces and the children are pushed onto it in reverse order.</param>
    private static void AppendHead(StringBuilder text, ScadInstance instance, Stack<object> work)
    {
        text.Append(Head(instance));

        if (instance.Children.Count > 0)
        {
            text.Append('{');
            work.Push("}");
            for (var i = instance.Children.Count - 1; i >= 0; i--)
            {
                work.Push(instance.Children[i]);
                if (i > 0)
                {
                    work.Push(";");
                }
            }
        }
    }

    /// <summary>
    ///     Describes an argument.
    /// </summary>
    /// <param name="argument">The argument.</param>
    /// <returns><c>value</c> or <c>name=value</c>.</returns>
    private static string Describe(ScadInstanceArgument argument) =>
        argument.Name is null ? argument.Value.ToEchoText() : $"{argument.Name}={argument.Value.ToEchoText()}";
}
