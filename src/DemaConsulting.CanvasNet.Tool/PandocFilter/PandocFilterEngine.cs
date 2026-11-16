using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using DemaConsulting.CanvasNet.Viewer;

namespace DemaConsulting.CanvasNet.Tool.PandocFilter;

/// <summary>
///     Draws the pictures of a Pandoc document in one process: the logic of the Lua filter (version 16), run once for the
///     whole document instead of once per picture.
/// </summary>
/// <remarks>
///     <para>
///         The run has three steps. <b>Plan</b>, sequentially in the order Pandoc's Lua filter visited the elements (every
///         <c>Image</c>, then every <c>CodeBlock</c>, see <see cref="PandocAst.FindAll"/>): check the attributes, find the
///         source, hash its files, compute the key, and look the picture up in the cache; a picture that misses becomes a
///         <see cref="DrawJob"/>, and a second picture with the same key follows the first one's job. <b>Draw</b> the jobs,
///         up to <c>jobs</c> at once. <b>Apply</b>, sequentially in document order: rewrite the elements and collect the
///         warnings, which the caller returns in the metadata.
///     </para>
///     <para>
///         A failure is recorded with the position of its picture. Planning stops at the first failure; a job above a
///         known failure may be skipped, and a job below it always runs, so the reported failure is always the one with the
///         lowest position, the one the Lua filter (which stopped at the first failure) reported, however the jobs were
///         scheduled. A job draws into a temporary file; the pictures, copies, and markers are committed into the cache
///         strictly in document order and only below the first failure, and the temporary files of the jobs after it are
///         removed. The output, the warnings, and the cache files are therefore the same for one job or many, also when
///         the run fails. The engine is used for one run at a time.
///     </para>
/// </remarks>
internal sealed partial class PandocFilterEngine
{
    /// <summary>
    ///     The text a warning about a diagram left as written ends with.
    /// </summary>
    private const string LeftAlone = "; it is left as written and not drawn";

    /// <summary>
    ///     The inputs of the run.
    /// </summary>
    private readonly FilterSettings _settings;

    /// <summary>
    ///     The drawer of the pictures that miss the cache.
    /// </summary>
    private readonly IPictureDrawer _drawer;

    /// <summary>
    ///     The most pictures drawn at once.
    /// </summary>
    private readonly int _jobs;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PandocFilterEngine"/> class.
    /// </summary>
    /// <param name="settings">The inputs of the run.</param>
    /// <param name="drawer">The drawer of the pictures that miss the cache: <see cref="ToolPictureDrawer"/>, or a test stub.</param>
    /// <param name="jobs">The most pictures drawn at once; 1 draws them one after the other.</param>
    internal PandocFilterEngine(FilterSettings settings, IPictureDrawer drawer, int jobs)
    {
        _settings = settings;
        _drawer = drawer;
        _jobs = Math.Max(1, jobs);
    }

    /// <summary>
    ///     Gets the warnings of the pictures before the failure when the last run failed, in document order (empty when it
    ///     succeeded), so the caller can show them as the Lua filter did before its error.
    /// </summary>
    internal IReadOnlyList<string> WarningsBeforeFailure { get; private set; } = [];

    /// <summary>
    ///     Reads a Pandoc document, draws its pictures, and writes the transformed document.
    /// </summary>
    /// <param name="input">The document as Pandoc's JSON (UTF-8).</param>
    /// <param name="pandocVersion">The version of Pandoc, for messages, or <see langword="null"/>.</param>
    /// <returns>The transformed document as UTF-8 JSON, with the warnings in the metadata key <see cref="PandocAst.WarningsKey"/>.</returns>
    /// <exception cref="ToolException">
    ///     Thrown with <see cref="ErrorCodes.CorruptInput"/> for an input that is not a Pandoc 3 document, with
    ///     <see cref="ErrorCodes.PandocFilterError"/> for an attribute, path, limit, environment, or cache error, and with the
    ///     library's code for a picture that cannot be drawn; the message is the Lua filter's.
    /// </exception>
    /// <remarks>Writes the pictures, copies, markers, and the files of Mermaid code blocks into the cache folder.</remarks>
    internal byte[] Run(ReadOnlySpan<byte> input, string? pandocVersion)
    {
        var document = PandocAst.Parse(input, pandocVersion);
        var warnings = Transform(document);
        PandocAst.SetWarnings(document, warnings);
        return PandocAst.Write(document);
    }

