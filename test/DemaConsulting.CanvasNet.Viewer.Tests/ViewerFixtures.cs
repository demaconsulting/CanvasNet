// cspell:ignore href pdf endobj endstream xref startxref scad
using System.Text;
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.Codecs;
using DemaConsulting.CanvasNet.ImageSources;
using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Viewer.Tests;

/// <summary>
///     A temporary folder that is deleted when disposed, for the tests of the viewer.
/// </summary>
internal sealed class ViewerTestFolder : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ViewerTestFolder"/> class and creates the folder.
    /// </summary>
    public ViewerTestFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "canvasnet-viewer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    /// <summary>Gets the full path of the folder.</summary>
    public string Path { get; }

    /// <summary>
    ///     Combines a relative path with the folder.
    /// </summary>
    /// <param name="relativePath">The relative path.</param>
    /// <returns>The full path.</returns>
    public string Combine(string relativePath)
    {
        return System.IO.Path.Combine(Path, relativePath);
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
///     The small documents and pictures the viewer tests are made of, and the custom sources they plug in.
/// </summary>
internal static class ViewerFixtures
{
    /// <summary>The gray of the background of the test SVG documents, so every color that is drawn stands out.</summary>
    public static readonly Rgba32 Gray = new(128, 128, 128, 255);

    /// <summary>The green of the pictures.</summary>
    public static readonly Rgba32 Green = new(0, 255, 0, 255);

    /// <summary>A model of one cube.</summary>
    public const string Cube = "cube(10, center = true);";

    /// <summary>A small bar chart with inline data.</summary>
    public const string BarChart =
        "{\"data\": {\"values\": [{\"a\": \"A\", \"b\": 3}, {\"a\": \"B\", \"b\": 5}]}, \"mark\": \"bar\", "
        + "\"encoding\": {\"x\": {\"field\": \"a\", \"type\": \"nominal\"}, \"y\": {\"field\": \"b\", \"type\": \"quantitative\"}}}";

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

    /// <summary>
    ///     Asserts that a list given out as read-only cannot be changed through a cast.
    /// </summary>
    /// <typeparam name="T">The type of an element.</typeparam>
    /// <param name="list">The list.</param>
    /// <param name="sample">An element that is used to try to add and to replace.</param>
    public static void AssertReadOnlyList<T>(IReadOnlyList<T> list, T sample)
    {
        // Neither an array nor a List can be handed out: both can be changed through a cast
        Assert.IsNotType<T[]>(list);
        Assert.False(list is List<T>);
        var mutable = Assert.IsAssignableFrom<IList<T>>(list);
        Assert.True(mutable.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutable.Add(sample));
        Assert.Throws<NotSupportedException>(() => mutable[0] = sample);
        Assert.Throws<NotSupportedException>(() => mutable.RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => mutable.Clear());
    }

    /// <summary>
    ///     Asserts that a dictionary given out as read-only cannot be changed through a cast.
    /// </summary>
    /// <typeparam name="TKey">The type of a key.</typeparam>
    /// <typeparam name="TValue">The type of a value.</typeparam>
    /// <param name="table">The dictionary.</param>
    /// <param name="key">A key that is used to try to add and to replace.</param>
    /// <param name="value">A value that is used to try to add and to replace.</param>
    public static void AssertReadOnlyDictionary<TKey, TValue>(IReadOnlyDictionary<TKey, TValue> table, TKey key, TValue value)
        where TKey : notnull
    {
        // A Dictionary can be changed through a cast; the read-only wrapper cannot
        Assert.False(table is Dictionary<TKey, TValue>);
        var mutable = Assert.IsAssignableFrom<IDictionary<TKey, TValue>>(table);
        Assert.True(mutable.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => mutable.Add(key, value));
        Assert.Throws<NotSupportedException>(() => mutable[key] = value);
        Assert.Throws<NotSupportedException>(() => mutable.Remove(key));
        Assert.Throws<NotSupportedException>(() => mutable.Clear());
    }

    /// <summary>
    ///     Encodes a solid-color PNG.
    /// </summary>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="color">The color of every pixel.</param>
    /// <returns>The bytes of the file.</returns>
    public static byte[] Png(int width, int height, Rgba32 color)
    {
        using var surface = Solid(width, height, color);
        using var stream = new MemoryStream();
        PngCodec.Save(surface, stream);
        return stream.ToArray();
    }

    /// <summary>
    ///     Creates a PNG file that only declares a size (the signature and a valid IHDR chunk, 33 bytes, no pixel data), so
    ///     a test can prove a huge picture is refused from its header: decoding the file would fail.
    /// </summary>
    /// <param name="width">The declared width.</param>
    /// <param name="height">The declared height.</param>
    /// <returns>The bytes of the file.</returns>
    public static byte[] PngDeclaring(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        var chunk = bytes.AsSpan(8);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(chunk, 13);
        "IHDR"u8.CopyTo(chunk[4..]);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(chunk[8..], width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(chunk[12..], height);
        chunk[16] = 8;
        chunk[17] = 6;
        var crc = 0xFFFFFFFFu;
        foreach (var b in chunk.Slice(4, 17))
        {
            crc ^= b;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }

        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(chunk[21..], ~crc);
        return bytes;
    }

    /// <summary>
    ///     Creates the text of an SVG document of 500 by 100 pixels on a gray background, with the given markup on top.
    /// </summary>
    /// <param name="body">The markup drawn over the background.</param>
    /// <returns>The document.</returns>
    public static string Svg(string body)
    {
        return "<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 500 100' width='500' height='100'>"
               + $"<rect width='500' height='100' fill='#808080'/>{body}</svg>";
    }

    /// <summary>
    ///     Creates the markup of an <c>image</c> element in the cell of a 500 by 100 document (cells are 100 wide).
    /// </summary>
    /// <param name="href">The link.</param>
    /// <param name="cell">The cell, 0 to 4.</param>
    /// <returns>The markup.</returns>
    public static string Cell(string href, int cell)
    {
        return $"<image href='{href}' x='{cell * 100}' y='0' width='100' height='100'/>";
    }

    /// <summary>
    ///     Renders an SVG document through a resolver at its own size.
    /// </summary>
    /// <param name="svg">The document.</param>
    /// <param name="resolver">The resolver.</param>
    /// <returns>The surface; the caller disposes it.</returns>
    public static Surface RenderSvg(string svg, IImageResolver resolver)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg));
        return SvgCodec.LoadWithFontFaces(stream, 500, 100, null, new SvgLoadOptions { ImageResolver = resolver });
    }

    /// <summary>
    ///     Creates a one-page PDF document of 144 by 96 points with a red rectangle in the top-left quarter and a blue one
    ///     in the bottom-right quarter.
    /// </summary>
    /// <returns>The bytes of the document.</returns>
    public static byte[] Pdf()
    {
        const string content = "1 0 0 rg 0 48 72 48 re f 0 0 1 rg 72 0 72 48 re f";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 96] /Contents 4 0 R /Resources << >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream"
        };
        var builder = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(builder.Length);
            builder.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = builder.Length;
        builder.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            builder.Append($"{offset:D10} 00000 n \n");
        }

        builder.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(builder.ToString());
    }

    /// <summary>
    ///     Creates a custom file-based source for a made-up extension: the text of the file is the color it draws
    ///     (<c>green</c>, <c>blue</c> or <c>magenta</c>), as a 4 by 4 surface.
    /// </summary>
    /// <param name="name">The name of the source.</param>
    /// <param name="extensions">The extensions it claims.</param>
    /// <returns>The source.</returns>
    public static ColorTextSource ColorText(string name, params string[] extensions)
    {
        return new ColorTextSource(name, extensions);
    }
}

