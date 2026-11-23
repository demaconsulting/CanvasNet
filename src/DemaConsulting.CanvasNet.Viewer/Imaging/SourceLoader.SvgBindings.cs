using DemaConsulting.CanvasNet.Svg;

namespace DemaConsulting.CanvasNet.Viewer.Imaging;

/// <summary>
///     The SVG position bindings of <see cref="SourceLoader"/>: what an SVG file's <c>cn:x1y1</c>, <c>cn:x2y2</c>,
///     <c>cn:center</c> and <c>cn:xy</c> attributes became, as notes, operations and the <c>--strict</c> policy.
/// </summary>
/// <remarks>
///     <para>
///         The SVG library resolves the bindings while it draws the file (see <see cref="SvgLoadOptions.BindingHandler"/>),
///         from the positions of the very pictures it draws. A resolved binding is the operation <c>bind</c> with the
///         element, the attribute, the image id, the selector, and the written <c>x</c> and <c>y</c>. A binding that was
///         not resolved keeps the numbers written in the file and is a warning with its own code
///         (<c>svg-binding-malformed</c>, <c>svg-binding-image-missing</c>, <c>svg-binding-unsupported</c>,
///         <c>svg-binding-unmatched</c>, <c>svg-binding-ambiguous</c>, <c>svg-binding-unavailable</c>,
///         <c>svg-binding-singular</c>).
///     </para>
///     <para>
///         With <see cref="SourceOptions.Strict"/> such a binding is the error <see cref="ErrorCodes.SvgBindingFailed"/>,
///         raised after the check of the pictures that were not drawn, so a picture the file-access policy denied is
///         still the error <c>file-access-denied</c> (its bindings are only <c>svg-binding-unavailable</c>).
///     </para>
/// </remarks>
public static partial class SourceLoader
{
    /// <summary>
    ///     The prefix of the codes of the position bindings that were not resolved.
    /// </summary>
    internal const string SvgBindingCodePrefix = "svg-binding-";

    /// <summary>
    ///     Gets the warnings of the bindings that were not resolved.
    /// </summary>
    /// <param name="bindings">The bindings the SVG library reported, in document order.</param>
    /// <returns>One <c>svg</c> warning per binding that was not resolved, with its code and message.</returns>
    internal static List<ImageDiagnostic> BindingNotes(IEnumerable<SvgPositionBinding> bindings)
    {
        return [.. bindings.Where(b => !b.IsResolved).Select(b => new ImageDiagnostic("svg", "warning", b.Code!, b.Message))];
    }

    /// <summary>
    ///     Gets one <c>bind</c> operation per binding that was resolved.
    /// </summary>
    /// <param name="bindings">The bindings the SVG library reported, in document order.</param>
    /// <returns>
    ///     The operations: <c>element</c> (the local name), <c>id</c> (when the element has one), <c>attribute</c>,
    ///     <c>image</c>, <c>selector</c>, <c>x</c> and <c>y</c> (the written numbers).
    /// </returns>
    internal static List<AppliedOperation> BindOperations(IEnumerable<SvgPositionBinding> bindings)
    {
        var operations = new List<AppliedOperation>();
        foreach (var binding in bindings.Where(b => b.IsResolved))
        {
            var fields = new List<(string Name, object Value)> { ("element", binding.Element) };
            if (binding.ElementId is { } id)
            {
                fields.Add(("id", id));
            }

            fields.Add(("attribute", binding.Attribute));
            fields.Add(("image", binding.ImageId!));
            fields.Add(("selector", binding.Selector!));
            fields.Add(("x", binding.X!.Value));
            fields.Add(("y", binding.Y!.Value));
            operations.Add(Operation("bind", [.. fields]));
        }

        return operations;
    }

    /// <summary>
    ///     Applies the <c>--strict</c> policy to the bindings: the first one that was not resolved is an error.
    /// </summary>
    /// <param name="notes">The notes of the render; only those with an <c>svg-binding-</c> code are considered.</param>
    /// <param name="fileName">The name of the rendered file, used when a note has no origin.</param>
    /// <exception cref="ViewerException">Thrown with <see cref="ErrorCodes.SvgBindingFailed"/> for the first such binding.</exception>
    internal static void ThrowForFailedBindings(IEnumerable<ImageDiagnostic> notes, string fileName)
    {
        var failed = notes.FirstOrDefault(n => n.Code.StartsWith(SvgBindingCodePrefix, StringComparison.Ordinal));
        if (failed is null)
        {
            return;
        }

        // A binding of a nested SVG file names that file: the last one of the chain the note came through
        var holder = failed.Origin.Count > 0 ? failed.Origin[^1] : fileName;
        throw new ViewerException(
            ErrorCodes.SvgBindingFailed,
            $"'{holder}' has a position binding that was not resolved ({failed.Code}): {failed.Message}",
            $"Without strict mode the binding keeps the numbers written in the file and is reported as the warning {failed.Code}.");
    }
}