    /// <summary>
    ///     Draws the pictures of a document and rewrites its elements in place.
    /// </summary>
    /// <param name="document">The document's root object.</param>
    /// <returns>The warnings, in document order, each <c>&lt;name&gt;: &lt;message&gt;</c>.</returns>
    /// <exception cref="ToolException">Thrown as <see cref="Run"/> describes; the document is then left partly unchanged.</exception>
    internal IReadOnlyList<string> Transform(JsonObject document)
    {
        WarningsBeforeFailure = [];

        // The elements in the order the Lua filter visited them: every image, then every code block
        var candidates = PandocAst.FindAll(document, "Image").Select(node => (Node: node, Block: false))
            .Concat(PandocAst.FindAll(document, "CodeBlock").Select(node => (Node: node, Block: true)))
            .ToList();

        // Plan, in that order, up to the first failure
        var planned = new List<PlannedPicture>();
        var jobs = new Dictionary<string, DrawJob>(StringComparer.Ordinal);
        var failures = new List<Failure>();
        for (var index = 0; index < candidates.Count; index++)
        {
            try
            {
                var (node, block) = candidates[index];
                var item = block ? PlanBlock(index, node, jobs) : PlanImage(index, node, jobs);
                if (item != null)
                {
                    planned.Add(item);
                }
            }
            catch (Exception ex) when (IsExpected(ex))
            {
                failures.Add(new Failure(index, ExceptionDispatchInfo.Capture(ex)));
                break;
            }
        }

        // Draw the pictures that missed the cache, commit them into the cache in document order up to the first failure,
        // then report the failure of the lowest position
        var ordered = jobs.Values.OrderBy(job => job.Index).ToList();
        failures.AddRange(Draw(ordered, failures.Count > 0 ? failures[0].Index : int.MaxValue));
        CommitInOrder(ordered, failures);
        if (failures.Count > 0)
        {
            // The warnings of the pictures before the failure, which the Lua filter printed before its error
            var failure = failures.MinBy(f => f.Index)!;
            var earlier = new List<string>();
            foreach (var item in planned.Where(item => item.Index < failure.Index))
            {
                CollectWarnings(item, earlier);
            }

            WarningsBeforeFailure = earlier;
            failure.Error.Throw();
        }

        // Apply, in document order
        var warnings = new List<string>();
        foreach (var item in planned)
        {
            Apply(item, warnings);
        }

        return warnings;
    }

    /// <summary>
    ///     Determines whether an exception is an expected failure (reported with its message) rather than a defect.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns><see langword="true"/> for the exceptions the tool reports as expected errors.</returns>
    private static bool IsExpected(Exception ex)
    {
        return ex is ArgumentException or InvalidOperationException;
    }

    /// <summary>
    ///     Plans an <c>Image</c>: an SVG drawing, a picture of a kind the tool draws, or nothing.
    /// </summary>
    /// <param name="index">The element's position.</param>
    /// <param name="node">The element.</param>
    /// <param name="jobs">The jobs of the run, by key.</param>
    /// <returns>The planned picture, or <see langword="null"/> when the element is left alone.</returns>
    private PlannedPicture? PlanImage(int index, JsonObject node, Dictionary<string, DrawJob> jobs)
    {
        var image = PandocAst.ReadImage(node);
        if (PictureAttributes.IsSvg(image.Src))
        {
            return PlanSvg(index, node, image, jobs);
        }

        var kind = PictureAttributes.KindOf(image.Src);
        if (kind == null)
        {
            return null;
        }

        var source = FilterPaths.LocateSource(image.Src, _settings.GetResourceDirectories())
                     ?? throw Fail($"cannot find '{image.Src}' in the document's resource path");
        var item = new PlannedPicture(index, image.Src, node, null, null);
        return PlanDraw(item, source, kind, image.Attr.Attributes, PictureCache.StemOf(source), jobs);
    }

