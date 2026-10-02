// cspell:ignore AESV StdCF sAlT ObjStm
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DemaConsulting.CanvasNet.Codecs;

namespace DemaConsulting.CanvasNet.Pdf.Tests;

/// <summary>
///     Tests for the PDF "Standard" security handler (<c>/Filter /Standard</c>) with an empty
///     user password: RC4 (40/128-bit), AES-128 (<c>/V 4</c>/<c>/CFM /AESV2</c>), and AES-256
///     using the simpler R5 key derivation (<c>/V 5</c>/<c>/R 5</c>/<c>/CFM /AESV3</c>), plus the
///     scope boundaries this phase deliberately still rejects (<c>/R 6</c> and a genuinely
///     required non-empty password).
/// </summary>
/// <remarks>
///     Every positive test below builds its own minimal encrypted PDF entirely in-memory, using
///     test-only helper methods that independently re-derive the ISO 32000-1 Algorithm 1/2/3/4/5
///     and ISO 32000-2 Algorithm 2.A steps directly from the specification text (not copy-pasted
///     from <c>PdfDocument.Encryption.cs</c>'s production implementation), so that a bug shared
///     between the production decrypt path and a copy-pasted test-side encrypt path could not
///     silently mask itself. RC4 is symmetric, so the same <see cref="Rc4"/> helper both
///     "encrypts" (here) and decrypts (in production); every other primitive (MD5/SHA-256/AES) is
///     the same BCL primitive either side would have to use regardless.
/// </remarks>
public class PdfDocumentEncryptionTests
{
    /// <summary>The opaque black color the <c>re f</c> operator sequence paints with.</summary>
    private static readonly Canvas.Rgba32 Black = new(0, 0, 0, 255);

    /// <summary>
    ///     The standard 32-byte password padding string (ISO 32000-1 7.6.3.3), used verbatim as
    ///     the padded empty password throughout every helper below.
    /// </summary>
    private static readonly byte[] PasswordPadding =
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    #region Test-only mirrored cryptographic helpers

    /// <summary>A from-scratch, hand-rolled RC4 stream cipher (classic KSA/PRGA) - symmetric, so this single method both "encrypts" test fixtures and would decrypt them.</summary>
    private static byte[] Rc4(byte[] key, byte[] data)
    {
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

    /// <summary>
    ///     Computes the Encrypt dictionary's <c>/O</c> entry per ISO 32000-1 Algorithm 3's encrypt
    ///     direction, given explicit padded owner and user passwords (both
    ///     <see cref="PasswordPadding"/> for the shared empty-password case most existing tests
    ///     still exercise).
    /// </summary>
    private static byte[] ComputeOwnerEntryAlgorithm3(int keyLengthBytes, int revision, byte[] paddedOwnerPasswordBytes, byte[] paddedUserPasswordBytes)
    {
        var digest = MD5.HashData(paddedOwnerPasswordBytes);
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                digest = MD5.HashData(digest.AsSpan(0, keyLengthBytes).ToArray());
            }
        }

        var ownerKey = digest.AsSpan(0, keyLengthBytes).ToArray();
        var result = Rc4(ownerKey, paddedUserPasswordBytes);

        if (revision >= 3)
        {
            for (var round = 1; round <= 19; round++)
            {
                var roundKey = new byte[ownerKey.Length];
                for (var i = 0; i < ownerKey.Length; i++)
                {
                    roundKey[i] = (byte)(ownerKey[i] ^ round);
                }

                result = Rc4(roundKey, result);
            }
        }

