// Portions derived from mermaid (MIT); see third-party/README.md
using static DemaConsulting.CanvasNet.Mermaid.Configuration.ColorMath;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <content>
///     The <c>dark</c> theme (Mermaid's <c>theme-dark.js</c>).
/// </content>
internal static partial class ThemeResolver
{
    /// <summary>
    ///     The constructor of the <c>dark</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void ConstructDark(ThemeObject t)
    {
        t["background"] = "#333";
        t["primaryColor"] = "#1f2020";
        t["secondaryColor"] = Lighten(t["primaryColor"], 16);
        t["tertiaryColor"] = Adjust(t["primaryColor"], h: -160);
        t["primaryBorderColor"] = Invert(t["background"]);
        t["secondaryBorderColor"] = MkBorder(t["secondaryColor"], t["darkMode"]);
        t["tertiaryBorderColor"] = MkBorder(t["tertiaryColor"], t["darkMode"]);
        t["primaryTextColor"] = Invert(t["primaryColor"]);
        t["secondaryTextColor"] = Invert(t["secondaryColor"]);
        t["tertiaryTextColor"] = Invert(t["tertiaryColor"]);
        t["lineColor"] = Invert(t["background"]);
        t["textColor"] = Invert(t["background"]);
        t["mainBkg"] = "#1f2020";
        t["secondBkg"] = "calculated";
        t["mainContrastColor"] = "lightgrey";
        t["darkTextColor"] = Lighten(Invert("#323D47"), 10);
        t["lineColor"] = "calculated";
        t["border1"] = "#ccc";
        t["border2"] = Rgba(255, 255, 255, 0.25);
        t["arrowheadColor"] = "calculated";
        t["fontFamily"] = "\"trebuchet ms\", verdana, arial, sans-serif";
        t["fontSize"] = "16px";
        t["labelBackground"] = "#181818";
        t["textColor"] = "#ccc";
        t["THEME_COLOR_LIMIT"] = 12.0;
        t["radius"] = 5.0;
        t["strokeWidth"] = 1.0;

        // Flowchart variables
        t["nodeBkg"] = "calculated";
        t["nodeBorder"] = "calculated";
        t["clusterBkg"] = "calculated";
        t["clusterBorder"] = "calculated";
        t["defaultLinkColor"] = "calculated";
        t["titleColor"] = "#F9FFFE";
        t["edgeLabelBackground"] = "calculated";

        // Sequence diagram variables
        foreach (var key in (ReadOnlySpan<string>)[
                     "actorBorder", "actorBkg", "actorTextColor", "actorLineColor", "signalColor", "signalTextColor",
                     "labelBoxBkgColor", "labelBoxBorderColor", "labelTextColor", "loopTextColor", "noteBorderColor"])
        {
            t[key] = "calculated";
        }

        t["noteBkgColor"] = "#fff5ad";
        t["noteTextColor"] = "calculated";
        t["activationBorderColor"] = "calculated";
        t["activationBkgColor"] = "calculated";
        t["sequenceNumberColor"] = "black";
        t["clusterBkg"] = "#302F3D";

        // Gantt chart variables
        t["sectionBkgColor"] = Darken("#EAE8D9", 30);
        t["altSectionBkgColor"] = "calculated";
        t["sectionBkgColor2"] = "#EAE8D9";
        t["excludeBkgColor"] = Darken(t["sectionBkgColor"], 10);
        t["taskBorderColor"] = Rgba(255, 255, 255, 70);
        t["taskBkgColor"] = "calculated";
        t["taskTextColor"] = "calculated";
        t["taskTextLightColor"] = "calculated";
        t["taskTextOutsideColor"] = "calculated";
        t["taskTextClickableColor"] = "#003163";
        t["activeTaskBorderColor"] = Rgba(255, 255, 255, 50);
        t["activeTaskBkgColor"] = "#81B1DB";
        t["gridColor"] = "calculated";
        t["doneTaskBkgColor"] = "calculated";
        t["doneTaskBorderColor"] = "grey";
        t["critBorderColor"] = "#E83737";
        t["critBkgColor"] = "#E83737";
        t["taskTextDarkColor"] = "calculated";
        t["todayLineColor"] = "#DB5757";
        t["vertLineColor"] = "#00BFFF";

        // C4 context diagram variables
        t["personBorder"] = t["primaryBorderColor"];
        t["personBkg"] = t["mainBkg"];

        // Architecture diagram variables
        t["archEdgeColor"] = "calculated";
        t["archEdgeArrowColor"] = "calculated";
        t["archEdgeWidth"] = "3";
        t["archGroupBorderColor"] = t["primaryBorderColor"];
        t["archGroupBorderWidth"] = "2px";

        // Entity relationship variables
        t.Or("rowOdd", () => Js.Or(Lighten(t["mainBkg"], 5), () => "#ffffff"));
        t.Or("rowEven", () => Darken(t["mainBkg"], 10));

        // State colors
        t["labelColor"] = "calculated";
        t["errorBkgColor"] = "#a44141";
        t["errorTextColor"] = "#ddd";
        t["useGradient"] = true;
        t["gradientStart"] = t["primaryBorderColor"];
        t["gradientStop"] = t["secondaryBorderColor"];
        t["dropShadow"] = "drop-shadow( 1px 2px 2px rgba(185,185,185,1))";
        t.Or("noteFontWeight", () => "normal");
        t.Or("fontWeight", () => "normal");
    }

