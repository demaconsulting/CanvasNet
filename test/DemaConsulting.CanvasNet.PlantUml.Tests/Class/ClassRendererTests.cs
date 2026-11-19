using DemaConsulting.CanvasNet.Canvas;
using DemaConsulting.CanvasNet.PlantUml.Class;
using DemaConsulting.CanvasNet.PlantUml.Scenegraph;
using static DemaConsulting.CanvasNet.PlantUml.Tests.Class.ClassTestHelpers;

namespace DemaConsulting.CanvasNet.PlantUml.Tests.Class;

/// <summary>
///     Unit tests for the <c>ClassRenderer</c> unit: the sizes of the boxes, the header with its spot, stereotypes, and
///     generic box, the compartments with their icons, modifiers, and separators, and the object and map boxes, against
///     the constants of PlantUML's drawing.
/// </summary>
public class ClassRendererTests
{
    /// <summary>
    ///     Validates the box of a class without members: the header is the circle with its margins (26 by 32) beside
    ///     the name with a margin of 3, and the two empty compartments are 8 high each.
    /// </summary>
    [Fact]
    public void PlantUmlClassRenderer_EmptyClass_HasPlantUmlSize()
    {
        // Arrange
        var model = Parse("class Drawable");
        var text = new TextLayout(TextLayout.CreateFontResolver());
        var nameWidth = text.Width("Drawable", new SceneFont("SansSerif", 14, false, false));

        // Act
        var image = Image(model, "Drawable");
        var items = image.Draw(10, 20);

        // Assert
        Assert.Equal(26 + nameWidth + 6, image.Width, 3);
        Assert.Equal(48, image.Height, 3);
        var box = Scene.BoundsOf([items.First(i => i.Role == "class")])!.Value;
        Assert.Equal((10.0, 20.0), (box.X, box.Y));
        var spot = Scene.BoundsOf([items.First(i => i.Role == "class-spot")])!.Value;
        Assert.Equal((15.0, 16.0, 22.0), (spot.X + 11 - 10, spot.Y + 11 - 20, spot.Width), new TupleComparer(0.01));
        Assert.Equal([52.0, 60.0], items.Where(i => i.Role == "class-separator").Select(i => Scene.BoundsOf([i])!.Value.Y));
    }

