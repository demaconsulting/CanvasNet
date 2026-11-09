// Portions derived from mermaid (MIT); see third-party/README.md
using static DemaConsulting.CanvasNet.Mermaid.Configuration.ColorMath;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <content>
///     The <c>neutral</c> theme (Mermaid's <c>theme-neutral.js</c>), a black-and-white theme for printing.
/// </content>
internal static partial class ThemeResolver
{
    /// <summary>
    ///     The constructor of the <c>neutral</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void ConstructNeutral(ThemeObject t)
    {
        t["primaryColor"] = "#eee";
        t["contrast"] = "#707070";
        t["secondaryColor"] = Lighten(t["contrast"], 55);
        t["background"] = "#ffffff";
        t["tertiaryColor"] = Adjust(t["primaryColor"], h: -160);
        t["primaryBorderColor"] = MkBorder(t["primaryColor"], t["darkMode"]);
        t["secondaryBorderColor"] = MkBorder(t["secondaryColor"], t["darkMode"]);
        t["tertiaryBorderColor"] = MkBorder(t["tertiaryColor"], t["darkMode"]);
        t["primaryTextColor"] = Invert(t["primaryColor"]);
        t["secondaryTextColor"] = Invert(t["secondaryColor"]);
        t["tertiaryTextColor"] = Invert(t["tertiaryColor"]);
        t["lineColor"] = Invert(t["background"]);
        t["textColor"] = Invert(t["background"]);
        t["mainBkg"] = "#eee";
        t["secondBkg"] = "calculated";
        t["lineColor"] = "#666";
        t["border1"] = "#999";
        t["border2"] = "calculated";
        t["note"] = "#ffa";
        t["text"] = "#333";
        t["critical"] = "#d42";
        t["done"] = "#bbb";
        t["arrowheadColor"] = "#333333";
        t["fontFamily"] = "\"trebuchet ms\", verdana, arial, sans-serif";
        t["fontSize"] = "16px";
        t["THEME_COLOR_LIMIT"] = 12.0;
        t["radius"] = 5.0;
        t["strokeWidth"] = 1.0;

        // Flowchart variables
        t["nodeBkg"] = "calculated";
        t["nodeBorder"] = "calculated";
        t["clusterBkg"] = "calculated";
        t["clusterBorder"] = "calculated";
        t["defaultLinkColor"] = "calculated";
        t["titleColor"] = "calculated";
        t["edgeLabelBackground"] = "white";

        // Sequence diagram variables
        t["actorBorder"] = "calculated";
        t["actorBkg"] = "calculated";
        t["actorTextColor"] = "calculated";
        t["actorLineColor"] = t["actorBorder"];
        foreach (var key in (ReadOnlySpan<string>)[
                     "signalColor", "signalTextColor", "labelBoxBkgColor", "labelBoxBorderColor", "labelTextColor", "loopTextColor",
                     "noteBorderColor", "noteBkgColor", "noteTextColor"])
        {
            t[key] = "calculated";
        }

        t["activationBorderColor"] = "#666";
        t["activationBkgColor"] = "#f4f4f4";
        t["sequenceNumberColor"] = "white";

        // Gantt chart variables
        t["sectionBkgColor"] = "calculated";
        t["altSectionBkgColor"] = "white";
        t["sectionBkgColor2"] = "calculated";
        t["excludeBkgColor"] = "#eeeeee";
        t["taskBorderColor"] = "calculated";
        t["taskBkgColor"] = "calculated";
        t["taskTextLightColor"] = "white";
        t["taskTextColor"] = "calculated";
        t["taskTextDarkColor"] = "calculated";
        t["taskTextOutsideColor"] = "calculated";
        t["taskTextClickableColor"] = "#003163";
        foreach (var key in (ReadOnlySpan<string>)[
                     "activeTaskBorderColor", "activeTaskBkgColor", "gridColor", "doneTaskBkgColor", "doneTaskBorderColor",
                     "critBkgColor", "critBorderColor", "todayLineColor", "vertLineColor"])
        {
            t[key] = "calculated";
        }

        // C4 context diagram variables
        t["personBorder"] = t["primaryBorderColor"];
        t["personBkg"] = t["mainBkg"];

        // Architecture diagram variables
        t["archEdgeColor"] = "calculated";
        t["archEdgeArrowColor"] = "calculated";
        t["archEdgeWidth"] = "3";
        t["archGroupBorderColor"] = t["primaryBorderColor"];
        t["archGroupBorderWidth"] = "2px";
        t["noteFontWeight"] = "normal";
        t["fontWeight"] = "normal";

        // Entity relationship diagram
        t.Or("rowOdd", () => Js.Or(Lighten(t["mainBkg"], 75), () => "#ffffff"));
        t.Or("rowEven", () => "#f4f4f4");

        // State colors
        t["labelColor"] = "black";
        t["errorBkgColor"] = "#552222";
        t["errorTextColor"] = "#552222";
        t["useGradient"] = true;
        t["gradientStart"] = t["primaryBorderColor"];
        t["gradientStop"] = t["secondaryBorderColor"];
        t["dropShadow"] = "drop-shadow( 1px 2px 2px rgba(185,185,185,1))";
    }