/// <summary>
///     A custom file-based source that any application could write: the file's text names the color of the picture.
/// </summary>
internal sealed class ColorTextSource : IImageSource
{
    /// <summary>The requests the source received.</summary>
    private readonly List<ImageSourceRequest> _requests = [];

    /// <summary>
    ///     Initializes a new instance of the <see cref="ColorTextSource"/> class.
    /// </summary>
    /// <param name="name">The name of the source.</param>
    /// <param name="extensions">The extensions it claims.</param>
    public ColorTextSource(string name, string[] extensions)
    {
        Name = name;
        Extensions = extensions;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => [];

    /// <summary>Gets the requests the source received, in order.</summary>
    public IReadOnlyList<ImageSourceRequest> Requests => _requests;

    /// <inheritdoc />
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        _requests.Add(request);
        using var reader = new StreamReader(request.OpenContent());
        var color = reader.ReadToEnd().Trim() switch
        {
            "blue" => new Rgba32(0, 0, 255, 255),
            "magenta" => new Rgba32(255, 0, 255, 255),
            _ => ViewerFixtures.Green
        };
        var (width, height) = request.SizeFor(4, 4);
        return new ImageSourceResult(ViewerFixtures.Solid(width, height, color)) { Format = "color-text" };
    }
}

/// <summary>
///     A custom scheme-claimed source that any application could write: <c>test-scheme:frame</c> draws a live frame whose
///     color changes on every call, like a video frame.
/// </summary>
internal sealed class LiveFrameSource : IImageSource
{
    /// <summary>The number of frames drawn so far.</summary>
    private int _frames;

    /// <summary>The requests the source received.</summary>
    private readonly List<ImageSourceRequest> _requests = [];

    /// <inheritdoc />
    public string Name => "live-frame";

    /// <inheritdoc />
    public IReadOnlyList<string> Extensions => [];

    /// <inheritdoc />
    public IReadOnlyList<string> Schemes => ["test-scheme"];

    /// <summary>Gets the requests the source received, in order.</summary>
    public IReadOnlyList<ImageSourceRequest> Requests => _requests;

    /// <summary>
    ///     Gets the color of the frame with a number: red grows by 40 per frame.
    /// </summary>
    /// <param name="frame">The frame number, from 1.</param>
    /// <returns>The color.</returns>
    public static Rgba32 ColorOf(int frame)
    {
        return new Rgba32((byte)(frame * 40), 0, 200, 255);
    }

    /// <inheritdoc />
    public ImageSourceResult Render(ImageSourceRequest request)
    {
        _requests.Add(request);
        _frames++;
        return new ImageSourceResult(ViewerFixtures.Solid(8, 8, ColorOf(_frames)));
    }
}
