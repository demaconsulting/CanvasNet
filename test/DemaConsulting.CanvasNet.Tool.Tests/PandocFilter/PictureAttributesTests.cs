using DemaConsulting.CanvasNet.Tool.PandocFilter;

namespace DemaConsulting.CanvasNet.Tool.Tests.PandocFilter;

/// <summary>
///     Unit tests for the PictureAttributes unit: the kinds, the attribute checks with the Lua filter's messages, the
///     defaults, the sorted options, <c>%.15g</c>, and the names of code blocks.
/// </summary>
public class PictureAttributesTests
{
    /// <summary>
    ///     Test the kind of a picture by its file name end, in the Lua filter's order; a URL scheme is never a file.
    /// </summary>
    /// <param name="src">The source.</param>
    /// <param name="kind">The kind, or <see langword="null"/>.</param>
    [Theory]
    [InlineData("chart.VL.JSON", "vegalite")]
    [InlineData("chart.vl", "vegalite")]
    [InlineData("C:/models/part.scad", "scad")]
    [InlineData("wiring.vsdx", "vsdx")]
    [InlineData("deck.pptx", "pptx")]
    [InlineData("report.pdf", "pdf")]
    [InlineData("flow.mmd", "mermaid")]
    [InlineData("flow.mermaid", "mermaid")]
    [InlineData("seq.puml", "plantuml")]
    [InlineData("seq.PlantUML", "plantuml")]
    [InlineData("seq.pu", "plantuml")]
    [InlineData("part.iuml", null)]
    [InlineData("https://example.com/part.scad", null)]
    [InlineData("photo.png", null)]
    public void PictureAttributes_KindOf_FileNameEnd_GivesTheKind(string src, string? kind)
    {
        Assert.Equal(kind, PictureAttributes.KindOf(src));
    }

    /// <summary>
    ///     Test that only a local name ending in <c>.svg</c> is a drawing.
    /// </summary>
    [Fact]
    public void PictureAttributes_IsSvg_LocalSvgOnly()
    {
        Assert.True(PictureAttributes.IsSvg("a/Drawing.SVG"));
        Assert.False(PictureAttributes.IsSvg("https://example.com/a.svg"));
        Assert.False(PictureAttributes.IsSvg("a.svgz"));
    }

    /// <summary>
    ///     Test the tables the shim's fast path is written from: the picture suffixes are exactly the names the engine
    ///     draws or flattens (in any letter case), and the code-block classes are exactly the classes it draws (case-sensitive).
    /// </summary>
    [Fact]
    public void PictureAttributes_PictureSuffixesAndBlockClasses_AreWhatTheEngineDraws()
    {
        // Assert: the lists
        Assert.Equal([".svg", ".vl.json", ".vl", ".scad", ".vsdx", ".pptx", ".pdf", ".mmd", ".mermaid", ".puml", ".plantuml", ".pu", ".mml"], PictureAttributes.PictureSuffixes);
        Assert.Equal(["mermaid", "plantuml", "mathml", "vega-lite", "vegalite"], PictureAttributes.BlockClasses);
        Assert.Equal(["openscad"], PictureAttributes.DrawBlockClasses);
        Assert.Equal("draw", PictureAttributes.DrawClass);

        // Assert: every suffix is a picture the engine handles, in either letter case
        Assert.All(PictureAttributes.PictureSuffixes, suffix =>
        {
            Assert.True(PictureAttributes.IsSvg("x" + suffix) || PictureAttributes.KindOf("x" + suffix) != null);
            Assert.True(PictureAttributes.IsSvg("X" + suffix.ToUpperInvariant()) || PictureAttributes.KindOf("X" + suffix.ToUpperInvariant()) != null);
        });

        // Assert: the classes of a code block
        Assert.Equal("mermaid", PictureAttributes.BlockKindOf(["mermaid"]));
        Assert.Equal("mermaid", PictureAttributes.BlockKindOf(["numberLines", "mermaid"]));
        Assert.Equal("plantuml", PictureAttributes.BlockKindOf(["plantuml"]));
        Assert.Equal("mathml", PictureAttributes.BlockKindOf(["mathml"]));
        Assert.Null(PictureAttributes.BlockKindOf(["mml"]));
        Assert.Null(PictureAttributes.BlockKindOf(["Mermaid"]));
        Assert.Null(PictureAttributes.BlockKindOf(["puml"]));
        Assert.Null(PictureAttributes.BlockKindOf(["bash"]));
        Assert.Null(PictureAttributes.BlockKindOf([]));
    }

