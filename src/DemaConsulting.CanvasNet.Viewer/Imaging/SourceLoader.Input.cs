// cspell:ignore scad
using System.Globalization;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Scad;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     The input and the validation of the options of <see cref="SourceLoader"/>: where the bytes of a source come from
///     (a file, or memory), the hooks an embedding resolver gives the loader, and the one place that decides whether a
///     <see cref="SourceOptions"/> is usable.
/// </summary>
public static partial class SourceLoader
{
    /// <summary>
    ///     The largest resolution, in dots per inch, that a document page may be sized at: 2400.
    /// </summary>
    /// <remarks>
    ///     It is a limit of the loader, so every caller gets the same answer: <see cref="Load(string, SourceOptions)"/>,
    ///     <see cref="Load(SourceInfo, SourceOptions)"/> and <see cref="OpenPages(SourceInfo, SourceOptions)"/> refuse a
    ///     larger <see cref="SourceOptions.Dpi"/> with the code <c>usage-error</c>, and so does the command line.
    /// </remarks>
    public const double MaxDpi = 2400;

    /// <summary>
    ///     The smallest <see cref="SourceOptions.Scale"/>.
    /// </summary>
    private const double MinScale = 0.001;

    /// <summary>
    ///     The largest absolute value of <see cref="ScadViewOptions.Time"/>.
    /// </summary>
    private const double MaxScadTime = 1e9;

    /// <summary>
    ///     A reason a <see cref="SourceOptions"/> cannot be used, with an optional hint.
    /// </summary>
    /// <param name="Message">The reason, as one sentence.</param>
    /// <param name="Hint">What to do about it, or <see langword="null"/>.</param>
    internal readonly record struct OptionProblem(string Message, string? Hint = null);

    /// <summary>
    ///     Where the bytes of a source come from: a file that is opened for every use, or memory.
    /// </summary>
    /// <remarks>
    ///     The loader reads a source through <see cref="Open"/> and never by path, so the same code serves a file (the
    ///     tool) and the bytes of an embedded link (<c>CanvasNetViewer</c>). <see cref="Name"/> is the name messages and
    ///     libraries use for the source, and <see cref="Directory"/> is the folder the includes and data files of the
    ///     source are looked up in; for a file they are the path as given and the folder of its full path, which is what
    ///     the format libraries use themselves when they are given a path.
    /// </remarks>
    internal sealed class SourceInput
    {
        /// <summary>
        ///     The path of the file, or <see langword="null"/> for memory.
        /// </summary>
        private readonly string? _path;

        /// <summary>
        ///     The bytes of a source in memory.
        /// </summary>
        private readonly ReadOnlyMemory<byte> _content;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SourceInput"/> class.
        /// </summary>
        /// <param name="name">The name of the source.</param>
        /// <param name="directory">The folder of the source, or <see langword="null"/>.</param>
        /// <param name="path">The path of the file, or <see langword="null"/> for memory.</param>
        /// <param name="content">The bytes of a source in memory.</param>
        private SourceInput(string name, string? directory, string? path, ReadOnlyMemory<byte> content)
        {
            Name = name;
            Directory = directory;
            _path = path;
            _content = content;
        }

        /// <summary>
        ///     Gets the name of the source, for messages: the path as it was given for a file.
        /// </summary>
        public string Name { get; }

        /// <summary>
        ///     Gets the full path of the folder that holds the source, or <see langword="null"/> when there is none.
        /// </summary>
        public string? Directory { get; }

        /// <summary>
        ///     Gets the size of the source in bytes.
        /// </summary>
        /// <exception cref="FileNotFoundException">Thrown when the file does not exist (a file input only).</exception>
        public long Length => _path is null ? _content.Length : new FileInfo(_path).Length;

        /// <summary>
        ///     Creates the input for a file.
        /// </summary>
        /// <param name="path">The path of the file as it was given.</param>
        /// <returns>The input; the file is not touched until it is opened.</returns>
        public static SourceInput FromFile(string path)
        {
            return new SourceInput(path, Path.GetDirectoryName(Path.GetFullPath(path)), path, default);
        }