    /// <summary>
    ///     The <c>updateColors</c> of the <c>neutral</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void UpdateNeutral(ThemeObject t)
    {
        t["secondBkg"] = Lighten(t["contrast"], 55);
        t["border2"] = t["contrast"];

        // Sequence diagram variables
        t["actorBorder"] = Lighten(t["border1"], 23);
        t["actorBkg"] = t["mainBkg"];
        t["actorTextColor"] = t["text"];
        t["actorLineColor"] = t["actorBorder"];
        t.Or("rectBkgColor", () => t["tertiaryColor"]);
        t["signalColor"] = t["text"];
        t["signalTextColor"] = t["text"];
        t["labelBoxBkgColor"] = t["actorBkg"];
        t["labelBoxBorderColor"] = t["actorBorder"];
        t["labelTextColor"] = t["text"];
        t["loopTextColor"] = t["text"];
        t["noteBorderColor"] = "#999";
        t["noteBkgColor"] = "#666";
        t["noteTextColor"] = "#fff";

        NeutralColorScale(t);
        NeutralDiagramVariables(t);
        FillTypes(t);

        // Pie diagram
        var limit = ColorLimit(t);
        for (var i = 0; i < limit; i++)
        {
            t[Key("pie", i)] = t[Key("cScale", i)];
        }

        t["pie12"] = t["pie0"];
        PieText(t, "taskTextDarkColor");

        // Venn
        for (var i = 0; i < 8; i++)
        {
            var index = i;
            t.Nullish(Key("venn", index + 1), () => t[Key("cScale", index)]);
        }

        t.Nullish("vennTitleTextColor", () => t["titleColor"]);
        t.Nullish("vennSetTextColor", () => t["textColor"]);
        Cynefin(t, ("#8B0000", "#E8F5E9", "#E3F2FD", "#FBE9E7", "#FFF8E1", "#F3E5F5"));
        Quadrant(t);
        XyChart(t, "#EEE,#6BB8E4,#8ACB88,#C7ACD6,#E8DCC2,#FFB2A8,#FFF380,#7E8D91,#FFD8B1,#FAF3E0");
        Radar(t);
        Wardley(t, "#dc3545", "background");
        Requirement(t, () => t["edgeLabelBackground"]);
        NeutralGit(t);
        GitTagsAndCommits(t);
        EventModelingLight(t);
        AttributeRows(t);
    }

    /// <summary>
    ///     The color scale, scale labels and surfaces of the <c>neutral</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void NeutralColorScale(ThemeObject t)
    {
        t.Or("cScale0", () => "#555");
        t.Or("cScale1", () => "#F4F4F4");
        t.Or("cScale2", () => "#555");
        t.Or("cScale3", () => "#BBB");
        t.Or("cScale4", () => "#777");
        t.Or("cScale5", () => "#999");
        t.Or("cScale6", () => "#DDD");
        t.Or("cScale7", () => "#FFF");
        t.Or("cScale8", () => "#DDD");
        t.Or("cScale9", () => "#BBB");
        t.Or("cScale10", () => "#999");
        t.Or("cScale11", () => "#777");

        var limit = ColorLimit(t);
        for (var i = 0; i < limit; i++)
        {
            var index = i;
            t.Or(Key("cScaleInv", index), () => Invert(t[Key("cScale", index)]));
        }

        for (var i = 0; i < limit; i++)
        {
            var index = i;
            t.Or(Key("cScalePeer", index), () => Js.Truthy(t["darkMode"])
                ? Lighten(t[Key("cScale", index)], 10)
                : Darken(t[Key("cScale", index)], 10));
        }

        t.Or("scaleLabelColor", () => Js.Truthy(t["darkMode"]) ? "black" : t["labelTextColor"]);
        t.Or("cScaleLabel0", () => t["cScale1"]);
        t.Or("cScaleLabel2", () => t["cScale1"]);
        for (var i = 0; i < limit; i++)
        {
            t.Or(Key("cScaleLabel", i), () => t["scaleLabelColor"]);
        }

        for (var i = 0; i < 5; i++)
        {
            var index = i;
            t.Or(Key("surface", index), () => Adjust(t["mainBkg"], l: -(5 + (index * 5))));
            t.Or(Key("surfacePeer", index), () => Adjust(t["mainBkg"], l: -(8 + (index * 5))));
        }
    }

