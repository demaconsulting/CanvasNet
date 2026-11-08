// Portions derived from mermaid (MIT); see third-party/README.md
using static DemaConsulting.CanvasNet.Mermaid.Configuration.ColorMath;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <content>
///     The <c>base</c> theme (Mermaid's <c>theme-base.js</c>), the theme meant for customization: almost every
///     variable is derived from a few base colors and keeps a value the user set.
/// </content>
internal static partial class ThemeResolver
{
    /// <summary>
    ///     The constructor of the <c>base</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void ConstructBase(ThemeObject t)
    {
        t["background"] = "#f4f4f4";
        t["primaryColor"] = "#fff4dd";
        t["noteBkgColor"] = "#fff5ad";
        t["noteTextColor"] = "#333";
        t["THEME_COLOR_LIMIT"] = 12.0;
        t["radius"] = 5.0;
        t["strokeWidth"] = 1.0;
        t["fontFamily"] = "\"trebuchet ms\", verdana, arial, sans-serif";
        t["fontSize"] = "16px";
        t["useGradient"] = true;
        t["dropShadow"] = "drop-shadow( 1px 2px 2px rgba(185,185,185,1))";
    }

    /// <summary>
    ///     The <c>updateColors</c> of the <c>base</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void UpdateBase(ThemeObject t)
    {
        BaseMain(t);
        BaseSequenceAndGantt(t);
        BaseStates(t);
        BaseColorScale(t);
        t.Or("classText", () => t["textColor"]);

        // User journey
        t.Or("fillType0", () => t["primaryColor"]);
        t.Or("fillType1", () => t["secondaryColor"]);
        t.Or("fillType2", () => Adjust(t["primaryColor"], h: 64));
        t.Or("fillType3", () => Adjust(t["secondaryColor"], h: 64));
        t.Or("fillType4", () => Adjust(t["primaryColor"], h: -64));
        t.Or("fillType5", () => Adjust(t["secondaryColor"], h: -64));
        t.Or("fillType6", () => Adjust(t["primaryColor"], h: 128));
        t.Or("fillType7", () => Adjust(t["secondaryColor"], h: 128));
        BasePieAndVenn(t);
        Cynefin(t, ("#8B0000", "#E8F5E9", "#E3F2FD", "#FBE9E7", "#FFF8E1", "#F3E5F5"));
        Radar(t);
        Wardley(t, "#dc3545", "background");

        // Architecture
        t.Or("archEdgeColor", () => "#777");
        t.Or("archEdgeArrowColor", () => "#777");
        t.Or("archEdgeWidth", () => "3");
        t.Or("archGroupBorderColor", () => "#000");
        t.Or("archGroupBorderWidth", () => "2px");
        Quadrant(t);
        XyChart(t, "#FFF4DD,#FFD8B1,#FFA07A,#ECEFF1,#D6DBDF,#C3E0A8,#FFB6A4,#FFD74D,#738FA7,#FFFFF0");
        Requirement(t, () => Js.Truthy(t["darkMode"]) ? Darken(t["secondaryColor"], 30) : t["secondaryColor"]);

        // Git
        GitColors(t);
        t.Or("gitInv0", () => Invert(t["git0"]));
        GitInverted(t);
        t.Or("branchLabelColor", () => Js.Truthy(t["darkMode"]) ? "black" : t["labelTextColor"]);
        for (var i = 0; i < 8; i++)
        {
            t.Or(Key("gitBranchLabel", i), () => t["branchLabelColor"]);
        }

        GitTagsAndCommits(t);
        EventModelingLight(t);
        AttributeRows(t);
        t["gradientStart"] = t["primaryBorderColor"];
        t["gradientStop"] = t["secondaryBorderColor"];
    }

