using System.Collections.ObjectModel;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     What the fragment of an input chose: a page, a viewpoint and variables for a model, parameters for a chart, a theme
///     for a diagram, a display mode for an equation. It is
///     created by <see cref="SourceLoader.Probe(InputReference)"/> and carried by <see cref="SourceInfo.Selection"/>, so
///     every operation that loads the input (render, inspect, compare, montage) applies it.
/// </summary>
/// <remarks>
///     <para>
///         <b>The grammar is the grammar of an SVG link.</b> The fragment is read by the built-in image source of the kind
///         of the file (the one that draws <c>&lt;image href="part.scad#view=top&amp;def.width=20"&gt;</c>), so an input and a
///         link accept the same parameters with the same values: <c>page</c> (and <c>slide</c> for a presentation) for a
///         document, <c>view</c>, <c>azimuth</c>, <c>elevation</c>, <c>projection</c>, and <c>def.&lt;name&gt;</c> for a model, <c>param.&lt;name&gt;</c> for a chart, <c>theme</c> for a
///         Mermaid diagram, <c>display</c> (<c>block</c> or <c>inline</c>) for a MathML equation; the values
///         of <c>def.</c> are read like <c>-D</c> (vectors such as <c>def.$vpt=[0,0,20]</c> included) and those of
///         <c>param.</c> like <c>--param</c>.
///     </para>
///     <para>
///         <b>Overrides.</b> <see cref="ApplyTo(SourceOptions)"/> lays the choices over the options of the command: the page
///         replaces the page of the command; the view, the azimuth, the elevation, and the projection each replace those of
///         the command (one the fragment does not set keeps the value of the command); a variable replaces a variable of
///         the same name of the command (the others are kept); a parameter replaces a parameter of the same name; the theme
///         replaces the theme; the display mode replaces the display mode.
///     </para>
///     <para>
///         <b>Errors and warnings.</b> A value that cannot be used (the fragment is too long or has too many parameters, a
///         page is not a whole number, a vector is not valid, a variable name is not an identifier, a view is not one of the
///         views, an azimuth or elevation is not a number of degrees or an elevation is outside -90 to 90, a projection is
///         not ortho, orthographic, or perspective, a theme is not a Mermaid theme, a display mode is not block or inline) is refused when the input is probed, as <see cref="ErrorCodes.ImageFragmentInvalid"/>; a page the file does
///         not have is <see cref="ErrorCodes.PageOutOfRange"/>. A parameter the kind does not use is the warning
///         <c>image-fragment-unknown-parameter</c>, a name given twice with different values is
///         <c>image-fragment-duplicate-parameter</c> (the last wins), and a <c>param.</c> name that the chart does not
///         declare is <c>vl-param-unknown</c> (and is dropped): they are in <see cref="Diagnostics"/>, which the
///         first page that is loaded from the input adds to its own notes, and they are never errors, not even with
///         <see cref="SourceOptions.Strict"/>. The type is immutable and thread-safe.
///     </para>
/// </remarks>
public sealed record InputSelection
{
    /// <summary>
    ///     Gets the fragment as written (still percent-encoded), without the <c>#</c>.
    /// </summary>
    public string Fragment { get; internal init; } = string.Empty;

    /// <summary>
    ///     Gets the 1-based page the fragment names, or <see langword="null"/> when it names none (and for every kind that has no pages).
    /// </summary>
    public int? Page { get; internal init; }

    /// <summary>
    ///     Gets what the fragment sets for a model: the viewpoint (when <see cref="ScadViewOptions.ViewGiven"/> is set), the
    ///     azimuth and elevation that replace its angles, the projection (when <see cref="ProjectionGiven"/> is set), and the
    ///     variables of the <c>def.</c> parameters; <see langword="null"/> when it sets none of them (and for every other kind).
    /// </summary>
    public ScadViewOptions? Scad { get; internal init; }

    /// <summary>
    ///     Gets a value indicating whether the fragment chose the projection of a model (<c>projection=</c>), so that
    ///     <see cref="ScadViewOptions.Perspective"/> of <see cref="Scad"/> replaces the projection of the command.
    /// </summary>
    /// <remarks>
    ///     A separate flag because <see cref="ScadViewOptions.Perspective"/> always has a value (orthographic by default),
    ///     so an orthographic choice cannot be told from no choice.
    /// </remarks>
    internal bool ProjectionGiven { get; init; }

