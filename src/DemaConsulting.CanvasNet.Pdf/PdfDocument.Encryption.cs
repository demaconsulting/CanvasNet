// cspell:ignore AESV StdCF PubSec sAlT
using System.Security.Cryptography;
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
    ///     <see cref="InitializeEncryption(PdfObject)"/> has determined a supported shape and
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
    ///     requires every password to be padded/truncated against before hashing. For the empty
    ///     user password this phase exclusively supports, the padded password is simply this
    ///     constant, verbatim.
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
    ///     The resolved file (or, for AES-256/R5, directly-usable) encryption key, set once by
    ///     <see cref="InitializeEncryption(PdfObject)"/> when the trailer declares a supported
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
    ///     Detects and, for the narrow scope this phase supports, transparently authenticates an
    ///     encrypted document's empty user password and derives its file encryption key, so every
    ///     subsequent indirect-object string and stream read (see <c>PdfDocument.Xref.cs</c>'s
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
    ///         <strong>Scope boundary</strong>: only the <c>/Filter /Standard</c> security handler
    ///         is supported, only RC4 (<c>/V 1</c>/<c>/V 2</c>), AES-128 (<c>/V 4</c>/
    ///         <c>/CFM /AESV2</c>), and AES-256 using the simpler R5 key derivation (<c>/V 5</c>/
    ///         <c>/R 5</c>/<c>/CFM /AESV3</c>) are supported, and only an empty user password is
    ///         ever authenticated - there is no API surface to supply any other password. Every
    ///         other shape (a non-<c>/Standard</c> filter, <c>/R 6</c>'s "hardened hash" key
    ///         derivation, a crypt filter other than the standard <c>/StdCF</c>, or a document that
    ///         genuinely requires a non-empty password) fails closed with
    ///         <see cref="UnsupportedImageFeatureException"/> and its own distinguishable
    ///         <see cref="UnsupportedImageFeatureException.Feature"/> token.
    ///     </para>
    /// </remarks>
    /// <param name="trailer">The document's resolved trailer dictionary.</param>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the <c>/Encrypt</c> dictionary (or a required entry within it, or the
    ///     trailer's own <c>/ID</c>) is malformed - present but not the shape the specification
    ///     requires - as opposed to merely declaring an unsupported-but-well-formed feature.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the document declares a security handler, crypt filter, or revision this
    ///     phase does not support, or when the empty user password does not authenticate (a real,
    ///     non-empty password is required to open the document).
    /// </exception>
    private void InitializeEncryption(PdfObject trailer)
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
                    idBytes);
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
                    idBytes);
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
                _encryptionKey = ComputeFileKeyAlgorithm2A(uBytes, ueBytes);
                _encryptionCipher = EncryptionCipher.Aes256;
                break;

            default:
                throw new UnsupportedImageFeatureException(
                    $"pdf-encrypted-v-{version}",
                    $"Encrypted PDF /V {version} is not supported.");
        }

        // Every object resolved and cached so far - by IsValidCatalogRoot's own /Root lookup
        // (normal path), by BuildLinearScanFallback's scan of every object number in the
        // document (fallback path), or by this method's own /CF/StdCF lookups above - was
        // cached before _encryptionKey existed, so none of those objects' own strings were ever
        // decrypted. Discard every cached object except the /Encrypt dictionary's own (which
        // must never be decrypted - see this method's remarks above) so each is correctly
        // re-resolved, and decrypted, the next time anything asks for it.
        InvalidateObjectCacheExceptEncryptDictionary(encryptEntry);
    }

    /// <summary>
    ///     Clears <see cref="_objectCache"/> of every object cached before <see cref="_encryptionKey"/>
    ///     was established - by <c>IsValidCatalogRoot</c>'s own <c>/Root</c> lookup, by
    ///     <c>BuildLinearScanFallback</c>'s scan of every object number in the document, or by
    ///     this class's own <c>/CF</c>/<c>/StdCF</c> lookups - except the <c>/Encrypt</c>
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
    ///     (AESV2): derives the file encryption key via ISO 32000-1 Algorithm 2, then authenticates
    ///     the empty user password against <c>/U</c> via Algorithm 4 (revision 2) or Algorithm 5
    ///     (revision 3/4), throwing <see cref="UnsupportedImageFeatureException"/> when a real,
    ///     non-empty password is actually required.
    /// </summary>
    private void InitializeRc4OrAesV2Encryption(
        EncryptionCipher cipher,
        int revision,
        int keyLengthBytes,
        byte[] oBytes,
        byte[] uBytes,
        int permissions,
        bool encryptMetadata,
        byte[] idBytes)
    {
        var fileKey = ComputeFileKeyAlgorithm2(oBytes, permissions, idBytes, keyLengthBytes, revision, encryptMetadata);
        AuthenticateEmptyUserPasswordAlgorithm45(fileKey, uBytes, idBytes, revision);
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
    ///     Computes the document's file encryption key per ISO 32000-1 Algorithm 2, for the empty
    ///     user password this phase exclusively supports (so the "pad or truncate the password"
    ///     step always yields <see cref="PasswordPadding"/> verbatim).
    /// </summary>
    /// <remarks>
    ///     Algorithm 2 alone never fails - it always produces <em>a</em> key, regardless of
    ///     whether the (here: empty) password is actually correct; only Algorithm 4/5/2.A's
    ///     separate comparison against <c>/U</c> can detect a wrong password (see
    ///     <see cref="AuthenticateEmptyUserPasswordAlgorithm45"/>).
    /// </remarks>
    /// <param name="oBytes">The Encrypt dictionary's raw <c>/O</c> entry bytes.</param>
    /// <param name="permissions">The Encrypt dictionary's <c>/P</c> entry, as a signed 32-bit integer.</param>
    /// <param name="idBytes">The trailer's <c>/ID</c> array's first element's raw bytes.</param>
    /// <param name="keyLengthBytes">The file key length in bytes (<c>/Length</c> in bits, divided by 8).</param>
    /// <param name="revision">The Encrypt dictionary's <c>/R</c> entry.</param>
    /// <param name="encryptMetadata">The Encrypt dictionary's <c>/EncryptMetadata</c> entry (default <see langword="true"/>).</param>
    /// <returns>The <paramref name="keyLengthBytes"/>-byte file encryption key.</returns>
    private static byte[] ComputeFileKeyAlgorithm2(
        byte[] oBytes,
        int permissions,
        byte[] idBytes,
        int keyLengthBytes,
        int revision,
        bool encryptMetadata)
    {
        // Step (a): pad/truncate the password to exactly 32 bytes - for the empty password this
        // phase supports, that is simply the standard padding string, verbatim.
        var padded = PasswordPadding;

        // Step (b)-(c): build the MD5 input (padded password + /O + /P as 4-byte little-endian
        // signed integer + the first /ID element's raw bytes + 0xFFFFFFFF when revision >= 4 and
        // /EncryptMetadata is explicitly false) and hash it.
        using var input = new MemoryStream();
        input.Write(padded);
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
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                digest = MD5.HashData(digest.AsSpan(0, keyLengthBytes));
            }
        }

        // Step (e): the file encryption key is the first keyLengthBytes of the final digest.
        return digest.AsSpan(0, keyLengthBytes).ToArray();
    }

    /// <summary>
    ///     Authenticates the empty user password for revisions 2-4 by recomputing the expected
    ///     <c>/U</c> value from the file encryption key (ISO 32000-1 Algorithm 4 for revision 2,
    ///     Algorithm 5 for revision 3/4) and comparing it against the document's actual <c>/U</c>
    ///     entry, throwing when they do not match (a real, non-empty password is required).
    /// </summary>
    /// <remarks>
    ///     Per the specification, only the first 16 of <c>/U</c>'s 32 bytes are compared for
    ///     revision 3/4 (the trailing 16 bytes are producer-defined padding, not a deterministic
    ///     function of the key) - comparing all 32 would reject documents produced by a conforming
    ///     writer using a different padding convention.
    /// </remarks>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="uBytes"/> is shorter than the specification requires
    ///     (32 bytes for revision 2, 16 bytes for revision 3/4).
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the recomputed value does not match <paramref name="uBytes"/> - a real,
    ///     non-empty password is required to open this document.
    /// </exception>
    private static void AuthenticateEmptyUserPasswordAlgorithm45(byte[] fileKey, byte[] uBytes, byte[] idBytes, int revision)
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

        if (!authenticated)
        {
            throw new UnsupportedImageFeatureException(
                "pdf-encrypted-password-required",
                "This encrypted PDF document requires a non-empty user password, which is not supported.");
        }
    }

    /// <summary>
    ///     Computes the file encryption key for AES-256/R5 (ISO 32000-2 Algorithm 2.A, simplified
    ///     to the empty-user-password case this phase exclusively supports): validates the empty
    ///     password against <c>/U</c>'s embedded validation salt, then unwraps <c>/UE</c> using a
    ///     key derived from <c>/U</c>'s embedded key salt.
    /// </summary>
    /// <param name="uBytes">
    ///     The Encrypt dictionary's 48-byte <c>/U</c> entry: 32 bytes of hash, 8 bytes of
    ///     validation salt, 8 bytes of key salt.
    /// </param>
    /// <param name="ueBytes">The Encrypt dictionary's 32-byte <c>/UE</c> entry.</param>
    /// <returns>The 32-byte file encryption key, used directly (no further per-object derivation).</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="uBytes"/>/<paramref name="ueBytes"/> are not the lengths
    ///     the specification requires.
    /// </exception>
    /// <exception cref="UnsupportedImageFeatureException">
    ///     Thrown when the empty password does not authenticate against <c>/U</c>'s validation
    ///     salt - a real, non-empty password is required to open this document.
    /// </exception>
    private static byte[] ComputeFileKeyAlgorithm2A(byte[] uBytes, byte[] ueBytes)
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

        // Step 1: for an empty password, SHA-256(password + validationSalt) is simply
        // SHA-256(validationSalt) - authenticate by comparing against /U's own embedded hash.
        var computedHash = SHA256.HashData(validationSalt);
        if (!computedHash.AsSpan().SequenceEqual(hash))
        {
            throw new UnsupportedImageFeatureException(
                "pdf-encrypted-password-required",
                "This encrypted PDF document requires a non-empty user password, which is not supported.");
        }

        // Step 2: the intermediate key is SHA-256(password + keySalt) - again just
        // SHA-256(keySalt) for an empty password.
        var intermediateKey = SHA256.HashData(keySalt);

        // Step 3: the file encryption key is AES-256-CBC-decrypt(/UE) using the intermediate key,
        // a zero IV, and no padding (/UE decrypts to exactly the raw 32-byte file key).
        return DecryptAesCbc(intermediateKey, new byte[16], ueBytes, 0, ueBytes.Length, CipherMode.CBC, PaddingMode.None);
    }

    /// <summary>
    ///     Computes a per-object encryption key per ISO 32000-1 Algorithm 1, used by RC4 and
    ///     AES-128/AESV2 (revisions 2-4). AES-256/R5 does not use this - it uses the file
    ///     encryption key directly for every object (see <see cref="ComputeFileKeyAlgorithm2A"/>'s
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
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = mode;
        aes.Padding = padding;

        try
        {
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