    /// <summary>
    ///     Plans a <c>CodeBlock</c>: a block of class <c>mermaid</c> is written to the cache folder and drawn like a
    ///     <c>.mmd</c> picture; any other block is left alone.
    /// </summary>
    /// <param name="index">The element's position.</param>
    /// <param name="node">The element.</param>
    /// <param name="jobs">The jobs of the run, by key.</param>
    /// <returns>The planned picture, or <see langword="null"/> when the element is left alone.</returns>
    private PlannedPicture? PlanBlock(int index, JsonObject node, Dictionary<string, DrawJob> jobs)
    {
        var block = PandocAst.ReadCodeBlock(node);
        if (!block.Attr.Classes.Contains(PictureAttributes.Mermaid))
        {
            return null;
        }

        var name = PictureAttributes.BlockName(block.Attr.Id, block.Text);
        var (caption, attributes) = PictureAttributes.SplitCaption(block.Attr.Attributes);
        var text = block.Text + "\n";
        var source = PictureCache.BlockSourceFile(CreateCacheFolder(), text);
        PictureCache.WriteTextAtomically(source, text);
        var item = new PlannedPicture(index, name, node, block, caption);
        return PlanDraw(item, source, PictureAttributes.Mermaid, attributes, PictureAttributes.Mermaid, jobs);
    }

    /// <summary>
    ///     Plans a picture of a kind the tool draws (<c>draw</c>, up to the render): options, identity, file access,
    ///     dependencies, key, then the cache, the marker of a diagram left as written, or a job.
    /// </summary>
    /// <param name="item">The picture.</param>
    /// <param name="source">The source file.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="attributes">The attributes to read.</param>
    /// <param name="stem">The stem of the cache name.</param>
    /// <param name="jobs">The jobs of the run, by key.</param>
    /// <returns>The planned picture.</returns>
    private PlannedPicture PlanDraw(
        PlannedPicture item, string source, string kind, IReadOnlyList<KeyValuePair<string, string>> attributes, string stem, Dictionary<string, DrawJob> jobs)
    {
        var options = PictureAttributes.Build(item.Name, kind, attributes);
        item.Keep = options.Keep;
        var identity = _settings.ToolIdentity;
        var access = _settings.FileAccessOf(source);
        var parts = DependencyHasher.ForPicture(kind, source, item.Name, _settings.PolicyOf(access, source));
        var key = PictureCache.ComputeKey(kind, identity, access.Key, options.Options, null, parts);

        // A picture drawn before is used; a diagram the tool does not draw is left as written, with its warning
        var cache = CreateCacheFolder();
        var file = PictureCache.PictureFile(cache, stem, key);
        if (FilterPaths.FileExists(file))
        {
            item.File = file;
            return item;
        }

        var marker = PictureCache.UnsupportedMarker(cache, key);
        if (PictureCache.ReadText(marker) is { } known)
        {
            item.Warnings.Add($"{item.Name}: {known}{LeftAlone}");
            return item;
        }

        Attach(item, jobs, new DrawJob(item.Index, item.Name, false, kind, source, options.Options, access, cache, key, file, marker));
        return item;
    }

