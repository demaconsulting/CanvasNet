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
}
