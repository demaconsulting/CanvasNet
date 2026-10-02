// cspell:ignore AESV StdCF PubSec sAlT SASLprep
using System.Security.Cryptography;
using System.Text;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf;

// S4790 ("Use a stronger hashing algorithm") is suppressed for this entire file: MD5 and RC4 are
// not a free security choice here - they are mandated verbatim by the PDF "Standard" security
// handler's own specification (ISO 32000-1 Algorithms 1/2/4/5) for revisions 2-4, which this file
// exists specifically to interoperate with. A real-world, already-encrypted document's own
// key-derivation algorithm cannot be substituted for a stronger one by this reader.
#pragma warning disable S4790
public sealed partial class PdfDocument
{
    /// <summary>
    ///     The crypt method this document's strings and streams are encrypted with, once
    ///     <see cref="InitializeEncryption(PdfObject, string?)"/> has determined a supported shape and
    ///     derived a file encryption key. <see cref="EncryptionCipher.None"/> (the default) means
    ///     the document is not encrypted at all, in which case <see cref="_encryptionKey"/> is
    ///     always <see langword="null"/> and no decryption is ever attempted.
    /// </summary>
    private enum EncryptionCipher
    {
        /// <summary>The document is not encrypted.</summary>
        None,

        /// <summary>RC4 (<c>/V 1</c> or <c>/V 2</c>), per-object key derived by Algorithm 1.</summary>
        Rc4,

        /// <summary>AES-128-CBC (<c>/V 4</c>, <c>/CFM /AESV2</c>), per-object key derived by Algorithm 1.</summary>
        Aes128,

        /// <summary>
        ///     AES-256-CBC (<c>/V 5</c>, <c>/R 5</c>, <c>/CFM /AESV3</c>), using the file
        ///     encryption key directly for every string/stream (no per-object derivation - see
        ///     ISO 32000-2 Algorithm 2.A's own remarks).
        /// </summary>
        Aes256,
    }

    /// <summary>
    ///     The 32-byte "standard password padding string" ISO 32000-1 Algorithm 2 step (a)
    ///     requires every password to be padded/truncated against before hashing. For an empty
    ///     password (the default, null-password path), the padded password is simply this
    ///     constant, verbatim; see <see cref="PadPasswordBytes"/> for the general case.
    /// </summary>
    private static readonly byte[] PasswordPadding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    /// <summary>
    ///     The 4 literal ASCII bytes <c>sAlT</c> ISO 32000-1 Algorithm 1 appends to the per-object
    ///     key material specifically when the crypt filter method is <c>AESV2</c> (never for
    ///     plain RC4).
    /// </summary>
    private static readonly byte[] AesV2KeySalt = [0x73, 0x41, 0x6C, 0x54];

    /// <summary>
    ///     Pads/truncates <paramref name="passwordBytes"/> to exactly 32 bytes per ISO 32000-1
    ///     Algorithm 2 step (a): the password's own bytes, followed by as many leading bytes of
    ///     <see cref="PasswordPadding"/> as are needed to reach 32 (or the password's own bytes
    ///     truncated to 32, if it is already that long or longer). When
    ///     <paramref name="passwordBytes"/> is empty, this reproduces <see cref="PasswordPadding"/>
    ///     verbatim - preserving byte-for-byte identical behavior for the null-password path.
    /// </summary>
    /// <param name="passwordBytes">The already-encoded (and, for a real password, already
    /// 127-byte-truncated) password bytes to pad/truncate.</param>
    /// <returns>The resulting exactly-32-byte padded password.</returns>
    private static byte[] PadPasswordBytes(byte[] passwordBytes)
    {
        var padded = new byte[32];
        var copyLength = Math.Min(passwordBytes.Length, 32);
        passwordBytes.AsSpan(0, copyLength).CopyTo(padded);
        if (copyLength < 32)
        {
            PasswordPadding.AsSpan(0, 32 - copyLength).CopyTo(padded.AsSpan(copyLength));
        }

        return padded;
    }

    /// <summary>
    ///     Encodes <paramref name="password"/> for an <c>/R 2</c>-<c>4</c> document per
    ///     PDFDocEncoding's ASCII-range subset (ISO 32000-1 7.6.3.3): each character is encoded as
    ///     a single Latin-1 byte, which is identical to PDFDocEncoding for every character in the
    ///     ASCII range (0-127).
    /// </summary>
    /// <remarks>
    ///     <strong>Scope boundary</strong>: full PDFDocEncoding (which remaps several bytes in the
    ///     128-255 range to specific Unicode characters outside Latin-1's own mapping) is not
    ///     implemented - a password containing any character outside ASCII therefore cannot be
    ///     correctly encoded, so this method fails closed with
    ///     <see cref="UnsupportedImageFeatureException"/> rather than silently producing wrong key
    ///     material from an incorrect encoding.
    /// </remarks>
    /// <param name="password">The caller-supplied password.</param>
    /// <returns>The password's Latin-1-encoded bytes, truncated to at most 127 bytes.</returns>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="password"/> contains any character outside ASCII (0-127).
    /// </exception>
    private static byte[] EncodeR2R4PasswordBytes(string password)
    {
        if (password.Any(c => c > 0x7F))
        {
            throw new UnsupportedImageFeatureException(
                "pdf-encrypted-password-non-ascii",
                "The supplied password contains a character outside ASCII (0-127), which is not supported for an /R 2-4 encrypted document (only PDFDocEncoding's ASCII-range subset is supported).");
        }

        var bytes = Encoding.Latin1.GetBytes(password);
        return bytes.Length > 127 ? bytes[..127] : bytes;
    }

    /// <summary>
    ///     Encodes <paramref name="password"/> for an <c>/R 5</c> document per ISO 32000-2
    ///     Algorithm 2.A step (a): UTF-8, truncated to at most 127 bytes.
    /// </summary>
    /// <remarks>
    ///     <strong>Scope boundary</strong>: SASLprep/Unicode normalization (ISO 32000-2 7.6.4.3.4)
    ///     is not applied - an ordinary ASCII password is unaffected (UTF-8 and SASLprep agree for
    ///     every ASCII character), but a password containing combining characters or other
    ///     normalization-sensitive Unicode may not authenticate against a document produced by a
    ///     writer that does apply SASLprep. This is an intentional, narrower scope than the full
    ///     specification, not a defect.
    /// </remarks>
    /// <param name="password">The caller-supplied password.</param>
    /// <returns>The password's UTF-8-encoded bytes, truncated to at most 127 bytes.</returns>
    private static byte[] EncodeR5PasswordBytes(string password)
    {
        var bytes = Encoding.UTF8.GetBytes(password);
        return bytes.Length > 127 ? bytes[..127] : bytes;
    }