    /// <summary>
    ///     The main and flowchart variables of the <c>base</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void BaseMain(ThemeObject t)
    {
        // Main
        t.Or("primaryTextColor", () => Js.Truthy(t["darkMode"]) ? "#eee" : "#333");
        t.Or("secondaryColor", () => Adjust(t["primaryColor"], h: -120));
        t.Or("tertiaryColor", () => Adjust(t["primaryColor"], h: 180, l: 5));
        t.Or("primaryBorderColor", () => MkBorder(t["primaryColor"], t["darkMode"]));
        t.Or("secondaryBorderColor", () => MkBorder(t["secondaryColor"], t["darkMode"]));
        t.Or("tertiaryBorderColor", () => MkBorder(t["tertiaryColor"], t["darkMode"]));
        t.Or("noteBorderColor", () => MkBorder(t["noteBkgColor"], t["darkMode"]));
        t.Or("noteBkgColor", () => "#fff5ad");
        t.Or("noteTextColor", () => "#333");
        t.Or("secondaryTextColor", () => Invert(t["secondaryColor"]));
        t.Or("tertiaryTextColor", () => Invert(t["tertiaryColor"]));
        t.Or("lineColor", () => Invert(t["background"]));
        t.Or("arrowheadColor", () => Invert(t["background"]));
        t.Or("textColor", () => t["primaryTextColor"]);
        t.Or("border2", () => t["tertiaryBorderColor"]);

        // Flowchart variables
        t.Or("nodeBkg", () => t["primaryColor"]);
        t.Or("mainBkg", () => t["primaryColor"]);
        t.Or("nodeBorder", () => t["primaryBorderColor"]);
        t.Or("clusterBkg", () => t["tertiaryColor"]);
        t.Or("clusterBorder", () => t["tertiaryBorderColor"]);
        t.Or("defaultLinkColor", () => t["lineColor"]);
        t.Or("titleColor", () => t["tertiaryTextColor"]);
        t.Or("edgeLabelBackground", () => Js.Truthy(t["darkMode"]) ? Darken(t["secondaryColor"], 30) : t["secondaryColor"]);
        t.Or("nodeTextColor", () => t["primaryTextColor"]);
    }

    /// <summary>
    ///     The sequence, gantt, person and entity-relationship variables of the <c>base</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void BaseSequenceAndGantt(ThemeObject t)
    {
        // Sequence diagram variables
        t.Or("actorBorder", () => t["primaryBorderColor"]);
        t.Or("actorBkg", () => t["mainBkg"]);
        t.Or("actorTextColor", () => t["primaryTextColor"]);
        t.Or("actorLineColor", () => t["actorBorder"]);
        t.Or("labelBoxBkgColor", () => t["actorBkg"]);
        t.Or("signalColor", () => t["textColor"]);
        t.Or("signalTextColor", () => t["textColor"]);
        t.Or("labelBoxBorderColor", () => t["actorBorder"]);
        t.Or("labelTextColor", () => t["actorTextColor"]);
        t.Or("loopTextColor", () => t["actorTextColor"]);
        t.Or("activationBorderColor", () => Darken(t["secondaryColor"], 10));
        t.Or("activationBkgColor", () => t["secondaryColor"]);
        t.Or("sequenceNumberColor", () => Invert(t["lineColor"]));
        t.Or("rectBkgColor", () => t["tertiaryColor"]);

        // Gantt chart variables
        t.Or("sectionBkgColor", () => t["tertiaryColor"]);
        t.Or("altSectionBkgColor", () => "white");
        t.Or("sectionBkgColor", () => t["secondaryColor"]);
        t.Or("sectionBkgColor2", () => t["primaryColor"]);
        t.Or("excludeBkgColor", () => "#eeeeee");
        t.Or("taskBorderColor", () => t["primaryBorderColor"]);
        t.Or("taskBkgColor", () => t["primaryColor"]);
        t.Or("activeTaskBorderColor", () => t["primaryColor"]);
        t.Or("activeTaskBkgColor", () => Lighten(t["primaryColor"], 23));
        t.Or("gridColor", () => "lightgrey");
        t.Or("doneTaskBkgColor", () => "lightgrey");
        t.Or("doneTaskBorderColor", () => "grey");
        t.Or("critBorderColor", () => "#ff8888");
        t.Or("critBkgColor", () => "red");
        t.Or("todayLineColor", () => "red");
        t.Or("vertLineColor", () => "navy");
        t.Or("taskTextColor", () => t["textColor"]);
        t.Or("taskTextOutsideColor", () => t["textColor"]);
        t.Or("taskTextLightColor", () => t["textColor"]);
        t.Or("taskTextColor", () => t["primaryTextColor"]);
        t.Or("taskTextDarkColor", () => t["textColor"]);
        t.Or("taskTextClickableColor", () => "#003163");
        t.Or("noteFontWeight", () => "normal");
        t.Or("fontWeight", () => "normal");

        // Person variables
        t.Or("personBorder", () => t["primaryBorderColor"]);
        t.Or("personBkg", () => t["mainBkg"]);

        // Entity relationship diagram
        if (Js.Truthy(t["darkMode"]))
        {
            t.Or("rowOdd", () => Js.Or(Darken(t["mainBkg"], 5), () => "#ffffff"));
            t.Or("rowEven", () => Darken(t["mainBkg"], 10));
        }
        else
        {
            t.Or("rowOdd", () => Js.Or(Lighten(t["mainBkg"], 75), () => "#ffffff"));
            t.Or("rowEven", () => Lighten(t["mainBkg"], 5));
        }
    }

