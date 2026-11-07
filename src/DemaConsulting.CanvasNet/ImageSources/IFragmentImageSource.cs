namespace DemaConsulting.CanvasNet.ImageSources;

/// <summary>
///     Optional interface of an <see cref="IImageSource"/> that understands parameters in the fragment of a link
///     (<c>report.pdf#page=3</c>): it declares the parameter names it uses, so the resolver can tell a parameter the
///     source ignores from one it uses.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="FolderImageResolver"/> parses the fragment into <see cref="ImageSourceRequest.Fragment"/> for every
///         source, but it warns about unknown parameters
///         (<see cref="FolderImageResolver.FragmentUnknownParameterCode"/>) only against a declaration: a source that
///         does not implement this interface declares nothing, so every parameter of a link to a file it draws is
///         unknown. (A source that claims a URI scheme owns its links: when it does not implement this interface it
///         receives the fragment and no warnings are made for it.)
///     </para>
///     <para>
///         <b>Invalid values.</b> A source that reads a parameter and finds the value unusable throws
///         <see cref="ImageSourceRefusedException"/>, normally through <see cref="ImageFragment.GetPositiveInt32(string[])"/>,
///         and the picture is not drawn.
///     </para>
///     <para>
///         <b>Prefixed parameters.</b> A source whose parameters are named by the link's author (the variables of a
///         model, the parameters of a chart) cannot list the names in advance. It declares a <i>prefix</i> instead
///         (<see cref="FragmentParameterPrefixes"/>, for example <c>def.</c>): every parameter whose name starts with the
///         prefix and has at least one more character is understood, so a built-in name can never collide with the
///         name of a variable. The source reads them with <see cref="ImageFragment.GetPrefixed(string)"/> or
///         <see cref="ImageParameterValues"/>.
///     </para>
///     <example>
///         <code language="csharp">
///         public sealed class MySource : IImageSource, IFragmentImageSource
///         {
///             public IReadOnlyList&lt;string&gt; FragmentParameters { get; } = ["page"];
///             // ... Name, Extensions, Schemes and Render(request) read request.Fragment.GetPositiveInt32("page")
///         }
///         </code>
///     </example>
/// </remarks>
public interface IFragmentImageSource
{
    /// <summary>
    ///     Gets the names of the fragment parameters the source uses, in lower case (names are matched ASCII
    ///     case-insensitively). An empty list declares none; <see langword="null"/> is treated as an empty list, and a
    ///     <see langword="null"/> entry is ignored, so a source never makes the resolver fail by what it declares.
    /// </summary>
    IReadOnlyList<string> FragmentParameters { get; }

    /// <summary>
    ///     Gets the prefixes of the fragment parameters the source understands whatever follows the prefix, in lower
    ///     case (matched ASCII case-insensitively), such as <c>def.</c>. A parameter is understood when its name starts
    ///     with a prefix and has at least one character after it. The default is an empty list, so an existing source
    ///     declares none; <see langword="null"/> is treated as an empty list and a <see langword="null"/> or empty entry
    ///     is ignored.
    /// </summary>
    /// <remarks>
    ///     The resolver reports a parameter that matches neither <see cref="FragmentParameters"/> nor a prefix as
    ///     <see cref="FolderImageResolver.FragmentUnknownParameterCode"/>. The names after a prefix are compared as
    ///     written (they may be case-sensitive, like an OpenSCAD variable), so <c>def.A</c> and <c>def.a</c> are two
    ///     parameters and are never reported as duplicates of each other.
    /// </remarks>
    IReadOnlyList<string> FragmentParameterPrefixes => [];
}