    /// <summary>
    ///     The resolved file (or, for AES-256/R5, directly-usable) encryption key, set once by
    ///     <see cref="InitializeEncryption(PdfObject, string?)"/> when the trailer declares a supported
    ///     <c>/Encrypt</c> shape, or left <see langword="null"/> for an unencrypted document.
    /// </summary>
    private byte[]? _encryptionKey;

    /// <summary>
    ///     The crypt method every string/stream is encrypted with, set alongside
    ///     <see cref="_encryptionKey"/>. Defaults to <see cref="EncryptionCipher.None"/> for an
    ///     unencrypted document.
    /// </summary>
    private EncryptionCipher _encryptionCipher;

    /// <summary>
    ///     Detects and, when a supported shape is declared, authenticates an encrypted document's
    ///     user or owner password and derives its file encryption key, so every subsequent
    ///     indirect-object string and stream read (see <c>PdfDocument.Xref.cs</c>'s
    ///     <c>ParseIndirectObjectAt</c>/<c>GetStreamRawBytes</c>) can transparently decrypt its
    ///     bytes before any of this class's other parsing logic ever sees them.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         This method resolves the trailer's own <c>/Encrypt</c> entry via <see cref="Resolve"/>
    ///         - which both parses and <em>caches</em> that object - before <see cref="_encryptionKey"/>
    ///         is ever set, so its own <c>/O</c>/<c>/U</c>/<c>/OE</c>/<c>/UE</c> strings are never
    ///         mistakenly decrypted. By the time this method runs, other objects may already be
    ///         cached pre-key too - the trailer's own <c>/Root</c> Catalog (resolved by the
    ///         constructor's own <c>IsValidCatalogRoot</c> check before this method ever runs) and,
    ///         on the linear-scan fallback path, <em>every</em> object in the document (resolved by
    ///         <c>BuildLinearScanFallback</c>'s own scan) - and none of those pre-key cache entries'
    ///         strings were ever decrypted either. Once <see cref="_encryptionKey"/> is established
    ///         below, this method's own <see cref="InvalidateObjectCacheExceptEncryptDictionary"/>
    ///         step discards every cached object except the <c>/Encrypt</c> dictionary's own, so
    ///         each one is correctly re-resolved - and, this time, decrypted - the next time
    ///         anything asks for it. See
    ///         <c>PdfDocument_Open_Encrypted_CatalogOwnStringIsDecrypted</c> for the regression test
    ///         covering this.
    ///     </para>
    ///     <para>
    ///         <strong>Password authentication</strong>: when <paramref name="password"/> is
    ///         <see langword="null"/>, only the empty user password is authenticated - byte-for-byte
    ///         the same behavior as before this parameter existed. When
    ///         <paramref name="password"/> is non-null, it is tried first as the user password
    ///         (the same Algorithm 2 + Algorithm 4/5/2.A path, except the password's own encoded
    ///         bytes are padded/truncated and hashed instead of always the empty-password padding
    ///         constant); if that does not authenticate, the same supplied password is tried as
    ///         the owner password instead - ISO 32000-1 Algorithm 3 for <c>/R 2</c>-<c>4</c>
    ///         (recovering the padded user password from <c>/O</c>, then deriving and
    ///         authenticating a candidate file key from it), or the owner-password variant of ISO
    ///         32000-2 Algorithm 2.A for <c>/R 5</c> (validating against, and unwrapping <c>/OE</c>
    ///         instead of <c>/UE</c>). A password is encoded via Latin-1 (PDFDocEncoding's ASCII
    ///         subset) for <c>/R 2</c>-<c>4</c> documents - throwing
    ///         <see cref="UnsupportedImageFeatureException"/> with feature
    ///         <c>pdf-encrypted-password-non-ascii</c> for any non-ASCII character, since full
    ///         PDFDocEncoding is out of this phase's scope - or via UTF-8 (no SASLprep/Unicode
    ///         normalization, also an intentional scope boundary) for <c>/R 5</c> documents; either
    ///         way the encoded bytes are truncated to at most 127 bytes before any hashing. If
    ///         neither the user nor the owner attempt authenticates, this method throws
    ///         <see cref="UnsupportedImageFeatureException"/> with feature
    ///         <c>pdf-encrypted-incorrect-password</c>.
    ///     </para>
    ///     <para>
    ///         <strong>Scope boundary</strong>: only the <c>/Filter /Standard</c> security handler
    ///         is supported, and only RC4 (<c>/V 1</c>/<c>/V 2</c>), AES-128 (<c>/V 4</c>/
    ///         <c>/CFM /AESV2</c>), and AES-256 using the simpler R5 key derivation (<c>/V 5</c>/
    ///         <c>/R 5</c>/<c>/CFM /AESV3</c>) are supported. Every other shape (a
    ///         non-<c>/Standard</c> filter, <c>/R 6</c>'s "hardened hash" key derivation, a crypt
    ///         filter other than the standard <c>/StdCF</c>) fails closed with
    ///         <see cref="UnsupportedImageFeatureException"/> and its own distinguishable
    ///         <see cref="UnsupportedImageFeatureException.Feature"/> token.
    ///     </para>
    /// </remarks>
    /// <param name="trailer">The document's resolved trailer dictionary.</param>
    /// <param name="password">
    ///     An optional password to authenticate with - see this method's own remarks above for the
    ///     full user-then-owner attempt order and encoding scope boundaries. <see langword="null"/>
    ///     (the default) authenticates only the empty user password.
    /// </param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the <c>/Encrypt</c> dictionary (or a required entry within it, or the
    ///     trailer's own <c>/ID</c>) is malformed - present but not the shape the specification
    ///     requires - as opposed to merely declaring an unsupported-but-well-formed feature.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the document declares a security handler, crypt filter, or revision this
    ///     phase does not support; when <paramref name="password"/> is <see langword="null"/> and
    ///     the empty user password does not authenticate (feature
    ///     <c>pdf-encrypted-password-required</c>); when <paramref name="password"/> is non-null
    ///     and authenticates as neither the user nor the owner password (feature
    ///     <c>pdf-encrypted-incorrect-password</c>); or when <paramref name="password"/> is
    ///     non-null, contains a non-ASCII character, and the document is <c>/R 2</c>-<c>4</c>
    ///     (feature <c>pdf-encrypted-password-non-ascii</c>).
    /// </exception>
    private void InitializeEncryption(PdfObject trailer, string? password = null)
    {
        var encryptEntry = trailer.Get("Encrypt");
        if (encryptEntry is null)
        {
            return;
        }

        // Resolving (and thereby caching) the /Encrypt dictionary's own object now, before
        // _encryptionKey is ever assigned, is what keeps its own /O, /U, /OE, /UE strings from
        // ever being mistakenly decrypted later - see this method's own remarks above.
        var encryptDict = Resolve(encryptEntry);
        if (encryptDict.Kind != PdfKind.Dictionary && encryptDict.Kind != PdfKind.Stream)
        {
            throw new InvalidDataException("Trailer /Encrypt does not resolve to a dictionary.");
        }

        var filter = GetNameValue(encryptDict, "Filter")
            ?? throw new InvalidDataException("Encrypt dictionary is missing required /Filter.");
        if (filter != "Standard")
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-encrypted-filter-{filter}",
                $"Encrypted PDF security handler '/Filter /{filter}' is not supported (only /Standard is supported).");
        }

        var version = GetRequiredIntEntry(encryptDict, "V");
        var revision = GetRequiredIntEntry(encryptDict, "R");
        var lengthBits = GetIntEntry(encryptDict, "Length", 40);
        var keyLengthBytes = version == 1 ? 5 : lengthBits / 8;

        // /V 5 (AES-256) never uses keyLengthBytes/lengthBits: its 32-byte file key is always
        // derived via ISO 32000-2 Algorithm 2.A (see the "case 5" branch below), independent of
        // whatever /Length value (conventionally 256 bits, outside the legacy 40-128 bit range)
        // the Encrypt dictionary happens to declare - so the ISO 32000-1 §7.6.2 range check below
        // applies only to the legacy /V 1/2/4 (RC4/AESV2) key-length path that actually consumes it.
        if (version != 5 && keyLengthBytes is < 5 or > 16)
        {
            throw new InvalidDataException(
                $"Encrypt dictionary's /Length entry ({lengthBits} bits) must be between 40 and 128 bits inclusive.");
        }

        var oBytes = GetRequiredBytesEntry(encryptDict, "O");
        var uBytes = GetRequiredBytesEntry(encryptDict, "U");
        var permissions = GetRequiredIntEntry(encryptDict, "P");
        var encryptMetadata = GetBooleanEntry(encryptDict, "EncryptMetadata", true);
        var idBytes = GetDocumentIdBytes(trailer);

        switch (version)
        {
            case 1:
            case 2:
                InitializeRc4OrAesV2Encryption(
                    EncryptionCipher.Rc4,
                    revision,
                    keyLengthBytes,
                    oBytes,
                    uBytes,
                    permissions,
                    encryptMetadata,
                    idBytes,
                    password);
                break;

            case 4:
                ValidateStandardCryptFilterName(encryptDict, "AESV2");
                InitializeRc4OrAesV2Encryption(
                    EncryptionCipher.Aes128,
                    revision,
                    keyLengthBytes,
                    oBytes,
                    uBytes,
                    permissions,
                    encryptMetadata,
                    idBytes,
                    password);
                break;

            case 5:
                if (revision == 6)
                {
                    throw new UnsupportedImageFeatureException(
                        "pdf-encrypted-r6-hardened-hash",
                        "AES-256 /R 6 (ISO 32000-2 Annex C 'hardened hash' key derivation) is not supported; only /R 5 is supported.");
                }

                if (revision != 5)
                {
                    throw new UnsupportedImageFeatureException(
                        $"pdf-encrypted-r-{revision}",
                        $"Encrypted PDF /V 5 with /R {revision} is not supported.");
                }

                ValidateStandardCryptFilterName(encryptDict, "AESV3");
                var ueBytes = GetRequiredBytesEntry(encryptDict, "UE");

                byte[] r5PasswordBytes = password is null ? [] : EncodeR5PasswordBytes(password);
                var r5FileKey = TryComputeFileKeyAlgorithm2A(r5PasswordBytes, uBytes, ueBytes);
                if (r5FileKey is null && password is not null)
                {
                    var oeBytes = GetRequiredBytesEntry(encryptDict, "OE");
                    r5FileKey = TryComputeFileKeyAlgorithm2AOwnerPassword(r5PasswordBytes, oBytes, oeBytes, uBytes);
                }

                if (r5FileKey is null)
                {
                    throw new UnsupportedImageFeatureException(
                        password is null ? "pdf-encrypted-password-required" : "pdf-encrypted-incorrect-password",
                        password is null
                            ? "This encrypted PDF document requires a non-empty user password, which was not supplied."
                            : "The supplied password does not authenticate as either the user or the owner password for this encrypted PDF document.");
                }

                _encryptionKey = r5FileKey;
                _encryptionCipher = EncryptionCipher.Aes256;
                break;

            default:
                throw new UnsupportedImageFeatureException(
                    $"pdf-encrypted-v-{version}",
                    $"Encrypted PDF /V {version} is not supported.");
        }

        // Every object resolved and cached so far - by BuildLinearScanFallback's scan of every
        // object number in the document (fallback path, where encryption cannot be initialized
        // until a usable trailer has been recovered), or by this method's own /CF/StdCF lookups
        // above - was cached before _encryptionKey existed, so none of those objects' own
        // strings were ever decrypted. Discard every cached object except the /Encrypt
        // dictionary's own (which
        // must never be decrypted - see this method's remarks above) so each is correctly
        // re-resolved, and decrypted, the next time anything asks for it.
        InvalidateObjectCacheExceptEncryptDictionary(encryptEntry);
    }

    /// <summary>
    ///     Clears <see cref="_objectCache"/> of every object cached before <see cref="_encryptionKey"/>
    ///     was established - by <c>BuildLinearScanFallback</c>'s scan of every object number in
    ///     the document (fallback path only; the normal path initializes encryption before
    ///     resolving <c>/Root</c>), or by this class's own <c>/CF</c>/<c>/StdCF</c> lookups -
    ///     except the <c>/Encrypt</c>
    ///     dictionary's own object (identified via <paramref name="encryptEntry"/>'s own
    ///     reference, not a redundant guard field), which must remain exactly as originally
    ///     cached (pre-key, hence never decrypted) per this file's own
    ///     <c>/O</c>/<c>/U</c>/<c>/OE</c>/<c>/UE</c> invariant.
    /// </summary>
    /// <param name="encryptEntry">The trailer's own, unresolved <c>/Encrypt</c> entry.</param>
    private void InvalidateObjectCacheExceptEncryptDictionary(PdfObject encryptEntry)
    {
        if (encryptEntry.Kind == PdfKind.Reference &&
            _objectCache.TryGetValue(encryptEntry.RefNumber, out var encryptObject))
        {
            _objectCache.Clear();
            _objectCache[encryptEntry.RefNumber] = encryptObject;
            return;
        }

        _objectCache.Clear();
    }

    /// <summary>
    ///     Shared RC4/AES-128 initialization path for <c>/V 1</c>/<c>/V 2</c> (RC4) and <c>/V 4</c>
    ///     (AESV2): derives a file key candidate via ISO 32000-1 Algorithm 2 and authenticates it
    ///     against <c>/U</c> via Algorithm 4 (revision 2) or Algorithm 5 (revision 3/4), first
    ///     treating <paramref name="password"/> as the user password, then - if that does not
    ///     authenticate and <paramref name="password"/> is non-null - recovering the padded user
    ///     password from <c>/O</c> via ISO 32000-1 Algorithm 3 (treating <paramref name="password"/>
    ///     as the owner password) and re-deriving/re-authenticating a second candidate from that.
    ///     Throws <see cref="UnsupportedImageFeatureException"/> when neither attempt authenticates.
    /// </summary>
    /// <param name="cipher">The resolved cipher to use for every subsequent string/stream decrypt.</param>
    /// <param name="revision">The Encrypt dictionary's <c>/R</c> entry.</param>
    /// <param name="keyLengthBytes">The file key length in bytes.</param>
    /// <param name="oBytes">The Encrypt dictionary's raw <c>/O</c> entry bytes.</param>
    /// <param name="uBytes">The Encrypt dictionary's raw <c>/U</c> entry bytes.</param>
    /// <param name="permissions">The Encrypt dictionary's <c>/P</c> entry.</param>
    /// <param name="encryptMetadata">The Encrypt dictionary's <c>/EncryptMetadata</c> entry.</param>
    /// <param name="idBytes">The trailer's <c>/ID</c> array's first element's raw bytes.</param>
    /// <param name="password">
    ///     An optional password to authenticate with - see <see cref="InitializeEncryption(PdfObject, string?)"/>'s
    ///     own remarks for the full user-then-owner attempt order and encoding scope boundaries.
    /// </param>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <paramref name="password"/> is <see langword="null"/> and the empty user
    ///     password does not authenticate (feature <c>pdf-encrypted-password-required</c>), or
    ///     when <paramref name="password"/> is non-null and authenticates as neither the user nor
    ///     the owner password (feature <c>pdf-encrypted-incorrect-password</c>), or when
    ///     <paramref name="password"/> contains a non-ASCII character (feature
    ///     <c>pdf-encrypted-password-non-ascii</c>).
    /// </exception>
    private void InitializeRc4OrAesV2Encryption(
        EncryptionCipher cipher,
        int revision,
        int keyLengthBytes,
        byte[] oBytes,
        byte[] uBytes,
        int permissions,
        bool encryptMetadata,
        byte[] idBytes,
        string? password)
    {
        var paddedPasswordBytes = password is null ? PasswordPadding : PadPasswordBytes(EncodeR2R4PasswordBytes(password));

        var fileKey = ComputeFileKeyAlgorithm2(paddedPasswordBytes, oBytes, permissions, idBytes, keyLengthBytes, revision, encryptMetadata);
        var authenticated = TryAuthenticateUserPasswordAlgorithm45(fileKey, uBytes, idBytes, revision);

        if (!authenticated && password is not null)
        {
            // The same supplied password did not authenticate as the user password - try it as
            // the owner password instead: recover the padded user password from /O (Algorithm 3),
            // then re-derive and re-authenticate a candidate file key from it. The recovered
            // bytes are already exactly 32 bytes (Algorithm 3's own output) - do not re-pad them.
            var recoveredPaddedUserPassword = RecoverPaddedUserPasswordAlgorithm3(paddedPasswordBytes, oBytes, keyLengthBytes, revision);
            var ownerFileKey = ComputeFileKeyAlgorithm2(recoveredPaddedUserPassword, oBytes, permissions, idBytes, keyLengthBytes, revision, encryptMetadata);
            if (TryAuthenticateUserPasswordAlgorithm45(ownerFileKey, uBytes, idBytes, revision))
            {
                fileKey = ownerFileKey;
                authenticated = true;
            }
        }

        if (!authenticated)
        {
            throw new UnsupportedImageFeatureException(
                password is null ? "pdf-encrypted-password-required" : "pdf-encrypted-incorrect-password",
                password is null
                    ? "This encrypted PDF document requires a non-empty user password, which was not supplied."
                    : "The supplied password does not authenticate as either the user or the owner password for this encrypted PDF document.");
        }

        _encryptionKey = fileKey;
        _encryptionCipher = cipher;
    }

    /// <summary>
    ///     Validates that both <c>/StmF</c> and <c>/StrF</c> name the standard <c>/StdCF</c> crypt
    ///     filter, and that <c>/CF/StdCF/CFM</c> names <paramref name="expectedCfm"/> - the only
    ///     crypt-filter shape this phase supports for <c>/V 4</c>/<c>/V 5</c> (a custom-named crypt
    ///     filter, <c>/Identity</c>, or a mismatched <c>/CFM</c> all fail closed with their own
    ///     distinguishable <see cref="UnsupportedImageFeatureException.Feature"/> token).
    /// </summary>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when <c>/StmF</c>/<c>/StrF</c> name anything other than <c>/StdCF</c>, or
    ///     <c>/CF/StdCF/CFM</c> names anything other than <paramref name="expectedCfm"/>.
    /// </exception>
    private void ValidateStandardCryptFilterName(PdfObject encryptDict, string expectedCfm)
    {
        foreach (var key in new[] { "StmF", "StrF" })
        {
            var name = GetNameValue(encryptDict, key) ?? "Identity";
            if (name != "StdCF")
            {
                throw new UnsupportedImageFeatureException(
                    $"pdf-encrypted-crypt-filter-{name}",
                    $"Encrypted PDF /{key} crypt filter '/{name}' is not supported (only /StdCF is supported).");
            }
        }

        var cryptFiltersEntry = encryptDict.Get("CF")
            ?? throw new InvalidDataException("Encrypt dictionary is missing required /CF.");
        var cryptFilters = Resolve(cryptFiltersEntry);
        var stdCfEntry = cryptFilters.Get("StdCF")
            ?? throw new InvalidDataException("Encrypt dictionary is missing required /CF/StdCF.");
        var stdCf = Resolve(stdCfEntry);
        var cfm = GetNameValue(stdCf, "CFM")
            ?? throw new InvalidDataException("Encrypt dictionary's /CF/StdCF is missing required /CFM.");
        if (cfm != expectedCfm)
        {
            throw new UnsupportedImageFeatureException(
                $"pdf-encrypted-cfm-{cfm}",
                $"Encrypted PDF crypt filter method '/CFM /{cfm}' is not supported (expected /{expectedCfm}).");
        }
    }

    /// <summary>
    ///     Computes the document's file encryption key per ISO 32000-1 Algorithm 2 steps (b)-(e),
    ///     given an already-padded 32-byte password (step (a) - see <see cref="PadPasswordBytes"/>).
    /// </summary>
    /// <remarks>
    ///     Algorithm 2 alone never fails - it always produces <em>a</em> key, regardless of
    ///     whether <paramref name="paddedPasswordBytes"/> is actually correct; only Algorithm
    ///     4/5/2.A's separate comparison against <c>/U</c> can detect a wrong password (see
    ///     <see cref="TryAuthenticateUserPasswordAlgorithm45"/>).
    /// </remarks>
    /// <param name="paddedPasswordBytes">
    ///     The already-padded/truncated-to-32-bytes password (step (a)): either
    ///     <see cref="PasswordPadding"/> verbatim for an empty password, the result of
    ///     <see cref="PadPasswordBytes"/> for a real supplied password, or - for the owner-password
    ///     recovery path - <see cref="RecoverPaddedUserPasswordAlgorithm3"/>'s own already-32-byte
    ///     output (which must not be re-padded).
    /// </param>
    /// <param name="oBytes">The Encrypt dictionary's raw <c>/O</c> entry bytes.</param>
    /// <param name="permissions">The Encrypt dictionary's <c>/P</c> entry, as a signed 32-bit integer.</param>
    /// <param name="idBytes">The trailer's <c>/ID</c> array's first element's raw bytes.</param>
    /// <param name="keyLengthBytes">The file key length in bytes (<c>/Length</c> in bits, divided by 8).</param>
    /// <param name="revision">The Encrypt dictionary's <c>/R</c> entry.</param>
    /// <param name="encryptMetadata">The Encrypt dictionary's <c>/EncryptMetadata</c> entry (default <see langword="true"/>).</param>
    /// <returns>The <paramref name="keyLengthBytes"/>-byte file encryption key.</returns>
    private static byte[] ComputeFileKeyAlgorithm2(
        byte[] paddedPasswordBytes,
        byte[] oBytes,
        int permissions,
        byte[] idBytes,
        int keyLengthBytes,
        int revision,
        bool encryptMetadata)
    {
        // Step (b)-(c): build the MD5 input (padded password + /O + /P as 4-byte little-endian
        // signed integer + the first /ID element's raw bytes + 0xFFFFFFFF when revision >= 4 and
        // /EncryptMetadata is explicitly false) and hash it.
        using var input = new MemoryStream();
        input.Write(paddedPasswordBytes);
        input.Write(oBytes);
        input.Write([(byte)permissions, (byte)(permissions >> 8), (byte)(permissions >> 16), (byte)(permissions >> 24)]);
        input.Write(idBytes);
        if (revision >= 4 && !encryptMetadata)
        {
            input.Write([0xFF, 0xFF, 0xFF, 0xFF]);
        }

        var digest = MD5.HashData(input.ToArray());

        // Step (d): revision 3 and above additionally re-hashes the first keyLengthBytes of the
        // previous digest, 50 times over.
        digest = Rehash50RoundsIfRevisionAtLeast3(digest, keyLengthBytes, revision);

        // Step (e): the file encryption key is the first keyLengthBytes of the final digest.
        return digest.AsSpan(0, keyLengthBytes).ToArray();
    }

    /// <summary>
    ///     Shared "re-hash the first <paramref name="keyLengthBytes"/> of <paramref name="digest"/>
    ///     50 times over" step used identically by both ISO 32000-1 Algorithm 2 step (d) (file key
    ///     derivation) and Algorithm 3 step (c) (owner-password recovery) for revision 3 and above
    ///     - revision 2 never re-hashes, returning <paramref name="digest"/> unchanged.
    /// </summary>
    /// <param name="digest">The initial MD5 digest.</param>
    /// <param name="keyLengthBytes">The number of leading bytes of <paramref name="digest"/> re-hashed each round.</param>
    /// <param name="revision">The Encrypt dictionary's <c>/R</c> entry.</param>
    /// <returns><paramref name="digest"/>, unchanged for revision 2, or re-hashed 50 times for revision 3 and above.</returns>
    private static byte[] Rehash50RoundsIfRevisionAtLeast3(byte[] digest, int keyLengthBytes, int revision)
    {
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                digest = MD5.HashData(digest.AsSpan(0, keyLengthBytes));
            }
        }

        return digest;
    }


    /// <summary>
    ///     Checks whether <paramref name="fileKey"/> authenticates as the user password for
    ///     revisions 2-4 by recomputing the expected <c>/U</c> value from it (ISO 32000-1
    ///     Algorithm 4 for revision 2, Algorithm 5 for revision 3/4) and comparing it against the
    ///     document's actual <c>/U</c> entry.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Algorithm 4/5 always hashes/encrypts the literal 32-byte padding string
    ///         (<see cref="PasswordPadding"/>), never the actual supplied password's own bytes -
    ///         per ISO 32000-1 7.6.3.3/7.6.3.4, the password's own bytes only ever feed into
    ///         Algorithm 2's file-key derivation (<see cref="ComputeFileKeyAlgorithm2"/>), not into
    ///         this comparison. This method therefore needs no password-bytes parameter of its
    ///         own - only the candidate <paramref name="fileKey"/> to verify.
    ///     </para>
    ///     <para>
    ///         Per the specification, only the first 16 of <c>/U</c>'s 32 bytes are compared for
    ///         revision 3/4 (the trailing 16 bytes are producer-defined padding, not a
    ///         deterministic function of the key) - comparing all 32 would reject documents
    ///         produced by a conforming writer using a different padding convention.
    ///     </para>
    /// </remarks>
    /// <param name="fileKey">The candidate file encryption key to verify.</param>
    /// <param name="uBytes">The Encrypt dictionary's raw <c>/U</c> entry bytes.</param>
    /// <param name="idBytes">The trailer's <c>/ID</c> array's first element's raw bytes.</param>
    /// <param name="revision">The Encrypt dictionary's <c>/R</c> entry.</param>
    /// <returns>
    ///     <see langword="true"/> when <paramref name="fileKey"/> authenticates against
    ///     <paramref name="uBytes"/>; <see langword="false"/> otherwise.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="uBytes"/> is shorter than the specification requires
    ///     (32 bytes for revision 2, 16 bytes for revision 3/4).
    /// </exception>
    private static bool TryAuthenticateUserPasswordAlgorithm45(byte[] fileKey, byte[] uBytes, byte[] idBytes, int revision)
    {
        bool authenticated;
        if (revision == 2)
        {
            // Algorithm 4: RC4-encrypt the padding string directly with the file key, and compare
            // the full 32 bytes.
            if (uBytes.Length < 32)
            {
                throw new InvalidDataException("Encrypt dictionary's /U entry is too short for revision 2.");
            }

            var expected = Rc4Transform(fileKey, PasswordPadding);
            authenticated = expected.AsSpan().SequenceEqual(uBytes.AsSpan(0, 32));
        }
        else
        {
            // Algorithm 5: MD5-hash(padding string + ID bytes), RC4-encrypt with the file key,
            // then 19 further rounds of RC4 with the key XORed byte-wise with the round number
            // (1-19), comparing only the first 16 of /U's 32 bytes.
            if (uBytes.Length < 16)
            {
                throw new InvalidDataException("Encrypt dictionary's /U entry is too short for revision 3/4.");
            }

            using var hashInput = new MemoryStream();
            hashInput.Write(PasswordPadding);
            hashInput.Write(idBytes);
            var result = MD5.HashData(hashInput.ToArray());
            result = Rc4Transform(fileKey, result);
            for (var round = 1; round <= 19; round++)
            {
                var roundKey = new byte[fileKey.Length];
                for (var i = 0; i < fileKey.Length; i++)
                {
                    roundKey[i] = (byte)(fileKey[i] ^ round);
                }

                result = Rc4Transform(roundKey, result);
            }

            authenticated = result.AsSpan().SequenceEqual(uBytes.AsSpan(0, 16));
        }

        return authenticated;
    }

    /// <summary>
    ///     Recovers the padded user password from <c>/O</c> per ISO 32000-1 Algorithm 3's decrypt
    ///     direction, treating <paramref name="paddedOwnerPasswordBytes"/> as the padded owner
    ///     password: MD5-hashes it (re-hashing 50 rounds for revision 3 and above, via the same
    ///     <see cref="Rehash50RoundsIfRevisionAtLeast3"/> helper Algorithm 2 step (d) uses) to
    ///     derive the owner RC4 key, then RC4-decrypts <c>/O</c> with it - a single pass for
    ///     revision 2, or 20 rounds (round 19 down to round 0, round 0 using the unmodified owner
    ///     key, each other round's key XORed byte-wise with its own round number) for revision 3/4.
    /// </summary>
    /// <param name="paddedOwnerPasswordBytes">The padded (32-byte) candidate owner password.</param>
    /// <param name="oBytes">The Encrypt dictionary's raw <c>/O</c> entry bytes (32 bytes).</param>
    /// <param name="keyLengthBytes">The file key length in bytes.</param>
    /// <param name="revision">The Encrypt dictionary's <c>/R</c> entry.</param>
    /// <returns>
    ///     The recovered 32-byte padded user password - already exactly 32 bytes (Algorithm 3's
    ///     own output), so callers must feed it directly into
    ///     <see cref="ComputeFileKeyAlgorithm2"/> without re-padding it via
    ///     <see cref="PadPasswordBytes"/>.
    /// </returns>
    private static byte[] RecoverPaddedUserPasswordAlgorithm3(byte[] paddedOwnerPasswordBytes, byte[] oBytes, int keyLengthBytes, int revision)
    {
        var digest = MD5.HashData(paddedOwnerPasswordBytes);
        digest = Rehash50RoundsIfRevisionAtLeast3(digest, keyLengthBytes, revision);
        var ownerKey = digest.AsSpan(0, keyLengthBytes).ToArray();

        var result = oBytes;
        if (revision == 2)
        {
            result = Rc4Transform(ownerKey, result);
        }
        else
        {
            for (var round = 19; round >= 0; round--)
            {
                var roundKey = new byte[ownerKey.Length];
                for (var i = 0; i < ownerKey.Length; i++)
                {
                    roundKey[i] = (byte)(ownerKey[i] ^ round);
                }

                result = Rc4Transform(roundKey, result);
            }
        }

        return result;
    }

    /// <summary>
    ///     Computes the file encryption key for AES-256/R5 by trying <paramref name="passwordBytes"/>
    ///     as the user password (ISO 32000-2 Algorithm 2.A): validates it against <c>/U</c>'s
    ///     embedded validation salt, then - on success - unwraps <c>/UE</c> using a key derived
    ///     from <c>/U</c>'s embedded key salt.
    /// </summary>
    /// <param name="passwordBytes">
    ///     The UTF-8-encoded, 127-byte-truncated candidate user password (empty for the
    ///     null-password path, in which case the hash/intermediate-key computation below reduces
    ///     to simply <c>SHA-256(salt)</c>).
    /// </param>
    /// <param name="uBytes">
    ///     The Encrypt dictionary's 48-byte <c>/U</c> entry: 32 bytes of hash, 8 bytes of
    ///     validation salt, 8 bytes of key salt.
    /// </param>
    /// <param name="ueBytes">The Encrypt dictionary's 32-byte <c>/UE</c> entry.</param>
    /// <returns>
    ///     The 32-byte file encryption key (used directly, with no further per-object derivation)
    ///     when <paramref name="passwordBytes"/> authenticates against <c>/U</c>'s validation
    ///     salt; <see langword="null"/> when it does not.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="uBytes"/>/<paramref name="ueBytes"/> are not the lengths
    ///     the specification requires.
    /// </exception>
    private static byte[]? TryComputeFileKeyAlgorithm2A(byte[] passwordBytes, byte[] uBytes, byte[] ueBytes)
    {
        if (uBytes.Length < 48)
        {
            throw new InvalidDataException("Encrypt dictionary's /U entry must be at least 48 bytes for /V 5.");
        }

        if (ueBytes.Length != 32)
        {
            throw new InvalidDataException("Encrypt dictionary's /UE entry must be exactly 32 bytes for /V 5.");
        }

        var hash = uBytes.AsSpan(0, 32);
        var validationSalt = uBytes.AsSpan(32, 8);
        var keySalt = uBytes.AsSpan(40, 8);

        // Step 1: SHA-256(password + validationSalt) - authenticate by comparing against /U's
        // own embedded hash.
        var computedHash = SHA256.HashData([.. passwordBytes, .. validationSalt]);
        if (!computedHash.AsSpan().SequenceEqual(hash))
        {
            return null;
        }

        // Step 2: the intermediate key is SHA-256(password + keySalt).
        var intermediateKey = SHA256.HashData([.. passwordBytes, .. keySalt]);

        // Step 3: the file encryption key is AES-256-CBC-decrypt(/UE) using the intermediate key,
        // a zero IV, and no padding (/UE decrypts to exactly the raw 32-byte file key).
        return DecryptAesCbc(intermediateKey, new byte[16], ueBytes, 0, ueBytes.Length, CipherMode.CBC, PaddingMode.None);
    }

    /// <summary>
    ///     Computes the file encryption key for AES-256/R5 by trying <paramref name="passwordBytes"/>
    ///     as the owner password - the owner-password variant of ISO 32000-2 Algorithm 2.A: the
    ///     hash/intermediate-key computation is the same shape as
    ///     <see cref="TryComputeFileKeyAlgorithm2A"/>'s user-password version, except the salts
    ///     come from <c>/O</c> instead of <c>/U</c>, the full 48-byte <c>/U</c> value (not a
    ///     sub-slice) is appended after the salt in both hash inputs, the validation hash is
    ///     compared against <c>/O</c>'s own embedded hash instead of <c>/U</c>'s, and <c>/OE</c>
    ///     (not <c>/UE</c>) is unwrapped to recover the file key.
    /// </summary>
    /// <param name="passwordBytes">
    ///     The UTF-8-encoded, 127-byte-truncated candidate owner password.
    /// </param>
    /// <param name="oBytes">
    ///     The Encrypt dictionary's 48-byte <c>/O</c> entry: 32 bytes of hash, 8 bytes of
    ///     validation salt, 8 bytes of key salt.
    /// </param>
    /// <param name="oeBytes">The Encrypt dictionary's 32-byte <c>/OE</c> entry.</param>
    /// <param name="uBytes">The Encrypt dictionary's full, raw 48-byte <c>/U</c> entry.</param>
    /// <returns>
    ///     The 32-byte file encryption key when <paramref name="passwordBytes"/> authenticates
    ///     against <c>/O</c>'s validation hash; <see langword="null"/> when it does not.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="oBytes"/>/<paramref name="oeBytes"/>/<paramref name="uBytes"/>
    ///     are not the lengths the specification requires.
    /// </exception>
    private static byte[]? TryComputeFileKeyAlgorithm2AOwnerPassword(byte[] passwordBytes, byte[] oBytes, byte[] oeBytes, byte[] uBytes)
    {
        if (oBytes.Length < 48)
        {
            throw new InvalidDataException("Encrypt dictionary's /O entry must be at least 48 bytes for /V 5.");
        }

        if (oeBytes.Length != 32)
        {
            throw new InvalidDataException("Encrypt dictionary's /OE entry must be exactly 32 bytes for /V 5.");
        }

        if (uBytes.Length < 48)
        {
            throw new InvalidDataException("Encrypt dictionary's /U entry must be at least 48 bytes for /V 5.");
        }

        var hash = oBytes.AsSpan(0, 32);
        var validationSalt = oBytes.AsSpan(32, 8);
        var keySalt = oBytes.AsSpan(40, 8);
        var fullU = uBytes.AsSpan(0, 48);

        // Step 1: SHA-256(password + validationSalt + U(48 bytes)) - authenticate by comparing
        // against /O's own embedded hash.
        var computedHash = SHA256.HashData([.. passwordBytes, .. validationSalt, .. fullU]);
        if (!computedHash.AsSpan().SequenceEqual(hash))
        {
            return null;
        }

        // Step 2: the intermediate key is SHA-256(password + keySalt + U(48 bytes)).
        var intermediateKey = SHA256.HashData([.. passwordBytes, .. keySalt, .. fullU]);

        // Step 3: the file encryption key is AES-256-CBC-decrypt(/OE) using the intermediate key,
        // a zero IV, and no padding (/OE decrypts to exactly the raw 32-byte file key).
        return DecryptAesCbc(intermediateKey, new byte[16], oeBytes, 0, oeBytes.Length, CipherMode.CBC, PaddingMode.None);
    }

    /// <summary>
    ///     Computes a per-object encryption key per ISO 32000-1 Algorithm 1, used by RC4 and
    ///     AES-128/AESV2 (revisions 2-4). AES-256/R5 does not use this - it uses the file
    ///     encryption key directly for every object (see <see cref="TryComputeFileKeyAlgorithm2A"/>'s
    ///     own remarks).
    /// </summary>
    /// <param name="fileKey">The file encryption key from <see cref="ComputeFileKeyAlgorithm2"/>.</param>
    /// <param name="objectNumber">The owning indirect object's object number.</param>
    /// <param name="generation">The owning indirect object's generation number.</param>
    /// <param name="isAes">
    ///     <see langword="true"/> for the AESV2 crypt filter, which additionally appends the
    ///     literal 4-byte <c>sAlT</c> suffix (<see cref="AesV2KeySalt"/>); <see langword="false"/>
    ///     for plain RC4.
    /// </param>
    /// <returns>
    ///     The per-object key: the first <c>min(fileKey.Length + 5, 16)</c> bytes of
    ///     <c>MD5(fileKey + objectNumber(3 bytes LE) + generation(2 bytes LE) [+ "sAlT"])</c>.
    /// </returns>
    private static byte[] ComputeObjectKeyAlgorithm1(byte[] fileKey, int objectNumber, int generation, bool isAes)
    {
        using var input = new MemoryStream();
        input.Write(fileKey);
        input.Write([(byte)objectNumber, (byte)(objectNumber >> 8), (byte)(objectNumber >> 16)]);
        input.Write([(byte)generation, (byte)(generation >> 8)]);
        if (isAes)
        {
            input.Write(AesV2KeySalt);
        }

        var digest = MD5.HashData(input.ToArray());
        var keyLength = Math.Min(fileKey.Length + 5, 16);
        return digest.AsSpan(0, keyLength).ToArray();
    }

    /// <summary>
    ///     Decrypts a single stream's raw (still-filtered) bytes, dispatching on
    ///     <see cref="_encryptionCipher"/>. Called by <c>PdfDocument.Xref.cs</c>'s
    ///     <c>GetStreamRawBytes</c>, before the generic <c>/Filter</c>/<c>/DecodeParms</c> pipeline
    ///     ever runs - so every downstream consumer (content-stream execution, object-stream
    ///     decompression, image-XObject decoding) sees already-plaintext filtered bytes, exactly
    ///     as it would for an unencrypted document.
    /// </summary>
    /// <param name="data">The stream's raw, still-encrypted bytes.</param>
    /// <param name="objectNumber">The owning indirect object's object number.</param>
    /// <param name="generation">The owning indirect object's generation number.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when AES-encrypted data is too short to contain its leading 16-byte IV, or when
    ///     the AES-CBC/PKCS7 padding is malformed (wrapped from <see cref="CryptographicException"/>).
    /// </exception>
    private byte[] DecryptStreamBytes(byte[] data, int objectNumber, int generation)
    {
        if (data.Length == 0)
        {
            return data;
        }

        switch (_encryptionCipher)
        {
            case EncryptionCipher.Rc4:
                return Rc4Transform(ComputeObjectKeyAlgorithm1(_encryptionKey!, objectNumber, generation, isAes: false), data);

            case EncryptionCipher.Aes128:
                {
                    var objectKey = ComputeObjectKeyAlgorithm1(_encryptionKey!, objectNumber, generation, isAes: true);
                    return DecryptIvPrefixedAesCbc(objectKey, data);
                }

            case EncryptionCipher.Aes256:
                return DecryptIvPrefixedAesCbc(_encryptionKey!, data);

            case EncryptionCipher.None:
            default:
                return data;
        }
    }

    /// <summary>
    ///     Decrypts strings found anywhere within a freshly-parsed top-level indirect object's
    ///     value, in place, recursing through <see cref="PdfObject.Items"/> (arrays) and
    ///     <see cref="PdfObject.Entries"/> (dictionaries/streams) but never through
    ///     <see cref="PdfKind.Reference"/> (an indirect reference is resolved, and therefore
    ///     decrypted if applicable, independently by its own call to <c>ParseIndirectObjectAt</c>
    ///     - following it here would either double-decrypt an already-cached object or recurse
    ///     into a different object entirely).
    /// </summary>
    /// <param name="value">The parsed value to decrypt strings within.</param>
    /// <param name="objectNumber">The owning indirect object's object number.</param>
    /// <param name="generation">The owning indirect object's generation number.</param>
    private void DecryptStringsInPlace(PdfObject value, int objectNumber, int generation)
    {
        switch (value.Kind)
        {
            case PdfKind.String:
                value.Bytes = DecryptStreamBytes(value.Bytes, objectNumber, generation);
                break;

            case PdfKind.Array:
                foreach (var item in value.Items)
                {
                    DecryptStringsInPlace(item, objectNumber, generation);
                }

                break;

            case PdfKind.Dictionary:
            case PdfKind.Stream:
                foreach (var entry in value.Entries.Values)
                {
                    DecryptStringsInPlace(entry, objectNumber, generation);
                }

                break;

            default:
                // Null, Boolean, Number, Name, and Reference values carry no string bytes of
                // their own to decrypt.
                break;
        }
    }

    /// <summary>
    ///     Decrypts an <c>{16-byte IV}{AES-CBC/PKCS7-encrypted ciphertext}</c> byte layout - the
    ///     wire format every AESV2/AESV3-encrypted string/stream uses, regardless of key size.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="data"/> is shorter than the 16-byte IV, or the AES-CBC/
    ///     PKCS7 padding is malformed.
    /// </exception>
    private static byte[] DecryptIvPrefixedAesCbc(byte[] key, byte[] data)
    {
        if (data.Length < 16)
        {
            throw new InvalidDataException("AES-encrypted data is too short to contain its leading 16-byte IV.");
        }

        var iv = data.AsSpan(0, 16).ToArray();
        var cipherLength = data.Length - 16;
        if (cipherLength == 0)
        {
            return [];
        }

        return DecryptAesCbc(key, iv, data, 16, cipherLength, CipherMode.CBC, PaddingMode.PKCS7);
    }

    /// <summary>
    ///     Thin wrapper around <see cref="Aes"/> for one-shot CBC decryption, wrapping any
    ///     <see cref="CryptographicException"/> (for example from malformed PKCS7 padding) into
    ///     this class's own <see cref="InvalidDataException"/> convention rather than letting an
    ///     unfamiliar exception type escape to the caller.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when decryption fails (propagated from a caught <see cref="CryptographicException"/>).
    /// </exception>
    private static byte[] DecryptAesCbc(byte[] key, byte[] iv, byte[] data, int offset, int length, CipherMode mode, PaddingMode padding)
    {
        using var aes = Aes.Create();
        try
        {
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = mode;
            aes.Padding = padding;
            using var decryptor = aes.CreateDecryptor();
            return decryptor.TransformFinalBlock(data, offset, length);
        }
        catch (CryptographicException ex)
        {
            throw new InvalidDataException("Encrypted PDF stream/string data is malformed (AES-CBC decryption failed).", ex);
        }
    }

    /// <summary>
    ///     A from-scratch, hand-rolled RC4 stream cipher (classic KSA/PRGA), since RC4 is not
    ///     available as a built-in primitive in modern .NET. RC4 is symmetric (the same operation
    ///     both encrypts and decrypts), so this single implementation backs every production
    ///     decrypt call site and every test-only encrypt-side helper alike.
    /// </summary>
    /// <param name="key">The RC4 key (1-256 bytes).</param>
    /// <param name="data">The data to transform.</param>
    /// <returns>A new array the same length as <paramref name="data"/>, XORed with the RC4 keystream.</returns>
    private static byte[] Rc4Transform(byte[] key, byte[] data)
    {
        // Key-scheduling algorithm (KSA): build a permutation of 0-255 seeded by the key.
        var state = new byte[256];
        for (var i = 0; i < 256; i++)
        {
            state[i] = (byte)i;
        }

        var j = 0;
        for (var i = 0; i < 256; i++)
        {
            j = (j + state[i] + key[i % key.Length]) & 0xFF;
            (state[i], state[j]) = (state[j], state[i]);
        }

        // Pseudo-random generation algorithm (PRGA): derive a keystream byte per input byte and
        // XOR it directly into the output.
        var output = new byte[data.Length];
        var x = 0;
        j = 0;
        for (var n = 0; n < data.Length; n++)
        {
            x = (x + 1) & 0xFF;
            j = (j + state[x]) & 0xFF;
            (state[x], state[j]) = (state[j], state[x]);
            var keystreamByte = state[(state[x] + state[j]) & 0xFF];
            output[n] = (byte)(data[n] ^ keystreamByte);
        }

        return output;
    }

    /// <summary>Reads a required integer dictionary entry (resolving an indirect reference).</summary>
    /// <exception cref="InvalidDataException">Thrown when the entry is absent, or present but not a number.</exception>
    private int GetRequiredIntEntry(PdfObject dictionary, string key)
    {
        var entry = dictionary.Get(key) ?? throw new InvalidDataException($"Encrypt dictionary is missing required /{key}.");
        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.Number)
        {
            throw new InvalidDataException($"Encrypt dictionary's /{key} must be a number.");
        }

        return (int)resolved.Number;
    }

    /// <summary>Reads a required string dictionary entry's raw bytes (resolving an indirect reference).</summary>
    /// <exception cref="InvalidDataException">Thrown when the entry is absent, or present but not a string.</exception>
    private byte[] GetRequiredBytesEntry(PdfObject dictionary, string key)
    {
        var entry = dictionary.Get(key) ?? throw new InvalidDataException($"Encrypt dictionary is missing required /{key}.");
        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.String)
        {
            throw new InvalidDataException($"Encrypt dictionary's /{key} must be a string.");
        }

        return resolved.Bytes;
    }

    /// <summary>Reads a boolean dictionary entry (resolving an indirect reference), or a default when absent.</summary>
    /// <exception cref="InvalidDataException">Thrown when the entry is present but not a boolean.</exception>
    private bool GetBooleanEntry(PdfObject dictionary, string key, bool defaultValue)
    {
        var entry = dictionary.Get(key);
        if (entry is null)
        {
            return defaultValue;
        }

        var resolved = Resolve(entry);
        if (resolved.Kind != PdfKind.Boolean)
        {
            throw new InvalidDataException($"Encrypt dictionary's /{key} must be a boolean.");
        }

        return resolved.Boolean;
    }

    /// <summary>
    ///     Reads the trailer's <c>/ID</c> array's first element's raw bytes, required by ISO
    ///     32000-1 Algorithm 2's file-key derivation.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <c>/ID</c> is absent, not a non-empty array, or its first element does not
    ///     resolve to a string.
    /// </exception>
    private byte[] GetDocumentIdBytes(PdfObject trailer)
    {
        var idEntry = trailer.Get("ID") ?? throw new InvalidDataException("Encrypted document trailer is missing required /ID.");
        var resolvedId = Resolve(idEntry);
        if (resolvedId.Kind != PdfKind.Array || resolvedId.Items.Count == 0)
        {
            throw new InvalidDataException("Encrypted document trailer's /ID must be a non-empty array.");
        }

        var firstId = Resolve(resolvedId.Items[0]);
        if (firstId.Kind != PdfKind.String)
        {
            throw new InvalidDataException("Encrypted document trailer's /ID first element must be a string.");
        }

        return firstId.Bytes;
    }
}
#pragma warning restore S4790
