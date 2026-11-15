using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;
using System.Text;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Mermaid;
using DemaConsulting.CanvasNet.Tool.Commands;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     A note the tool reported while drawing a picture.
/// </summary>
/// <param name="Code">The stable code, for example <c>mermaid-diagram-unsupported</c>.</param>
/// <param name="Message">The message.</param>
/// <param name="Embedded">Whether the note is about a file the picture embeds (it has an origin) rather than the picture itself.</param>
internal sealed record PictureDiagnostic(string Code, string Message, bool Embedded);

/// <summary>
///     What the tool reported for a picture it drew.
/// </summary>
/// <param name="Flattened">For a drawing, whether a flattened copy was written; <see langword="true"/> for a render.</param>
/// <param name="Diagnostics">The notes, in order.</param>
/// <param name="Echo">The text a model printed, in order.</param>
internal sealed record PictureDrawing(bool Flattened, IReadOnlyList<PictureDiagnostic> Diagnostics, IReadOnlyList<string> Echo);

/// <summary>
///     Draws a picture as <c>canvasnet render</c> or <c>canvasnet flatten</c> would: the seam between the engine and the
///     tool's commands, so that the engine can be tested with a stub.
/// </summary>
/// <remarks>An implementation must be safe to call from several threads at once.</remarks>
internal interface IPictureDrawer
{
    /// <summary>
    ///     Renders a picture.
    /// </summary>
    /// <param name="args">The arguments of <c>render</c>: the source, the options, the file-access arguments, and <c>--output FILE</c>.</param>
    /// <returns>What the tool reported.</returns>
    /// <exception cref="ViewerException">Thrown with a stable code when the picture cannot be drawn.</exception>
    PictureDrawing Render(IReadOnlyList<string> args);

    /// <summary>
    ///     Flattens an SVG drawing.
    /// </summary>
    /// <param name="args">The arguments of <c>flatten</c>: the drawing, the options, the file-access arguments, and <c>--output FILE</c>.</param>
    /// <returns>What the tool reported, and whether a copy was written.</returns>
    /// <exception cref="ViewerException">Thrown with a stable code when the drawing cannot be flattened.</exception>
    PictureDrawing Flatten(IReadOnlyList<string> args);
}

/// <summary>
///     The production drawer: runs <see cref="RenderCommand.Execute"/> and <see cref="FlattenCommand.Execute"/> in the
///     process, with the arguments the Lua filter passed to <c>canvasnet render ... --json</c> and
///     <c>canvasnet flatten ... --json</c>, so a picture is drawn exactly as those commands draw it.
/// </summary>
/// <remarks>Stateless and safe to call from several threads at once.</remarks>
internal sealed class ToolPictureDrawer : IPictureDrawer
{
    /// <inheritdoc />
    public PictureDrawing Render(IReadOnlyList<string> args)
    {
        var (_, _, result) = RenderCommand.Execute(args);
        return new PictureDrawing(
            true,
            [.. result.Pages.SelectMany(page => page.Diagnostics).Select(d => new PictureDiagnostic(d.Code, d.Message, d.Origin.Count > 0))],
            [.. result.Pages.SelectMany(page => page.Echo)]);
    }

    /// <inheritdoc />
    public PictureDrawing Flatten(IReadOnlyList<string> args)
    {
        var (_, result, _, _) = FlattenCommand.Execute(args);
        return new PictureDrawing(
            result.Flattened,
            [.. result.Diagnostics.Select(d => new PictureDiagnostic(d.Code, d.Message, d.Origin.Count > 0))],
            [.. result.Echo]);
    }
}

/// <summary>
///     The drawing step of <see cref="PandocFilterEngine"/>: the jobs, run up to <c>jobs</c> at once, and what each
///     produced.
/// </summary>
internal sealed partial class PandocFilterEngine
{
    /// <summary>
    ///     Runs the jobs, in order of position, up to <see cref="_jobs"/> at once.
    /// </summary>
    /// <param name="jobs">The jobs, sorted by position.</param>
    /// <param name="limit">The position of a failure already known (a failed plan), or <see cref="int.MaxValue"/>.</param>
    /// <returns>The failures of the jobs.</returns>
    /// <remarks>
    ///     A job above a known failure is skipped; a job below every known failure always runs. Every exception of a job is
    ///     recorded, a defect too, so the caller reports the one of the lowest position after every worker has finished.
    /// </remarks>
    private List<Failure> Draw(List<DrawJob> jobs, int limit)
    {
        var lowest = new[] { limit };
        var failures = new ConcurrentBag<Failure>();
        void RunJob(DrawJob job)
        {
            if (job.Index > Volatile.Read(ref lowest[0]))
            {
                return;
            }

            try
            {
                job.Outcome = job.Svg ? Flatten(job) : Render(job);
            }
            // Generic catch is justified here: every failure, a defect too, is recorded with its position, and the one of
            // the lowest position is rethrown once every worker has finished
            catch (Exception ex)
            {
                failures.Add(new Failure(job.Index, ExceptionDispatchInfo.Capture(ex)));
                int seen;
                do
                {
                    seen = Volatile.Read(ref lowest[0]);
                }
                while (job.Index < seen && Interlocked.CompareExchange(ref lowest[0], job.Index, seen) != seen);
            }
        }

        if (_jobs == 1 || jobs.Count < 2)
        {
            jobs.ForEach(RunJob);
        }
        else
        {
            // No buffering, so the jobs start in order of position
            var partitioner = Partitioner.Create(jobs, EnumerablePartitionerOptions.NoBuffering);
            Parallel.ForEach(partitioner, new ParallelOptions { MaxDegreeOfParallelism = _jobs }, RunJob);
        }

        return [.. failures];
    }

