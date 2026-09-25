using System.IO;

namespace DemaConsulting.CanvasNet.Codecs;

/// <summary>
///     The exception thrown when a codec's <c>Load</c> method refuses to decode a file that is
///     otherwise well-formed per the file format's specification, because the file declares a
///     feature the codec does not implement (for example PNG Adam7 interlacing - see
///     <see cref="PngCodec.Load(System.IO.Stream)"/>'s remarks).
/// </summary>
/// <remarks>
///     This is a distinct type from <see cref="InvalidDataException"/> specifically so a caller
///     can distinguish "well-formed but unsupported" from "malformed" without having to
///     string-match <see cref="Exception.Message"/>. <see cref="InvalidDataException"/> itself is
///     a <see langword="sealed"/> class in .NET and therefore cannot be a base type here; this
///     type instead derives from <see cref="IOException"/> - a sibling of
///     <see cref="InvalidDataException"/>, not its base type: <see cref="InvalidDataException"/>
///     actually derives directly from <see cref="SystemException"/>, not <see cref="IOException"/>.
///     <b>This means an existing <c>catch (InvalidDataException)</c> block does
///     not catch this exception</b> - and, because <see cref="IOException"/> is <em>not</em> a
///     common base of the two types, a caller that wants to handle both the malformed and the
///     well-formed-but-unsupported cases together cannot do so with a single
///     <c>catch (IOException)</c> clause; it must add two explicit clauses, one per type
///     (<c>catch (UnsupportedImageFeatureException)</c> and
///     <c>catch (InvalidDataException)</c>). This is a deliberate, narrow
///     behavior change limited to the one throw site this type replaces (PNG Adam7 interlacing);
///     it is the direct mechanism by which callers gain the ability to distinguish the two cases
///     at all - a caller that does not need to distinguish them can simply catch both types
///     explicitly instead of only <see cref="InvalidDataException"/>.
///     <see cref="Feature"/> is a short, stable, machine-matchable token identifying which
///     unsupported feature was encountered (for example <c>"png-adam7-interlace"</c>), distinct
///     from the free-text, human-readable <see cref="Exception.Message"/>, so that branching on
///     the specific feature does not require parsing the message text.
///     <para>
///         A codec's <c>GetInfo</c> method never throws this exception - it is thrown only by
///         <c>Load</c> (or a method <c>Load</c> delegates to), when a caller commits to decoding a
///         file <c>GetInfo</c> already reported as well-formed. A caller that wants to detect this
///         case before calling <c>Load</c> at all should instead check the corresponding
///         <see cref="ImageInfo.CanDecode"/> value returned by <c>GetInfo</c>.
///     </para>
/// </remarks>
public sealed class UnsupportedImageFeatureException : IOException
{
    /// <summary>
    ///     A short, stable, machine-matchable token identifying which unsupported feature caused
    ///     this exception to be thrown (for example <c>"png-adam7-interlace"</c>), or
    ///     <see cref="string.Empty"/> when this exception was constructed via one of the standard
    ///     parameterless/message-only/inner-exception constructors rather than the two
    ///     feature-carrying constructors below.
    /// </summary>
    public string Feature { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="UnsupportedImageFeatureException"/> class
    ///     with a default message and an empty <see cref="Feature"/>.
    /// </summary>
    public UnsupportedImageFeatureException()
        : this(string.Empty, "An unsupported image feature was encountered.")
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="UnsupportedImageFeatureException"/> class
    ///     with the specified message and an empty <see cref="Feature"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public UnsupportedImageFeatureException(string message)
        : this(string.Empty, message)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="UnsupportedImageFeatureException"/> class
    ///     with the specified message, inner exception, and an empty <see cref="Feature"/>.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public UnsupportedImageFeatureException(string message, Exception innerException)
        : this(string.Empty, message, innerException)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="UnsupportedImageFeatureException"/> class
    ///     with the specified feature token and message.
    /// </summary>
    /// <param name="feature">
    ///     A short, stable, machine-matchable token identifying the unsupported feature (for
    ///     example <c>"png-adam7-interlace"</c>).
    /// </param>
    /// <param name="message">The message that describes the error.</param>
    public UnsupportedImageFeatureException(string feature, string message)
        : base(message)
        => Feature = feature;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UnsupportedImageFeatureException"/> class
    ///     with the specified feature token, message, and inner exception.
    /// </summary>
    /// <param name="feature">
    ///     A short, stable, machine-matchable token identifying the unsupported feature (for
    ///     example <c>"png-adam7-interlace"</c>).
    /// </param>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public UnsupportedImageFeatureException(string feature, string message, Exception innerException)
        : base(message, innerException)
        => Feature = feature;
}
