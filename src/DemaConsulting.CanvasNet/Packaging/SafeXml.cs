using System.Xml;
using System.Xml.Linq;

namespace DemaConsulting.CanvasNet.Packaging;

/// <summary>
///     Loads XML from untrusted input with a hardened, bounded reader: a document type declaration
///     (DTD) is rejected, no external resource is ever resolved, and the total number of characters
///     read is capped.
/// </summary>
/// <remarks>
///     <para>
///         Every method builds its <see cref="XmlReader"/> from <see cref="XmlReaderSettings"/> with
///         <see cref="XmlReaderSettings.DtdProcessing"/> set to <see cref="DtdProcessing.Prohibit"/>,
///         <see cref="XmlReaderSettings.XmlResolver"/> set to <see langword="null"/>,
///         <see cref="XmlReaderSettings.MaxCharactersInDocument"/> set to the caller's
///         <c>maxCharacters</c> and <see cref="XmlReaderSettings.MaxCharactersFromEntities"/> set to a
///         small fixed bound. A document that declares a DTD (the vehicle for external-entity and
///         "billion laughs" entity-expansion attacks) therefore fails to parse with an
///         <see cref="XmlException"/> instead of being processed, and a document larger than
///         <c>maxCharacters</c> is rejected as soon as the reader has consumed that many characters,
///         before the in-memory tree is built.
///     </para>
///     <para>
///         Malformed, DTD-bearing, or oversized input surfaces as <see cref="XmlException"/>, which is
///         left for the caller to translate into its own exception type and wording.
///     </para>
///     <para>
///         <b>Thread safety</b>: this is a stateless static class and is safe to use from any number of
///         threads at once. The returned <see cref="XDocument"/> and <see cref="XmlReader"/> instances
///         are not thread-safe.
///     </para>
/// </remarks>
public static class SafeXml
{
    /// <summary>
    ///     The bound applied to <see cref="XmlReaderSettings.MaxCharactersFromEntities"/>. With DTD
    ///     processing prohibited no entity can be declared, so this is defense in depth only and is far
    ///     above anything a legitimate document can reach.
    /// </summary>
    private const long MaxCharactersFromEntities = 1024;

    /// <summary>
    ///     Parses the XML in <paramref name="stream"/> into an <see cref="XDocument"/> using the
    ///     hardened settings described in the type remarks.
    /// </summary>
    /// <param name="stream">The readable stream to parse. It is left open.</param>
    /// <param name="maxCharacters">The largest number of characters the document may contain.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="maxCharacters"/> is zero or negative (zero would otherwise mean
    ///     "unlimited" to <see cref="XmlReaderSettings.MaxCharactersInDocument"/>).
    /// </exception>
    /// <exception cref="XmlException">
    ///     Thrown when the input is not well-formed XML, declares a DTD, or is larger than
    ///     <paramref name="maxCharacters"/> characters.
    /// </exception>
    public static XDocument Load(Stream stream, int maxCharacters)
    {
        using var reader = CreateReader(stream, maxCharacters);
        return XDocument.Load(reader, LoadOptions.None);
    }

    /// <summary>
    ///     Parses <paramref name="xml"/> into an <see cref="XDocument"/> using the hardened settings
    ///     described in the type remarks.
    /// </summary>
    /// <param name="xml">The XML text to parse.</param>
    /// <param name="maxCharacters">The largest number of characters the document may contain.</param>
    /// <returns>The parsed document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="xml"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="maxCharacters"/> is zero or negative.
    /// </exception>
    /// <exception cref="XmlException">
    ///     Thrown when the input is not well-formed XML, declares a DTD, or is larger than
    ///     <paramref name="maxCharacters"/> characters.
    /// </exception>
    public static XDocument Load(string xml, int maxCharacters)
    {
        ArgumentNullException.ThrowIfNull(xml);
        var settings = CreateSettings(maxCharacters);
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, settings);
        return XDocument.Load(reader, LoadOptions.None);
    }

    /// <summary>
    ///     Creates a forward-only <see cref="XmlReader"/> over <paramref name="stream"/> with the
    ///     hardened settings described in the type remarks, for callers that must stream a document
    ///     (for example to read only its root start tag) instead of building a tree.
    /// </summary>
    /// <param name="stream">The readable stream to parse. It is left open when the reader is disposed.</param>
    /// <param name="maxCharacters">The largest number of characters the reader may consume.</param>
    /// <returns>The reader; the caller disposes it.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="stream"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="stream"/> is not readable.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="maxCharacters"/> is zero or negative.
    /// </exception>
    /// <remarks>
    ///     The reader raises <see cref="XmlException"/> from its read methods when the input is
    ///     malformed, declares a DTD, or exceeds <paramref name="maxCharacters"/> characters.
    /// </remarks>
    public static XmlReader CreateReader(Stream stream, int maxCharacters)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
        {
            throw new ArgumentException("The stream must be readable.", nameof(stream));
        }

        return XmlReader.Create(stream, CreateSettings(maxCharacters));
    }

    /// <summary>Builds the hardened reader settings shared by every method.</summary>
    /// <param name="maxCharacters">The character bound, which must be positive.</param>
    /// <returns>The settings.</returns>
    private static XmlReaderSettings CreateSettings(int maxCharacters)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxCharacters);

        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maxCharacters,
            MaxCharactersFromEntities = MaxCharactersFromEntities,
        };
    }
}
