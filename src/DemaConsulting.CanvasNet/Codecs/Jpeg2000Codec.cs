using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Decodes JPEG 2000 images (ITU-T T.800 | ISO/IEC 15444-1) from raw codestreams (<c>.j2k</c>/<c>.j2c</c>)
///     and from JP2 files (<c>.jp2</c>) into a <see cref="Surface"/> or a <see cref="Jpeg2000Image"/>.
/// </summary>
/// <remarks>
///     <para>
///         This is a decode-only, dependency-free implementation of the complete Part 1 decoder: all five
///         progression orders, progression order changes, SOP/EPH markers, packed packet headers (PPM/PPT),
///         tiles and tile-parts, all precinct and code-block configurations, every code-block style
///         (bypass, reset, termination on each pass, vertically causal contexts, segmentation symbols),
///         region-of-interest max-shift, the reversible 5/3 and irreversible 9/7 wavelets, the reversible and
///         irreversible component transforms, subsampled components, signed and 1 to 16 bit samples, and
///         the JP2 color specification, palette, component mapping, channel definition boxes.
///     </para>
///     <para>
///         Samples are scaled to 8 bits per channel. ICC profiles are reported by
///         <see cref="Decode(Stream)"/> but never applied. Part 2 extensions, high-throughput (HTJ2K)
///         codestreams, more than 16 components and samples deeper than 16 bits are rejected with
///         <see cref="UnsupportedImageFeatureException"/>.
///     </para>
///     <para>
///         The decoder fails closed: malformed or incomplete input (including a codestream that lacks tiles)
///         raises <see cref="InvalidDataException"/>, and inputs that would need unreasonable memory or
///         time (decompression bombs) are rejected with it as well; the bounds are the
///         <see cref="Jpeg2000DecoderLimits"/>, checked against the headers before anything is allocated.
///         There is no recovery or partial decoding. Every structural field is validated where it is
///         parsed; a last-resort backstop converts an arithmetic or index fault that slipped through
///         into <see cref="InvalidDataException"/> as well, so no other exception type escapes on bad input.
///     </para>
///     <para>
///         Four color channels are interpreted as CMYK only when the file says so (a JP2 color
///         specification of CMYK) or, for a raw codestream or a JP2 file without any color specification,
///         as a documented heuristic. Four channels in any other declared color space are rejected with
///         <see cref="UnsupportedImageFeatureException"/> instead of being guessed.
///     </para>
/// </remarks>
public static partial class Jpeg2000Codec
{
    /// <summary>
    ///     Loads a <see cref="Surface"/> from a JPEG 2000 codestream or JP2 file.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read from. Reading begins at the current position and consumes the remainder of the stream.
    /// </param>
    /// <returns>
    ///     A new <see cref="Surface"/>. Grey images are expanded to R=G=B, CMYK images are converted to RGB with
    ///     a simple device conversion, and images without alpha are fully opaque.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the data is malformed, exceeds the decoder resource limits, or is wider or taller than
    ///     <see cref="Surface.MaxDimension"/>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">Thrown when the file uses a feature the decoder does not support.</exception>
    public static Surface Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var image = Decode(stream);
        return ToSurface(image);
    }

    /// <summary>
    ///     Loads a <see cref="Surface"/> from a JPEG 2000 file at the specified path.
    /// </summary>
    /// <param name="path">The path of the file. Must not be null or empty.</param>
    /// <returns>A new <see cref="Surface"/>; see <see cref="Load(Stream)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="InvalidDataException">Thrown for the same conditions as <see cref="Load(Stream)"/>.</exception>
    /// <exception cref="UnsupportedImageFeatureException">Thrown for the same conditions as <see cref="Load(Stream)"/>.</exception>
    /// <remarks>File-system exceptions raised while opening <paramref name="path"/> propagate to the caller.</remarks>
    public static Surface Load(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return Load(stream);
    }

    /// <summary>
    ///     Reports the dimensions and channel layout of a JPEG 2000 image without decoding any tile data.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read from. Reading begins at the current position and consumes the remainder of the stream.
    /// </param>
    /// <returns>
    ///     An <see cref="ImageInfo"/> whose <see cref="ImageInfo.Channels"/> counts the color channels plus the
    ///     alpha channel, if any. <see cref="Surface.MaxDimension"/> is not enforced.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">Thrown when the container or SIZ marker segment is malformed.</exception>
    /// <exception cref="UnsupportedImageFeatureException">Thrown when the file uses a feature the decoder does not support.</exception>
    public static ImageInfo GetInfo(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var data = ReadAllBytes(stream, Jpeg2000DecoderLimits.Default);
        return Guard(() =>
        {
            var jp2 = ParseContainer(data);
            var siz = Codestream.ParseSizOnly(data, jp2.CodestreamStart, jp2.CodestreamEnd, out _);
            var layout = ResolveLayout(jp2, siz.Csiz);
            var hasAlpha = layout.Alpha is not null;
            return new ImageInfo((int)Math.Min(siz.Width, int.MaxValue), (int)Math.Min(siz.Height, int.MaxValue), layout.Color.Length + (hasAlpha ? 1 : 0), hasAlpha);
        });
    }

    /// <summary>
    ///     Reports the dimensions and channel layout of the JPEG 2000 file at the specified path.
    /// </summary>
    /// <param name="path">The path of the file. Must not be null or empty.</param>
    /// <returns>An <see cref="ImageInfo"/>; see <see cref="GetInfo(Stream)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="path"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> is an empty string.</exception>
    /// <exception cref="InvalidDataException">Thrown for the same conditions as <see cref="GetInfo(Stream)"/>.</exception>
    /// <exception cref="UnsupportedImageFeatureException">Thrown for the same conditions as <see cref="GetInfo(Stream)"/>.</exception>
    /// <remarks>File-system exceptions raised while opening <paramref name="path"/> propagate to the caller.</remarks>
    public static ImageInfo GetInfo(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Length == 0)
        {
            throw new ArgumentException("Path must not be empty.", nameof(path));
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read);
        return GetInfo(stream);
    }

    /// <summary>
    ///     Decodes a JPEG 2000 codestream or JP2 file to 8-bit component samples without converting to a <see cref="Surface"/>.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read from. Reading begins at the current position and consumes the remainder of the stream.
    /// </param>
    /// <returns>The decoded image with its color space, alpha plane and ICC profile.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the data is malformed, exceeds the decoder resource limits, or is wider or taller than
    ///     <see cref="Surface.MaxDimension"/>.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">Thrown when the file uses a feature the decoder does not support.</exception>
    public static Jpeg2000Image Decode(Stream stream) => Decode(stream, Jpeg2000DecoderLimits.Default);

    /// <summary>
    ///     Decodes a JPEG 2000 codestream or JP2 file to 8-bit component samples, enforcing the given limits.
    /// </summary>
    /// <param name="stream">
    ///     The stream to read from. Reading begins at the current position and consumes the remainder of the stream.
    /// </param>
    /// <param name="limits">The resource limits to enforce.</param>
    /// <returns>The decoded image; see <see cref="Decode(Stream)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> or <paramref name="limits"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a limit is not positive or is too large.</exception>
    /// <exception cref="InvalidDataException">Thrown when the data is malformed or exceeds <paramref name="limits"/>.</exception>
    /// <exception cref="UnsupportedImageFeatureException">Thrown when the file uses a feature the decoder does not support.</exception>
    public static Jpeg2000Image Decode(Stream stream, Jpeg2000DecoderLimits limits)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();

        return Decode(ReadAllBytes(stream, limits), limits);
    }

    /// <summary>
    ///     Decodes a JPEG 2000 codestream or JP2 file held in memory.
    /// </summary>
    /// <param name="data">The file or codestream bytes.</param>
    /// <returns>The decoded image; see <see cref="Decode(Stream)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> is null.</exception>
    /// <exception cref="InvalidDataException">Thrown for the same conditions as <see cref="Decode(Stream)"/>.</exception>
    /// <exception cref="UnsupportedImageFeatureException">Thrown for the same conditions as <see cref="Decode(Stream)"/>.</exception>
    public static Jpeg2000Image Decode(byte[] data) => Decode(data, Jpeg2000DecoderLimits.Default);

    /// <summary>
    ///     Decodes a JPEG 2000 codestream or JP2 file held in memory, enforcing the given limits.
    /// </summary>
    /// <param name="data">The file or codestream bytes.</param>
    /// <param name="limits">The resource limits to enforce.</param>
    /// <returns>The decoded image; see <see cref="Decode(Stream)"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="data"/> or <paramref name="limits"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when a limit is not positive or is too large.</exception>
    /// <exception cref="InvalidDataException">Thrown when the data is malformed or exceeds <paramref name="limits"/>.</exception>
    /// <exception cref="UnsupportedImageFeatureException">Thrown when the file uses a feature the decoder does not support.</exception>
    public static Jpeg2000Image Decode(byte[] data, Jpeg2000DecoderLimits limits)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(limits);
        limits.Validate();

        if (data.Length > limits.MaxInputBytes)
        {
            throw Malformed("input is too large.");
        }

        return Guard(() => DecodeImage(data, limits));
    }

    /// <summary>Converts a decoded image to a <see cref="Surface"/>.</summary>
    /// <param name="image">The decoded image.</param>
    /// <returns>A new surface.</returns>
    private static Surface ToSurface(Jpeg2000Image image)
    {
        var surface = new Surface(image.Width, image.Height);
        try
        {
            var n = image.ColorChannelCount;
            var src = image.ColorSamples;
            for (var y = 0; y < image.Height; y++)
            {
                var row = surface.GetRowSpan(y);
                for (var x = 0; x < image.Width; x++)
                {
                    var p = ((y * image.Width) + x) * n;
                    var a = image.AlphaSamples is null ? (byte)255 : image.AlphaSamples[(y * image.Width) + x];
                    row[x] = ToPixel(src, p, n, a, image.AlphaPremultiplied);
                }
            }

            return surface;
        }
        catch
        {
            surface.Dispose();
            throw;
        }
    }

    private static Rgba32 ToPixel(byte[] src, int p, int channels, byte alpha, bool premultiplied)
    {
        byte r, g, b;
        switch (channels)
        {
            case 1:
                r = g = b = src[p];
                break;
            case 4:
                {
                    // Four channels are only ever CMYK here: ResolveLayout rejects every other 4-channel space.
                    // Naive device CMYK to RGB conversion.
                    var k = src[p + 3];
                    r = (byte)(255 - Math.Min(255, src[p] + k));
                    g = (byte)(255 - Math.Min(255, src[p + 1] + k));
                    b = (byte)(255 - Math.Min(255, src[p + 2] + k));
                    break;
                }

            default:
                r = src[p];
                g = src[p + 1];
                b = src[p + 2];
                break;
        }

        if (premultiplied && alpha is > 0 and < 255)
        {
            r = (byte)Math.Min(255, ((r * 255) + (alpha / 2)) / alpha);
            g = (byte)Math.Min(255, ((g * 255) + (alpha / 2)) / alpha);
            b = (byte)Math.Min(255, ((b * 255) + (alpha / 2)) / alpha);
        }

        return new Rgba32(r, g, b, alpha);
    }

    /// <summary>Reads a stream fully, rejecting inputs above <see cref="Jpeg2000DecoderLimits.MaxInputBytes"/>.</summary>
    /// <param name="stream">The stream to read.</param>
    /// <param name="limits">The resource limits.</param>
    /// <returns>The bytes read.</returns>
    private static byte[] ReadAllBytes(Stream stream, Jpeg2000DecoderLimits limits)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (memory.Length + read > limits.MaxInputBytes)
            {
                throw Malformed("input is too large.");
            }

            memory.Write(buffer, 0, read);
        }

        return memory.ToArray();
    }

    /// <summary>
    ///     A last-resort backstop for the decoding steps: converts an index or arithmetic fault into
    ///     <see cref="InvalidDataException"/> so that only the documented exception types escape on bad input.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="action">The decoding step.</param>
    /// <returns>The step's result.</returns>
    /// <remarks>
    ///     This is deliberately narrow. Every structural field is validated explicitly where it is parsed
    ///     (marker lengths, indices, counts, exponents, box sizes), so well-understood malformed input fails with
    ///     a specific message and no inner exception. Only <see cref="IndexOutOfRangeException"/> and
    ///     <see cref="OverflowException"/> (raised by <see langword="checked"/> size arithmetic) are mapped; the
    ///     original is kept as the inner exception, which the tests assert to be absent for named malformed
    ///     cases. Null references, invalid casts, argument faults and out-of-memory conditions are never
    ///     mapped: they indicate decoder defects and propagate. Memory is bounded up front by
    ///     <see cref="Jpeg2000DecoderLimits"/> instead of by catching <see cref="OutOfMemoryException"/>.
    /// </remarks>
    private static T Guard<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or OverflowException)
        {
            throw new InvalidDataException("Invalid JPEG 2000 data: " + ex.Message, ex);
        }
    }
}