    /// <summary>
    ///     The flowchart, gantt, architecture, state and class variables of the <c>neutral</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void NeutralDiagramVariables(ThemeObject t)
    {
        // Flowchart variables
        t["nodeBkg"] = t["mainBkg"];
        t["nodeBorder"] = t["border1"];
        t["clusterBkg"] = t["secondBkg"];
        t["clusterBorder"] = t["border2"];
        t["defaultLinkColor"] = t["lineColor"];
        t["titleColor"] = t["text"];

        // Gantt chart variables
        t["sectionBkgColor"] = Lighten(t["contrast"], 30);
        t["sectionBkgColor2"] = Lighten(t["contrast"], 30);
        t["taskBorderColor"] = Darken(t["contrast"], 10);
        t["taskBkgColor"] = t["contrast"];
        t["taskTextColor"] = t["taskTextLightColor"];
        t["taskTextDarkColor"] = t["text"];
        t["taskTextOutsideColor"] = t["taskTextDarkColor"];
        t["activeTaskBorderColor"] = t["taskBorderColor"];
        t["activeTaskBkgColor"] = t["mainBkg"];
        t["gridColor"] = Lighten(t["border1"], 30);
        t["doneTaskBkgColor"] = t["done"];
        t["doneTaskBorderColor"] = t["lineColor"];
        t["critBkgColor"] = t["critical"];
        t["critBorderColor"] = Darken(t["critBkgColor"], 10);
        t["todayLineColor"] = t["critBkgColor"];
        t["vertLineColor"] = t["critBkgColor"];

        // Architecture diagram variables
        t["archEdgeColor"] = t["lineColor"];
        t["archEdgeArrowColor"] = t["lineColor"];

        // State colors
        t.Or("transitionColor", () => "#000");
        t.Or("transitionLabelColor", () => t["textColor"]);
        t.Or("stateLabelColor", () => Js.Or(t["stateBkg"], () => t["primaryTextColor"]));
        t.Or("stateBkg", () => t["mainBkg"]);
        t.Or("labelBackgroundColor", () => t["stateBkg"]);
        t.Or("compositeBackground", () => Js.Or(t["background"], () => t["tertiaryColor"]));
        t.Or("altBackground", () => "#f4f4f4");
        t.Or("compositeTitleBackground", () => t["mainBkg"]);
        t.Or("stateBorder", () => "#000");
        t["innerEndBackground"] = t["primaryBorderColor"];
        t["specialStateColor"] = "#222";
        t.Or("errorBkgColor", () => t["tertiaryColor"]);
        t.Or("errorTextColor", () => t["tertiaryTextColor"]);

        // Class
        t["classText"] = t["primaryTextColor"];
    }

    /// <summary>
    ///     The git variables of the <c>neutral</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void NeutralGit(ThemeObject t)
    {
        t["git0"] = Js.Or(Darken(t["pie1"], 25), () => t["primaryColor"]);
        t["git1"] = Js.Or(t["pie2"], () => t["secondaryColor"]);
        t["git2"] = Js.Or(t["pie3"], () => t["tertiaryColor"]);
        t["git3"] = Js.Or(t["pie4"], () => Adjust(t["primaryColor"], h: -30));
        t["git4"] = Js.Or(t["pie5"], () => Adjust(t["primaryColor"], h: -60));
        t["git5"] = Js.Or(t["pie6"], () => Adjust(t["primaryColor"], h: -90));
        t["git6"] = Js.Or(t["pie7"], () => Adjust(t["primaryColor"], h: 60));
        t["git7"] = Js.Or(t["pie8"], () => Adjust(t["primaryColor"], h: 120));
        t.Or("gitInv0", () => Invert(t["git0"]));
        GitInverted(t);
        t.Or("branchLabelColor", () => t["labelTextColor"]);
        t["gitBranchLabel0"] = t["branchLabelColor"];
        t["gitBranchLabel1"] = "white";
        t["gitBranchLabel2"] = t["branchLabelColor"];
        t["gitBranchLabel3"] = "white";
        for (var i = 4; i < 8; i++)
        {
            t[Key("gitBranchLabel", i)] = t["branchLabelColor"];
        }
    }
}
