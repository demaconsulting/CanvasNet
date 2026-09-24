using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Represents the dimensions and pixel-format metadata declared by an image file's header,
///     as reported by a codec's <c>GetInfo</c> method (for example
///     <see cref="BmpCodec.GetInfo(Stream)"/>) without decoding any pixel data.
/// </summary>
/// <remarks>
///     <c>ImageInfo</c> is a small, shared supporting data type used by all four codecs in this
///     namespace (<see cref="BmpCodec"/>, <see cref="PngCodec"/>, <see cref="TiffCodec"/>, and
///     <see cref="JpegCodec"/>) rather than being owned by any single one of them, mirroring how
///     <see cref="Rgba32"/> is a shared supporting type for <see cref="Surface"/>. It exists to
///     let a caller inspect a file's declared width and height - and therefore estimate the
///     memory a full decode would allocate - before committing to a full pixel decode via the
///     corresponding <c>Load</c> method. This "bomb triage" use case is the primary motivation:
///     a maliciously or accidentally crafted file can declare dimensions large enough to exhaust
///     memory if decoded blindly, and <c>GetInfo</c> lets a caller reject such a file cheaply,
///     typically by comparing <see cref="Width"/>/<see cref="Height"/> (or their product) against
///     <see cref="Surface.MaxDimension"/> before ever calling <c>Load</c>.
///     <para>
///         Deliberately, <c>GetInfo</c> never enforces <see cref="Surface.MaxDimension"/> itself -
///         it always reports the raw header-declared values, even when they exceed that bound.
///         Enforcing the bound inside <c>GetInfo</c> would defeat its purpose: a caller inspecting
///         an oversized file specifically to reject it before decoding could never observe the
///         oversized value if <c>GetInfo</c> itself threw first. The corresponding <c>Load</c>
///         method continues to enforce <see cref="Surface.MaxDimension"/> exactly as before.
///     </para>
///     <para>
///         This type is a plain, immutable data carrier with no behavior beyond its record-struct
///         value equality; it deliberately has no new struct type per format, since all four
///         codecs report the same four properties from their respective header formats:
///     </para>
///     <list type="bullet">
///         <item>
///             <description>
///                 BMP: <see cref="Channels"/> is 3 or 4 (bytes per pixel, derived from the
///                 BITMAPINFOHEADER bit depth); <see cref="HasAlpha"/> is <see langword="true"/>
///                 only for the 32-bit-per-pixel variant.
///             </description>
///         </item>
///         <item>
///             <description>
///                 PNG: <see cref="Channels"/>/<see cref="HasAlpha"/> are derived from the file's
///                 declared color type, independently of whether <c>Load</c> can actually decode
///                 that color type/bit depth/interlace combination (see
///                 <see cref="PngCodec.GetInfo(Stream)"/>'s remarks): grayscale (0) reports 1
///                 channel, no alpha; Truecolor (2) reports 3 channels, no alpha; palette/indexed
///                 (3) reports 1 channel, no alpha - this is the <em>raw file encoding</em> (one
///                 palette-index sample per pixel, packed at sub-byte bit depths), deliberately
///                 <em>not</em> the 4-channel RGBA result a full <c>Load</c> would produce after
///                 resolving each index through the
///                 file's <c>PLTE</c>/<c>tRNS</c> chunks, since <c>GetInfo</c> never reads those
///                 chunks; grayscale-with-alpha (4) reports 2 channels, has alpha; Truecolor-with-
///                 alpha (6) reports 4 channels, has alpha.
///             </description>
///         </item>
///         <item>
///             <description>
///                 TIFF: <see cref="Channels"/> is the <c>SamplesPerPixel</c> tag's value,
///                 defaulting to <c>BitsPerSample</c>'s entry count when the tag is
///                 absent, resolved through the same validating parser for every seekable
///                 stream (a seekable stream is required; a non-seekable stream causes
///                 <c>GetInfo</c> to throw <see cref="NotSupportedException"/> immediately);
///                 <see cref="HasAlpha"/> is <see langword="true"/> only for an RGB image
///                 with 4 samples per pixel and a valid <c>ExtraSamples</c> tag value of
///                 2.
///             </description>
///         </item>
///         <item>
///             <description>
///                 JPEG: <see cref="Channels"/> is the number of components declared in the
///                 SOF0/SOF2 marker (1 for grayscale, 3 for YCbCr); <see cref="HasAlpha"/> is
///                 always <see langword="false"/>, since JPEG has no alpha channel.
///             </description>
///         </item>
///     </list>
/// </remarks>
/// <param name="Width">The image width, in pixels, as declared by the file's header.</param>
/// <param name="Height">The image height, in pixels, as declared by the file's header.</param>
/// <param name="Channels">
///     The number of color/alpha channels per pixel that decoding this file would normally
///     produce, as declared by the file's header (see the per-format derivation in the type-level
///     remarks) - except for a PNG palette (color type 3) image, where this instead reports the
///     raw file's single palette-index channel rather than the 4-channel RGBA a full
///     <see cref="PngCodec.Load(Stream)"/> would produce (see the PNG entry in the type-level
///     remarks above for the full explanation).
/// </param>
/// <param name="HasAlpha">
///     <see langword="true"/> if the file's header declares an alpha channel;
///     <see langword="false"/> otherwise (see the per-format derivation in the type-level
///     remarks).
/// </param>
public readonly record struct ImageInfo(int Width, int Height, int Channels, bool HasAlpha);