    /// <summary>
    ///     Gets the parameters the fragment sets for a chart (only names the chart declares);
    ///     <see langword="null"/> when it sets none (and for every other kind).
    /// </summary>
    public ChartOptions? VegaLite { get; internal init; }

    /// <summary>
    ///     Gets the theme the fragment names for a Mermaid diagram (<c>flow.mmd#theme=dark</c>); <see langword="null"/> when it
    ///     names none (and for every other kind).
    /// </summary>
    public DiagramOptions? Mermaid { get; internal init; }

    /// <summary>
    ///     Gets the display mode the fragment names for a MathML equation (<c>eq.mml#display=inline</c>); <see langword="null"/>
    ///     when it names none (and for every other kind).
    /// </summary>
    public EquationOptions? MathMl { get; internal init; }

    /// <summary>
    ///     Gets the notes about the fragment: the warnings listed in the remarks; empty when there are none.
    /// </summary>
    public IReadOnlyList<ImageDiagnostic> Diagnostics { get; internal init; } = [];

    /// <summary>
    ///     Lays what the fragment chose over the options of a command.
    /// </summary>
    /// <param name="options">The options of the command, which apply to every input.</param>
    /// <returns>
    ///     A copy of <paramref name="options"/> with the page, the viewpoint, projection, and variables of a model, the parameters of a chart,
    ///     the theme of a diagram, and the display mode of an equation that the fragment chose; what the fragment does not name is left as it is. Applying the selection twice gives the same result.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="options"/> is <see langword="null"/>.</exception>
    public SourceOptions ApplyTo(SourceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options with
        {
            Page = Page ?? options.Page,
            Scad = MergeScad(options.Scad),
            VegaLite = MergeChart(options.VegaLite),
            Mermaid = Mermaid ?? options.Mermaid,
            MathMl = MathMl ?? options.MathMl
        };
    }

    /// <summary>
    ///     Reads the fragment of an input against the kind of its file.
    /// </summary>
    /// <param name="input">The input as the user wrote it.</param>
    /// <param name="info">The facts about the file.</param>
    /// <returns>What the fragment chose.</returns>
    /// <exception cref="ViewerException">
    ///     Thrown with code <c>image-fragment-invalid</c> when the fragment or a value in it cannot be used, and with code
    ///     <c>page-out-of-range</c> when it names a page the file does not have.
    /// </exception>
    internal static InputSelection Create(InputReference input, SourceInfo info)
    {
        var fragment = ImageFragment.Parse(input.Fragment);
        if (fragment.Problem is { } problem)
        {
            throw Invalid(input, problem);
        }

        var source = CanvasNetViewer.FindBuiltIn(info.Format);
        int? page;
        ScadViewOptions? scad;
        ChartOptions? chart;
        DiagramOptions? diagram;
        EquationOptions? equation;
        try
        {
            page = source.ReadPage(fragment);
            scad = source.ReadScad(fragment);
            chart = source.ReadChart(fragment);
            diagram = source.ReadDiagram(fragment);
            equation = source.ReadEquation(fragment);
        }
        catch (ImageSourceRefusedException ex)
        {
            throw Invalid(input, ex.Message);
        }

        // The page is checked now, so a page the file does not have is refused before anything is drawn
        if (page is { } named)
        {
            SourceLoader.CheckPage(info, named);
        }

        var notes = fragment.ReviewParameters("input", input.Text, source)
            .Select(note => new ImageDiagnostic("input", "warning", note.Code, note.Message))
            .ToList();
        (chart, var unknown) = DropUnknownParameters(chart, info);
        notes.AddRange(unknown);
        return new InputSelection
        {
            Fragment = input.Fragment,
            Page = page,
            Scad = scad,
            ProjectionGiven = scad is not null && fragment.GetValue(LoaderImageSource.ProjectionParameter) is not null,
            VegaLite = chart,
            Mermaid = diagram,
            MathMl = equation,
            Diagnostics = new ReadOnlyCollection<ImageDiagnostic>(notes)
        };
    }

