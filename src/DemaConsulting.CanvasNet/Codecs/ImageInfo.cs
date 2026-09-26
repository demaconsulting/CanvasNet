using DemaConsulting.CanvasNet.Canvas;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     Represents the dimensions and pixel-format metadata declared by an image file's header,
///     as reported by a codec's <c>GetInfo</c> method (for example
///     <see cref="BmpCodec.GetInfo(Stream)"/>) without decoding any pixel data - except
///     <see cref="GifCodec.GetInfo(System.IO.Stream)"/>, which does decode the first frame's
///     compressed pixel data (to determine <see cref="CanDecode"/>) without ever resolving it
///     into a <see cref="Surface"/>; see this type's remarks for the full reasoning.
/// </summary>
/// <remarks>
///     <c>ImageInfo</c> is a small, shared supporting data type used by all five raster codecs in
///     this namespace (<see cref="BmpCodec"/>, <see cref="PngCodec"/>, <see cref="TiffCodec"/>,
///     <see cref="JpegCodec"/>, and <see cref="GifCodec"/>) rather than being owned by any single
///     one of them, mirroring how
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
///         rejects that <c>Load</c> would otherwise have accepted. GIF's Image Descriptors can
///         legitimately number more than one (an animation), and <see cref="GifCodec.GetInfo(System.IO.Stream)"/>
///         reports that true count via <see cref="FrameCount"/> by walking every block in the
///         file - not merely the Logical Screen Descriptor - yet still upholds this invariant:
///         it reuses the exact same per-frame structural-validation helpers
///         <see cref="GifCodec.Load(System.IO.Stream)"/> itself uses, and never invokes the LZW
///         decoder for any frame after the first (matching <c>Load</c>'s own decode-only-the-
///         first-frame scope), so it accepts every input <c>Load</c> accepts (and rejects every
///         input <c>Load</c> rejects for a structural, non-pixel-data reason) without needing to
///         decode any later frame's pixels. The first frame is handled differently: <c>GetInfo</c>
///         does attempt an LZW-decode of the first frame's compressed data - reusing <c>Load</c>'s
///         own decoder but discarding its decoded output instead of resolving it into a
///         <see cref="Surface"/> - specifically so a corrupt first-frame LZW payload can be
///         reported via <see cref="CanDecode"/> instead of silently accepted; see
///         <see cref="GifCodec.GetInfo(System.IO.Stream)"/>'s remarks for the full design, and
///         <see cref="CanDecode"/>'s own remarks for this as its third documented
///         well-formed-but-undecodable case, alongside <see cref="PngCodec"/>'s Adam7-interlacing
///         case.
///     </para>
///     <para>
///         This type is a plain, immutable data carrier with no behavior beyond its record-struct
///     value equality; it deliberately has no new struct type per format, since all five raster
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
///         <item>
///             <description>
///                 GIF: <see cref="Channels"/> is always 1 (one palette-index byte per pixel);
///                 <see cref="HasAlpha"/> is always <see langword="false"/> - this is the raw
///                 file's single palette-index-per-pixel encoding, deliberately not the
///                 4-channel RGBA result a full <c>Load</c> produces after resolving the active
///                 color table and any Graphic Control Extension transparency flag; although
///                 <c>GetInfo</c> does read (and skip) any Graphic Control Extension while
///                 walking the file's blocks to compute <see cref="FrameCount"/>, it never
///                 resolves the transparency flag or transparent color index either extension
///                 carries (see <see cref="GifCodec.GetInfo(Stream)"/>'s remarks).
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
    ///     header declares a well-formed feature that <c>Load</c> does not implement, or its
    ///     pixel data is corrupt in a way only detectable by attempting to decode it (a
    ///     <em>well-formed-but-unsupported/undecodable</em> file, as distinct from a malformed
    ///     one - a malformed file makes <c>GetInfo</c> itself throw, rather than returning an
    ///     <see cref="ImageInfo"/> with this property set to <see langword="false"/>). Defaults
    ///     to <see langword="true"/> for every <see cref="ImageInfo"/> constructed via its primary
    ///     constructor (which is how every codec's <c>GetInfo</c> constructs its result), since
    ///     most codecs have no well-formed-but-unsupported/undecodable case at all - see
    ///     <see cref="PngCodec.GetInfo(System.IO.Stream)"/>'s remarks for the case (Adam7
    ///     interlacing) where this is <see langword="false"/> because <c>Load</c> does not
    ///     implement a well-formed feature, and
    ///     <see cref="GifCodec.GetInfo(System.IO.Stream)"/>'s remarks for the case where this is
    ///     <see langword="false"/> because the first frame's compressed pixel data fails to
    ///     LZW-decode even though the file's block structure is otherwise entirely well-formed.
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
    ///     <para>
    ///         <b>Caveat: <c>default(ImageInfo)</c> (or an uninitialized array/field of type
    ///         <see cref="ImageInfo"/>) has <c>CanDecode == false</c>, not <see langword="true"/>
    ///         as the property initializer above would otherwise suggest.</b> Because
    ///         <see cref="ImageInfo"/> is a <see langword="struct"/>, <c>default(ImageInfo)</c>
    ///         zero-initializes every field directly, bypassing this property's <c>= true</c>
    ///         initializer entirely (a <see langword="struct"/>'s field initializers only run
    ///         when one of its declared constructors runs). No codec's <c>GetInfo</c> ever
    ///         produces a bare <c>default(ImageInfo)</c> - every <c>GetInfo</c> implementation
    ///         calls the primary constructor, which does apply this initializer - so this caveat
    ///         only matters to a caller that itself default-constructs, or allocates an
    ///         uninitialized array of, <see cref="ImageInfo"/> values directly.
    ///     </para>
    /// </remarks>
    public bool CanDecode { get; init; } = true;

    /// <summary>
    ///     The total number of Image Descriptors ("frames") a well-formed file declares. Defaults
    ///     to <c>1</c> for every <see cref="ImageInfo"/> constructed via its primary constructor
    ///     (which is how every codec's <c>GetInfo</c> constructs its result): BMP, PNG, TIFF, and
    ///     JPEG have no concept of multiple frames at all, so their <c>GetInfo</c> methods never
    ///     override this default. <see cref="GifCodec"/> is the sole exception - a GIF file may
    ///     legitimately declare more than one Image Descriptor (an animation), and
    ///     <see cref="GifCodec.GetInfo(System.IO.Stream)"/> reports the file's true count by
    ///     walking its block structure (skipping, never decoding, each frame after the first's
    ///     LZW-compressed pixel data - though it does attempt to LZW-decode the first frame's
    ///     compressed data, discarding the decoded output, purely to determine
    ///     <see cref="CanDecode"/>) - see that method's remarks for how this upholds both of this
    ///     type's documented invariants: it never enforces <see cref="Surface.MaxDimension"/>,
    ///     and it never throws for an input <see cref="GifCodec.Load(System.IO.Stream)"/> would
    ///     otherwise accept, because it shares the same structural-validation helpers
    ///     <c>Load</c> uses for every frame, reserving the LZW decoder for the one frame
    ///     <c>Load</c> itself decodes.
    /// </summary>
    /// <remarks>
    ///     Deliberately declared here as an <see langword="init"/>-only member outside this
    ///     record struct's primary constructor parameter list, rather than as a fifth positional
    ///     parameter - for the same reason as <see cref="CanDecode"/> (see its remarks): doing so
    ///     preserves the primary constructor's and <c>Deconstruct</c>'s emitted signature. A
    ///     caller that wants to set this property uses object-initializer syntax:
    ///     <c>new ImageInfo(width, height, channels, hasAlpha) { FrameCount = frameCount }</c>.
    ///     <para>
    ///         <b>Caveat: <c>default(ImageInfo)</c> (or an uninitialized array/field of type
    ///         <see cref="ImageInfo"/>) has <c>FrameCount == 0</c>, not <c>1</c> as the property
    ///         initializer above would otherwise suggest</b> - the same <see langword="struct"/>
    ///         field-initializer caveat documented on <see cref="CanDecode"/>'s remarks applies
    ///         identically here: a <see langword="struct"/>'s field initializers only run when
    ///         one of its declared constructors runs, and <c>default(ImageInfo)</c> bypasses all
    ///         of them. No codec's <c>GetInfo</c> ever produces a bare <c>default(ImageInfo)</c>.
    ///     </para>
    /// </remarks>
    public int FrameCount { get; init; } = 1;
}