    /// <summary>
    ///     Plans an SVG drawing (<c>flatten_svg</c>, up to the flatten): left alone when Pandoc cannot find it or it links
    ///     no file only the tool draws; otherwise the marker of a drawing with nothing to flatten, the cache, or a job.
    /// </summary>
    /// <param name="index">The element's position.</param>
    /// <param name="node">The element.</param>
    /// <param name="image">The element as read.</param>
    /// <param name="jobs">The jobs of the run, by key.</param>
    /// <returns>The planned picture, or <see langword="null"/> when the drawing is left alone.</returns>
    private PlannedPicture? PlanSvg(int index, JsonObject node, PandocImage image, Dictionary<string, DrawJob> jobs)
    {
        // A drawing Pandoc cannot find is left to Pandoc, which reports it
        var source = FilterPaths.LocateSource(image.Src, _settings.GetResourceDirectories());
        if (source == null)
        {
            return null;
        }

        var root = FilterPaths.Directory(source);
        var access = _settings.FileAccessOf(source);
        var (parts, composite) = DependencyHasher.ForSvg(source, _settings.PolicyOf(access, source));
        if (!composite)
        {
            return null;
        }

        // The key names the drawing's folder as seen from the cache folder: the links the tool rebases lead there
        var item = new PlannedPicture(index, image.Src, node, null, null);
        var options = PictureAttributes.BuildSvg(image.Src, image.Attr.Attributes);
        item.Keep = options.Keep;
        var identity = _settings.ToolIdentity;
        var folder = FilterPaths.RelativePath(_settings.CacheFolder, root) ?? root;
        var key = PictureCache.ComputeKey("svg", identity, access.Key, options.Options, folder, parts);

        // The tool's warnings are kept in the marker, so a broken link is reported on every build
        var cache = CreateCacheFolder();
        var file = PictureCache.FlattenedFile(cache, PictureCache.StemOf(source), key);
        var marker = PictureCache.PlainMarker(cache, key);
        if (PictureCache.ReadText(marker) is { } known)
        {
            item.Warnings.AddRange(known.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => $"{image.Src}: {line}"));
            return item;
        }

        if (FilterPaths.FileExists(file))
        {
            item.File = file;
            return item;
        }

