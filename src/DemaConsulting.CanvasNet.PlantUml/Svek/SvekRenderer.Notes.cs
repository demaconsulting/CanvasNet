// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using DemaConsulting.CanvasNet.PlantUml.Styling;

namespace DemaConsulting.CanvasNet.PlantUml.Svek;

/// <summary>
///     The notes of <see cref="SvekRenderer"/> (PlantUML's <c>EntityImageNote</c> and <c>Opale</c>).
/// </summary>
internal static partial class SvekRenderer
{
    /// <summary>The size of the folded corner of a note.</summary>
    private const double NoteCorner = 10;

    /// <summary>The half width of the pointer where it leaves a note.</summary>
    private const double PointerDelta = 4;

    /// <summary>
    ///     Draws a placed note: its outline with the folded corner (and the pointer to what it is attached to, from the
    ///     side the layout chose), the fold, and its text 6 from the left and 5 from the top.
    /// </summary>
    /// <param name="note">The placed note.</param>
    /// <param name="theme">The theme.</param>
    /// <param name="dx">The horizontal offset of the drawing.</param>
    /// <param name="dy">The vertical offset of the drawing.</param>
    /// <returns>The items.</returns>
    public static IReadOnlyList<SceneItem> DrawNote(PlacedNote note, SkinTheme theme, double dx, double dy)
    {
        ArgumentNullException.ThrowIfNull(note);
        ArgumentNullException.ThrowIfNull(theme);
        var style = theme[SkinElement.Note];
        var box = Shift(note.Box, dx, dy);
        var (x, y, w, h) = (box.X, box.Y, box.Width, box.Height);
        var pointerBase = new SvekPoint(note.PointerBase.X + dx - x, note.PointerBase.Y + dy - y);
        var tip = (note.PointerTip.X + dx - x, note.PointerTip.Y + dy - y);

        // The outline, relative to the note's top-left corner, with the pointer on its side
        (double X, double Y)[] outline;
        switch (note.PointerSide)
        {
            case NotePosition.Left:
                {
                    var y1 = Math.Clamp(pointerBase.Y - PointerDelta, 0, Math.Max(0, h - (2 * PointerDelta)));
                    outline = [(0, 0), (0, y1), tip, (0, y1 + (2 * PointerDelta)), (0, h), (w, h), (w, NoteCorner), (w - NoteCorner, 0)];
                    break;
                }

            case NotePosition.Right:
                {
                    var y1 = Math.Clamp(pointerBase.Y - PointerDelta, NoteCorner, Math.Max(NoteCorner, h - (2 * PointerDelta)));
                    outline = [(0, 0), (0, h), (w, h), (w, y1 + (2 * PointerDelta)), tip, (w, y1), (w, NoteCorner), (w - NoteCorner, 0)];
                    break;
                }

            case NotePosition.Top:
                {
                    var x1 = Math.Clamp(pointerBase.X - PointerDelta, 0, Math.Max(0, w - NoteCorner));
                    outline = [(0, 0), (0, h), (w, h), (w, NoteCorner), (w - NoteCorner, 0), (x1 + (2 * PointerDelta), 0), tip, (x1, 0)];
                    break;
                }

            case NotePosition.Bottom:
                {
                    var x1 = Math.Clamp(pointerBase.X - PointerDelta, 0, w);
                    outline = [(0, 0), (0, h), (x1, h), tip, (x1 + (2 * PointerDelta), h), (w, h), (w, NoteCorner), (w - NoteCorner, 0)];
                    break;
                }

            default:
                outline = [(0, 0), (0, h), (w, h), (w, NoteCorner), (w - NoteCorner, 0)];
                break;
        }

        var fill = note.Note.Fill ?? style.Fill;
        var stroke = StrokeOf(style.Line, style.LineWidth);
        var items = new List<SceneItem>
        {
            new ScenePath(SceneShapes.Polygon(outline.Select(p => (x + p.X, y + p.Y)).ToArray()))
            {
                Role = "note",
                Id = note.Note.Id,
                Location = note.Note.Location,
                Fill = fill,
                Stroke = stroke,
            },
            new ScenePath(SceneShapes.Polygon((x + w - NoteCorner, y), (x + w - NoteCorner, y + NoteCorner), (x + w, y + NoteCorner)))
            {
                Role = "note",
                Id = note.Note.Id,
                Location = note.Note.Location,
                Fill = fill,
                Stroke = stroke,
            },
        };
        items.AddRange(note.Text.Draw(x + SvekLayout.NoteMarginLeft, y + SvekLayout.NoteMarginY, TextAlign.Left, "note-text", note.Note.Location)
            .Select(t => t with { Id = note.Note.Id }));
        return items;
    }
}
