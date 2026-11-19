// Portions derived from PlantUML (MIT); see third-party/README.md
using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;

namespace DemaConsulting.CanvasNet.PlantUml.Class;

/// <content>
///     The member compartments of the boxes (PlantUML's <c>BodierLikeClassOrObject.getBody</c>,
///     <c>MethodsOrFieldsArea</c>, <c>BodyEnhanced1</c>, <c>TextBlockLineBefore</c>, <c>UHorizontalLine</c>, and
///     <c>VisibilityModifier</c>).
/// </content>
internal static partial class ClassRenderer
{
    /// <summary>
    ///     Builds the compartments of a class box: the fields and the methods, or the blocks between the separators of
    ///     a body that has separators.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="portions">What is shown of the entity.</param>
    /// <returns>The compartments from the top; none when both are hidden.</returns>
    private static List<Compartment> ClassCompartments(Painter painter, ClassPortions portions)
    {
        var body = painter.Entity.Body;
        if (body.Any(ClassBody.IsSeparator))
        {
            return portions.Fields || portions.Methods ? Blocks(painter, body, methodsByLine: true) : [];
        }

        var (fields, methods) = ClassBody.Split(body);
        var result = new List<Compartment>();
        if (portions.Fields)
        {
            result.Add(MeasureCompartment(painter, '\0', null, fields.Select(f => (f, false))));
        }

        if (portions.Methods)
        {
            result.Add(MeasureCompartment(painter, '\0', null, methods.Select(m => (m, true))));
        }

        return result;
    }

    /// <summary>
    ///     Splits a body into the blocks between its separators (<c>BodyEnhanced1</c>, first block below a plain line).
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="body">The body lines.</param>
    /// <param name="methodsByLine">Whether a line with parentheses is a method (classes), or every line is a field (objects).</param>
    /// <returns>The blocks.</returns>
    private static List<Compartment> Blocks(Painter painter, IReadOnlyList<string> body, bool methodsByLine)
    {
        var result = new List<Compartment>();
        var separator = '_';
        string? title = null;
        var lines = new List<(string, bool)>();
        foreach (var line in body)
        {
            if (ClassBody.IsSeparator(line))
            {
                result.Add(MeasureCompartment(painter, separator, title, lines));
                (separator, title, lines) = (line[0], ClassBody.SeparatorTitle(line), []);
                continue;
            }

            lines.Add((line, methodsByLine && ClassBody.IsMethod(line)));
        }

        result.Add(MeasureCompartment(painter, separator, title, lines));
        return result;
    }

    /// <summary>
    ///     Measures a compartment: its members (with a column for the visibility icons when one of them has a
    ///     visibility and the icons are drawn) and its optional title.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="separator">The kind of its line: <c>\0</c> or <c>_</c> plain, <c>-</c>, <c>.</c>, or <c>=</c>.</param>
    /// <param name="title">The title written in the separator, or null.</param>
    /// <param name="lines">The member lines with whether each one is a method.</param>
    /// <returns>The compartment.</returns>
    private static Compartment MeasureCompartment(Painter painter, char separator, string? title, IEnumerable<(string Line, bool IsMethod)> lines)
    {
        var iconSize = painter.Theme.ClassAttributeIconSize;
        var rows = new List<MemberRow>();
        foreach (var (line, isMethod) in lines)
        {
            var member = ClassMember.Parse(line);
            var shown = iconSize == 0 ? ClassMember.CharacterOf(member.Visibility) + member.Text : member.Text;
            var text = painter.Layout(shown, painter.MemberStyle, member.IsAbstract);
            rows.Add(new MemberRow(member.Visibility, isMethod, member.IsStatic ? Underlined(text) : text));
        }

        var icons = iconSize > 0 && rows.Any(r => r.Visibility != MemberVisibility.None);
        var titleBlock = title is null ? null : painter.Layout(title, painter.MemberStyle);
        return new Compartment(separator, titleBlock, rows, icons ? painter.Theme.CircledCharacterRadius + 3 : 0, icons ? iconSize : 0);
    }

    /// <summary>
    ///     Draws a compartment: its line (split around its title), its title, and its members with their icons.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="compartment">The compartment.</param>
    /// <param name="x">The left of the box.</param>
    /// <param name="top">The top of the compartment.</param>
    /// <param name="width">The width of the box.</param>
    /// <returns>The items.</returns>
    private static IEnumerable<SceneItem> DrawCompartment(Painter painter, Compartment compartment, double x, double top, double width)
    {
        // The line starts and ends 1 inside the box; a title splits it in two halves around the centered title
        var titleHeight = compartment.Title?.Height ?? 0;
        var lineY = top + (titleHeight / 2);
        var (lineWidth, dash) = compartment.Separator switch
        {
            '-' or '=' => (1.0, (IReadOnlyList<double>?)null),
            '.' => (1.0, [1, 2]),
            _ => (painter.Style.LineWidth, null),
        };
        var (start, end) = (x + 1, x + width - 1);
        var spans = new List<(double From, double To)>();
        if (compartment.Title is { } title)
        {
            var half = (end - start - title.Width) / 2;
            spans.Add((start, start + half));
            spans.Add((end - half, end));
        }
        else
        {
            spans.Add((start, end));
        }

        foreach (var offset in compartment.Separator == '=' ? new[] { 0.0, 2.0 } : [0.0])
        {
            foreach (var (from, to) in spans)
            {
                yield return painter.Path(SceneShapes.Line(from, lineY + offset, to, lineY + offset), "class-separator", null, painter.Stroke(lineWidth, dash));
            }
        }

        if (compartment.Title is { } drawnTitle)
        {
            foreach (var item in painter.DrawText(drawnTitle, start + ((end - start - drawnTitle.Width) / 2), lineY - (titleHeight / 2) - 0.5, TextAlign.Left, "class-separator-title"))
            {
                yield return item;
            }
        }

        // The members, below the line's margin (half the title below it when there is a title)
        var y = top + (compartment.Title is null ? MarginY : titleHeight);
        var areaX = x + MarginX;
        foreach (var row in compartment.Rows)
        {
            var height = compartment.RowHeight(row);
            if (compartment.IconColumn > 0)
            {
                foreach (var icon in DrawIcon(painter, row, areaX, y + 2 + ((height - (compartment.IconSize + 1)) / 2), compartment.IconSize))
                {
                    yield return icon;
                }
            }

            foreach (var item in painter.DrawText(row.Text, areaX + compartment.IconColumn, y + ((height - row.Text.Height) / 2), TextAlign.Left, "class-member"))
            {
                yield return item;
            }

            y += height;
        }
    }