        /// <summary>
        ///     Creates the input for bytes in memory.
        /// </summary>
        /// <param name="content">The bytes; they are shared and never modified.</param>
        /// <param name="name">The name of the source for messages.</param>
        /// <param name="directory">The folder includes and data files are looked up in, or <see langword="null"/> for none.</param>
        /// <returns>The input.</returns>
        public static SourceInput FromMemory(ReadOnlyMemory<byte> content, string name, string? directory)
        {
            return new SourceInput(name, directory, null, content);
        }

        /// <summary>
        ///     Opens the source for reading.
        /// </summary>
        /// <returns>A new, readable, seekable stream; the caller disposes it.</returns>
        public Stream Open()
        {
            if (_path is not null)
            {
                return new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }

            return System.Runtime.InteropServices.MemoryMarshal.TryGetArray(_content, out var segment) && segment.Array is not null
                ? new MemoryStream(segment.Array, segment.Offset, segment.Count, false)
                : new MemoryStream(_content.ToArray(), false);
        }
    }

    /// <summary>
    ///     The hooks and policies that an embedding resolver gives the loader. The tool gives none (<see cref="Default"/>).
    /// </summary>
    internal sealed class LoadContext
    {
        /// <summary>
        ///     Gets the context with no hook and the policy of the tool.
        /// </summary>
        public static LoadContext Default { get; } = new();

        /// <summary>
        ///     Gets the token that cancels the load; it is checked before every page is loaded.
        /// </summary>
        public CancellationToken CancellationToken { get; init; }

        /// <summary>
        ///     Gets the hook that charges a surface to the pixel budget of the render, called with the size of every
        ///     surface the loader is about to allocate, before it allocates it; it throws
        ///     <see cref="ImageSourceBudgetException"/> when the size does not fit.
        /// </summary>
        public Action<int, int>? Charge { get; init; }

        /// <summary>
        ///     Gets a value indicating whether the pictures an SVG file links to are loaded by <see cref="ScopedResolver"/>
        ///     (the resolver of an embedding document) instead of by a resolver the loader creates itself, confined to the
        ///     folder of the file.
        /// </summary>
        /// <remarks>
        ///     When it is set, the resolver that owns <see cref="ScopedResolver"/> collects the notes of the nested
        ///     pictures, so the loader adds none of its own and the <c>--strict</c> check of unresolved links is left to
        ///     that owner.
        /// </remarks>
        public bool UseScopedResolver { get; init; }

        /// <summary>
        ///     Gets the resolver that loads the pictures an SVG file links to when <see cref="UseScopedResolver"/> is set;
        ///     <see langword="null"/> then means that the links of the file are not resolved at all.
        /// </summary>
        public IImageResolver? ScopedResolver { get; init; }

        /// <summary>
        ///     Gets a value indicating whether a page of a document is returned at the size it was drawn at (fitted
        ///     inside the requested box), instead of centered in a margin of the size of the box.
        /// </summary>
        public bool ReturnFittedPage { get; init; }

        /// <summary>
        ///     Charges a surface about to be allocated, when a hook is set.
        /// </summary>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        /// <exception cref="ImageSourceBudgetException">Thrown when the surface does not fit.</exception>
        public void ChargeSurface(int width, int height)
        {
            Charge?.Invoke(width, height);
        }
    }