    /// <summary>
    ///     The state variables of the <c>base</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void BaseStates(ThemeObject t)
    {
        t.Or("transitionColor", () => t["lineColor"]);
        t.Or("transitionLabelColor", () => t["textColor"]);
        t.Or("stateLabelColor", () => Js.Or(t["stateBkg"], () => t["primaryTextColor"]));
        t.Or("stateBkg", () => t["mainBkg"]);
        t.Or("labelBackgroundColor", () => t["stateBkg"]);
        t.Or("compositeBackground", () => Js.Or(t["background"], () => t["tertiaryColor"]));
        t.Or("altBackground", () => t["tertiaryColor"]);
        t.Or("compositeTitleBackground", () => t["mainBkg"]);
        t.Or("compositeBorder", () => t["nodeBorder"]);
        t["innerEndBackground"] = t["nodeBorder"];
        t.Or("errorBkgColor", () => t["tertiaryColor"]);
        t.Or("errorTextColor", () => t["tertiaryTextColor"]);
        t.Or("transitionColor", () => t["lineColor"]);
        t["specialStateColor"] = t["lineColor"];
    }

    /// <summary>
    ///     The color scale, scale labels and surfaces of the <c>base</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void BaseColorScale(ThemeObject t)
    {
        ColorScaleDefaults(t, hue8Lightness: 150);
        var limit = ColorLimit(t);
        var dark = Js.Truthy(t["darkMode"]);
        for (var i = 0; i < limit; i++)
        {
            t[Key("cScale", i)] = Darken(t[Key("cScale", i)], dark ? 75 : 25);
        }

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

        t.Or("scaleLabelColor", () => t["labelTextColor"]);
        for (var i = 0; i < limit; i++)
        {
            t.Or(Key("cScaleLabel", i), () => t["scaleLabelColor"]);
        }

        var multiplier = dark ? -4 : -1;
        for (var i = 0; i < 5; i++)
        {
            var index = i;
            t.Or(Key("surface", index), () => Adjust(t["mainBkg"], h: 180, s: -15, l: multiplier * (5 + (index * 3))));
            t.Or(Key("surfacePeer", index), () => Adjust(t["mainBkg"], h: 180, s: -15, l: multiplier * (8 + (index * 3))));
        }
    }

    /// <summary>
    ///     The pie and venn variables of the <c>base</c> theme.
    /// </summary>
    /// <param name="t">The theme object.</param>
    private static void BasePieAndVenn(ThemeObject t)
    {
        t.Or("pie1", () => t["primaryColor"]);
        t.Or("pie2", () => t["secondaryColor"]);
        t.Or("pie3", () => t["tertiaryColor"]);
        t.Or("pie4", () => Adjust(t["primaryColor"], l: -10));
        t.Or("pie5", () => Adjust(t["secondaryColor"], l: -10));
        t.Or("pie6", () => Adjust(t["tertiaryColor"], l: -10));
        t.Or("pie7", () => Adjust(t["primaryColor"], h: 60, l: -10));
        t.Or("pie8", () => Adjust(t["primaryColor"], h: -60, l: -10));
        t.Or("pie9", () => Adjust(t["primaryColor"], h: 120, l: 0));
        t.Or("pie10", () => Adjust(t["primaryColor"], h: 60, l: -20));
        t.Or("pie11", () => Adjust(t["primaryColor"], h: -60, l: -20));
        t.Or("pie12", () => Adjust(t["primaryColor"], h: 120, l: -10));
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