    /// <summary>
    ///     Validates the spot of every kind: its character, its color, and the italic name of an abstract class and an
    ///     interface.
    /// </summary>
    /// <param name="keyword">The declaration keyword.</param>
    /// <param name="character">The expected character.</param>
    /// <param name="rgb">The expected color.</param>
    /// <param name="italic">Whether the name is italic.</param>
    [Theory]
    [InlineData("class", "C", 0xADD1B2, false)]
    [InlineData("abstract", "A", 0xA9DCDF, true)]
    [InlineData("interface", "I", 0xB4A7E5, true)]
    [InlineData("enum", "E", 0xEB937F, false)]
    [InlineData("annotation", "@", 0xE3664A, false)]
    [InlineData("entity", "E", 0xADD1B2, false)]
    [InlineData("exception", "X", 0xD94321, false)]
    [InlineData("metaclass", "M", 0xCCCCCC, false)]
    [InlineData("stereotype", "S", 0xFF77FF, false)]
    [InlineData("dataclass", "D", 0x7E57C2, false)]
    [InlineData("record", "R", 0xFF8F00, false)]
    [InlineData("struct", "S", 0xFFFFFF, false)]
    [InlineData("protocol", "P", 0xFFFFFF, false)]
    public void PlantUmlClassRenderer_Spot_HasTheKindsCharacterAndColor(string keyword, string character, int rgb, bool italic)
    {
        // Act
        var items = Draw(Parse($"{keyword} K"), "K");

        // Assert
        var spot = items.OfType<ScenePath>().Single(p => p.Role == "class-spot");
        Assert.Equal(new Rgba32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255), spot.Fill);
        Assert.Equal((new Rgba32(0x18, 0x18, 0x18, 255), 1.0), (spot.Stroke!.Color, spot.Stroke.Width));
        var letter = items.OfType<SceneText>().Single(t => t.Role == "class-spot-text");
        Assert.Equal((character, "Monospaced", 17.0, true), (letter.Text, letter.Runs[0].Font.Family, letter.Runs[0].Font.Size, letter.Runs[0].Font.Bold));
        Assert.Equal(italic, items.OfType<SceneText>().Single(t => t.Role == "class-name").Runs[0].Font.Italic);
    }

    /// <summary>
    ///     Validates the header parts: the stereotypes in guillemets in italic 12 above the name, the generic box dashed
    ///     and white over the top-right corner, a spot written in a stereotype, and a hidden circle.
    /// </summary>
    [Fact]
    public void PlantUmlClassRenderer_Header_DrawsStereotypesGenericAndSpot()
    {
        // Arrange
        var model = Parse("class Box<T> << (G,#FF9900) widget >>\nclass Plain\nhide Plain circle");

        // Act
        var items = Draw(model, "Box");
        var plain = Draw(model, "Plain");

        // Assert
        var stereotype = items.OfType<SceneText>().Single(t => t.Role == "class-stereotype");
        Assert.Equal(("\u00ABwidget\u00BB", 12.0, true), (stereotype.Text, stereotype.Runs[0].Font.Size, stereotype.Runs[0].Font.Italic));
        Assert.True(stereotype.Baseline < items.OfType<SceneText>().Single(t => t.Role == "class-name").Baseline);
        var generic = items.OfType<ScenePath>().Single(p => p.Role == "class-generic");
        var box = Scene.BoundsOf([items.First(i => i.Role == "class")])!.Value;
        var genericBox = Scene.BoundsOf([generic])!.Value;
        Assert.Equal((box.Right + 4 - 1, box.Y - 3), (genericBox.Right, genericBox.Y), new TupleComparer(0.01));
        Assert.Equal(SkinColors.White, generic.Fill);
        Assert.Equal([2.0, 2.0], generic.Stroke!.Dash!);
        Assert.Equal("T", items.OfType<SceneText>().Single(t => t.Role == "class-generic-text").Text);
        Assert.Equal(("G", new Rgba32(0xFF, 0x99, 0x00, 255)), (items.OfType<SceneText>().Single(t => t.Role == "class-spot-text").Text, items.OfType<ScenePath>().Single(p => p.Role == "class-spot").Fill!.Value));
        Assert.DoesNotContain(plain, i => i.Role is "class-spot" or "class-spot-text");
    }

    /// <summary>
    ///     Validates the member compartments: the visibility icons (shape, hollow for a field, filled for a method, in
    ///     the colors of the skin), the icon column of 14, the underlined static member, and the italic abstract one.
    /// </summary>
    [Fact]
    public void PlantUmlClassRenderer_Members_DrawIconsAndModifiers()
    {
        // Arrange
        var model = Parse("class W {\n- a : int\n# b : int\n~ c : int\n+ d : int\n- e()\n+ {static} f()\n{abstract} g()\n* h : int\n}");

        // Act
        var items = Draw(model, "W");

        // Assert
        var icons = items.OfType<ScenePath>().Where(p => p.Role == "class-icon").ToList();
        Assert.Equal(
            [0xC82930, 0xB38D22, 0x1963A0, 0x038048, 0x000000, 0xC82930, 0x038048],
            icons.Select(p => (p.Stroke!.Color.R << 16) | (p.Stroke.Color.G << 8) | p.Stroke.Color.B));
        Assert.All(icons.Take(4), i => Assert.Null(i.Fill));
        Assert.Equal(new Rgba32(0xF2, 0x4D, 0x5C, 255), icons[5].Fill);
        Assert.Equal(new Rgba32(0x84, 0xBE, 0x84, 255), icons[6].Fill);
        var members = items.OfType<SceneText>().Where(t => t.Role == "class-member").ToList();
        var box = Scene.BoundsOf([items.First(i => i.Role == "class")])!.Value;
        Assert.All(members, m => Assert.Equal(box.X + 6 + 14, m.X, 3));
        Assert.Equal(SkinColors.Black, icons[4].Fill);
        Assert.True(members.Single(m => m.Text == "f()").Runs[0].Underline);
        Assert.True(members.Single(m => m.Text == "g()").Runs[0].Font.Italic);
    }

    /// <summary>
    ///     Validates that <c>skinparam classAttributeIconSize 0</c> writes the visibility characters instead of icons.
    /// </summary>
    [Fact]
    public void PlantUmlClassRenderer_IconSizeZero_WritesTheCharacters()
    {
        // Act
        var items = Draw(Parse("skinparam classAttributeIconSize 0\nclass W {\n- a : int\n+ b()\n}"), "W");

        // Assert
        Assert.DoesNotContain(items, i => i.Role == "class-icon");
        Assert.Equal(["-a : int", "+b()"], items.OfType<SceneText>().Where(t => t.Role == "class-member").Select(t => t.Text));
    }

    /// <summary>
    ///     Validates the separators: a solid line of 1 for <c>--</c>, a dotted line for <c>..</c>, a double line for
    ///     <c>==</c>, a thin line for <c>__</c>, and a title centered between two halves of the line.
    /// </summary>
    [Fact]
    public void PlantUmlClassRenderer_Separators_DrawTheirLinesAndTitles()
    {
        // Act
        var items = Draw(Parse("class T {\na\n--\nb\n.. dotted ..\nc\n==\nd\n__ thin __\n}"), "T");

        // Assert
        var lines = items.OfType<ScenePath>().Where(p => p.Role == "class-separator").ToList();
        Assert.Equal([0.5, 1, 1, 1, 1, 1, 0.5, 0.5], lines.Select(l => l.Stroke!.Width));
        Assert.Equal([1.0, 2.0], lines[2].Stroke!.Dash!);
        Assert.Equal(Scene.BoundsOf([lines[4]])!.Value.Y + 2, Scene.BoundsOf([lines[5]])!.Value.Y, 3);
        Assert.Equal(["dotted", "thin"], items.OfType<SceneText>().Where(t => t.Role == "class-separator-title").Select(t => t.Text));
    }

    /// <summary>
    ///     Validates an object: its centered name with a padding of 2, its fields below a line 6 from the side, and the
    ///     16 pixels of an object without fields.
    /// </summary>
    [Fact]
    public void PlantUmlClassRenderer_Object_DrawsNameAndFields()
    {
        // Arrange
        var model = Parse("object user {\n  name = \"Dummy\"\n  id = 123\n}\nobject group");
        var text = new TextLayout(TextLayout.CreateFontResolver());
        var font = new SceneFont("SansSerif", 14, false, false);
        var (height, _) = text.LineMetrics(font);

        // Act
        var user = Image(model, "user");
        var group = Image(model, "group");
        var items = user.Draw(0, 0);

        // Assert
        Assert.Equal((text.Width("name = \"Dummy\"", font) + 12, height + 4 + (2 * height) + 8), (user.Width, user.Height), new TupleComparer(0.01));
        Assert.Equal((text.Width("group", font) + 14, height + 4 + 16), (group.Width, group.Height), new TupleComparer(0.01));
        var name = items.OfType<SceneText>().Single(t => t.Role == "class-name");
        Assert.Equal((user.Width - name.Box.Width) / 2, name.X, 3);
        Assert.Equal([6.0, 6.0], items.OfType<SceneText>().Where(t => t.Role == "class-member").Select(t => t.X));
        Assert.DoesNotContain(items, i => i.Role == "class-spot");
    }

    /// <summary>
    ///     Validates a map: a row per entry, the keys centered in their column, the values 5 from theirs, and lines one
    ///     pixel wide.
    /// </summary>
    [Fact]
    public void PlantUmlClassRenderer_Map_DrawsATableOfEntries()
    {
        // Arrange
        var model = Parse("map Capital {\n  UK => London\n  USA => Washington\n}");
        var text = new TextLayout(TextLayout.CreateFontResolver());
        var font = new SceneFont("SansSerif", 14, false, false);
        var columnA = text.Width("USA", font) + 10;

        // Act
        var image = Image(model, "Capital");
        var items = image.Draw(0, 0);

        // Assert
        Assert.Equal(columnA + text.Width("Washington", font) + 10, image.Width, 3);
        var keys = items.OfType<SceneText>().Where(t => t.Role == "map-key").ToList();
        Assert.Equal((columnA - keys[0].Box.Width) / 2, keys[0].X, 3);
        Assert.All(items.OfType<SceneText>().Where(t => t.Role == "map-value"), v => Assert.Equal(columnA + 5, v.X, 3));
        Assert.Equal(4, items.Count(i => i.Role == "map-separator"));
        Assert.All(items.OfType<ScenePath>().Where(p => p.Role == "map-separator"), p => Assert.Equal(1, p.Stroke!.Width));
    }

    /// <summary>
    ///     Compares tuples of numbers within a tolerance.
    /// </summary>
    /// <param name="tolerance">The tolerance.</param>
    private sealed class TupleComparer(double tolerance) : IEqualityComparer<(double, double)>, IEqualityComparer<(double, double, double)>
    {
        /// <inheritdoc/>
        public bool Equals((double, double) x, (double, double) y) => Math.Abs(x.Item1 - y.Item1) <= tolerance && Math.Abs(x.Item2 - y.Item2) <= tolerance;

        /// <inheritdoc/>
        public bool Equals((double, double, double) x, (double, double, double) y) =>
            Equals((x.Item1, x.Item2), (y.Item1, y.Item2)) && Math.Abs(x.Item3 - y.Item3) <= tolerance;

        /// <inheritdoc/>
        public int GetHashCode((double, double) obj) => 0;

        /// <inheritdoc/>
        public int GetHashCode((double, double, double) obj) => 0;
    }
}
