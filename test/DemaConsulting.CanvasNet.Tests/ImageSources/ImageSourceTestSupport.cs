// cspell:ignore href hrefs
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.ImageSources;

namespace DemaConsulting.CanvasNet.Tests.ImageSources;

/// <summary>
///     A temporary folder that is deleted when disposed, for the tests of the folder resolver.
/// </summary>
internal sealed class ImageTestFolder : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ImageTestFolder"/> class and creates the folder.
    /// </summary>
    public ImageTestFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "canvasnet-images-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the full path of the folder.</summary>
    public string Path { get; }

    /// <summary>
    ///     Combines a relative path with the folder.
    /// </summary>
    /// <param name="parts">The parts of the relative path.</param>
    /// <returns>The full path.</returns>
    public string Combine(params string[] parts)
    {
        return System.IO.Path.Combine([Path, .. parts]);
    }

    /// <summary>
    ///     Writes a text file, creating its folder.
    /// </summary>
    /// <param name="relativePath">The path relative to the folder.</param>
    /// <param name="text">The text.</param>
    /// <returns>The full path of the file.</returns>
    public string WriteText(string relativePath, string text)
    {
        var full = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        return full;
    }

    /// <summary>
    ///     Writes a binary file, creating its folder.
    /// </summary>
    /// <param name="relativePath">The path relative to the folder.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The full path of the file.</returns>
    public string WriteBytes(string relativePath, byte[] bytes)
    {
        var full = Combine(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
        return full;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, true);
        }
    }
}

/// <summary>
///     Assertions that a list handed out by the library cannot be changed by casting it to a mutable type.
/// </summary>
internal static class ReadOnlyListAssert
{
    /// <summary>
    ///     Asserts that a list is not an array or a <see cref="List{T}"/>, and that when it can be cast to
    ///     <see cref="IList{T}"/> it reports read-only and refuses to add.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="sample">An element to try to add.</param>
    public static void IsReadOnly<T>(IReadOnlyList<T> list, T sample)
    {
        Assert.False(list is T[]);
        Assert.False(list is List<T>);
        if (list is IList<T> mutable)
        {
            Assert.True(mutable.IsReadOnly);
            Assert.Throws<NotSupportedException>(() => mutable.Add(sample));
        }
    }
}

/// <summary>
///     An image source for tests: it claims what it is told to and draws what a delegate says, recording every request.
/// </summary>
internal sealed class StubImageSource : IImageSource
{
    /// <summary>The picture colors the stubs draw, for tests that tell sources apart by pixels.</summary>
    public static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>A second color, to tell two sources apart.</summary>
    public static readonly Rgba32 Blue = new(0, 0, 255, 255);

    /// <summary>The recorded requests, in call order.</summary>
    private readonly List<ImageSourceRequest> _requests = [];

    /// <summary>The delegate that draws.</summary>
    private readonly Func<ImageSourceRequest, ImageSourceResult> _render;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StubImageSource"/> class.
    /// </summary>
    /// <param name="name">The name of the source.</param>
    /// <param name="extensions">The claimed extensions.</param>
    /// <param name="schemes">The claimed schemes.</param>
    /// <param name="render">The delegate that draws, or <see langword="null"/> to draw a 4 by 4 green surface.</param>
    public StubImageSource(
        string name,
        string[]? extensions = null,
        string[]? schemes = null,
        Func<ImageSourceRequest, ImageSourceResult>? render = null)
    {
        Name = name;
        Extensions = extensions ?? [];
        Schemes = schemes ?? [];
        _render = render ?? (_ => new ImageSourceResult(Solid(4, 4, Green)));
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes { get; }

    /// <summary>Gets the requests the source received, in order.</summary>
    public IReadOnlyList<ImageSourceRequest> Requests => _requests;

    /// <summary>
    ///     Creates a surface of one color.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color.</param>
    /// <returns>The surface.</returns>
    public static Surface Solid(int width, int height, Rgba32 color)
    {
        var surface = new Surface(width, height);
        surface.Clear(color);
        return surface;
    }

    /// <inheritdoc />
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        _requests.Add(request);
        return _render(request);
    }
}
