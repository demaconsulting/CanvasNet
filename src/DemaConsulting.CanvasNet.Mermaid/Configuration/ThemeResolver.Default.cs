// Portions derived from mermaid (MIT); see third-party/README.md
using static DemaConsulting.CanvasNet.Mermaid.Configuration.ColorMath;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <content>
///     The <c>default</c> theme (Mermaid's <c>theme-default.js</c>).
/// </content>
internal static partial class ThemeResolver
{
    /// <summary>
    ///     The constructor of the <c>default</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void ConstructDefault(ThemeObject t)
    {
        // Base variables
        t["background"] = "#f4f4f4";
        t["primaryColor"] = "#ECECFF";
        t["secondaryColor"] = Adjust(t["primaryColor"], h: 120);
        t["secondaryColor"] = "#ffffde";
        t["tertiaryColor"] = Adjust(t["primaryColor"], h: -160);
        t["primaryBorderColor"] = MkBorder(t["primaryColor"], t["darkMode"]);
        t["secondaryBorderColor"] = MkBorder(t["secondaryColor"], t["darkMode"]);
        t["tertiaryBorderColor"] = MkBorder(t["tertiaryColor"], t["darkMode"]);
        t["primaryTextColor"] = Invert(t["primaryColor"]);
        t["secondaryTextColor"] = Invert(t["secondaryColor"]);
        t["tertiaryTextColor"] = Invert(t["tertiaryColor"]);
        t["lineColor"] = Invert(t["background"]);
        t["textColor"] = Invert(t["background"]);
        t["background"] = "white";
        t["mainBkg"] = "#ECECFF";
        t["secondBkg"] = "#ffffde";
        t["lineColor"] = "#333333";
        t["border1"] = "#9370DB";
        t["primaryBorderColor"] = MkBorder(t["primaryColor"], t["darkMode"]);
        t["border2"] = "#aaaa33";
        t["arrowheadColor"] = "#333333";
        t["fontFamily"] = "\"trebuchet ms\", verdana, arial, sans-serif";
        t["fontSize"] = "16px";
        t["labelBackground"] = "rgba(232,232,232, 0.8)";
        t["textColor"] = "#333";
        t["THEME_COLOR_LIMIT"] = 12.0;
        t["radius"] = 5.0;
        t["strokeWidth"] = 1.0;

        // Flowchart variables
        foreach (var key in (ReadOnlySpan<string>)["nodeBkg", "nodeBorder", "clusterBkg", "clusterBorder", "defaultLinkColor", "titleColor", "edgeLabelBackground"])
        {
            t[key] = "calculated";
        }

        // Sequence diagram variables
        t["actorBorder"] = "calculated";
        t["actorBkg"] = "calculated";
        t["actorTextColor"] = "black";
        t["actorLineColor"] = "calculated";
        t["signalColor"] = "calculated";
        t["signalTextColor"] = "calculated";
        t["labelBoxBkgColor"] = "calculated";
        t["labelBoxBorderColor"] = "calculated";
        t["labelTextColor"] = "calculated";
        t["loopTextColor"] = "calculated";
        t["noteBorderColor"] = "calculated";
        t["noteBkgColor"] = "#fff5ad";
        t["noteTextColor"] = "calculated";
        t["activationBorderColor"] = "#666";
        t["activationBkgColor"] = "#f4f4f4";
        t["sequenceNumberColor"] = "white";
        t["clusterBkg"] = "#FBFBFF";

        // Gantt chart variables
        t["sectionBkgColor"] = "calculated";
        t["altSectionBkgColor"] = "calculated";
        t["sectionBkgColor2"] = "calculated";
        t["excludeBkgColor"] = "#eeeeee";
        t["taskBorderColor"] = "calculated";
        t["taskBkgColor"] = "calculated";
        t["taskTextLightColor"] = "calculated";
        t["taskTextColor"] = t["taskTextLightColor"];
        t["taskTextDarkColor"] = "calculated";
        t["taskTextOutsideColor"] = t["taskTextDarkColor"];
        foreach (var key in (ReadOnlySpan<string>)[
                     "taskTextClickableColor", "activeTaskBorderColor", "activeTaskBkgColor", "gridColor", "doneTaskBkgColor",
                     "doneTaskBorderColor", "critBorderColor", "critBkgColor", "todayLineColor", "vertLineColor"])
        {
            t[key] = "calculated";
        }

        t["sectionBkgColor"] = Rgba(102, 102, 255, 0.49);
        t["altSectionBkgColor"] = "white";
        t["sectionBkgColor2"] = "#fff400";
        t["taskBorderColor"] = "#534fbc";
        t["taskBkgColor"] = "#8a90dd";
        t["taskTextLightColor"] = "white";
        t["taskTextColor"] = "calculated";
        t["taskTextDarkColor"] = "black";
        t["taskTextOutsideColor"] = "calculated";
        t["taskTextClickableColor"] = "#003163";
        t["activeTaskBorderColor"] = "#534fbc";
        t["activeTaskBkgColor"] = "#bfc7ff";
        t["gridColor"] = "lightgrey";
        t["doneTaskBkgColor"] = "lightgrey";
        t["doneTaskBorderColor"] = "grey";
        t["critBorderColor"] = "#ff8888";
        t["critBkgColor"] = "red";
        t["todayLineColor"] = "red";
        t["vertLineColor"] = "navy";
        t.Or("noteFontWeight", () => "normal");
        t.Or("fontWeight", () => "normal");

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
        t["rowOdd"] = "calculated";
        t["rowEven"] = "calculated";

        // State colors
        t["labelColor"] = "black";
        t["errorBkgColor"] = "#552222";
        t["errorTextColor"] = "#552222";

        // Neo-specific
        t["useGradient"] = false;
        t["gradientStart"] = t["primaryBorderColor"];
        t["gradientStop"] = t["secondaryBorderColor"];
        t["dropShadow"] = "drop-shadow(1px 2px 2px rgba(185, 185, 185, 1))";
        UpdateDefault(t);
    }