    /// <summary>
    ///     Renders a picture into the cache (<c>draw</c>, from the render on).
    /// </summary>
    /// <param name="job">The job.</param>
    /// <returns>What the job produced.</returns>
    /// <exception cref="ToolException">
    ///     Thrown when the tool fails (<c>drawing '...' failed: ...</c>, with the tool's code), a restrictive mode denied a
    ///     file, or the picture cannot be written.
    /// </exception>
    private JobOutcome Render(DrawJob job)
    {
        var partial = PictureCache.PartialFile(job.Cache, job.Key, "png");
        PictureDrawing drawing;
        try
        {
            drawing = _drawer.Render([job.Source, .. job.Options, .. job.Access.Arguments, "--output", partial]);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            PictureCache.Delete(partial);
            throw ToolFailure($"drawing '{job.Name}' failed: ", ex);
        }

        FailOnDenial(job, drawing, partial);

        // Only the diagram itself being a type the tool does not draw leaves the picture alone; the same report about a
        // picture embedded in it is an ordinary note
        if (job.Kind == PictureAttributes.Mermaid
            && drawing.Diagnostics.FirstOrDefault(d => d.Code == MermaidDiagnosticCodes.DiagramUnsupported && !d.Embedded) is { } unsupported)
        {
            PictureCache.Delete(partial);
            PictureCache.WriteTextAtomically(job.Marker, unsupported.Message);
            return new JobOutcome(false, [$"{job.Name}: {unsupported.Message}{LeftAlone}"], unsupported.Message, null);
        }

        var warnings = drawing.Diagnostics.Select(d => $"{job.Name}: {d.Message}").Concat(drawing.Echo.Select(line => $"{job.Name}: {line}")).ToList();
        PictureCache.Commit(partial, job.File, $"could not write the picture of '{job.Name}' to {job.File}");
        return new JobOutcome(true, warnings, null, null);
    }

    /// <summary>
    ///     Flattens a drawing into the cache (<c>flatten_svg</c>, from the flatten on).
    /// </summary>
    /// <param name="job">The job.</param>
    /// <returns>What the job produced.</returns>
    /// <exception cref="ToolException">
    ///     Thrown when the tool fails (<c>flattening '...' failed: ...</c>, with the tool's code), a restrictive mode denied
    ///     a file, or the copy cannot be written.
    /// </exception>
    private JobOutcome Flatten(DrawJob job)
    {
        var partial = PictureCache.PartialFile(job.Cache, job.Key, "svg");
        PictureDrawing drawing;
        try
        {
            drawing = _drawer.Flatten([job.Source, .. job.Options, .. job.Access.Arguments, "--output", partial]);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            PictureCache.Delete(partial);
            throw ToolFailure($"flattening '{job.Name}' failed: ", ex);
        }

        // A denial stops the build before a marker or a copy is written
        FailOnDenial(job, drawing, partial);
        var messages = drawing.Diagnostics.Select(d => OneLine(d.Message)).ToList();
        var warnings = messages.Select(m => $"{job.Name}: {m}").Concat(drawing.Echo.Select(line => $"{job.Name}: {line}")).ToList();
        if (!drawing.Flattened)
        {
            // The marker keeps the warnings, so a later build reports them again without starting the tool
            PictureCache.Delete(partial);
            PictureCache.WriteTextAtomically(job.Marker, string.Join('\n', messages) + "\n");
            return new JobOutcome(false, warnings, null, [.. messages.Where(m => m.Length > 0)]);
        }

        PictureCache.Commit(partial, job.File, $"could not write the flattened copy of '{job.Name}' to {job.File}");
        return new JobOutcome(true, warnings, null, null);
    }

