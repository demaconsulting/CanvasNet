using System.Runtime.InteropServices;

namespace CanvasNet.Canvas;

/// <summary>
///     Represents a single 32-bit RGBA pixel (8 bits per channel).
/// </summary>
/// <remarks>
///     This is the first pixel format supported by CanvasNet. It is deliberately kept as a
///     plain 4-byte carrier of channel values with no additional behavior, so that
///     <see cref="Surface"/> row buffers can be reinterpreted between raw <see cref="byte"/> spans
///     and <see cref="Rgba32"/> spans via <see cref="System.Runtime.InteropServices.MemoryMarshal"/>
///     without any copying or conversion. Additional pixel formats (if ever needed) would be
///     introduced as new standalone struct types rather than by extending this one, keeping this
///     type's memory layout stable.
/// </remarks>
[StructLayout(LayoutKind.Sequential)]
public struct Rgba32 : IEquatable<Rgba32>
{
    /// <summary>
    ///     The red channel value.
    /// </summary>
    public byte R;

    /// <summary>
    ///     The green channel value.
    /// </summary>
    public byte G;

    /// <summary>
    ///     The blue channel value.
    /// </summary>
    public byte B;

    /// <summary>
    ///     The alpha (opacity) channel value.
    /// </summary>
    public byte A;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Rgba32"/> struct with the specified channel values.
    /// </summary>
    /// <param name="r">The red channel value.</param>
    /// <param name="g">The green channel value.</param>
    /// <param name="b">The blue channel value.</param>
    /// <param name="a">The alpha channel value.</param>
    /// <remarks>
    ///     Provides a convenient way to construct a fully specified pixel value in a single
    ///     expression, rather than requiring callers to set each field individually.
    /// </remarks>
    public Rgba32(byte r, byte g, byte b, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>
    ///     Determines whether this instance and another <see cref="Rgba32"/> have equal channel values.
    /// </summary>
    /// <param name="other">The other <see cref="Rgba32"/> to compare against.</param>
    /// <returns><see langword="true"/> if all four channels are equal; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    ///     Value equality is provided so that pixel values can be directly compared in tests and
    ///     application code, matching the value-type semantics expected of a plain data carrier.
    /// </remarks>
    public readonly bool Equals(Rgba32 other) => R == other.R && G == other.G && B == other.B && A == other.A;

    /// <summary>
    ///     Determines whether this instance and a specified object are equal.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="obj"/> is an <see cref="Rgba32"/> with equal
    ///     channel values; otherwise, <see langword="false"/>.
    /// </returns>
    public override readonly bool Equals(object? obj) => obj is Rgba32 other && Equals(other);

    /// <summary>
    ///     Returns a hash code for this instance based on its channel values.
    /// </summary>
    /// <returns>A hash code for this instance.</returns>
    public override readonly int GetHashCode() =>
        // Packing all four channels into a single int gives a fast, allocation-free hash that
        // is consistent with Equals (equal instances always produce equal packed values), without
        // depending on System.HashCode, which is not available on all supported target frameworks
        (R << 24) | (G << 16) | (B << 8) | A;

    /// <summary>
    ///     Determines whether two <see cref="Rgba32"/> instances have equal channel values.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are equal; otherwise, <see langword="false"/>.</returns>
    public static bool operator ==(Rgba32 left, Rgba32 right) => left.Equals(right);

    /// <summary>
    ///     Determines whether two <see cref="Rgba32"/> instances have different channel values.
    /// </summary>
    /// <param name="left">The first instance to compare.</param>
    /// <param name="right">The second instance to compare.</param>
    /// <returns><see langword="true"/> if the instances are different; otherwise, <see langword="false"/>.</returns>
    public static bool operator !=(Rgba32 left, Rgba32 right) => !left.Equals(right);
}
