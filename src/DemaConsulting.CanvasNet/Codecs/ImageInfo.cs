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
///         A second, equally deliberate invariant: <b><c>GetInfo</c> never throws for an input
///         that <c>Load</c> would successfully decode</b>. A caller that first calls <c>GetInfo</c>
///         to triage a file's declared dimensions must be able to trust that, having done so
///         successfully, a subsequent <c>Load</c> call on the same bytes will not itself fail for
///         a reason <c>GetInfo</c> could have - but did not - already surfaced. Each codec upholds
///         this differently: BMP/PNG's headers are always at a small, fixed offset near the start
///         of the file, so their <c>GetInfo</c> methods read exactly the same fixed-size header
///         <c>Load</c> itself reads, with no separate code path to diverge from it. TIFF's Image
///         File Directory can legitimately be located anywhere in the file, including on a
///         non-seekable stream; <see cref="TiffCodec.GetInfo(Stream)"/> upholds the invariant by
///         falling back to buffering the whole stream (exactly as <see cref="TiffCodec.Load(Stream)"/>
///         already does unconditionally) whenever the input is non-seekable, rather than
///         rejecting it. JPEG's marker segments preceding the frame header have no fixed bound in
///         a well-formed file; <see cref="JpegCodec.GetInfo(Stream)"/> upholds the invariant by
///         treating its incremental probe cap as a soft threshold - once reached without finding
///         a SOF0/SOF2 marker, it keeps scanning segment headers past the cap - one marker
///         segment at a time, exactly as it does below the cap - until a SOF0/SOF2 marker is
///         found or the stream genuinely ends, rather than giving up while more data still
///         remains, and without ever reading into entropy-coded scan data. Additionally, JPEG's
///         <see cref="JpegCodec.GetInfo(Stream)"/> and <see cref="JpegCodec.Load(Stream)"/> both
///         enforce the exact same two independent ceilings on that pre-SOF marker-segment
///         walk - <see cref="JpegCodec.MaxProbeHeaderBytesHardLimit"/> (16 MiB of leading
///         marker-segment data) and <see cref="JpegCodec.MaxProbeSegmentCount"/> (512
///         non-terminating marker segments) - so a pathological JPEG that exceeds either ceiling
///         is rejected consistently by both methods, upholding the invariant unconditionally
///         rather than through a documented exception: there is no JPEG input <c>GetInfo</c>
///         rejects that <c>Load</c> would otherwise have accepted.
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
///                 absent, resolved through the same validating parser regardless of whether the
///                 source stream is seekable (a non-seekable stream is buffered into memory
///                 first; see <see cref="TiffCodec.GetInfo(Stream)"/>'s remarks);
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
public readonly record struct ImageInfo(int Width, int Height, int Channels, bool HasAlpha)
{
    /// <summary>
    ///     <see langword="true"/> if a subsequent call to the corresponding codec's <c>Load</c>
    ///     method on the same bytes is expected to succeed; <see langword="false"/> if the file's
    ///     header declares a well-formed feature that <c>Load</c> does not implement (a
    ///     <em>well-formed-but-unsupported</em> file, as distinct from a malformed one - a
    ///     malformed file makes <c>GetInfo</c> itself throw, rather than returning an
    ///     <see cref="ImageInfo"/> with this property set to <see langword="false"/>). Defaults
    ///     to <see langword="true"/>, since every codec except <see cref="PngCodec"/> today has no
    ///     well-formed-but-unsupported case at all - see
    ///     <see cref="PngCodec.GetInfo(System.IO.Stream)"/>'s remarks for the one case (Adam7
    ///     interlacing) where this is <see langword="false"/>.
    /// </summary>
    /// <remarks>
    ///     Deliberately declared here as an <see langword="init"/>-only member outside this
    ///     record struct's primary constructor parameter list, rather than as a fifth positional
    ///     parameter, so that adding it does not change the primary constructor's or
    ///     <c>Deconstruct</c>'s emitted signature - both of which every existing caller that
    ///     constructs or positionally deconstructs an <see cref="ImageInfo"/> already depends on
    ///     at the IL level, not just at the source level. A caller that wants to set this
    ///     property uses object-initializer syntax:
    ///     <c>new ImageInfo(width, height, channels, hasAlpha) { CanDecode = false }</c>.
    /// </remarks>
    public bool CanDecode { get; init; } = true;
}