    /// <summary>
    ///     Test that a code block of class <c>vega-lite</c> or its alias <c>vegalite</c> is drawn as a Vega-Lite chart,
    ///     whatever other classes it has, and that the class is matched exactly.
    /// </summary>
    [Fact]
    public void PictureAttributes_BlockOf_VegaLiteAndItsAlias_AreCharts()
    {
        // Act
        var dashed = PictureAttributes.BlockOf(["vega-lite"]);
        var alias = PictureAttributes.BlockOf(["wide", "vegalite"]);

        // Assert: both are charts, each named by the class as written, and drawn without draw
        Assert.Equal(new BlockClass("vega-lite", PictureAttributes.VegaLite, false), dashed);
        Assert.Equal(new BlockClass("vegalite", PictureAttributes.VegaLite, false), alias);
        Assert.Equal(PictureAttributes.VegaLite, PictureAttributes.BlockKindOf(["vega-lite", "draw"]));
        Assert.Null(PictureAttributes.BlockOf(["Vega-Lite"]));
        Assert.Null(PictureAttributes.BlockOf(["vl"]));
        Assert.Null(PictureAttributes.BlockOf(["json"]));
    }

    /// <summary>
    ///     Test that a code block of class <c>openscad</c> is drawn as a model only when it also has the class <c>draw</c>,
    ///     in either order, and that neither class alone, nor another letter case, makes a picture.
    /// </summary>
    [Fact]
    public void PictureAttributes_BlockOf_OpenScadNeedsTheDrawClass()
    {
        // Act
        var plain = PictureAttributes.BlockOf(["openscad"]);
        var drawn = PictureAttributes.BlockOf(["openscad", "draw"]);
        var reversed = PictureAttributes.BlockOf(["draw", "numberLines", "openscad"]);

        // Assert: only the block that asks for the picture is drawn, as a model
        Assert.Null(plain);
        Assert.Equal(new BlockClass("openscad", PictureAttributes.Scad, true), drawn);
        Assert.Equal(drawn, reversed);
        Assert.Null(PictureAttributes.BlockOf(["draw"]));
        Assert.Null(PictureAttributes.BlockOf(["OpenSCAD", "draw"]));
        Assert.Null(PictureAttributes.BlockOf(["scad", "draw"]));
        Assert.Null(PictureAttributes.BlockOf(["openscad", "Draw"]));
    }

    /// <summary>
    ///     Test that a message names a code block by the class the author wrote, so a <c>vegalite</c> block is not called a
    ///     <c>vega-lite</c> block, and an OpenSCAD block is an <c>openscad</c> block rather than one of its kind.
    /// </summary>
    [Fact]
    public void PictureAttributes_BlockName_NamesTheClassTheAuthorWrote()
    {
        // Act / Assert
        Assert.Equal("vega-lite block #sales", PictureAttributes.BlockName("sales", "{}", "vega-lite"));
        Assert.Equal("vegalite block ({\"mark\": \"bar\",)", PictureAttributes.BlockName("", "\n{\"mark\": \"bar\",\n}", "vegalite"));
        Assert.Equal("openscad block (cube(10);)", PictureAttributes.BlockName("", "cube(10);", "openscad"));
        Assert.Equal("openscad block #part", PictureAttributes.BlockName("part", "cube(10);", "openscad"));
    }

