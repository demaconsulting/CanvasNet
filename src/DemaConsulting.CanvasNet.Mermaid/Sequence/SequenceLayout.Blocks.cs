// Portions derived from mermaid (MIT); see third-party/README.md
namespace DemaConsulting.CanvasNet.Mermaid.Sequence;

/// <content>
///     Blocks and menus: the end of a block in the draw loop (Mermaid's <c>*_END</c> cases, which draw the frame or keep
///     a <c>rect</c> block for the backgrounds), and the participants' menus (<c>drawActorsPopup</c> and
///     <c>getRequiredPopupWidth</c>), whose size counts in the view box.
/// </content>
internal sealed partial class SequenceLayout
{
    /// <summary>
    ///     Closes the innermost block, draws its frame (or keeps a <c>rect</c> block to draw behind everything), and moves
    ///     below it.
    /// </summary>
    /// <param name="msg">The message that ends the block.</param>
    /// <exception cref="MermaidRenderException">
    ///     Thrown when no block is open, or when the block has nothing inside it: Mermaid gives such a block no extent and
    ///     lays out everything after it at coordinates that are not numbers (NaN), so the diagram cannot be drawn.
    /// </exception>
    private void EndLoop(SequenceMessage msg)
    {
        var loop = _bounds.EndLoop() ?? throw _meter.Failure("a block ends that was not started");
        if (loop.StopY is null)
        {
            throw new MermaidRenderException(
                MermaidDiagnosticCodes.RenderFailed,
                "the block has nothing inside it; Mermaid gives it no size and places the rest of the diagram at coordinates that are not numbers",
                loop.Location,
                "put a message or a note inside the block, or remove the block");
        }

        // A rect block is drawn last, behind everything; the other blocks get their frame now
        if (msg.Type == SequenceLineType.RectEnd)
        {
            _backgrounds.Add(loop);
        }
        else
        {
            _renderer.DrawLoop(loop, BlockLabel(msg.Type), msg);
        }

        _bounds.BumpVerticalPos(loop.StopY.Value - _bounds.VerticalPos);
        _bounds.LoopModels.Add(loop);
    }

    /// <summary>
    ///     Gets the keyword Mermaid writes in the label box of a block from the type of the message that ends it.
    /// </summary>
    /// <param name="type">The line type of the block's end.</param>
    /// <returns>The keyword.</returns>
    private static string BlockLabel(int type) => type switch
    {
        SequenceLineType.LoopEnd => "loop",
        SequenceLineType.OptEnd => "opt",
        SequenceLineType.AltEnd => "alt",
        SequenceLineType.ParEnd => "par",
        SequenceLineType.CriticalEnd => "critical",
        _ => "break",
    };

    /// <summary>
    ///     Draws the menus of the participants drawn that have links (Mermaid's <c>drawActorsPopup</c>), and gets the size
    ///     they need: the bottom of the lowest menu and the right edge of the rightmost.
    /// </summary>
    /// <param name="actorKeys">The ids of the participants drawn.</param>
    /// <returns>The height and the right edge the menus need (zero without menus).</returns>
    /// <exception cref="MermaidRenderException">Thrown for a participant with links whose type has no box (Mermaid fails).</exception>
    private (double MaxHeight, double MaxWidth) DrawActorsPopup(List<string> actorKeys)
    {
        var maxHeight = 0.0;
        var maxWidth = 0.0;
        foreach (var actor in actorKeys.Select(key => _actors[key]).Where(actor => actor.Actor.Links.Count > 0))
        {
            var (height, width) = _renderer.DrawPopup(actor, RequiredPopupWidth(actor));
            if (height > maxHeight)
            {
                maxHeight = height;
            }

            if (width + actor.X > maxWidth)
            {
                maxWidth = width + actor.X;
            }
        }

        return (maxHeight, maxWidth);
    }

    /// <summary>
    ///     Gets the width a participant's menu needs (Mermaid's <c>getRequiredPopupWidth</c>): the widest link label in the
    ///     participant font, with the wrap padding and the box margin on both sides.
    /// </summary>
    /// <param name="actor">The participant.</param>
    /// <returns>The width.</returns>
    private double RequiredPopupWidth(SequenceActorLayout actor)
    {
        var required = 0.0;
        foreach (var label in actor.Actor.Links.Keys)
        {
            var width = _labels.CalculateTextDimensions(label, _conf.ActorFont).Width + (2 * _conf.WrapPadding) + (2 * _conf.BoxMargin);
            if (required < width)
            {
                required = width;
            }
        }

        return required;
    }
}