        Attach(item, jobs, new DrawJob(index, image.Src, true, "svg", source, options.Options, access, cache, key, file, marker));
        return item;
    }

    /// <summary>
    ///     Attaches a picture to the job of its key: a new job it leads, or the job of an earlier picture with the same key,
    ///     which it follows (the picture is drawn once).
    /// </summary>
    /// <param name="item">The picture.</param>
    /// <param name="jobs">The jobs of the run, by key.</param>
    /// <param name="job">The job to add when the key has none.</param>
    private static void Attach(PlannedPicture item, Dictionary<string, DrawJob> jobs, DrawJob job)
    {
        if (jobs.TryGetValue(job.Key, out var existing))
        {
            item.Job = existing;
            return;
        }

        jobs[job.Key] = job;
        item.Job = job;
        item.Leads = true;
    }

    /// <summary>
    ///     Applies the result of a planned picture: its warnings, and the rewritten element when a picture was drawn.
    /// </summary>
    /// <param name="item">The picture.</param>
    /// <param name="warnings">The warnings of the run.</param>
    private void Apply(PlannedPicture item, List<string> warnings)
    {
        CollectWarnings(item, warnings);
        var file = item.File;
        if (item.Job is { } job)
        {
            file = job.Outcome!.Drawn ? job.File : null;
        }

        if (file == null)
        {
            return;
        }

        var src = _settings.PictureSrc(file);
        if (item.Block is not { } block)
        {
            PandocAst.RewriteImage(item.Node, src, item.Keep);
            return;
        }

        // A code block becomes the picture: a figure with the caption, or a paragraph that holds the picture
        var classes = block.Attr.Classes.Where(c => c != PictureAttributes.Mermaid).ToList();
        JsonObject replacement;
        if (item.Caption is { } caption)
        {
            var image = PandocAst.Image(new PandocAttr(string.Empty, classes, item.Keep), [PandocAst.MarkdownPlaceholder(caption)], src);
            replacement = PandocAst.Figure(block.Attr.Id, [PandocAst.Plain(PandocAst.MarkdownPlaceholder(caption))], [PandocAst.Plain(image)]);
        }
        else
        {
            replacement = PandocAst.Para(PandocAst.Image(new PandocAttr(block.Attr.Id, classes, item.Keep), [], src));
        }

        item.Node.ReplaceWith(replacement);
    }

    /// <summary>
    ///     Adds the warnings of a planned picture: those found while planning, then those of its job.
    /// </summary>
    /// <param name="item">The picture, whose job (if any) has run.</param>
    /// <param name="warnings">The warnings to add to.</param>
    private static void CollectWarnings(PlannedPicture item, List<string> warnings)
    {
        warnings.AddRange(item.Warnings);
        if (item.Job?.Outcome is { } outcome)
        {
            // The picture that led the job reports what the tool said; a follower reports what a later build would
            warnings.AddRange(item.Leads ? outcome.Warnings : outcome.FollowerWarnings(item.Name));
        }
    }

    /// <summary>
    ///     Creates the cache folder when it does not exist.
    /// </summary>
    /// <returns>The cache folder.</returns>
    /// <exception cref="ToolException">Thrown with <see cref="ErrorCodes.PandocFilterError"/> when the folder cannot be created.</exception>
    private string CreateCacheFolder()
    {
        var folder = _settings.CacheFolder;
        try
        {
            Directory.CreateDirectory(folder);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw Fail($"could not create {folder}: {ex.Message}");
        }

        return folder;
    }

    /// <summary>
    ///     Creates the error of the filter.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <returns>The exception to throw.</returns>
    private static ToolException Fail(string message)
    {
        return new ToolException(ErrorCodes.PandocFilterError, message);
    }

    /// <summary>
    ///     A failure and the position of the picture it belongs to.
    /// </summary>
    /// <param name="Index">The position of the picture.</param>
    /// <param name="Error">The exception, kept with its stack trace.</param>
    private sealed record Failure(int Index, ExceptionDispatchInfo Error);

    /// <summary>
    ///     A picture the plan handles: its element, what it keeps, and either a cached file, a job, or only warnings.
    /// </summary>
    /// <param name="index">The position of the element.</param>
    /// <param name="name">The name messages give it: its <c>src</c>, or the name of a code block.</param>
    /// <param name="node">The element.</param>
    /// <param name="block">The code block, or <see langword="null"/> for an image.</param>
    /// <param name="caption">The caption of a code block, or <see langword="null"/>.</param>
    private sealed class PlannedPicture(int index, string name, JsonObject node, PandocCodeBlock? block, string? caption)
    {
        /// <summary>Gets the position of the element.</summary>
        public int Index { get; } = index;

        /// <summary>Gets the name messages give the picture.</summary>
        public string Name { get; } = name;

        /// <summary>Gets the element.</summary>
        public JsonObject Node { get; } = node;

        /// <summary>Gets the code block, or <see langword="null"/> for an image.</summary>
        public PandocCodeBlock? Block { get; } = block;

        /// <summary>Gets the caption of a code block, or <see langword="null"/>.</summary>
        public string? Caption { get; } = caption;

        /// <summary>Gets or sets the attributes that stay on the picture.</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Keep { get; set; } = [];

        /// <summary>Gets the warnings found while planning (a marker read from the cache).</summary>
        public List<string> Warnings { get; } = [];

        /// <summary>Gets or sets the picture found in the cache, or <see langword="null"/>.</summary>
        public string? File { get; set; }

        /// <summary>Gets or sets the job that draws the picture, or <see langword="null"/>.</summary>
        public DrawJob? Job { get; set; }

        /// <summary>Gets or sets a value indicating whether this picture leads its job (rather than following an earlier one).</summary>
        public bool Leads { get; set; }
    }
}
