// Portions derived from mermaid (MIT); see third-party/README.md
using static DemaConsulting.CanvasNet.Mermaid.Configuration.ColorMath;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <content>
///     The <c>forest</c> theme (Mermaid's <c>theme-forest.js</c>).
/// </content>
internal static partial class ThemeResolver
{
    /// <summary>
    ///     The constructor of the <c>forest</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void ConstructForest(ThemeObject t)
    {
        // Base values
        t["background"] = "#f4f4f4";
        t["primaryColor"] = "#cde498";
        t["secondaryColor"] = "#cdffb2";
        t["background"] = "white";
        t["mainBkg"] = "#cde498";
        t["secondBkg"] = "#cdffb2";
        t["lineColor"] = "green";
        t["border1"] = "#13540c";
        t["border2"] = "#6eaa49";
        t["arrowheadColor"] = "green";
        t["fontFamily"] = "\"trebuchet ms\", verdana, arial, sans-serif";
        t["fontSize"] = "16px";
        t["tertiaryColor"] = Lighten("#cde498", 10);
        t["primaryBorderColor"] = MkBorder(t["primaryColor"], t["darkMode"]);
        t["secondaryBorderColor"] = MkBorder(t["secondaryColor"], t["darkMode"]);
        t["tertiaryBorderColor"] = MkBorder(t["tertiaryColor"], t["darkMode"]);
        t["primaryTextColor"] = Invert(t["primaryColor"]);
        t["secondaryTextColor"] = Invert(t["secondaryColor"]);
        t["tertiaryTextColor"] = Invert(t["primaryColor"]);
        t["lineColor"] = Invert(t["background"]);
        t["textColor"] = Invert(t["background"]);
        t["THEME_COLOR_LIMIT"] = 12.0;
        t["radius"] = 5.0;
        t["strokeWidth"] = 1.0;

        // Flowchart variables
        t["nodeBkg"] = "calculated";
        t["nodeBorder"] = "calculated";
        t["clusterBkg"] = "calculated";
        t["clusterBorder"] = "calculated";
        t["defaultLinkColor"] = "calculated";
        t["titleColor"] = "#333";
        t["edgeLabelBackground"] = "#e8e8e8";

        // Sequence diagram variables
        t["actorBorder"] = "calculated";
        t["actorBkg"] = "calculated";
        t["actorTextColor"] = "black";
        t["actorLineColor"] = "calculated";
        t["signalColor"] = "#333";
        t["signalTextColor"] = "#333";
        t["labelBoxBkgColor"] = "calculated";
        t["labelBoxBorderColor"] = "#326932";
        t["labelTextColor"] = "calculated";
        t["loopTextColor"] = "calculated";
        t["noteBorderColor"] = "calculated";
        t["noteBkgColor"] = "#fff5ad";
        t["noteTextColor"] = "calculated";
        t["activationBorderColor"] = "#666";
        t["activationBkgColor"] = "#f4f4f4";
        t["sequenceNumberColor"] = "white";

        // Gantt chart variables
        t["sectionBkgColor"] = "#6eaa49";
        t["altSectionBkgColor"] = "white";
        t["sectionBkgColor2"] = "#6eaa49";
        t["excludeBkgColor"] = "#eeeeee";
        t["taskBorderColor"] = "calculated";
        t["taskBkgColor"] = "#487e3a";
        t["taskTextLightColor"] = "white";
        t["taskTextColor"] = "calculated";
        t["taskTextDarkColor"] = "black";
        t["taskTextOutsideColor"] = "calculated";
        t["taskTextClickableColor"] = "#003163";
        t["activeTaskBorderColor"] = "calculated";
        t["activeTaskBkgColor"] = "calculated";
        t["gridColor"] = "lightgrey";
        t["doneTaskBkgColor"] = "lightgrey";
        t["doneTaskBorderColor"] = "grey";
        t["critBorderColor"] = "#ff8888";
        t["critBkgColor"] = "red";
        t["todayLineColor"] = "red";
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
        t["noteFontWeight"] = "normal";
        t["fontWeight"] = "normal";

        // State colors
        t["labelColor"] = "black";
        t["errorBkgColor"] = "#552222";
        t["errorTextColor"] = "#552222";
        t["useGradient"] = true;
        t["gradientStart"] = t["primaryBorderColor"];
        t["gradientStop"] = t["secondaryBorderColor"];
        t["dropShadow"] = "drop-shadow( 1px 2px 2px rgba(185,185,185,0.5))";
    }

    /// <summary>
    ///     The <c>updateColors</c> of the <c>forest</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void UpdateForest(ThemeObject t)
    {
        // Sequence diagram variables
        t["actorBorder"] = Darken(t["mainBkg"], 20);
        t["actorBkg"] = t["mainBkg"];
        t["labelBoxBkgColor"] = t["actorBkg"];
        t["labelTextColor"] = t["actorTextColor"];
        t["loopTextColor"] = t["actorTextColor"];
        t["noteBorderColor"] = t["border2"];
        t["noteTextColor"] = t["actorTextColor"];
        t["actorLineColor"] = t["actorBorder"];
        t.Or("rectBkgColor", () => t["tertiaryColor"]);

        ForestColorScale(t);
        ForestDiagramVariables(t);
        FillTypes(t);
        ForestPieAndVenn(t);
        Cynefin(t, ("#8B4513", "#C8E6C9", "#DCEDC8", "#FFE0B2", "#FFF9C4", "#D7CCC8"));
        Quadrant(t);

        // Packet
        var packet = new ThemeObject();
        packet["startByteColor"] = t["primaryTextColor"];
        packet["endByteColor"] = t["primaryTextColor"];
        packet["labelColor"] = t["primaryTextColor"];
        packet["titleColor"] = t["primaryTextColor"];
        packet["blockStrokeColor"] = t["primaryTextColor"];
        packet["blockFillColor"] = t["mainBkg"];
        t["packet"] = packet;
        Radar(t);
        Wardley(t, "#dc3545", "background");
        XyChart(t, "#CDE498,#FF6B6B,#A0D2DB,#D7BDE2,#F0F0F0,#FFC3A0,#7FD8BE,#FF9A8B,#FAF3E0,#FFF176");
        Requirement(t, () => t["edgeLabelBackground"]);

        // Git
        GitColors(t);
        t.Or("gitInv0", () => Invert(t["git0"]));
        GitInverted(t);
        GitBranchLabels(t);
        GitTagsAndCommits(t);
        EventModelingLight(t);
        AttributeRows(t);
    }