    /// <summary>
    ///     Stops the build for a picture a restrictive file-access mode denied (<c>fail_on_denial</c>): a document must not be
    ///     published with a picture silently missing.
    /// </summary>
    /// <param name="job">The job.</param>
    /// <param name="drawing">What the tool reported.</param>
    /// <param name="partial">The temporary file, removed on a denial.</param>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.FileAccessDenied"/> for a denied picture, include, or data file.</exception>
    private static void FailOnDenial(DrawJob job, PictureDrawing drawing, string partial)
    {
        if (!job.Access.Restrictive)
        {
            return;
        }

        var denied = drawing.Diagnostics.FirstOrDefault(d => d.Code == FolderImageResolver.AccessDeniedCode || d.Code == ErrorCodes.FileAccessDenied);
        if (denied != null)
        {
            PictureCache.Delete(partial);
            throw new ToolException(
                ErrorCodes.FileAccessDenied,
                $"drawing '{job.Name}' failed: {denied.Message} (denied by {FilterSettings.FileAccessVariable}/{FilterSettings.RootsVariable})");
        }
    }

    /// <summary>
    ///     Creates the error of a failed render or flatten: the tool's message and hint after a prefix, with the code the
    ///     tool would have reported in its JSON failure document.
    /// </summary>
    /// <param name="prefix">The prefix, for example <c>drawing 'flow.mmd' failed: </c>.</param>
    /// <param name="ex">The tool's failure.</param>
    /// <returns>The exception to throw.</returns>
    private static ToolException ToolFailure(string prefix, Exception ex)
    {
        var (code, hint) = ex switch
        {
            ViewerException viewer => (viewer.Code, viewer.Hint),
            ArgumentException => (ErrorCodes.UsageError, null),
            _ => (ErrorCodes.OperationFailed, (string?)null)
        };
        var message = hint == null ? ex.Message : $"{ex.Message} ({hint})";
        return new ToolException(code, prefix + message, null, ex);
    }

    /// <summary>
    ///     Makes a message one line: every run of carriage returns and line feeds becomes one space.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The message on one line.</returns>
    private static string OneLine(string message)
    {
        var builder = new StringBuilder(message.Length);
        var i = 0;
        while (i < message.Length)
        {
            if (message[i] is not ('\r' or '\n'))
            {
                builder.Append(message[i++]);
                continue;
            }

            builder.Append(' ');
            while (i < message.Length && message[i] is '\r' or '\n')
            {
                i++;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    ///     A picture to draw: everything the render or flatten needs, and, once it ran, what it produced.
    /// </summary>
    /// <param name="Index">The position of the picture that leads the job.</param>
    /// <param name="Name">The name of that picture, for messages.</param>
    /// <param name="Svg">Whether the job flattens a drawing rather than rendering a picture.</param>
    /// <param name="Kind">The kind of the picture, or <c>svg</c>.</param>
    /// <param name="Source">The source file.</param>
    /// <param name="Options">The tool options.</param>
    /// <param name="Access">The file-access arguments.</param>
    /// <param name="Cache">The cache folder.</param>
    /// <param name="Key">The cache key.</param>
    /// <param name="File">The picture or copy the job writes.</param>
    /// <param name="Marker">The marker the job writes when the picture is left alone.</param>
    private sealed record DrawJob(
        int Index,
        string Name,
        bool Svg,
        string Kind,
        string Source,
        IReadOnlyList<string> Options,
        FileAccessArguments Access,
        string Cache,
        string Key,
        string File,
        string Marker)
    {
        /// <summary>
        ///     Gets or sets what the job produced, once it ran.
        /// </summary>
        public JobOutcome? Outcome { get; set; }
    }

    /// <summary>
    ///     What a job produced.
    /// </summary>
    /// <param name="Drawn">Whether the picture or copy was written (the elements point at it).</param>
    /// <param name="Warnings">The warnings of the picture that led the job.</param>
    /// <param name="UnsupportedMessage">For a diagram left as written, the tool's message; otherwise <see langword="null"/>.</param>
    /// <param name="PlainLines">For a drawing with nothing to flatten, the lines of its marker; otherwise <see langword="null"/>.</param>
    private sealed record JobOutcome(bool Drawn, IReadOnlyList<string> Warnings, string? UnsupportedMessage, IReadOnlyList<string>? PlainLines)
    {
        /// <summary>
        ///     Gets the warnings of a later picture with the same key, which the Lua filter found in the cache: the marker of a
        ///     diagram left as written or of a drawing with nothing to flatten; none for a drawn picture.
        /// </summary>
        /// <param name="name">The name of the later picture.</param>
        /// <returns>The warnings.</returns>
        public IEnumerable<string> FollowerWarnings(string name)
        {
            if (UnsupportedMessage != null)
            {
                return [$"{name}: {UnsupportedMessage}{LeftAlone}"];
            }

            return (PlainLines ?? []).Select(line => $"{name}: {line}");
        }
    }
}