    /// <summary>
    ///     Removes the parameters the chart does not declare and reports each as a warning.
    /// </summary>
    /// <param name="chart">The parameters the fragment sets, or <see langword="null"/>.</param>
    /// <param name="info">The facts about the file; for a chart they list the declared parameters.</param>
    /// <returns>The parameters the chart declares (or <see langword="null"/> when none remain) and the warnings.</returns>
    private static (ChartOptions? Chart, IReadOnlyList<ImageDiagnostic> Notes) DropUnknownParameters(ChartOptions? chart, SourceInfo info)
    {
        if (chart is null)
        {
            return (null, []);
        }

        var declared = new HashSet<string>(info.Chart?.Parameters ?? [], StringComparer.Ordinal);
        var unknown = chart.Parameters.Keys.Where(name => !declared.Contains(name)).ToList();
        if (unknown.Count == 0)
        {
            return (chart, []);
        }

        var kept = chart.Parameters.Where(p => declared.Contains(p.Key)).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        return (
            kept.Count == 0 ? null : chart with { Parameters = kept },
            [.. SourceLoader.UnknownParameterNotes(unknown.Order(StringComparer.Ordinal), declared)]);
    }

    /// <summary>
    ///     Creates the error for a fragment that cannot be used.
    /// </summary>
    /// <param name="input">The input.</param>
    /// <param name="reason">Why the fragment cannot be used.</param>
    /// <returns>The exception to throw.</returns>
    private static ViewerException Invalid(InputReference input, string reason)
    {
        // Show the file name and the fragment, each capped, so a long folder never hides the fragment that is wrong
        static string Shown(string text, int limit) =>
            new([.. text.Take(limit).Select(c => char.IsControl(c) ? '?' : c), .. text.Length > limit ? "..." : string.Empty]);

        var file = Shown(System.IO.Path.GetFileName(input.Path), 80);
        var fragment = Shown(input.Fragment, 200);
        return new ViewerException(
            ErrorCodes.ImageFragmentInvalid,
            $"The fragment '#{fragment}' of the input '{file}' cannot be used: {reason}",
            "A fragment is name=value parameters joined by &, as in an SVG link: page=3, view=top, def.width=20, param.cutoff=5, display=inline. Quote the input in a shell so that & is not read as a command separator.");
    }

    /// <summary>
    ///     Merges what the fragment sets for a model into the options of the command.
    /// </summary>
    /// <param name="shared">The options of the command, or <see langword="null"/>.</param>
    /// <returns>The merged options; the fragment's view, azimuth, elevation, projection, and variables win.</returns>
    private ScadViewOptions? MergeScad(ScadViewOptions? shared)
    {
        if (Scad is null)
        {
            return shared;
        }

        var options = shared ?? new ScadViewOptions();
        var defines = new Dictionary<string, object?>(options.Defines);
        foreach (var (name, value) in Scad.Defines)
        {
            defines[name] = value;
        }

        // Each of the view, the azimuth, the elevation, and the projection that the fragment sets replaces that of the command
        return options with
        {
            View = Scad.ViewGiven ? Scad.View : options.View,
            ViewGiven = options.ViewGiven || Scad.ViewGiven,
            Azimuth = Scad.Azimuth ?? options.Azimuth,
            Elevation = Scad.Elevation ?? options.Elevation,
            Perspective = ProjectionGiven ? Scad.Perspective : options.Perspective,
            Defines = defines
        };
    }

    /// <summary>
    ///     Merges what the fragment sets for a chart into the options of the command.
    /// </summary>
    /// <param name="shared">The options of the command, or <see langword="null"/>.</param>
    /// <returns>The merged options; the fragment's parameters win.</returns>
    private ChartOptions? MergeChart(ChartOptions? shared)
    {
        if (VegaLite is null)
        {
            return shared;
        }

        var parameters = new Dictionary<string, object?>(shared?.Parameters ?? new Dictionary<string, object?>(), StringComparer.Ordinal);
        foreach (var (name, value) in VegaLite.Parameters)
        {
            parameters[name] = value;
        }

        return (shared ?? VegaLite) with { Parameters = parameters };
    }
}
