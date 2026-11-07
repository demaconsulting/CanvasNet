// cspell:ignore canvasnet scad
namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     An input as a user names it: a file path with an optional URL fragment (<c>assembly.scad#def.explode=0.5</c>,
///     <c>report.pdf#page=3</c>), the same syntax a link in an SVG document uses.
/// </summary>
/// <remarks>
///     <para>
///         <b>The rule.</b> A <c>#</c> starts a fragment only when the literal path does not exist as a file. So the name
///         a user types is first tried as it is, <c>#</c> included: a real file called <c>chart#2.png</c> keeps working,
///         and wins over a file <c>chart</c> that has a fragment <c>2.png</c>. Only when there is no such file is the text
///         split at a <c>#</c>: the longest start of the text, up to a <c>#</c>, that names an existing file is the path,
///         and what follows that <c>#</c> is the fragment (so <c>a#b.scad#view=top</c> is the file <c>a#b.scad</c> with the
///         fragment <c>view=top</c>, and a <c>#</c> inside a folder name is not a problem). When no start names a file the
///         text is split at its first <c>#</c>, and <see cref="SourceLoader.Probe(InputReference)"/> reports that file as
///         missing.
///     </para>
///     <para>
///         The fragment is kept as written (percent-encoded); <see cref="SourceLoader.Probe(InputReference)"/> parses it with
///         the grammar of an SVG link (<c>DemaConsulting.CanvasNet.ImageSources.ImageFragment</c>) and checks it against the
///         kind of the file. In a shell a <c>&amp;</c> separates commands, so quote an input that has more than one
///         parameter: <c>"assembly.scad#view=top&amp;def.explode=0.5"</c>. The type is immutable and thread-safe.
///     </para>
/// </remarks>
/// <example>
///     <code language="csharp">
///     var input = InputReference.Parse("assembly.scad#def.explode=0.5");
///     var info = SourceLoader.Probe(input);   // the file assembly.scad, drawn with explode = 0.5
///     Console.WriteLine($"{input.Path} {input.Fragment}");   // assembly.scad def.explode=0.5
///     </code>
/// </example>
public sealed record InputReference
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="InputReference"/> class.
    /// </summary>
    /// <param name="path">The path of the file, without the fragment.</param>
    /// <param name="fragment">The fragment as written, without the <c>#</c>; empty for none.</param>
    private InputReference(string path, string fragment)
    {
        Path = path;
        Fragment = fragment;
    }

    /// <summary>
    ///     Gets the path of the file, without the fragment.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Gets the fragment as written (still percent-encoded), without the <c>#</c>; empty when the input has none.
    /// </summary>
    public string Fragment { get; }

    /// <summary>
    ///     Gets the input as it was written: the path, and <c>#</c> and the fragment when there is one.
    /// </summary>
    public string Text => Fragment.Length == 0 ? Path : $"{Path}#{Fragment}";

    /// <summary>
    ///     Reads an input path as the user wrote it.
    /// </summary>
    /// <param name="text">The text: a file path, optionally followed by <c>#</c> and a fragment.</param>
    /// <returns>The path and the fragment, by the rule in the remarks of <see cref="InputReference"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is <see langword="null"/>.</exception>
    public static InputReference Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // A name without a '#', or a file that really has the '#' in its name, is a path as it is
        var first = text.IndexOf('#');
        if (first < 0 || File.Exists(text))
        {
            return new InputReference(text, string.Empty);
        }

        // Otherwise the longest start that names a file is the path: the last '#' that leaves an existing file before it
        for (var i = text.LastIndexOf('#'); i >= 0; i = i == 0 ? -1 : text.LastIndexOf('#', i - 1))
        {
            if (i > 0 && File.Exists(text[..i]))
            {
                return new InputReference(text[..i], text[(i + 1)..]);
            }
        }

        // No file is named: the first '#' splits, so the report of the missing file names what was probably meant
        return new InputReference(text[..first], text[(first + 1)..]);
    }
}