    /// <summary>
    ///     Draws the visibility icon of a member (<c>VisibilityModifier.drawInternal</c>): a square (private), a diamond
    ///     (protected), a triangle (package), or a circle (public, and black for mandatory), hollow for a field and
    ///     filled for a method.
    /// </summary>
    /// <param name="painter">The painter of the entity.</param>
    /// <param name="row">The member.</param>
    /// <param name="x">The left of the icon.</param>
    /// <param name="y">The top of the icon.</param>
    /// <param name="iconSize">The icon size (<c>classAttributeIconSize</c>).</param>
    /// <returns>The items; none for a member without a visibility.</returns>
    private static IEnumerable<SceneItem> DrawIcon(Painter painter, MemberRow row, double x, double y, double iconSize)
    {
        if (row.Visibility == MemberVisibility.None)
        {
            yield break;
        }

        var size = (int)iconSize;
        size -= size % 2;
        var (line, back) = IconColors(row.Visibility);
        Rgba32? fill = row.IsMethod || row.Visibility == MemberVisibility.Mandatory ? back : null;
        var stroke = new SceneStroke(line, 1);
        var d = size - 2;
        IReadOnlyList<SceneSegment> shape = row.Visibility switch
        {
            MemberVisibility.Private => SceneShapes.Rect(x + 2, y + 2, size - 4, size - 4),
            MemberVisibility.Protected => SceneShapes.Polygon((x + 1 + (d / 2.0), y), (x + 1 + d, y + (d / 2.0)), (x + 1 + (d / 2.0), y + d), (x + 1, y + (d / 2.0))),
            MemberVisibility.Package => SceneShapes.Polygon((x + 1 + (d / 2.0), y + 1), (x + 1, y + d - 1), (x + 1 + d, y + d - 1)),
            _ => SceneShapes.Ellipse(x + 2 + ((size - 4) / 2.0), y + 2 + ((size - 4) / 2.0), (size - 4) / 2.0, (size - 4) / 2.0),
        };
        yield return painter.Path(shape, "class-icon", fill, stroke);
    }

    /// <summary>
    ///     Underlines every run of a block (a static member).
    /// </summary>
    /// <param name="block">The block.</param>
    /// <returns>The underlined block.</returns>
    private static TextBlock Underlined(TextBlock block) =>
        new(block.Lines.Select(l => l with { Runs = l.Runs.Select(r => r with { Underline = true }).ToList() }).ToList());

    /// <summary>
    ///     One member row of a compartment.
    /// </summary>
    /// <param name="Visibility">The visibility.</param>
    /// <param name="IsMethod">Whether the member is a method (its icon is filled).</param>
    /// <param name="Text">The laid-out text.</param>
    private sealed record MemberRow(MemberVisibility Visibility, bool IsMethod, TextBlock Text);

    /// <summary>
    ///     A measured compartment: its line kind, its title, its rows, and the icon column.
    /// </summary>
    /// <param name="Separator">The kind of its line.</param>
    /// <param name="Title">The title, or null.</param>
    /// <param name="Rows">The member rows.</param>
    /// <param name="IconColumn">The width of the icon column (the circle radius plus 3), or zero without icons.</param>
    /// <param name="IconSize">The icon size, or zero without icons.</param>
    private sealed record Compartment(char Separator, TextBlock? Title, IReadOnlyList<MemberRow> Rows, double IconColumn, double IconSize)
    {
        /// <summary>Gets the width of the members.</summary>
        public double AreaWidth => (Rows.Count == 0 ? 0 : Rows.Max(r => r.Text.Width)) + IconColumn;

        /// <summary>Gets the height of the members.</summary>
        public double AreaHeight => Rows.Sum(RowHeight);

        /// <summary>Gets the width of the compartment with its margins.</summary>
        public double Width => Title is null ? AreaWidth + (2 * MarginX) : Math.Max(AreaWidth + (2 * MarginX), Title.Width + 8);

        /// <summary>Gets the height of the compartment with its margins and title.</summary>
        public double Height => Title is null
            ? AreaHeight + (2 * MarginY)
            : (Title.Height / 2) + Math.Max((Title.Height / 2) + AreaHeight + MarginY, Title.Height);

        /// <summary>
        ///     Gets the height of a row: its text, or the icon (its size plus 1, a member without a visibility 1) when it
        ///     is taller.
        /// </summary>
        /// <param name="row">The row.</param>
        /// <returns>The height.</returns>
        public double RowHeight(MemberRow row)
        {
            if (IconColumn <= 0)
            {
                return row.Text.Height;
            }

            var icon = row.Visibility == MemberVisibility.None ? 1 : IconSize + 1;
            return Math.Max(icon, row.Text.Height);
        }
    }
}