        return result;
    }

    /// <summary>Computes the file encryption key per ISO 32000-1 Algorithm 2, given an already-padded 32-byte password.</summary>
    private static byte[] ComputeFileKeyAlgorithm2(byte[] paddedPasswordBytes, byte[] oBytes, int permissions, byte[] idBytes, int keyLengthBytes, int revision)
    {
        using var input = new MemoryStream();
        input.Write(paddedPasswordBytes);
        input.Write(oBytes);
        input.Write([(byte)permissions, (byte)(permissions >> 8), (byte)(permissions >> 16), (byte)(permissions >> 24)]);
        input.Write(idBytes);

        var digest = MD5.HashData(input.ToArray());
        if (revision >= 3)
        {
            for (var i = 0; i < 50; i++)
            {
                digest = MD5.HashData(digest.AsSpan(0, keyLengthBytes).ToArray());
            }
        }

        return digest.AsSpan(0, keyLengthBytes).ToArray();
    }

    /// <summary>Encodes (Latin-1) and pads/truncates a real password to exactly 32 bytes, independently re-derived from (not copy-pasted from) production's own <c>PadPasswordBytes</c>/<c>EncodeR2R4PasswordBytes</c> helpers.</summary>
    private static byte[] EncodeAndPadPassword(string password)
    {
        var encoded = Encoding.Latin1.GetBytes(password);
        var padded = new byte[32];
        var copyLength = Math.Min(encoded.Length, 32);
        encoded.AsSpan(0, copyLength).CopyTo(padded);
        if (copyLength < 32)
        {
            PasswordPadding.AsSpan(0, 32 - copyLength).CopyTo(padded.AsSpan(copyLength));
        }

        return padded;
    }



    /// <summary>
    ///     Computes the Encrypt dictionary's <c>/U</c> entry per ISO 32000-1 Algorithm 4
    ///     (revision 2) or Algorithm 5 (revision 3/4), for an empty user password. For revision
    ///     3/4, the trailing 16 of 32 bytes are filled with arbitrary non-zero padding (mirroring
    ///     a real-world producer convention) specifically to prove that production authentication
    ///     only ever compares the first 16 bytes.
    /// </summary>
    private static byte[] ComputeUserEntryAlgorithm45(byte[] fileKey, byte[] idBytes, int revision)
    {
        if (revision == 2)
        {
            return Rc4(fileKey, PasswordPadding);
        }

        using var hashInput = new MemoryStream();
        hashInput.Write(PasswordPadding);
        hashInput.Write(idBytes);
        var result = MD5.HashData(hashInput.ToArray());
        result = Rc4(fileKey, result);
        for (var round = 1; round <= 19; round++)
        {
            var roundKey = new byte[fileKey.Length];
            for (var i = 0; i < fileKey.Length; i++)
            {
                roundKey[i] = (byte)(fileKey[i] ^ round);
            }

            result = Rc4(roundKey, result);
        }

        var padded = new byte[32];
        result.CopyTo(padded, 0);
        for (var i = 16; i < 32; i++)
        {
            padded[i] = (byte)(0xAA + i);
        }

        return padded;
    }

    /// <summary>Computes a per-object encryption key per ISO 32000-1 Algorithm 1 (RC4/AESV2 only).</summary>
    private static byte[] ComputeObjectKeyAlgorithm1(byte[] fileKey, int objectNumber, int generation, bool isAes)
    {
        using var input = new MemoryStream();
        input.Write(fileKey);
        input.Write([(byte)objectNumber, (byte)(objectNumber >> 8), (byte)(objectNumber >> 16)]);
        input.Write([(byte)generation, (byte)(generation >> 8)]);
        if (isAes)
        {
            input.Write("sAlT"u8.ToArray());
        }

        var digest = MD5.HashData(input.ToArray());
        var keyLength = Math.Min(fileKey.Length + 5, 16);
        return digest.AsSpan(0, keyLength).ToArray();
    }

    /// <summary>Encrypts <paramref name="data"/> as <c>{iv}{AES-CBC/PKCS7 ciphertext}</c>, the wire format every AESV2/AESV3 string/stream uses.</summary>
    private static byte[] AesCbcEncryptIvPrefixed(byte[] key, byte[] iv, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var encryptor = aes.CreateEncryptor();
        var cipher = encryptor.TransformFinalBlock(data, 0, data.Length);
        return [.. iv, .. cipher];
    }

    /// <summary>Encrypts <paramref name="data"/> (an exact multiple of 16 bytes) with a zero IV and no padding - the wire format ISO 32000-2 Algorithm 2.A's <c>/UE</c> uses.</summary>
    private static byte[] AesCbcEncryptNoIv(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = new byte[16];
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        using var encryptor = aes.CreateEncryptor();
        return encryptor.TransformFinalBlock(data, 0, data.Length);
    }

    /// <summary>
    ///     Builds an R5 <c>/O</c>/<c>/OE</c> owner-password pair per the owner-password variant of
    ///     ISO 32000-2 Algorithm 2.A: hashes/encrypts over <c>ownerPassword + salt + fullU</c>
    ///     (the full 48-byte <c>/U</c> value, not a sub-slice), independently re-derived from (not
    ///     copy-pasted from) production's own <c>TryComputeFileKeyAlgorithm2AOwnerPassword</c>.
    /// </summary>
    private static (byte[] OBytes, byte[] OeBytes) BuildOwnerEntryAndOeAlgorithm2AOwnerPassword(string ownerPassword, byte[] fileKey, byte[] fullUBytes)
    {
        var ownerPasswordBytes = Encoding.UTF8.GetBytes(ownerPassword);
        var validationSalt = (byte[])[.. Enumerable.Range(0, 8).Select(i => (byte)(0x80 + i))];
        var keySalt = (byte[])[.. Enumerable.Range(0, 8).Select(i => (byte)(0x90 + i))];

        var hash = SHA256.HashData([.. ownerPasswordBytes, .. validationSalt, .. fullUBytes]);
        var oBytes = (byte[])[.. hash, .. validationSalt, .. keySalt];

        var intermediateKey = SHA256.HashData([.. ownerPasswordBytes, .. keySalt, .. fullUBytes]);
        var oeBytes = AesCbcEncryptNoIv(intermediateKey, fileKey);

        return (oBytes, oeBytes);
    }

    #endregion

    #region PDF construction helpers

    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes);

    /// <summary>Builds a stream object's body (dictionary header + raw bytes + <c>endstream</c>), matching this project's hand-authored fixture style.</summary>
    private static byte[] BuildStreamBody(byte[] data)
    {
        var header = Encoding.ASCII.GetBytes($"<< /Length {data.Length} >>\nstream\n");
        var footer = "\nendstream"u8.ToArray();
        var body = new byte[header.Length + data.Length + footer.Length];
        header.CopyTo(body, 0);
        data.CopyTo(body, header.Length);
        footer.CopyTo(body, header.Length + data.Length);
        return body;
    }

    /// <summary>
    ///     Builds an in-memory, single-page (<c>/MediaBox [0 0 100 100]</c>), classic-xref,
    ///     encrypted PDF: object 1-3 are the Catalog/Pages/Page, object 4 is the (possibly
    ///     encrypted) <c>/Contents</c> stream holding <paramref name="contentBytes"/> verbatim,
    ///     object 5 is the Encrypt dictionary (<paramref name="encryptDictBody"/>), and the
    ///     trailer declares <c>/Encrypt 5 0 R</c> plus a two-element <c>/ID</c> array (both
    ///     elements set to <paramref name="idBytes"/>, matching the common real-world convention
    ///     of identical creation/update IDs for a freshly-written file). <paramref name="catalogBody"/>
    ///     defaults to a plain Catalog with no strings of its own, but a caller may supply a body
    ///     carrying an encrypted direct string (object 1's own per-object key) to prove the
    ///     Catalog's own strings - not just a referenced stream's - are correctly decrypted.
    /// </summary>
    private static byte[] BuildEncryptedPdf(string encryptDictBody, byte[] idBytes, byte[] contentBytes, string? catalogBody = null)
    {
        var bodies = new List<byte[]>
        {
            Encoding.ASCII.GetBytes(catalogBody ?? "<< /Type /Catalog /Pages 2 0 R >>"),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 100 100] >>"u8.ToArray(),
            "<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>"u8.ToArray(),
            BuildStreamBody(contentBytes),
            Encoding.ASCII.GetBytes(encryptDictBody),
        };

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        var xrefOffset = buffer.Count;
        buffer.AddRange(Encoding.ASCII.GetBytes($"xref\n0 {bodies.Count + 1}\n"));
        buffer.AddRange("0000000000 65535 f \n"u8.ToArray());
        foreach (var offset in offsets)
        {
            buffer.AddRange(Encoding.ASCII.GetBytes($"{offset:D10} 00000 n \n"));
        }

        var idHex = ToHex(idBytes);
        buffer.AddRange(Encoding.ASCII.GetBytes(
            $"trailer\n<< /Size {bodies.Count + 1} /Root 1 0 R /Encrypt 5 0 R /ID [<{idHex}> <{idHex}>] >>\nstartxref\n{xrefOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    /// <summary>
    ///     Compresses <paramref name="data"/> into a standards-conformant zlib stream (header +
    ///     deflate + Adler-32), as <c>FlateDecode</c> expects. Duplicated (rather than shared)
    ///     from <c>PdfDocumentTests.cs</c>'s own identical helper, consistent with this file's own
    ///     "independently re-derived, not copy-pasted" philosophy for its test-only helpers.
    /// </summary>
    private static byte[] ZlibCompress(byte[] data)
    {
        using var output = new MemoryStream();
        using (var zlib = new System.IO.Compression.ZLibStream(output, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
        {
            zlib.Write(data, 0, data.Length);
        }

        return output.ToArray();
    }

    /// <summary>
    ///     Builds an in-memory, single-page, <c>/Type /XRef</c> cross-reference-stream-based,
    ///     encrypted PDF whose single compressed object (object 6, a plain dictionary) lives
    ///     inside an encrypted <c>/Type /ObjStm</c> container (object 7): objects 1-3 are the
    ///     Catalog/Pages/Page (plain, unencrypted bodies), object 4 is the RC4-encrypted
    ///     <c>/Contents</c> stream holding <paramref name="encryptedContentBytes"/> verbatim,
    ///     object 5 is the Encrypt dictionary (<paramref name="encryptDictBody"/>), object 7 is the
    ///     <c>/Type /ObjStm</c> container whose own raw (zlib-compressed) bytes are
    ///     <paramref name="encryptedObjStmBytes"/> (RC4-encrypted <em>as a whole</em>, exactly like
    ///     any other stream's raw bytes - never re-encrypted per contained object, proving the
    ///     single-pass decryption guarantee), and object 8 is the cross-reference stream itself
    ///     (a self-referential type-1 entry, left deliberately unencrypted per ISO 32000-1 7.6.1 -
    ///     a cross-reference stream is always parsed before <c>_encryptionKey</c> exists, so the
    ///     production code never attempts to decrypt it). A classic <c>xref</c>/<c>trailer</c>
    ///     table cannot express object 6's type-2 (compressed) cross-reference entry, hence the
    ///     cross-reference-stream format here, mirroring <c>PdfDocumentTests.cs</c>'s own
    ///     <c>xref-stream-single-page.pdf</c>/<c>object-stream.pdf</c> fixture pair's own
    ///     <c>/W [1 4 1]</c> field-width convention.
    /// </summary>
    private static byte[] BuildEncryptedPdfWithObjectStream(
        string encryptDictBody,
        byte[] idBytes,
        byte[] encryptedContentBytes,
        byte[] encryptedObjStmBytes)
    {
        var bodies = new List<byte[]>
        {
            "<< /Type /Catalog /Pages 2 0 R >>"u8.ToArray(),
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 100 100] >>"u8.ToArray(),
            "<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>"u8.ToArray(),
            BuildStreamBody(encryptedContentBytes),
            Encoding.ASCII.GetBytes(encryptDictBody),
        };

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            offsets.Add(buffer.Count);
            buffer.AddRange(Encoding.ASCII.GetBytes($"{i + 1} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        // Object 7: the /Type /ObjStm container. /First 4 matches the fixed "6 0\n" 4-byte
        // header this helper's callers always use ahead of the single contained object's body.
        // Its /Length is the encrypted (ciphertext) byte count - identical to the plaintext
        // zlib-compressed byte count, since RC4 never changes a stream's length.
        var objStmOffset = buffer.Count;
        buffer.AddRange("7 0 obj\n"u8.ToArray());
        buffer.AddRange(Encoding.ASCII.GetBytes(
            $"<< /Type /ObjStm /N 1 /First 4 /Filter /FlateDecode /Length {encryptedObjStmBytes.Length} >>\nstream\n"));
        buffer.AddRange(encryptedObjStmBytes);
        buffer.AddRange("\nendstream\nendobj\n"u8.ToArray());

        // Object 8: the cross-reference stream itself - self-referential (its own type-1 entry
        // below points back at xrefStreamOffset) and deliberately left unencrypted.
        var xrefStreamOffset = buffer.Count;
        var entries = new (int Type, int Field2, int Field3)[]
        {
            (0, 0, 0), // object 0: free
            (1, offsets[0], 0), // object 1: Catalog
            (1, offsets[1], 0), // object 2: Pages
            (1, offsets[2], 0), // object 3: Page
            (1, offsets[3], 0), // object 4: Contents stream
            (1, offsets[4], 0), // object 5: Encrypt dictionary
            (2, 7, 0), // object 6: compressed inside object 7, index 0
            (1, objStmOffset, 0), // object 7: ObjStm container
            (1, xrefStreamOffset, 0), // object 8: the xref stream itself
        };

        var rawEntries = new byte[entries.Length * 6];
        for (var i = 0; i < entries.Length; i++)
        {
            var (type, field2, field3) = entries[i];
            var position = i * 6;
            rawEntries[position] = (byte)type;
            rawEntries[position + 1] = (byte)(field2 >> 24);
            rawEntries[position + 2] = (byte)(field2 >> 16);
            rawEntries[position + 3] = (byte)(field2 >> 8);
            rawEntries[position + 4] = (byte)field2;
            rawEntries[position + 5] = (byte)field3;
        }

        var compressedXref = ZlibCompress(rawEntries);
        var idHex = ToHex(idBytes);
        buffer.AddRange("8 0 obj\n"u8.ToArray());
        buffer.AddRange(Encoding.ASCII.GetBytes(
            $"<< /Type /XRef /Size {entries.Length} /W [1 4 1] /Root 1 0 R /Encrypt 5 0 R /ID [<{idHex}> <{idHex}>] " +
            $"/Filter /FlateDecode /Length {compressedXref.Length} >>\nstream\n"));
        buffer.AddRange(compressedXref);
        buffer.AddRange("\nendstream\nendobj\n"u8.ToArray());

        buffer.AddRange(Encoding.ASCII.GetBytes($"startxref\n{xrefStreamOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    /// <summary>
    ///     Builds an in-memory, single-page, <c>/Type /XRef</c> cross-reference-stream-based,
    ///     encrypted PDF whose <c>/Type /Catalog</c> object itself (object 1) is compressed inside
    ///     an encrypted <c>/Type /ObjStm</c> container (object 6): objects 2-4 are the
    ///     Pages/Page/Contents (the latter RC4-encrypted, holding <paramref name="encryptedContentBytes"/>
    ///     verbatim), object 5 is the Encrypt dictionary (<paramref name="encryptDictBody"/>),
    ///     object 6 is the <c>/Type /ObjStm</c> container whose own raw (zlib-compressed) bytes are
    ///     <paramref name="encryptedObjStmBytes"/> (RC4-encrypted as a whole), and object 7 is the
    ///     cross-reference stream itself (a self-referential type-1 entry, left deliberately
    ///     unencrypted). Resolving <c>/Root</c> therefore requires the file decryption key to
    ///     already be established - the regression scenario for the fix that initializes
    ///     encryption before validating the catalog root.
    /// </summary>
    private static byte[] BuildEncryptedPdfWithCompressedCatalog(
        string encryptDictBody,
        byte[] idBytes,
        byte[] encryptedContentBytes,
        byte[] encryptedObjStmBytes)
    {
        var bodies = new List<byte[]>
        {
            "<< /Type /Pages /Kids [3 0 R] /Count 1 /MediaBox [0 0 100 100] >>"u8.ToArray(),
            "<< /Type /Page /Parent 2 0 R /Contents 4 0 R >>"u8.ToArray(),
            BuildStreamBody(encryptedContentBytes),
            Encoding.ASCII.GetBytes(encryptDictBody),
        };

        var buffer = new List<byte>();
        buffer.AddRange("%PDF-1.7\n"u8.ToArray());
        var offsets = new List<int>();
        for (var i = 0; i < bodies.Count; i++)
        {
            // Bodies start at object 2 (object 1, the Catalog, is compressed - see below).
            offsets.Add(buffer.Count);
            buffer.AddRange(Encoding.ASCII.GetBytes($"{i + 2} 0 obj\n"));
            buffer.AddRange(bodies[i]);
            buffer.AddRange("\nendobj\n"u8.ToArray());
        }

        // Object 6: the /Type /ObjStm container holding the compressed Catalog (object 1).
        // /First 4 matches the fixed "1 0\n" 4-byte header this helper's callers always use
        // ahead of the single contained object's body.
        var objStmOffset = buffer.Count;
        buffer.AddRange("6 0 obj\n"u8.ToArray());
        buffer.AddRange(Encoding.ASCII.GetBytes(
            $"<< /Type /ObjStm /N 1 /First 4 /Filter /FlateDecode /Length {encryptedObjStmBytes.Length} >>\nstream\n"));
        buffer.AddRange(encryptedObjStmBytes);
        buffer.AddRange("\nendstream\nendobj\n"u8.ToArray());

        // Object 7: the cross-reference stream itself - self-referential (its own type-1 entry
        // below points back at xrefStreamOffset) and deliberately left unencrypted.
        var xrefStreamOffset = buffer.Count;
        var entries = new (int Type, int Field2, int Field3)[]
        {
            (0, 0, 0), // object 0: free
            (2, 6, 0), // object 1: Catalog, compressed inside object 6, index 0
            (1, offsets[0], 0), // object 2: Pages
            (1, offsets[1], 0), // object 3: Page
            (1, offsets[2], 0), // object 4: Contents stream
            (1, offsets[3], 0), // object 5: Encrypt dictionary
            (1, objStmOffset, 0), // object 6: ObjStm container
            (1, xrefStreamOffset, 0), // object 7: the xref stream itself
        };

        var rawEntries = new byte[entries.Length * 6];
        for (var i = 0; i < entries.Length; i++)
        {
            var (type, field2, field3) = entries[i];
            var position = i * 6;
            rawEntries[position] = (byte)type;
            rawEntries[position + 1] = (byte)(field2 >> 24);
            rawEntries[position + 2] = (byte)(field2 >> 16);
            rawEntries[position + 3] = (byte)(field2 >> 8);
            rawEntries[position + 4] = (byte)field2;
            rawEntries[position + 5] = (byte)field3;
        }

        var compressedXref = ZlibCompress(rawEntries);
        var idHex = ToHex(idBytes);
        buffer.AddRange("7 0 obj\n"u8.ToArray());
        buffer.AddRange(Encoding.ASCII.GetBytes(
            $"<< /Type /XRef /Size {entries.Length} /W [1 4 1] /Root 1 0 R /Encrypt 5 0 R /ID [<{idHex}> <{idHex}>] " +
            $"/Filter /FlateDecode /Length {compressedXref.Length} >>\nstream\n"));
        buffer.AddRange(compressedXref);
        buffer.AddRange("\nendstream\nendobj\n"u8.ToArray());

        buffer.AddRange(Encoding.ASCII.GetBytes($"startxref\n{xrefStreamOffset}\n%%EOF\n"));
        return [.. buffer];
    }

    /// <summary>The fixed, arbitrary 16-byte <c>/ID</c> every test below uses, since a real-world ID's exact value carries no meaning beyond its role as Algorithm 2/4/5 input.</summary>
    private static byte[] TestIdBytes { get; } = [.. Enumerable.Range(0, 16).Select(i => (byte)(0x10 + i))];

    /// <summary>The content-stream text every positive test below encrypts: a filled 40x40 rectangle at user (10,10)-(50,50), matching <c>PdfSystemIntegrationTests</c>'s own pixel-assertion convention.</summary>
    private const string PlaintextContent = "10 10 40 40 re f";

    #endregion

    /// <summary>Proves an RC4 40-bit (<c>/V 1</c>/<c>/R 2</c>) encrypted document with an empty user password opens and renders correctly.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedRc4_40Bit_DecryptsAndRenders()
    {
        const int keyLengthBytes = 5;
        const int revision = 2;
        const int permissions = -3904;

        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, PasswordPadding);
        var fileKey = ComputeFileKeyAlgorithm2(PasswordPadding, oBytes, permissions, TestIdBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, TestIdBytes, revision);

        var objectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: false);
        var encryptedContent = Rc4(objectKey, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody = $"<< /Filter /Standard /V 1 /R {revision} /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));
        using var surface = document.Render(0, 100, 100);

        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves an RC4 128-bit (<c>/V 2</c>/<c>/R 3</c>) encrypted document with an empty user
    ///     password opens and renders correctly, including when <c>/U</c>'s trailing 16 padding
    ///     bytes are non-zero (proving only the first 16 bytes are ever compared).
    /// </summary>
    [Fact]
    public void PdfDocument_Open_EncryptedRc4_128Bit_DecryptsAndRenders()
    {
        const int keyLengthBytes = 16;
        const int revision = 3;
        const int permissions = -3904;

        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, PasswordPadding);
        var fileKey = ComputeFileKeyAlgorithm2(PasswordPadding, oBytes, permissions, TestIdBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, TestIdBytes, revision);

        var objectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: false);
        var encryptedContent = Rc4(objectKey, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody =
            $"<< /Filter /Standard /V 2 /R {revision} /Length 128 /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));
        using var surface = document.Render(0, 100, 100);

        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves an AES-128 (<c>/V 4</c>/<c>/R 4</c>/<c>/CFM /AESV2</c>) encrypted document with an empty user password opens and renders correctly.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedAesV2_128Bit_DecryptsAndRenders()
    {
        const int keyLengthBytes = 16;
        const int revision = 4;
        const int permissions = -3904;

        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, PasswordPadding);
        var fileKey = ComputeFileKeyAlgorithm2(PasswordPadding, oBytes, permissions, TestIdBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, TestIdBytes, revision);

        var objectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: true);
        var contentIv = (byte[])[.. Enumerable.Range(0, 16).Select(i => (byte)(0x30 + i))];
        var encryptedContent = AesCbcEncryptIvPrefixed(objectKey, contentIv, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody =
            $"<< /Filter /Standard /V 4 /R {revision} /Length 128 /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} " +
            "/CF << /StdCF << /CFM /AESV2 /Length 16 >> >> /StmF /StdCF /StrF /StdCF >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));
        using var surface = document.Render(0, 100, 100);

        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves an AES-256 R5 (<c>/V 5</c>/<c>/R 5</c>/<c>/CFM /AESV3</c>) encrypted document with an empty user password opens and renders correctly.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedAesV3_R5_DecryptsAndRenders()
    {
        const int permissions = -3904;

        var fileKey = (byte[])[.. Enumerable.Range(0, 32).Select(i => (byte)(0x40 + i))];
        var validationSalt = (byte[])[.. Enumerable.Range(0, 8).Select(i => (byte)(0x50 + i))];
        var keySalt = (byte[])[.. Enumerable.Range(0, 8).Select(i => (byte)(0x60 + i))];

        var hash = SHA256.HashData(validationSalt);
        var uBytes = (byte[])[.. hash, .. validationSalt, .. keySalt];
        var intermediateKey = SHA256.HashData(keySalt);
        var ueBytes = AesCbcEncryptNoIv(intermediateKey, fileKey);
        var oBytes = new byte[32];
        var oeBytes = new byte[32];

        var contentIv = (byte[])[.. Enumerable.Range(0, 16).Select(i => (byte)(0x70 + i))];
        var encryptedContent = AesCbcEncryptIvPrefixed(fileKey, contentIv, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody =
            $"<< /Filter /Standard /V 5 /R 5 /Length 256 /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> " +
            $"/OE <{ToHex(oeBytes)}> /UE <{ToHex(ueBytes)}> /P {permissions} " +
            "/CF << /StdCF << /CFM /AESV3 /Length 32 >> >> /StmF /StdCF /StrF /StdCF >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));
        using var surface = document.Render(0, 100, 100);

        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves an RC4 128-bit (<c>/V 2</c>/<c>/R 3</c>) encrypted document with a correct, non-empty, real user password opens and renders correctly when that password is supplied to <see cref="PdfDocument.Open(Stream, string?)"/>.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedRc4_CorrectUserPassword_DecryptsAndRenders()
    {
        const int keyLengthBytes = 16;
        const int revision = 3;
        const int permissions = -3904;
        const string userPassword = "test";

        var paddedUserPassword = EncodeAndPadPassword(userPassword);
        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, paddedUserPassword);
        var fileKey = ComputeFileKeyAlgorithm2(paddedUserPassword, oBytes, permissions, TestIdBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, TestIdBytes, revision);

        var objectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: false);
        var encryptedContent = Rc4(objectKey, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody =
            $"<< /Filter /Standard /V 2 /R {revision} /Length 128 /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes), userPassword);
        using var surface = document.Render(0, 100, 100);

        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves an AES-128 (<c>/V 4</c>/<c>/R 4</c>/<c>/CFM /AESV2</c>) encrypted document with a correct, non-empty, real user password opens and renders correctly when that password is supplied to <see cref="PdfDocument.Open(Stream, string?)"/>.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedAesV2_CorrectUserPassword_DecryptsAndRenders()
    {
        const int keyLengthBytes = 16;
        const int revision = 4;
        const int permissions = -3904;
        const string userPassword = "test";

        var paddedUserPassword = EncodeAndPadPassword(userPassword);
        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, paddedUserPassword);
        var fileKey = ComputeFileKeyAlgorithm2(paddedUserPassword, oBytes, permissions, TestIdBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, TestIdBytes, revision);

        var objectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: true);
        var contentIv = (byte[])[.. Enumerable.Range(0, 16).Select(i => (byte)(0x31 + i))];
        var encryptedContent = AesCbcEncryptIvPrefixed(objectKey, contentIv, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody =
            $"<< /Filter /Standard /V 4 /R {revision} /Length 128 /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} " +
            "/CF << /StdCF << /CFM /AESV2 /Length 16 >> >> /StmF /StdCF /StrF /StdCF >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes), userPassword);
        using var surface = document.Render(0, 100, 100);

        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>Proves an AES-256 R5 (<c>/V 5</c>/<c>/R 5</c>/<c>/CFM /AESV3</c>) encrypted document with a correct, non-empty, real user password opens and renders correctly when that password is supplied to <see cref="PdfDocument.Open(Stream, string?)"/>.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedAesV3_CorrectUserPassword_DecryptsAndRenders()
    {
        const int permissions = -3904;
        const string userPassword = "test";
        var userPasswordBytes = Encoding.UTF8.GetBytes(userPassword);

        var fileKey = (byte[])[.. Enumerable.Range(0, 32).Select(i => (byte)(0x41 + i))];
        var validationSalt = (byte[])[.. Enumerable.Range(0, 8).Select(i => (byte)(0x51 + i))];
        var keySalt = (byte[])[.. Enumerable.Range(0, 8).Select(i => (byte)(0x61 + i))];

        var hash = SHA256.HashData([.. userPasswordBytes, .. validationSalt]);
        var uBytes = (byte[])[.. hash, .. validationSalt, .. keySalt];
        var intermediateKey = SHA256.HashData([.. userPasswordBytes, .. keySalt]);
        var ueBytes = AesCbcEncryptNoIv(intermediateKey, fileKey);
        var oBytes = new byte[32];
        var oeBytes = new byte[32];

        var contentIv = (byte[])[.. Enumerable.Range(0, 16).Select(i => (byte)(0x71 + i))];
        var encryptedContent = AesCbcEncryptIvPrefixed(fileKey, contentIv, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody =
            $"<< /Filter /Standard /V 5 /R 5 /Length 256 /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> " +
            $"/OE <{ToHex(oeBytes)}> /UE <{ToHex(ueBytes)}> /P {permissions} " +
            "/CF << /StdCF << /CFM /AESV3 /Length 32 >> >> /StmF /StdCF /StrF /StdCF >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes), userPassword);
        using var surface = document.Render(0, 100, 100);

        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves an RC4 128-bit (<c>/V 2</c>/<c>/R 3</c>) encrypted document - created with a
    ///     real, distinct owner password and a different, real user password - opens and renders
    ///     correctly when the owner password is supplied to <see cref="PdfDocument.Open(Stream, string?)"/>,
    ///     via the ISO 32000-1 Algorithm 3 owner-password-recovery path (the supplied password
    ///     fails to authenticate as the user password first, exactly as required).
    /// </summary>
    [Fact]
    public void PdfDocument_Open_EncryptedRc4_CorrectOwnerPassword_DecryptsAndRenders()
    {
        const int keyLengthBytes = 16;
        const int revision = 3;
        const int permissions = -3904;
        const string ownerPassword = "owner-secret";
        const string userPassword = "user-secret";

        var paddedOwnerPassword = EncodeAndPadPassword(ownerPassword);
        var paddedUserPassword = EncodeAndPadPassword(userPassword);
        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, paddedOwnerPassword, paddedUserPassword);
        var fileKey = ComputeFileKeyAlgorithm2(paddedUserPassword, oBytes, permissions, TestIdBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, TestIdBytes, revision);

        var objectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: false);
        var encryptedContent = Rc4(objectKey, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody =
            $"<< /Filter /Standard /V 2 /R {revision} /Length 128 /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes), ownerPassword);
        using var surface = document.Render(0, 100, 100);

        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves an AES-256 R5 (<c>/V 5</c>/<c>/R 5</c>/<c>/CFM /AESV3</c>) encrypted document -
    ///     created with a real, distinct owner password and an empty (unrelated) user password -
    ///     opens and renders correctly when the owner password is supplied to
    ///     <see cref="PdfDocument.Open(Stream, string?)"/>, via
    ///     <c>TryComputeFileKeyAlgorithm2AOwnerPassword</c> (the supplied password fails to
    ///     authenticate as the user password first, exactly as required).
    /// </summary>
    [Fact]
    public void PdfDocument_Open_EncryptedAesV3_CorrectOwnerPassword_DecryptsAndRenders()
    {
        const int permissions = -3904;
        const string ownerPassword = "owner-test";

        var fileKey = (byte[])[.. Enumerable.Range(0, 32).Select(i => (byte)(0x42 + i))];
        var userValidationSalt = (byte[])[.. Enumerable.Range(0, 8).Select(i => (byte)(0x52 + i))];
        var userKeySalt = (byte[])[.. Enumerable.Range(0, 8).Select(i => (byte)(0x62 + i))];

        // /U/UE are built from an unrelated, empty user password, so the supplied owner password
        // necessarily fails the user-password attempt first.
        var userHash = SHA256.HashData(userValidationSalt);
        var uBytes = (byte[])[.. userHash, .. userValidationSalt, .. userKeySalt];
        var userIntermediateKey = SHA256.HashData(userKeySalt);
        var ueBytes = AesCbcEncryptNoIv(userIntermediateKey, fileKey);

        var (oBytes, oeBytes) = BuildOwnerEntryAndOeAlgorithm2AOwnerPassword(ownerPassword, fileKey, uBytes);

        var contentIv = (byte[])[.. Enumerable.Range(0, 16).Select(i => (byte)(0x72 + i))];
        var encryptedContent = AesCbcEncryptIvPrefixed(fileKey, contentIv, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody =
            $"<< /Filter /Standard /V 5 /R 5 /Length 256 /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> " +
            $"/OE <{ToHex(oeBytes)}> /UE <{ToHex(ueBytes)}> /P {permissions} " +
            "/CF << /StdCF << /CFM /AESV3 /Length 32 >> >> /StmF /StdCF /StrF /StdCF >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes), ownerPassword);
        using var surface = document.Render(0, 100, 100);

        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Proves that a well-formed, real-password-protected document throws
    ///     <see cref="UnsupportedImageFeatureException"/> with the distinguishable
    ///     <c>pdf-encrypted-incorrect-password</c> feature token when the supplied password is
    ///     wrong for both the user and the owner role - distinct from the null-password
    ///     <c>pdf-encrypted-password-required</c> token.
    /// </summary>
    [Fact]
    public void PdfDocument_Open_Encrypted_IncorrectPassword_ThrowsUnsupportedImageFeatureException()
    {
        const int keyLengthBytes = 16;
        const int revision = 3;
        const int permissions = -3904;

        var paddedOwnerPassword = EncodeAndPadPassword("owner-secret");
        var paddedUserPassword = EncodeAndPadPassword("user-secret");
        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, paddedOwnerPassword, paddedUserPassword);
        var fileKey = ComputeFileKeyAlgorithm2(paddedUserPassword, oBytes, permissions, TestIdBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, TestIdBytes, revision);

        var encryptDictBody =
            $"<< /Filter /Standard /V 2 /R {revision} /Length 128 /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, Encoding.ASCII.GetBytes(PlaintextContent));

        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => PdfDocument.Open(new MemoryStream(pdfBytes), "wrong-password"));
        Assert.Equal("pdf-encrypted-incorrect-password", exception.Feature);
    }

    /// <summary>
    ///     Proves that supplying a password containing a character outside ASCII (0-127) for an
    ///     <c>/R 2</c>-<c>4</c> document throws <see cref="UnsupportedImageFeatureException"/>
    ///     with the distinguishable <c>pdf-encrypted-password-non-ascii</c> feature token, before
    ///     any RC4/MD5 authentication work is attempted.
    /// </summary>
    [Fact]
    public void PdfDocument_Open_Encrypted_NonAsciiPassword_ThrowsUnsupportedImageFeatureException()
    {
        const int keyLengthBytes = 5;
        const int revision = 2;
        const int permissions = -3904;

        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, PasswordPadding);
        var fileKey = ComputeFileKeyAlgorithm2(PasswordPadding, oBytes, permissions, TestIdBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, TestIdBytes, revision);

        var encryptDictBody = $"<< /Filter /Standard /V 1 /R {revision} /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, Encoding.ASCII.GetBytes(PlaintextContent));

        var exception = Assert.Throws<UnsupportedImageFeatureException>(
            () => PdfDocument.Open(new MemoryStream(pdfBytes), "caf\u00e9"));
        Assert.Equal("pdf-encrypted-password-non-ascii", exception.Feature);
    }

    /// <summary>Proves <c>/R 6</c> (AES-256 "hardened hash" key derivation) throws <see cref="UnsupportedImageFeatureException"/> with its own distinguishable feature token, instead of being silently mishandled as a regular R5 document.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedAesV3_R6_ThrowsUnsupportedImageFeatureException()
    {
        var zero32 = new byte[32];
        var zero48 = new byte[48];
        var encryptDictBody =
            $"<< /Filter /Standard /V 5 /R 6 /O <{ToHex(zero32)}> /U <{ToHex(zero48)}> " +
            $"/OE <{ToHex(zero32)}> /UE <{ToHex(zero32)}> /P -1 >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, Encoding.ASCII.GetBytes(PlaintextContent));

        var exception = Assert.Throws<UnsupportedImageFeatureException>(() => PdfDocument.Open(new MemoryStream(pdfBytes)));
        Assert.Equal("pdf-encrypted-r6-hardened-hash", exception.Feature);
    }

    /// <summary>Proves that an Encrypt dictionary whose <c>/Length</c> entry resolves to a key length outside the ISO 32000-1 §7.6.2 valid range of 40-128 bits (5-16 bytes) throws <see cref="InvalidDataException"/> before any key derivation is attempted, instead of deriving a nonsensical-length key or indexing out of range later.</summary>
    [Fact]
    public void PdfDocument_Open_Encrypted_LengthOutOfRange_ThrowsInvalidDataException()
    {
        var zero32 = new byte[32];
        var encryptDictBody = $"<< /Filter /Standard /V 2 /R 3 /Length 0 /O <{ToHex(zero32)}> /U <{ToHex(zero32)}> /P -3904 >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, Encoding.ASCII.GetBytes(PlaintextContent));

        Assert.Throws<InvalidDataException>(() => PdfDocument.Open(new MemoryStream(pdfBytes)));
    }

    /// <summary>Proves that a document whose <c>/U</c> does not authenticate against the empty password throws <see cref="UnsupportedImageFeatureException"/> (a real, non-empty password is required), instead of silently proceeding with the wrong key.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedRc4_WrongUserPasswordHash_ThrowsUnsupportedImageFeatureException()
    {
        const int keyLengthBytes = 5;
        const int revision = 2;
        const int permissions = -3904;

        // A well-formed /O (so the file key derives without error) paired with a deliberately
        // wrong /U (all zero bytes, which cannot be the real RC4(fileKey, padding) result) -
        // simulating a document that genuinely requires a non-empty password to open.
        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, PasswordPadding);
        var wrongUBytes = new byte[32];

        var encryptDictBody =
            $"<< /Filter /Standard /V 1 /R {revision} /O <{ToHex(oBytes)}> /U <{ToHex(wrongUBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, Encoding.ASCII.GetBytes(PlaintextContent));

        var exception = Assert.Throws<UnsupportedImageFeatureException>(() => PdfDocument.Open(new MemoryStream(pdfBytes)));
        Assert.Equal("pdf-encrypted-password-required", exception.Feature);
    }

    /// <summary>Proves that an AES-256 R5 document whose empty-password validation hash does not match <c>/U</c> throws <see cref="UnsupportedImageFeatureException"/> with the same password-required feature token as the RC4/AESV2 path.</summary>
    [Fact]
    public void PdfDocument_Open_EncryptedAesV3_WrongValidationHash_ThrowsUnsupportedImageFeatureException()
    {
        const int permissions = -3904;
        var wrongUBytes = new byte[48];
        var oeBytes = new byte[32];
        var ueBytes = new byte[32];

        var encryptDictBody =
            $"<< /Filter /Standard /V 5 /R 5 /Length 256 /O <{ToHex(new byte[32])}> /U <{ToHex(wrongUBytes)}> " +
            $"/OE <{ToHex(oeBytes)}> /UE <{ToHex(ueBytes)}> /P {permissions} " +
            "/CF << /StdCF << /CFM /AESV3 /Length 32 >> >> /StmF /StdCF /StrF /StdCF >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, Encoding.ASCII.GetBytes(PlaintextContent));

        var exception = Assert.Throws<UnsupportedImageFeatureException>(() => PdfDocument.Open(new MemoryStream(pdfBytes)));
        Assert.Equal("pdf-encrypted-password-required", exception.Feature);
    }

    /// <summary>
    ///     Regression test proving the <c>/Encrypt</c> dictionary's own <c>/O</c>/<c>/U</c>
    ///     strings are never mistakenly decrypted a second time. If <c>InitializeEncryption</c>'s
    ///     cache-before-key-exists ordering were ever broken, re-resolving the <c>/Encrypt</c>
    ///     dictionary <em>after</em> <c>_encryptionKey</c> had been set would run its <c>/O</c>/
    ///     <c>/U</c> bytes through a spurious extra RC4 pass, so this document's own
    ///     Algorithm 2/4/5 computation would authenticate against corrupted bytes and
    ///     <see cref="UnsupportedImageFeatureException"/> would be thrown instead of the document
    ///     opening and rendering correctly - exactly as every other positive test above also
    ///     implicitly proves, but reconstructed standalone here as a dedicated anchor for this
    ///     specific guarantee.
    /// </summary>
    [Fact]
    public void PdfDocument_Open_Encrypted_EncryptDictionaryStringsAreNeverDecrypted()
    {
        const int keyLengthBytes = 5;
        const int revision = 2;
        const int permissions = -44;
        var idBytes = (byte[])[.. Enumerable.Range(0, 16).Select(i => (byte)(0xC0 + i))];

        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, PasswordPadding);
        var fileKey = ComputeFileKeyAlgorithm2(PasswordPadding, oBytes, permissions, idBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, idBytes, revision);

        var objectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: false);
        var encryptedContent = Rc4(objectKey, Encoding.ASCII.GetBytes(PlaintextContent));

        var encryptDictBody = $"<< /Filter /Standard /V 1 /R {revision} /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, idBytes, encryptedContent);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));

        Assert.Equal(1, document.PageCount);
        using var surface = document.Render(0, 100, 100);
        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }

    /// <summary>
    ///     Regression test for the object-caching ordering defect fixed alongside this test:
    ///     every object resolved and cached before <c>_encryptionKey</c> is established - most
    ///     notably the trailer's own <c>/Root</c> Catalog, resolved by the constructor's
    ///     <c>IsValidCatalogRoot</c> check strictly before <c>InitializeEncryption</c> ever runs -
    ///     must still have its own direct strings correctly decrypted once the key exists, not
    ///     silently left as ciphertext forever. Embeds an RC4-encrypted <c>/Lang (en-US)</c>
    ///     literal string directly in the Catalog's own body (object 1), encrypted with object 1's
    ///     own per-object key exactly like every other string in the document, then - after
    ///     confirming the whole document still opens and renders correctly - uses reflection to
    ///     re-fetch the (now post-fix, re-resolved) cached Catalog object and asserts its
    ///     <c>/Lang</c> bytes are the plaintext <c>"en-US"</c>, not the original ciphertext.
    /// </summary>
    [Fact]
    public void PdfDocument_Open_Encrypted_CatalogOwnStringIsDecrypted()
    {
        const int keyLengthBytes = 5;
        const int revision = 2;
        const int permissions = -3904;

        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, PasswordPadding);
        var fileKey = ComputeFileKeyAlgorithm2(PasswordPadding, oBytes, permissions, TestIdBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, TestIdBytes, revision);

        var contentObjectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: false);
        var encryptedContent = Rc4(contentObjectKey, Encoding.ASCII.GetBytes(PlaintextContent));

        // Object 1 is the Catalog: RC4-encrypt "en-US" with its own per-object key (object
        // number 1, generation 0) exactly like every other string in the document.
        var catalogObjectKey = ComputeObjectKeyAlgorithm1(fileKey, 1, 0, isAes: false);
        var encryptedLang = Rc4(catalogObjectKey, Encoding.ASCII.GetBytes("en-US"));
        var catalogBody = $"<< /Type /Catalog /Pages 2 0 R /Lang <{ToHex(encryptedLang)}> >>";

        var encryptDictBody = $"<< /Filter /Standard /V 1 /R {revision} /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdf(encryptDictBody, TestIdBytes, encryptedContent, catalogBody);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));

        // (a) the whole document still opens and renders correctly.
        Assert.Equal(1, document.PageCount);
        using var surface = document.Render(0, 100, 100);
        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);

        // (b) the Catalog's own /Lang string is correctly decrypted, proving the object cache
        // entry cached pre-key by IsValidCatalogRoot was invalidated and the Catalog re-resolved
        // (and, this time, decrypted) after InitializeEncryption established _encryptionKey.
        var getObject = typeof(PdfDocument).GetMethod("GetObject", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var catalog = (PdfDocument.PdfObject)getObject.Invoke(document, [1])!;
        Assert.Equal(Encoding.ASCII.GetBytes("en-US"), catalog.Get("Lang")!.Bytes);
    }

    /// <summary>
    ///     Proves an object-stream-compressed (<c>/Type /ObjStm</c>) object inside an encrypted
    ///     document is decrypted/decompressed correctly, and exactly once (never double-decrypted):
    ///     <c>GetObject</c> dispatches each indirect object to exactly one of two mutually
    ///     exclusive paths - <c>ParseIndirectObjectAt</c> (which calls
    ///     <c>DecryptStringsInPlace</c>) for a direct cross-reference entry, or
    ///     <c>LoadCompressedObject</c> (which never calls <c>DecryptStringsInPlace</c>) for a
    ///     compressed entry. A compressed object's strings are only ever decrypted once, as part
    ///     of its containing <c>/Type /ObjStm</c> stream's own raw bytes being decrypted by
    ///     <c>GetStreamRawBytes</c> before decompression - <c>LoadCompressedObject</c> then parses
    ///     the already-plaintext decompressed bytes directly, with no second decrypt call site to
    ///     even reach. Constructs a synthetic <c>/Type /XRef</c> cross-reference stream whose
    ///     compressed object 6 is <c>&lt;&lt; /Greeting (Hello, Encrypted World!) &gt;&gt;</c>,
    ///     held inside object 7's RC4-encrypted (as a whole) <c>/Type /ObjStm</c> container, and
    ///     asserts both that the document still renders correctly (object 4's own encrypted
    ///     <c>/Contents</c> stream decrypts correctly) and that the compressed object's own
    ///     <c>/Greeting</c> string decompresses/decrypts to the correct plaintext.
    /// </summary>
    [Fact]
    public void PdfDocument_Open_Encrypted_ObjStmContainedObjects_AreNeverDoubleDecrypted_DocumentedByDesign()
    {
        const int keyLengthBytes = 5;
        const int revision = 2;
        const int permissions = -3904;
        var idBytes = (byte[])[.. Enumerable.Range(0, 16).Select(i => (byte)(0x80 + i))];

        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, PasswordPadding);
        var fileKey = ComputeFileKeyAlgorithm2(PasswordPadding, oBytes, permissions, idBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, idBytes, revision);

        var contentObjectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: false);
        var encryptedContent = Rc4(contentObjectKey, Encoding.ASCII.GetBytes(PlaintextContent));

        // Object 6 (compressed inside object 7's ObjStm container at index 0): a plain
        // dictionary with one string entry. The object stream header is "6 0\n" (object number 6
        // at relative offset 0), so /First is 4 (the header's own byte length).
        const string greeting = "Hello, Encrypted World!";
        var objStmPlaintext = Encoding.ASCII.GetBytes($"6 0\n<< /Greeting ({greeting}) >>");
        var compressedObjStm = ZlibCompress(objStmPlaintext);

        // Object 7's own raw (compressed) bytes are RC4-encrypted as a whole with object 7's own
        // per-object key - never re-encrypted per contained object - proving the single-pass
        // decryption guarantee this test exists to verify.
        var objStmObjectKey = ComputeObjectKeyAlgorithm1(fileKey, 7, 0, isAes: false);
        var encryptedObjStm = Rc4(objStmObjectKey, compressedObjStm);

        var encryptDictBody = $"<< /Filter /Standard /V 1 /R {revision} /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdfWithObjectStream(encryptDictBody, idBytes, encryptedContent, encryptedObjStm);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));

        // Object 4's own encrypted /Contents stream decrypted correctly.
        Assert.Equal(1, document.PageCount);
        using var surface = document.Render(0, 100, 100);
        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);

        // Object 6's own /Greeting string, reached only via LoadCompressedObject's decompression
        // of object 7's already-decrypted container bytes, is correct - proving exactly one
        // decrypt pass (the container stream's own), not a second, erroneous per-object pass.
        var getObject = typeof(PdfDocument).GetMethod("GetObject", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var compressedObject = (PdfDocument.PdfObject)getObject.Invoke(document, [6])!;
        Assert.Equal(Encoding.ASCII.GetBytes(greeting), compressedObject.Get("Greeting")!.Bytes);
    }

    /// <summary>
    ///     Proves that an encrypted document whose <c>/Type /Catalog</c> object itself is
    ///     compressed inside an encrypted <c>/Type /ObjStm</c> container still opens and renders
    ///     correctly, instead of incorrectly falling back to a linear scan. Resolving <c>/Root</c>
    ///     (to validate the catalog) requires decompressing the container object, which in turn
    ///     requires the container's own raw bytes to already be correctly decrypted - so this only
    ///     succeeds when the file decryption key is established before the catalog root is
    ///     validated, not after.
    /// </summary>
    [Fact]
    public void PdfDocument_Open_Encrypted_CompressedCatalog_OpensWithoutLinearScanFallback()
    {
        const int keyLengthBytes = 5;
        const int revision = 2;
        const int permissions = -3904;
        var idBytes = (byte[])[.. Enumerable.Range(0, 16).Select(i => (byte)(0xA0 + i))];

        var oBytes = ComputeOwnerEntryAlgorithm3(keyLengthBytes, revision, PasswordPadding, PasswordPadding);
        var fileKey = ComputeFileKeyAlgorithm2(PasswordPadding, oBytes, permissions, idBytes, keyLengthBytes, revision);
        var uBytes = ComputeUserEntryAlgorithm45(fileKey, idBytes, revision);

        var contentObjectKey = ComputeObjectKeyAlgorithm1(fileKey, 4, 0, isAes: false);
        var encryptedContent = Rc4(contentObjectKey, Encoding.ASCII.GetBytes(PlaintextContent));

        // Object 1 (the Catalog, compressed inside object 6's ObjStm container at index 0). The
        // object stream header is "1 0\n" (object number 1 at relative offset 0), so /First is 4.
        var objStmPlaintext = Encoding.ASCII.GetBytes("1 0\n<< /Type /Catalog /Pages 2 0 R >>");
        var compressedObjStm = ZlibCompress(objStmPlaintext);

        // Object 6's own raw (compressed) bytes are RC4-encrypted as a whole with object 6's own
        // per-object key - the catalog is only reachable once this container decrypts correctly.
        var objStmObjectKey = ComputeObjectKeyAlgorithm1(fileKey, 6, 0, isAes: false);
        var encryptedObjStm = Rc4(objStmObjectKey, compressedObjStm);

        var encryptDictBody = $"<< /Filter /Standard /V 1 /R {revision} /O <{ToHex(oBytes)}> /U <{ToHex(uBytes)}> /P {permissions} >>";
        var pdfBytes = BuildEncryptedPdfWithCompressedCatalog(encryptDictBody, idBytes, encryptedContent, encryptedObjStm);

        using var document = PdfDocument.Open(new MemoryStream(pdfBytes));

        // The document opened via normal cross-reference parsing - not the linear-scan fallback,
        // which would have scanned for "N G obj" markers and never found object 1 (it has no such
        // marker; it only exists compressed inside object 6) - and renders correctly.
        Assert.Equal(1, document.PageCount);
        using var surface = document.Render(0, 100, 100);
        Assert.Equal(Black, surface[30, 70]);
        Assert.Equal(default, surface[5, 5]);
    }
}