    /// <summary>
    ///     The <c>updateColors</c> of the <c>dark</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void UpdateDark(ThemeObject t)
    {
        DarkDiagramVariables(t);
        FillTypes(t);
        DarkColorScale(t);

        // Pie diagram
        var limit = ColorLimit(t);
        for (var i = 0; i < limit; i++)
        {
            t[Key("pie", i)] = t[Key("cScale", i)];
        }

        PieText(t, "mainContrastColor");

        // Venn
        for (var i = 0; i < 8; i++)
        {
            var index = i;
            t.Nullish(Key("venn", index + 1), () => Lighten(t[Key("cScale", index)], 30));
        }

        t.Nullish("vennTitleTextColor", () => t["titleColor"]);
        t.Nullish("vennSetTextColor", () => t["textColor"]);
        Cynefin(t, ("#FF6B6B", "#1B5E20", "#0D47A1", "#BF360C", "#F57F17", "#4A148C"));
        Quadrant(t);
        XyChart(t, "#3498db,#2ecc71,#e74c3c,#f1c40f,#bdc3c7,#ffffff,#34495e,#9b59b6,#1abc9c,#e67e22");
        DarkPacket(t);
        Radar(t);
        Wardley(t, "#ff6b6b", "mainBkg");
        t["classText"] = t["primaryTextColor"];
        Requirement(t, () => Js.Truthy(t["darkMode"]) ? Darken(t["secondaryColor"], 30) : t["secondaryColor"]);
        DarkGit(t);
        GitTagsAndCommits(t);
        DarkEventModeling(t);

        // Entity relationship diagrams
        t.Or("attributeBackgroundColorOdd", () => Lighten(t["background"], 12));
        t.Or("attributeBackgroundColorEven", () => Lighten(t["background"], 2));
        t.Or("nodeBorder", () => "#999");
    }

    /// <summary>
    ///     The flowchart, sequence, gantt, architecture and state variables of the <c>dark</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void DarkDiagramVariables(ThemeObject t)
    {
        t["secondBkg"] = Lighten(t["mainBkg"], 16);
        t["lineColor"] = t["mainContrastColor"];
        t["arrowheadColor"] = t["mainContrastColor"];

        // Flowchart variables
        t["nodeBkg"] = t["mainBkg"];
        t["nodeBorder"] = t["border1"];
        t["clusterBkg"] = t["secondBkg"];
        t["clusterBorder"] = t["border2"];
        t["defaultLinkColor"] = t["lineColor"];
        t["edgeLabelBackground"] = Lighten(t["labelBackground"], 25);

        // Sequence diagram variables
        t["actorBorder"] = t["border1"];
        t["actorBkg"] = t["mainBkg"];
        t["actorTextColor"] = t["mainContrastColor"];
        t["actorLineColor"] = t["actorBorder"];
        t["signalColor"] = t["mainContrastColor"];
        t["signalTextColor"] = t["mainContrastColor"];
        t["labelBoxBkgColor"] = t["actorBkg"];
        t["labelBoxBorderColor"] = t["actorBorder"];
        t["labelTextColor"] = t["mainContrastColor"];
        t["loopTextColor"] = t["mainContrastColor"];
        t["noteBorderColor"] = t["secondaryBorderColor"];
        t["noteBkgColor"] = t["secondBkg"];
        t["noteTextColor"] = t["secondaryTextColor"];
        t["activationBorderColor"] = t["border1"];
        t["activationBkgColor"] = t["secondBkg"];
        t.Or("rectBkgColor", () => t["tertiaryColor"]);

        // Gantt chart variables
        t["altSectionBkgColor"] = t["background"];
        t["taskBkgColor"] = Lighten(t["mainBkg"], 23);
        t["taskTextColor"] = t["darkTextColor"];
        t["taskTextLightColor"] = t["mainContrastColor"];
        t["taskTextOutsideColor"] = t["taskTextLightColor"];
        t["gridColor"] = t["mainContrastColor"];
        t["doneTaskBkgColor"] = t["mainContrastColor"];
        t["taskTextDarkColor"] = Invert(t["doneTaskBkgColor"]);

        // Architecture diagram variables
        t["archEdgeColor"] = t["lineColor"];
        t["archEdgeArrowColor"] = t["lineColor"];

        // State colors
        t.Or("transitionColor", () => t["lineColor"]);
        t.Or("transitionLabelColor", () => t["textColor"]);
        t.Or("stateLabelColor", () => Js.Or(t["stateBkg"], () => t["primaryTextColor"]));
        t.Or("stateBkg", () => t["mainBkg"]);
        t.Or("labelBackgroundColor", () => t["stateBkg"]);
        t.Or("compositeBackground", () => Js.Or(t["background"], () => t["tertiaryColor"]));
        t.Or("altBackground", () => "#555");
        t.Or("compositeTitleBackground", () => t["mainBkg"]);
        t.Or("compositeBorder", () => t["nodeBorder"]);
        t["innerEndBackground"] = t["primaryBorderColor"];
        t["specialStateColor"] = "#f4f4f4";
        t.Or("errorBkgColor", () => t["tertiaryColor"]);
        t.Or("errorTextColor", () => t["tertiaryTextColor"]);
    }