    /// <summary>
    ///     Finds the first reason a <see cref="SourceOptions"/> cannot be used.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <returns>The problem, or <see langword="null"/> when the options are usable.</returns>
    /// <remarks>
    ///     This is the single validator: the public entry points, <c>CanvasNetViewer.Render</c> and the tool all apply
    ///     these ranges. The checks need no file, so they run before anything is read. A <see cref="SourceOptions.Page"/>
    ///     is not checked here because whether it exists depends on the source (see <see cref="CheckPage"/>), and a
    ///     <see cref="SourceOptions.Width"/> or <see cref="SourceOptions.Height"/> above
    ///     <see cref="Surface.MaxDimension"/> stays the <c>size-limit</c> error of the load. The whole-number rule of the
    ///     resolution of a Visio drawing depends on the kind of the file and is checked when the drawing is opened.
    /// </remarks>
    internal static OptionProblem? FindOptionProblem(SourceOptions options)
    {
        // Sizes: a side is at least one pixel
        if (options.Width is < 1)
        {
            return new OptionProblem($"The width {options.Width} is not valid: it must be at least 1 pixel.");
        }

        if (options.Height is < 1)
        {
            return new OptionProblem($"The height {options.Height} is not valid: it must be at least 1 pixel.");
        }

        if (options.Scale is { } scale && (!double.IsFinite(scale) || scale < MinScale || scale > Surface.MaxDimension))
        {
            return new OptionProblem(
                string.Create(CultureInfo.InvariantCulture, $"The scale {scale} is not valid: it must be from {MinScale} to {Surface.MaxDimension}."));
        }

        if (options.Dpi is { } dpi)
        {
            if (!double.IsFinite(dpi) || dpi <= 0)
            {
                return new OptionProblem($"The resolution {dpi} is not valid: it must be greater than zero.");
            }

            if (dpi > MaxDpi)
            {
                return new OptionProblem(
                    string.Create(CultureInfo.InvariantCulture, $"The resolution {dpi} is not valid: it must be at most {MaxDpi} dots per inch."));
            }
        }

        if (!Enum.IsDefined(options.Filter))
        {
            return new OptionProblem($"The resampling filter {(int)options.Filter} is not valid.");
        }

        return options.Scad is { } scad ? FindScadProblem(scad) : null;
    }

    /// <summary>
    ///     Finds the first reason the options of a model cannot be used.
    /// </summary>
    /// <param name="view">The options of the model.</param>
    /// <returns>The problem, or <see langword="null"/> when they are usable.</returns>
    private static OptionProblem? FindScadProblem(ScadViewOptions view)
    {
        if (!ScadViews.Any(v => string.Equals(v.Name, view.View, StringComparison.OrdinalIgnoreCase)))
        {
            return new OptionProblem($"'{view.View}' is not a view.", $"The views are: {string.Join(", ", ScadViews.Select(v => v.Name))}.");
        }

        if (view.Azimuth is { } azimuth && !double.IsFinite(azimuth))
        {
            return new OptionProblem($"The azimuth {azimuth} is not valid: it must be a finite number of degrees.");
        }

        if (view.Elevation is { } elevation && (!double.IsFinite(elevation) || elevation < -90 || elevation > 90))
        {
            return new OptionProblem($"The elevation {elevation} is not valid: it must be from -90 to 90 degrees.");
        }

        if (view.Time is { } time && (!double.IsFinite(time) || Math.Abs(time) > MaxScadTime))
        {
            return new OptionProblem(
                string.Create(CultureInfo.InvariantCulture, $"The time {time} is not valid: it must be from -{MaxScadTime:0} to {MaxScadTime:0}."));
        }

        if (view.MaxTriangles is < 1)
        {
            return new OptionProblem($"The triangle limit {view.MaxTriangles} is not valid: it must be at least 1.");
        }

        // The library says which variable names it accepts, so it is asked rather than guessed
        if (view.Defines.Count > 0)
        {
            try
            {
                _ = new ScadEvaluationOptions { Variables = view.Defines };
            }
            catch (ArgumentException ex)
            {
                return new OptionProblem($"A -D definition is not valid: {OneLine(ex.Message)}");
            }
        }

        return null;
    }

    /// <summary>
    ///     Refuses unusable options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ViewerException">Thrown with code <c>usage-error</c> for the first problem <see cref="FindOptionProblem"/> finds.</exception>
    private static void ValidateOptions(SourceOptions options)
    {
        if (FindOptionProblem(options) is { } problem)
        {
            throw new ViewerException(ErrorCodes.UsageError, problem.Message, problem.Hint);
        }
    }
}