    /// <summary>
    ///     The <c>updateColors</c> of the <c>default</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void UpdateDefault(ThemeObject t)
    {
        DefaultColorScale(t);
        DefaultDiagramVariables(t);
        FillTypes(t);
        DefaultPieAndVenn(t);
        Cynefin(t, ("#8B0000", "#E8F5E9", "#E3F2FD", "#FBE9E7", "#FFF8E1", "#F3E5F5"));
        Quadrant(t);
        Radar(t);
        Wardley(t, "#dc3545", "background");
        XyChart(t, "#ECECFF,#8493A6,#FFC3A0,#DCDDE1,#B8E994,#D1A36F,#C3CDE6,#FFB6C1,#496078,#F8F3E3");
        Requirement(t, () => t["labelBackground"]);

        // Git
        GitColors(t);
        t.Or("gitInv0", () => Darken(Invert(t["git0"]), 25));
        GitInverted(t);
        GitBranchLabels(t);
        GitTagsAndCommits(t);
        EventModelingLight(t);
        AttributeRows(t);
    }

    /// <summary>
    ///     The color scale, surfaces and scale labels of the <c>default</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void DefaultColorScale(ThemeObject t)
    {
        ColorScaleDefaults(t);
        t.Or("cScalePeer1", () => Darken(t["secondaryColor"], 45));
        t.Or("cScalePeer2", () => Darken(t["tertiaryColor"], 40));
        var limit = ColorLimit(t);
        for (var i = 0; i < limit; i++)
        {
            var index = i;
            t[Key("cScale", index)] = Darken(t[Key("cScale", index)], 10);
            t.Or(Key("cScalePeer", index), () => Darken(t[Key("cScale", index)], 25));
        }

        for (var i = 0; i < limit; i++)
        {
            var index = i;
            t.Or(Key("cScaleInv", index), () => Adjust(t[Key("cScale", index)], h: 180));
        }

        for (var i = 0; i < 5; i++)
        {
            var index = i;
            t.Or(Key("surface", index), () => Adjust(t["mainBkg"], h: 30, l: -(5 + (index * 5))));
            t.Or(Key("surfacePeer", index), () => Adjust(t["mainBkg"], h: 30, l: -(7 + (index * 5))));
        }

        t["scaleLabelColor"] = !Equals(t["scaleLabelColor"], "calculated") && Js.Truthy(t["scaleLabelColor"])
            ? t["scaleLabelColor"]
            : t["labelTextColor"];
        if (!Equals(t["labelTextColor"], "calculated"))
        {
            t.Or("cScaleLabel0", () => Invert(t["labelTextColor"]));
            t.Or("cScaleLabel3", () => Invert(t["labelTextColor"]));
            for (var i = 0; i < limit; i++)
            {
                t.Or(Key("cScaleLabel", i), () => t["labelTextColor"]);
            }
        }
    }

