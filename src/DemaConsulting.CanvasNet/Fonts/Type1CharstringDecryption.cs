// cspell:ignore eexec lenIV charstring charstrings
namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Implements the classic PostScript Type 1 font "eexec" cipher: a simple additive stream
///     cipher (a linear congruential generator keyed by the cipher byte itself) used to encrypt
///     both a font program's private dictionary (the <c>eexec</c>-wrapped segment as a whole) and,
///     independently, each individual charstring/subroutine entry within that segment.
/// </summary>
/// <remarks>
///     <para>
///     The two uses share the exact same algorithm and differ only in their starting key
///     (<see cref="EexecR0"/> vs. <see cref="CharstringR0"/>) and how many leading decrypted bytes
///     are discarded: the eexec-wrapped segment always discards a fixed <c>4</c> leading bytes;
///     each individual charstring/subroutine discards <c>lenIV</c> leading bytes (conventionally
///     <c>4</c>, but overridable per font via a <c>/lenIV</c> declaration - see
///     <see cref="Type1Table"/>). The discarded bytes are random padding the original encoder used
///     to prime the cipher state before the real content begins; they carry no semantic meaning.
///     </para>
///     <para>
///     This is a pure, allocation-bounded operation: the amount of work performed by
///     <see cref="Decrypt"/> is always exactly proportional to the requested byte range, with
///     no unbounded loop or recursion, so it cannot itself be a resource-exhaustion vector
///     independent of however large a slice of the font file the caller supplies.
///     </para>
/// </remarks>
internal static class Type1CharstringDecryption
{
    /// <summary>
    ///     The starting cipher key used to decrypt a font program's <c>eexec</c>-wrapped private
    ///     dictionary segment as a whole.
    /// </summary>
    public const ushort EexecR0 = 55665;

    /// <summary>
    ///     The starting cipher key used to decrypt each individual charstring or subroutine entry
    ///     within an already-eexec-decrypted private dictionary segment.
    /// </summary>
    public const ushort CharstringR0 = 4330;

    /// <summary>
    ///     The cipher's first multiplier constant, fixed by the Type 1 Font Format specification.
    /// </summary>
    private const ushort C1 = 52845;

    /// <summary>
    ///     The cipher's additive constant, fixed by the Type 1 Font Format specification.
    /// </summary>
    private const ushort C2 = 22719;

    /// <summary>
    ///     Decrypts <paramref name="length"/> bytes of Type 1 "eexec"-ciphered data starting at
    ///     <paramref name="offset"/> within <paramref name="data"/>, then discards the first
    ///     <paramref name="discardCount"/> decrypted bytes (random cipher-priming padding with no
    ///     semantic meaning).
    /// </summary>
    /// <param name="data">The byte array containing the ciphered data.</param>
    /// <param name="offset">The offset of the ciphered data within <paramref name="data"/>.</param>
    /// <param name="length">The number of ciphered bytes to decrypt.</param>
    /// <param name="r">
    ///     The starting cipher key: <see cref="EexecR0"/> for an eexec-wrapped segment as a whole,
    ///     or <see cref="CharstringR0"/> for an individual charstring/subroutine entry.
    /// </param>
    /// <param name="discardCount">
    ///     The number of leading decrypted bytes to discard (<c>4</c> for an eexec-wrapped
    ///     segment; the font's declared <c>lenIV</c>, conventionally <c>4</c>, for an individual
    ///     charstring/subroutine entry).
    /// </param>
    /// <returns>
    ///     The decrypted bytes, with the first <paramref name="discardCount"/> bytes already
    ///     removed - a new array of length <c>length - discardCount</c>.
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="offset"/>/<paramref name="length"/> fall outside
    ///     <paramref name="data"/>'s bounds, or when <paramref name="discardCount"/> is negative or
    ///     greater than <paramref name="length"/>.
    /// </exception>
    public static byte[] Decrypt(byte[] data, int offset, int length, ushort r, int discardCount)
    {
        if (offset < 0 || length < 0 || checked((long)offset + length) > data.Length)
        {
            throw new InvalidDataException("Type 1 encrypted data is truncated.");
        }

        if (discardCount < 0 || discardCount > length)
        {
            throw new InvalidDataException("Type 1 encrypted data is shorter than its declared discard count.");
        }

        var plain = new byte[length];
        var key = r;
        for (var i = 0; i < length; i++)
        {
            var cipher = data[offset + i];
            plain[i] = (byte)(cipher ^ (key >> 8));
            key = (ushort)(((cipher + key) * C1) + C2);
        }

        return discardCount == 0 ? plain : plain[discardCount..];
    }
}