    /// <summary>
    ///     Test every attribute check and wrong-kind rule with the Lua filter's exact message.
    /// </summary>
    /// <param name="name">The picture's name.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="key">The attribute.</param>
    /// <param name="value">The value.</param>
    /// <param name="message">The expected message.</param>
    [Theory]
    [InlineData("m.scad", "scad", "size", "0x10", "'m.scad' has size=0x10; write WIDTHxHEIGHT in pixels (each 1 to 8192), for example 1600x1200")]
    [InlineData("m.scad", "scad", "size", "8193x10", "'m.scad' has size=8193x10; write WIDTHxHEIGHT in pixels (each 1 to 8192), for example 1600x1200")]
    [InlineData("m.scad", "scad", "scale", "0", "'m.scad' has scale=0; write a number from 0.001 up to 8192")]
    [InlineData("m.scad", "scad", "scale", "1e3", "'m.scad' has scale=1e3; write a number from 0.001 up to 8192")]
    [InlineData("m.scad", "scad", "background", "#12", "'m.scad' has background=#12; write a color such as #F0F0F0, a name such as white, or transparent")]
    [InlineData("d.pdf", "pdf", "page", "0", "'d.pdf' has page=0; write the page number, counting from 1")]
    [InlineData("d.vsdx", "vsdx", "dpi", "1.5", "'d.vsdx' has dpi=1.5; write a whole number above 0 up to 2400")]
    [InlineData("d.pdf", "pdf", "dpi", "0", "'d.pdf' has dpi=0; write a number above 0 up to 2400")]
    [InlineData("m.scad", "scad", "view", "sideways", "'m.scad' has view=sideways; write iso, front, back, left, right, top or bottom")]
    [InlineData("m.scad", "scad", "azimuth", "360001", "'m.scad' has azimuth=360001; write a number from -360000 up to 360000")]
    [InlineData("m.scad", "scad", "elevation", "-90.5", "'m.scad' has elevation=-90.5; write a number from -90 up to 90")]
    [InlineData("m.scad", "scad", "projection", "fish", "'m.scad' has projection=fish; write ortho or perspective")]
    [InlineData("f.mmd", "mermaid", "theme", "x", "'f.mmd' has theme=x; write default, base, dark, forest or neutral")]
    [InlineData("d.pdf", "pdf", "view", "iso", "'d.pdf' has view=iso, but view applies to .scad models only")]
    [InlineData("m.scad", "scad", "page", "1", "'m.scad' has page=1, but page applies to .vsdx, .pptx and .pdf files only")]
    [InlineData("m.scad", "scad", "theme", "dark", "'m.scad' has theme=dark, but theme applies to Mermaid diagrams only")]
    [InlineData("d.pdf", "pdf", "define-x", "1", "'d.pdf' has define-x=1, but define-NAME applies to .scad models only")]
    [InlineData("m.scad", "scad", "define-1x", "2", "'m.scad' has define-1x; the variable name must be letters, digits and underscores, not starting with a digit")]
    [InlineData("m.scad", "scad", "define-", "2", "'m.scad' has define-; the variable name must be letters, digits and underscores, not starting with a digit")]
    [InlineData("m.scad", "scad", "param-x", "1", "'m.scad' has param-x=1, but param-NAME applies to Vega-Lite charts only")]
    [InlineData("c.vl", "vegalite", "param-a=b", "c", "'c.vl' has param-a=b; the parameter name cannot contain an equals sign")]
    [InlineData("c.vl", "vegalite", "param-", "c", "'c.vl' has param-; the parameter name cannot be empty")]
    [InlineData("m.scad", "scad", "param-", "1", "'m.scad' has param-=1, but param-NAME applies to Vega-Lite charts only")]
    [InlineData("m.scad", "scad", "background", "a\tb", "'m.scad' has background=a\tb; the value must be text without control characters")]
    [InlineData("m.scad", "scad", "view", "", "'m.scad' has view=; the value must be text without control characters")]
    public void PictureAttributes_Build_InvalidAttribute_ThrowsTheLuaMessage(string name, string kind, string key, string value, string message)
    {
        // Act
        var error = Assert.Throws<ToolException>(() => PictureAttributes.Build(name, kind, [new(key, value)]));

        // Assert: the filter's code and its exact message, without a hint
        Assert.Equal(ErrorCodes.PandocFilterError, error.Code);
        Assert.Equal(message, error.Message);
        Assert.Null(error.Hint);
    }

    /// <summary>
    ///     Test that size and scale together are an error.
    /// </summary>
    [Fact]
    public void PictureAttributes_Build_SizeAndScale_ThrowsUseOneOfThem()
    {
        var error = Assert.Throws<ToolException>(() => PictureAttributes.Build("f.mmd", "mermaid", [new("size", "10x10"), new("scale", "2")]));
        Assert.Equal("'f.mmd' has both size and scale; use one of them", error.Message);
    }

    /// <summary>
    ///     Test that a page number too large for a number (it would overflow to infinity) is the filter's page error,
    ///     not a formatting failure.
    /// </summary>
    [Fact]
    public void PictureAttributes_Build_PageBeyondANumber_ThrowsThePageError()
    {
        // Arrange
        var page = new string('9', 400);

        // Act
        var error = Assert.Throws<ToolException>(() => PictureAttributes.Build("d.pdf", "pdf", [new("page", page)]));

        // Assert
        Assert.Equal(ErrorCodes.PandocFilterError, error.Code);
        Assert.Equal($"'d.pdf' has page={page}; write the page number, counting from 1", error.Message);
    }