    /// <summary>
    ///     The color scale, scale labels and surfaces of the <c>forest</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void ForestColorScale(ThemeObject t)
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

        t["scaleLabelColor"] = !Equals(t["scaleLabelColor"], "calculated") && Js.Truthy(t["scaleLabelColor"])
            ? t["scaleLabelColor"]
            : t["labelTextColor"];
        for (var i = 0; i < limit; i++)
        {
            t.Or(Key("cScaleLabel", i), () => t["scaleLabelColor"]);
        }

        for (var i = 0; i < 5; i++)
        {
            var index = i;
            t.Or(Key("surface", index), () => Adjust(t["mainBkg"], h: 30, s: -30, l: -(5 + (index * 5))));
            t.Or(Key("surfacePeer", index), () => Adjust(t["mainBkg"], h: 30, s: -30, l: -(8 + (index * 5))));
        }
    }

    /// <summary>
    ///     The flowchart, gantt, architecture, entity-relationship, state and class variables of the <c>forest</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void ForestDiagramVariables(ThemeObject t)
    {
        // Flowchart variables
        t["nodeBkg"] = t["mainBkg"];
        t["nodeBorder"] = t["border1"];
        t["clusterBkg"] = t["secondBkg"];
        t["clusterBorder"] = t["border2"];
        t["defaultLinkColor"] = t["lineColor"];

        // Gantt chart variables
        t["taskBorderColor"] = t["border1"];
        t["taskTextColor"] = t["taskTextLightColor"];
        t["taskTextOutsideColor"] = t["taskTextDarkColor"];
        t["activeTaskBorderColor"] = t["taskBorderColor"];
        t["activeTaskBkgColor"] = t["mainBkg"];

        // Architecture diagram variables
        t["archEdgeColor"] = t["lineColor"];
        t["archEdgeArrowColor"] = t["lineColor"];

        // Entity relationship diagram
        t.Or("rowOdd", () => Js.Or(Lighten(t["mainBkg"], 75), () => "#ffffff"));
        t.Or("rowEven", () => Lighten(t["mainBkg"], 20));

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
        t["innerEndBackground"] = t["primaryBorderColor"];
        t["specialStateColor"] = t["lineColor"];
        t.Or("errorBkgColor", () => t["tertiaryColor"]);
        t.Or("errorTextColor", () => t["tertiaryTextColor"]);
        t.Or("transitionColor", () => t["lineColor"]);

        // Class
        t["classText"] = t["primaryTextColor"];
    }

    /// <summary>
    ///     The pie and venn variables of the <c>forest</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void ForestPieAndVenn(ThemeObject t)
    {
        t.Or("pie1", () => t["primaryColor"]);
        t.Or("pie2", () => t["secondaryColor"]);
        t.Or("pie3", () => t["tertiaryColor"]);
        t.Or("pie4", () => Adjust(t["primaryColor"], l: -30));
        t.Or("pie5", () => Adjust(t["secondaryColor"], l: -30));
        t.Or("pie6", () => Adjust(t["tertiaryColor"], h: 40, l: -40));
        t.Or("pie7", () => Adjust(t["primaryColor"], h: 60, l: -10));
        t.Or("pie8", () => Adjust(t["primaryColor"], h: -60, l: -10));
        t.Or("pie9", () => Adjust(t["primaryColor"], h: 120, l: 0));
        t.Or("pie10", () => Adjust(t["primaryColor"], h: 60, l: -50));
        t.Or("pie11", () => Adjust(t["primaryColor"], h: -60, l: -50));
        t.Or("pie12", () => Adjust(t["primaryColor"], h: 120, l: -50));
        PieText(t, "taskTextDarkColor");

        t.Nullish("venn1", () => Adjust(t["primaryColor"], l: -30));
        t.Nullish("venn2", () => Adjust(t["secondaryColor"], l: -30));
        t.Nullish("venn3", () => Adjust(t["tertiaryColor"], l: -30));
        t.Nullish("venn4", () => Adjust(t["primaryColor"], h: 60, l: -30));
        t.Nullish("venn5", () => Adjust(t["primaryColor"], h: -60, l: -30));
        t.Nullish("venn6", () => Adjust(t["secondaryColor"], h: 60, l: -30));
        t.Nullish("venn7", () => Adjust(t["primaryColor"], h: 120, l: -30));
        t.Nullish("venn8", () => Adjust(t["secondaryColor"], h: 120, l: -30));
        t.Nullish("vennTitleTextColor", () => t["titleColor"]);
        t.Nullish("vennSetTextColor", () => t["textColor"]);
    }
}