    /// <summary>
    ///     The color scale, surfaces and scale labels of the <c>dark</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void DarkColorScale(ThemeObject t)
    {
        t.Or("cScale1", () => "#0b0000");
        t.Or("cScale2", () => "#4d1037");
        t.Or("cScale3", () => "#3f5258");
        t.Or("cScale4", () => "#4f2f1b");
        t.Or("cScale5", () => "#6e0a0a");
        t.Or("cScale6", () => "#3b0048");
        t.Or("cScale7", () => "#995a01");
        t.Or("cScale8", () => "#154706");
        t.Or("cScale9", () => "#161722");
        t.Or("cScale10", () => "#00296f");
        t.Or("cScale11", () => "#01629c");
        t.Or("cScale12", () => "#010029");
        ColorScaleDefaults(t);

        var limit = ColorLimit(t);
        for (var i = 0; i < limit; i++)
        {
            var index = i;
            t.Or(Key("cScaleInv", index), () => Invert(t[Key("cScale", index)]));
        }

        for (var i = 0; i < limit; i++)
        {
            var index = i;
            t.Or(Key("cScalePeer", index), () => Lighten(t[Key("cScale", index)], 10));
        }

        for (var i = 0; i < 5; i++)
        {
            var index = i;
            t.Or(Key("surface", index), () => Adjust(t["mainBkg"], h: 30, s: -30, l: -(-10 + (index * 4))));
            t.Or(Key("surfacePeer", index), () => Adjust(t["mainBkg"], h: 30, s: -30, l: -(-7 + (index * 4))));
        }

        t.Or("scaleLabelColor", () => Js.Truthy(t["darkMode"]) ? "black" : t["labelTextColor"]);
        for (var i = 0; i < limit; i++)
        {
            t.Or(Key("cScaleLabel", i), () => t["scaleLabelColor"]);
        }
    }

    /// <summary>
    ///     The packet variables of the <c>dark</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void DarkPacket(ThemeObject t)
    {
        var packet = new ThemeObject();
        packet["startByteColor"] = t["primaryTextColor"];
        packet["endByteColor"] = t["primaryTextColor"];
        packet["labelColor"] = t["primaryTextColor"];
        packet["titleColor"] = t["primaryTextColor"];
        packet["blockStrokeColor"] = t["primaryTextColor"];
        packet["blockFillColor"] = t["background"];
        t["packet"] = packet;
    }

    /// <summary>
    ///     The git variables of the <c>dark</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void DarkGit(ThemeObject t)
    {
        t["git0"] = Lighten(t["secondaryColor"], 20);
        t["git1"] = Lighten(Js.Or(t["pie2"], () => t["secondaryColor"]), 20);
        t["git2"] = Lighten(Js.Or(t["pie3"], () => t["tertiaryColor"]), 20);
        t["git3"] = Lighten(Js.Or(t["pie4"], () => Adjust(t["primaryColor"], h: -30)), 20);
        t["git4"] = Lighten(Js.Or(t["pie5"], () => Adjust(t["primaryColor"], h: -60)), 20);
        t["git5"] = Lighten(Js.Or(t["pie6"], () => Adjust(t["primaryColor"], h: -90)), 10);
        t["git6"] = Lighten(Js.Or(t["pie7"], () => Adjust(t["primaryColor"], h: 60)), 10);
        t["git7"] = Lighten(Js.Or(t["pie8"], () => Adjust(t["primaryColor"], h: 120)), 20);
        t.Or("gitInv0", () => Invert(t["git0"]));
        GitInverted(t);
        GitBranchLabels(t);
    }

    /// <summary>
    ///     The event-modeling variables of the <c>dark</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void DarkEventModeling(ThemeObject t)
    {
        t.Or("emUiFill", () => "#2d2d2d");
        t.Or("emUiStroke", () => "#555");
        t.Or("emProcessorFill", () => Lighten("#5a3d5c", 10));
        t.Or("emProcessorStroke", () => "#8a6d8c");
        t.Or("emReadModelFill", () => Lighten("#3d5a2d", 10));
        t.Or("emReadModelStroke", () => "#6d8c5c");
        t.Or("emCommandFill", () => Lighten("#2d3d5a", 10));
        t.Or("emCommandStroke", () => "#5c6d8c");
        t.Or("emEventFill", () => Lighten("#5a452d", 10));
        t.Or("emEventStroke", () => "#8c755c");
        t.Or("emSwimlaneBackgroundOdd", () => Lighten(t["background"], 5));
        t.Or("emSwimlaneBackgroundStroke", () => Lighten(t["background"], 12));
        t.Or("emArrowhead", () => t["lineColor"]);
        t.Or("emRelationStroke", () => t["lineColor"]);
    }
}