    /// <summary>
    ///     Test the defaults of a model, the normalized values, the sorted options, and the attributes that stay in their
    ///     order with a repeated key's last value.
    /// </summary>
    [Fact]
    public void PictureAttributes_Build_Model_AddsDefaultsAndSortsByBytes()
    {
        // Act: attributes in an arbitrary order, with a repeated kept key and two variables
        var result = PictureAttributes.Build(
            "m.scad",
            "scad",
            [new("width", "50%"), new("view", "TOP"), new("define-b", "2"), new("id", "x"), new("define-a", "1"), new("width", "60%")]);

        // Assert
        Assert.Equal(["--background=white", "--define=a=1", "--define=b=2", "--max-triangles=2000000", "--size=1600x1200", "--view=top"], result.Options);
        Assert.Equal([new("width", "60%"), new KeyValuePair<string, string>("id", "x")], result.Keep);
    }

    /// <summary>
    ///     Test the defaults and the normalized values of a document page: page 1, leading zeros dropped, and numbers
    ///     written with <c>%.15g</c>.
    /// </summary>
    [Fact]
    public void PictureAttributes_Build_Document_NormalizesPageAndNumbers()
    {
        // Act
        var first = PictureAttributes.Build("d.pdf", "pdf", [new("dpi", "150.0")]);
        var later = PictureAttributes.Build("d.pptx", "pptx", [new("page", "007"), new("dpi", "0.00001"), new("background", "transparent")]);
        var sized = PictureAttributes.Build("f.mmd", "mermaid", [new("size", "0640x0480"), new("theme", "Forest")]);

        // Assert
        Assert.Equal(["--dpi=150", "--page=1"], first.Options);
        Assert.Equal(["--background=transparent", "--dpi=1e-05", "--page=7"], later.Options);
        Assert.Equal(["--size=640x480", "--theme=forest"], sized.Options);
    }

    /// <summary>
    ///     Test that a <c>.mml</c> picture is a MathML equation in any letter case, and that a name that only looks like
    ///     one is not.
    /// </summary>
    [Fact]
    public void PictureAttributes_KindOf_Mml_IsMathMl()
    {
        Assert.Equal("mathml", PictureAttributes.KindOf("eq.mml"));
        Assert.Equal("mathml", PictureAttributes.KindOf("docs/Quadratic.MML"));
        Assert.Null(PictureAttributes.KindOf("eq.mathml"));
        Assert.Null(PictureAttributes.KindOf("eq.mml#display=inline"));
        Assert.Null(PictureAttributes.KindOf("https://example.com/eq.mml"));
    }

    /// <summary>
    ///     Test that an equation is drawn at scale 2 unless a size or a scale is given, that its own scale, size, and
    ///     background are kept as options, and that the natural display width is wanted only when no size, width, or
    ///     height is given.
    /// </summary>
    [Fact]
    public void PictureAttributes_Build_MathMlPicture_DefaultsToScaleTwo()
    {
        // Act
        var plain = PictureAttributes.Build("eq.mml", "mathml", [new("id", "eq")]);
        var scaled = PictureAttributes.Build("eq.mml", "mathml", [new("scale", "3"), new("background", "transparent")]);
        var sized = PictureAttributes.Build("eq.mml", "mathml", [new("size", "400x100")]);
        var wide = PictureAttributes.Build("eq.mml", "mathml", [new("width", "50%")]);

        // Assert: the default scale, and the author's options as written
        Assert.Equal(["--scale=2"], plain.Options);
        Assert.Equal([new KeyValuePair<string, string>("id", "eq")], plain.Keep);
        Assert.Equal(["--background=transparent", "--scale=3"], scaled.Options);
        Assert.Equal(["--size=400x100"], sized.Options);
        Assert.Equal(["--scale=2"], wide.Options);
        Assert.Equal([new KeyValuePair<string, string>("width", "50%")], wide.Keep);

        // Assert: the natural width only for an equation without a size, width, or height
        Assert.True(PictureAttributes.NaturalWidthNeeded("mathml", [new("id", "eq")]));
        Assert.True(PictureAttributes.NaturalWidthNeeded("mathml", [new("scale", "3")]));
        Assert.False(PictureAttributes.NaturalWidthNeeded("mathml", [new("size", "400x100")]));
        Assert.False(PictureAttributes.NaturalWidthNeeded("mathml", [new("width", "50%")]));
        Assert.False(PictureAttributes.NaturalWidthNeeded("mathml", [new("height", "2em")]));
        Assert.False(PictureAttributes.NaturalWidthNeeded("mermaid", []));
    }