    /// <summary>
    ///     The flowchart, sequence, gantt, architecture, entity-relationship, state and class variables of the
    ///     <c>default</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void DefaultDiagramVariables(ThemeObject t)
    {
        // Flowchart variables
        t["nodeBkg"] = t["mainBkg"];
        t["nodeBorder"] = t["border1"];
        t["clusterBkg"] = t["secondBkg"];
        t["clusterBorder"] = t["border2"];
        t["defaultLinkColor"] = t["lineColor"];
        t["titleColor"] = t["textColor"];
        t["edgeLabelBackground"] = t["labelBackground"];

        // Sequence diagram variables
        t["actorBorder"] = t["border1"];
        t["actorBkg"] = t["mainBkg"];
        t["labelBoxBkgColor"] = t["actorBkg"];
        t["signalColor"] = t["textColor"];
        t["signalTextColor"] = t["textColor"];
        t["labelBoxBorderColor"] = t["actorBorder"];
        t["labelTextColor"] = t["actorTextColor"];
        t["loopTextColor"] = t["actorTextColor"];
        t["noteBorderColor"] = t["border2"];
        t["noteTextColor"] = t["actorTextColor"];
        t["actorLineColor"] = t["actorBorder"];
        t.Or("rectBkgColor", () => t["tertiaryColor"]);

        // Gantt chart variables
        t["taskTextColor"] = t["taskTextLightColor"];
        t["taskTextOutsideColor"] = t["taskTextDarkColor"];

        // Architecture diagram variables
        t["archEdgeColor"] = t["lineColor"];
        t["archEdgeArrowColor"] = t["lineColor"];

        // Entity relationship variables
        t.Or("rowOdd", () => Js.Or(Lighten(t["primaryColor"], 75), () => "#ffffff"));
        t.Or("rowEven", () => Lighten(t["primaryColor"], 1));

        // State colors
        t.Or("transitionColor", () => t["lineColor"]);
        t.Or("transitionLabelColor", () => t["textColor"]);
        t.Or("stateLabelColor", () => Js.Or(t["stateBkg"], () => t["primaryTextColor"]));
        t.Or("stateBkg", () => t["mainBkg"]);
        t.Or("labelBackgroundColor", () => t["stateBkg"]);
        t.Or("compositeBackground", () => Js.Or(t["background"], () => t["tertiaryColor"]));
        t.Or("altBackground", () => "#f0f0f0");
        t.Or("compositeTitleBackground", () => t["mainBkg"]);
        t.Or("compositeBorder", () => t["nodeBorder"]);
        t["innerEndBackground"] = t["nodeBorder"];
        t["specialStateColor"] = t["lineColor"];
        t.Or("errorBkgColor", () => t["tertiaryColor"]);
        t.Or("errorTextColor", () => t["tertiaryTextColor"]);
        t.Or("transitionColor", () => t["lineColor"]);

        // Class
        t["classText"] = t["primaryTextColor"];
    }

    /// <summary>
    ///     The pie and venn variables of the <c>default</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void DefaultPieAndVenn(ThemeObject t)
    {
        t.Or("pie1", () => t["primaryColor"]);
        t.Or("pie2", () => t["secondaryColor"]);
        t.Or("pie3", () => Adjust(t["tertiaryColor"], l: -40));
        t.Or("pie4", () => Adjust(t["primaryColor"], l: -10));
        t.Or("pie5", () => Adjust(t["secondaryColor"], l: -30));
        t.Or("pie6", () => Adjust(t["tertiaryColor"], l: -20));
        t.Or("pie7", () => Adjust(t["primaryColor"], h: 60, l: -20));
        t.Or("pie8", () => Adjust(t["primaryColor"], h: -60, l: -40));
        t.Or("pie9", () => Adjust(t["primaryColor"], h: 120, l: -40));
        t.Or("pie10", () => Adjust(t["primaryColor"], h: 60, l: -40));
        t.Or("pie11", () => Adjust(t["primaryColor"], h: -90, l: -40));
        t.Or("pie12", () => Adjust(t["primaryColor"], h: 120, l: -30));
        PieText(t, "taskTextDarkColor");

        t.Nullish("venn1", () => Adjust(t["primaryColor"], l: -30));
        t.Nullish("venn2", () => Adjust(t["secondaryColor"], l: -30));
        t.Nullish("venn3", () => Adjust(t["tertiaryColor"], l: -40));
        t.Nullish("venn4", () => Adjust(t["primaryColor"], h: 60, l: -30));
        t.Nullish("venn5", () => Adjust(t["primaryColor"], h: -60, l: -30));
        t.Nullish("venn6", () => Adjust(t["secondaryColor"], h: 60, l: -30));
        t.Nullish("venn7", () => Adjust(t["primaryColor"], h: 120, l: -30));
        t.Nullish("venn8", () => Adjust(t["secondaryColor"], h: 120, l: -30));
        t.Nullish("vennTitleTextColor", () => t["titleColor"]);
        t.Nullish("vennSetTextColor", () => t["textColor"]);
    }
}