    /// <summary>
    ///     Test that the attributes of other kinds are refused on an equation with the Lua filter's messages: a theme is
    ///     for Mermaid diagrams only, and a page for documents only.
    /// </summary>
    [Fact]
    public void PictureAttributes_Build_MathMlTheme_IsRefused()
    {
        // Act
        var theme = Assert.Throws<ToolException>(() => PictureAttributes.Build("eq.mml", "mathml", [new("theme", "dark")]));
        var page = Assert.Throws<ToolException>(() => PictureAttributes.Build("mathml block #eq", "mathml", [new("page", "2")]));

        // Assert
        Assert.Equal(ErrorCodes.PandocFilterError, theme.Code);
        Assert.Equal("'eq.mml' has theme=dark, but theme applies to Mermaid diagrams only", theme.Message);
        Assert.Equal("'mathml block #eq' has page=2, but page applies to .vsdx, .pptx and .pdf files only", page.Message);
    }

    /// <summary>
    ///     Test <c>%.15g</c>: fifteen significant digits, trailing zeros removed, and the exponent form below 1e-4 and from
    ///     1e15, unlike <c>G15</c>.
    /// </summary>
    /// <param name="value">The number.</param>
    /// <param name="expected">The text C's printf writes.</param>
    [Theory]
    [InlineData(0.00001, "1e-05")]
    [InlineData(0.0001, "0.0001")]
    [InlineData(1e15, "1e+15")]
    [InlineData(123456789012345.6, "123456789012346")]
    [InlineData(0.30000000000000004, "0.3")]
    [InlineData(2400.0, "2400")]
    [InlineData(-1.5, "-1.5")]
    [InlineData(-0.0, "-0")]
    [InlineData(1e100, "1e+100")]
    public void PictureAttributes_FormatNumber_MatchesPrintfG15(double value, string expected)
    {
        Assert.Equal(expected, PictureAttributes.FormatNumber(value));
    }

    /// <summary>
    ///     Test the options of a drawing: only <c>scale</c> is read, in order; every other attribute stays.
    /// </summary>
    [Fact]
    public void PictureAttributes_BuildSvg_ReadsScaleOnly()
    {
        // Act
        var result = PictureAttributes.BuildSvg("d.svg", [new("width", "50%"), new("scale", "2.50"), new("view", "top")]);

        // Assert
        Assert.Equal(["--scale=2.5"], result.Options);
        Assert.Equal([new("width", "50%"), new KeyValuePair<string, string>("view", "top")], result.Keep);
    }

    /// <summary>
    ///     Test the name of a code block: the identifier, else the first non-blank line trimmed and cut to 40 characters,
    ///     and the caption split off.
    /// </summary>
    [Fact]
    public void PictureAttributes_BlockNameAndCaption_FollowTheLuaFilter()
    {
        // Act / Assert: the names
        Assert.Equal("mermaid block #flow", PictureAttributes.BlockName("flow", "graph"));
        Assert.Equal("mermaid block (flowchart LR)", PictureAttributes.BlockName("", "\n   flowchart LR  \r\nA-->B"));
        Assert.Equal("mermaid block (" + new string('é', 40) + "...)", PictureAttributes.BlockName("", new string('é', 41)));
        Assert.Equal("plantuml block #seq", PictureAttributes.BlockName("seq", "@startuml", PictureAttributes.PlantUml));
        Assert.Equal("plantuml block (@startuml)", PictureAttributes.BlockName("", "@startuml\nA -> B", PictureAttributes.PlantUml));
        Assert.Equal("mathml block (<math display=\"block\">)", PictureAttributes.BlockName("", "<math display=\"block\">\n<mi>x</mi>", PictureAttributes.MathMl));

        // Act / Assert: the last caption is the caption, the other attributes stay
        var (caption, rest) = PictureAttributes.SplitCaption([new("caption", "A"), new("theme", "dark"), new("caption", "B *c*")]);
        Assert.Equal("B *c*", caption);
        Assert.Equal([new KeyValuePair<string, string>("theme", "dark")], rest);
    }
}
