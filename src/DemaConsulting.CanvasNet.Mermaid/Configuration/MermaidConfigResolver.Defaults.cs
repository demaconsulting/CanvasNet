// Portions derived from mermaid (MIT); see third-party/README.md
using System.Collections.Frozen;
using System.Text.Json.Nodes;

namespace DemaConsulting.CanvasNet.Mermaid.Configuration;

/// <content>
///     Mermaid's default configuration (the defaults of <c>config.schema.yaml</c> and <c>defaultConfig.ts</c>) for the
///     global keys, the flowchart, the sequence, class, state and entity relationship diagrams, the pie and gantt
///     charts, and the set of configuration keys that Mermaid's <c>sanitizeDirective</c> accepts.
/// </content>
internal static partial class MermaidConfigResolver
{
    /// <summary>The default font family of the configuration (with Mermaid's trailing semicolon).</summary>
    internal const string DefaultFontFamily = "\"trebuchet ms\", verdana, arial, sans-serif;";

    /// <summary>
    ///     Gets every key name of Mermaid's default configuration, at any depth (<c>configKeys</c> of
    ///     <c>defaultConfig.ts</c>: the keys of every diagram section and of the default theme variables, without the keys
    ///     whose default is an array). A key of a frontmatter or directive configuration that is not in the set is deleted
    ///     by Mermaid, so this version reports it as unknown.
    /// </summary>
    internal static FrozenSet<string> ConfigKeys { get; } = new[]
    {
        "$ref", "activationBkgColor", "activationBorderColor", "activationWidth", "activeTaskBkgColor",
        "activeTaskBorderColor", "actorBkg", "actorBorder", "actorFont", "actorFontFamily", "actorFontSize",
        "actorFontWeight", "actorLineColor", "actorMargin", "actorTextColor", "altBackground", "altSectionBkgColor",
        "annotationFill", "annotationStroke", "annotationTextColor", "archEdgeArrowColor", "archEdgeColor",
        "archEdgeWidth", "archGroupBorderColor", "archGroupBorderWidth", "architecture", "arrowColor",
        "arrowheadColor", "arrowMarkerAbsolute", "arrowWidth", "attributeBackgroundColorEven",
        "attributeBackgroundColorOdd", "automaticLaneOrdering", "axisColor", "axisFormat", "axisLabelFactor",
        "axisLabelFontSize", "axisLineWidth", "axisScaleFactor", "axisStrokeWidth", "axisTextColor", "background",
        "backgroundColor", "barGap", "barHeight", "bitsPerRow", "bitWidth", "block", "border1", "border2",
        "borderWidth", "bottom", "bottomMarginAdj", "boundaryAmplitude", "boundaryColor", "boundaryFont",
        "boundaryFontFamily", "boundaryFontSize", "boundaryFontWeight", "boundaryWidth", "boxMargin", "boxTextMargin",
        "c4", "c4BoundaryInRow", "c4ShapeInRow", "c4ShapeMargin", "c4ShapePadding", "chaoticBg", "chartHeight",
        "chartOrientation", "chartWidth", "class", "classText", "clearBg", "cliffColor", "cliffWidth", "clusterBkg",
        "clusterBorder", "commentFill", "commentStroke", "commentTextColor", "commitLabelBackground",
        "commitLabelColor", "commitLabelFontSize", "complexBg", "complicatedBg", "component_bg_color",
        "component_border_color", "component_db_bg_color", "component_db_border_color", "component_dbFont",
        "component_dbFontFamily", "component_dbFontSize", "component_dbFontWeight", "component_queue_bg_color",
        "component_queue_border_color", "component_queueFont", "component_queueFontFamily", "component_queueFontSize",
        "component_queueFontWeight", "componentFill", "componentFont", "componentFontFamily", "componentFontSize",
        "componentFontWeight", "componentLabelColor", "componentStroke", "compositeBackground", "compositeBorder",
        "compositeTitleBackground", "compositTitleSize", "confusionBg", "considerModelOrder", "container_bg_color",
        "container_border_color", "container_db_bg_color", "container_db_border_color", "container_dbFont",
        "container_dbFontFamily", "container_dbFontSize", "container_dbFontWeight", "container_queue_bg_color",
        "container_queue_border_color", "container_queueFont", "container_queueFontFamily", "container_queueFontSize",
        "container_queueFontWeight", "containerFont", "containerFontFamily", "containerFontSize",
        "containerFontWeight", "critBkgColor", "critBorderColor", "cScale0", "cScale1", "cScale10", "cScale11",
        "cScale2", "cScale3", "cScale4", "cScale5", "cScale6", "cScale7", "cScale8", "cScale9", "cScaleInv0",
        "cScaleInv1", "cScaleInv10", "cScaleInv11", "cScaleInv2", "cScaleInv3", "cScaleInv4", "cScaleInv5",
        "cScaleInv6", "cScaleInv7", "cScaleInv8", "cScaleInv9", "cScaleLabel0", "cScaleLabel1", "cScaleLabel10",
        "cScaleLabel11", "cScaleLabel2", "cScaleLabel3", "cScaleLabel4", "cScaleLabel5", "cScaleLabel6",
        "cScaleLabel7", "cScaleLabel8", "cScaleLabel9", "cScalePeer0", "cScalePeer1", "cScalePeer10", "cScalePeer11",
        "cScalePeer2", "cScalePeer3", "cScalePeer4", "cScalePeer5", "cScalePeer6", "cScalePeer7", "cScalePeer8",
        "cScalePeer9", "curve", "curveOpacity", "curveStrokeWidth", "curveTension", "cynefin", "darkMode",
        "dataLabelColor", "defaultIconPack", "defaultLinkColor", "defaultRenderer", "deterministicIds",
        "deterministicIDSeed", "diagramMarginX", "diagramMarginY", "diagramPadding", "disableMulticolor",
        "displayMode", "dividerMargin", "domainFontSize", "doneTaskBkgColor", "doneTaskBorderColor", "donutHole",
        "dropShadow", "edgeElasticity", "edgeLabelBackground", "edgeLengthFactor", "elk", "emArrowhead",
        "emCommandFill", "emCommandStroke", "emEventFill", "emEventStroke", "emProcessorFill", "emProcessorStroke",
        "emReadModelFill", "emReadModelStroke", "emRelationStroke", "emSwimlaneBackgroundOdd",
        "emSwimlaneBackgroundStroke", "emUiFill", "emUiStroke", "entityPadding", "er", "errorBkgColor",
        "errorTextColor", "eventmodeling", "evolutionStroke", "excludeBkgColor", "extensionIcons",
        "external_component_bg_color", "external_component_border_color", "external_component_db_bg_color",
        "external_component_db_border_color", "external_component_dbFont", "external_component_dbFontFamily",
        "external_component_dbFontSize", "external_component_dbFontWeight", "external_component_queue_bg_color",
        "external_component_queue_border_color", "external_component_queueFont", "external_component_queueFontFamily",
        "external_component_queueFontSize", "external_component_queueFontWeight", "external_componentFont",
        "external_componentFontFamily", "external_componentFontSize", "external_componentFontWeight",
        "external_container_bg_color", "external_container_border_color", "external_container_db_bg_color",
        "external_container_db_border_color", "external_container_dbFont", "external_container_dbFontFamily",
        "external_container_dbFontSize", "external_container_dbFontWeight", "external_container_queue_bg_color",
        "external_container_queue_border_color", "external_container_queueFont", "external_container_queueFontFamily",
        "external_container_queueFontSize", "external_container_queueFontWeight", "external_containerFont",
        "external_containerFontFamily", "external_containerFontSize", "external_containerFontWeight",
        "external_person_bg_color", "external_person_border_color", "external_personFont",
        "external_personFontFamily", "external_personFontSize", "external_personFontWeight",
        "external_system_bg_color", "external_system_border_color", "external_system_db_bg_color",
        "external_system_db_border_color", "external_system_dbFont", "external_system_dbFontFamily",
        "external_system_dbFontSize", "external_system_dbFontWeight", "external_system_queue_bg_color",
        "external_system_queue_border_color", "external_system_queueFont", "external_system_queueFontFamily",
        "external_system_queueFontSize", "external_system_queueFontWeight", "external_systemFont",
        "external_systemFontFamily", "external_systemFontSize", "external_systemFontWeight", "filenameIcons", "fill",
        "fillType0", "fillType1", "fillType2", "fillType3", "fillType4", "fillType5", "fillType6", "fillType7",
        "flowchart", "fontFamily", "fontSize", "fontSizeFactor", "fontWeight", "forceLegacyMathML", "forceMenus",
        "forceNodeModelOrder", "forkHeight", "forkWidth", "gantt", "git0", "git1", "git2", "git3", "git4", "git5",
        "git6", "git7", "gitBranchLabel0", "gitBranchLabel1", "gitBranchLabel2", "gitBranchLabel3", "gitBranchLabel4",
        "gitBranchLabel5", "gitBranchLabel6", "gitBranchLabel7", "gitGraph", "gitInv0", "gitInv1", "gitInv2",
        "gitInv3", "gitInv4", "gitInv5", "gitInv6", "gitInv7", "gradientStart", "gradientStop", "graticuleColor",
        "graticuleOpacity", "graticuleStrokeWidth", "gridColor", "gridLineStartPadding", "handDrawnSeed", "height",
        "hideEmptyMembersBox", "hideUnusedParticipants", "hierarchicalNamespaces", "highlightSlice", "htmlLabels",
        "iconSize", "idealEdgeLengthMultiplier", "ignoreCrossLaneEdges", "inheritDir", "innerEndBackground",
        "ishikawa", "itemFontSize", "journey", "kanban", "keepEntryNodeOnTop", "labelBackground",
        "labelBackgroundColor", "labelBoxBkgColor", "labelBoxBorderColor", "labelBoxHeight", "labelBoxWidth",
        "labelColor", "labelFontSize", "labelHeight", "labelPadding", "labelRotation", "labelStyle", "labelTextColor",
        "layout", "layoutAlgorithm", "layoutDirection", "leftMargin", "leftPadding", "legacyMathML", "legendBoxSize",
        "legendFontSize", "legendPadding", "legendPosition", "legendTextColor", "line_height", "lineColor",
        "lineHops", "lineThickness", "linkColor", "linkStroke", "logLevel", "look", "loopTextColor", "mainBkg",
        "mainBranchName", "mainBranchOrder", "marginBottom", "marginLeft", "marginRight", "marginTop",
        "markdownAutoWrap", "markerFill", "maxEdges", "maxLabelWidth", "maxNodeWidth", "maxTextSize", "mergeEdges",
        "messageAlign", "messageFont", "messageFontFamily", "messageFontSize", "messageFontWeight", "messageMargin",
        "mindmap", "minEntityHeight", "minEntityWidth", "miniPadding", "mirrorActors", "nextLinePaddingX",
        "nodeAlignment", "nodeBkg", "nodeBorder", "nodeColors", "nodeHeight", "nodeLabel", "nodePadding",
        "nodePlacementAlignment", "nodePlacementStrategy", "nodeSeparation", "nodeSpacing", "nodeWidth",
        "nonTerminalFill", "nonTerminalStroke", "nonTerminalTextColor", "noteAlign", "noteBkgColor",
        "noteBorderColor", "noteFont", "noteFontFamily", "noteFontSize", "noteFontWeight", "noteMargin",
        "noteTextColor", "numberSectionStyles", "numIter", "optimizeRanksByCrossings", "packet", "padding",
        "paddingX", "paddingY", "parallelCommits", "person_bg_color", "person_border_color", "personBkg",
        "personBorder", "personFont", "personFontFamily", "personFontSize", "personFontWeight", "pie", "pie1",
        "pie10", "pie11", "pie12", "pie2", "pie3", "pie4", "pie5", "pie6", "pie7", "pie8", "pie9",
        "pieLegendTextColor", "pieLegendTextSize", "pieOpacity", "pieOuterStrokeColor", "pieOuterStrokeWidth",
        "pieSectionTextColor", "pieSectionTextSize", "pieStrokeColor", "pieStrokeWidth", "pieTitleTextColor",
        "pieTitleTextSize", "plotColorPalette", "plotReservedSpacePercent", "pointLabelFontSize", "pointRadius",
        "pointTextPadding", "prefix", "primaryBorderColor", "primaryColor", "primaryTextColor", "quadrant1Fill",
        "quadrant1TextFill", "quadrant2Fill", "quadrant2TextFill", "quadrant3Fill", "quadrant3TextFill",
        "quadrant4Fill", "quadrant4TextFill", "quadrantChart", "quadrantExternalBorderStrokeFill",
        "quadrantExternalBorderStrokeWidth", "quadrantInternalBorderStrokeFill", "quadrantInternalBorderStrokeWidth",
        "quadrantLabelFontSize", "quadrantPadding", "quadrantPointFill", "quadrantPointTextFill",
        "quadrantTextTopPadding", "quadrantTitleFill", "quadrantXAxisTextFill", "quadrantYAxisTextFill", "radar",
        "radius", "railroad", "randomize", "rankSpacing", "rect_border_color", "rect_border_size", "rect_fill",
        "rect_min_height", "rect_min_width", "rect_padding", "rectBkgColor", "relationColor",
        "relationLabelBackground", "relationLabelColor", "requirement", "requirementBackground",
        "requirementBorderColor", "requirementBorderSize", "requirementTextColor", "rightAngles", "rightPadding",
        "rotateCommitLabel", "rowEven", "rowHeight", "rowIndent", "rowOdd", "ruleNameColor", "sankey",
        "scaleLabelColor", "secondaryBorderColor", "secondaryColor", "secondaryTextColor", "secondBkg",
        "sectionBkgColor", "sectionBkgColor2", "sectionFontSize", "sectionWidth", "securityLevel", "seed", "sequence",
        "sequenceNumberColor", "showAxisLine", "showBits", "showBranches", "showCommitLabel", "showDataLabel",
        "showDataLabelOutsideBar", "showDomainDescriptions", "showIcons", "showLabel", "showLegend",
        "showSequenceNumbers", "showTick", "showTitle", "showValues", "signalColor", "signalTextColor", "sizeUnit",
        "specialFill", "specialStateColor", "specialStroke", "startOnLoad", "state", "stateBkg", "stateLabelColor",
        "stroke", "strokeWidth", "subGraphTitleMargin", "suffix", "suppressErrorRendering", "surface0", "surface1",
        "surface2", "surface3", "surface4", "surfacePeer0", "surfacePeer1", "surfacePeer2", "surfacePeer3",
        "surfacePeer4", "swimlane", "system_bg_color", "system_border_color", "system_db_bg_color",
        "system_db_border_color", "system_dbFont", "system_dbFontFamily", "system_dbFontSize", "system_dbFontWeight",
        "system_queue_bg_color", "system_queue_border_color", "system_queueFont", "system_queueFontFamily",
        "system_queueFontSize", "system_queueFontWeight", "systemFont", "systemFontFamily", "systemFontSize",
        "systemFontWeight", "tagLabelBackground", "tagLabelBorder", "tagLabelColor", "tagLabelFontSize",
        "taskBkgColor", "taskBorderColor", "taskFontFamily", "taskFontSize", "taskMargin", "taskTextClickableColor",
        "taskTextColor", "taskTextDarkColor", "taskTextLightColor", "taskTextOutsideColor", "terminalFill",
        "terminalStroke", "terminalTextColor", "tertiaryBorderColor", "tertiaryColor", "tertiaryTextColor",
        "text_color", "textColor", "textHeight", "textPlacement", "textPosition", "theme", "THEME_COLOR_LIMIT",
        "themeCSS", "themeVariables", "ticketBaseUrl", "tickInterval", "tickLength", "tickWidth", "timeline",
        "titleColor", "titleFontFamily", "titleFontSize", "titlePadding", "titleShift", "titleTopMargin",
        "todayLineColor", "top", "topAxis", "topPadding", "transitionColor", "transitionLabelColor", "treemap",
        "treeView", "useDebugLayout", "useGradient", "useMaxWidth", "useWidth", "valueFontSize", "valueFormat",
        "venn", "venn1", "venn2", "venn3", "venn4", "venn5", "venn6", "venn7", "venn8", "vennSetTextColor",
        "vennTitleTextColor", "vertLineColor", "wardley", "wardleyEvolutionColor", "weekday", "width", "wrap",
        "wrapPadding", "wrappingWidth", "x", "xAxis", "xAxisLabelColor", "xAxisLabelFontSize", "xAxisLabelPadding",
        "xAxisLineColor", "xAxisPosition", "xAxisTickColor", "xAxisTitleColor", "xyChart", "y", "yAxis",
        "yAxisLabelColor", "yAxisLabelFontSize", "yAxisLabelPadding", "yAxisLineColor", "yAxisPosition",
        "yAxisTickColor", "yAxisTitleColor",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     Gets the keys whose change the text cannot make by default (the <c>secure</c> default).
    /// </summary>
    internal static IReadOnlyList<string> DefaultSecureKeys { get; } =
        ["secure", "securityLevel", "startOnLoad", "maxTextSize", "suppressErrorRendering", "maxEdges"];

    /// <summary>
    ///     Builds Mermaid's default configuration for the global keys, the flowchart, the sequence diagram and the class
    ///     diagram, with the default theme's variables. A new object is built for every use, because a JSON object is not
    ///     safe for concurrent reads.
    /// </summary>
    /// <returns>The default configuration.</returns>
    internal static JsonObject CreateDefaults()
    {
        // JsonValue.Create(string) is reflection-free; the generic JsonArray.Add<T> is not
        var secure = new JsonArray();
        foreach (var key in DefaultSecureKeys)
        {
            secure.Add(JsonValue.Create(key));
        }

        return new JsonObject
        {
            ["flowchart"] = new JsonObject
            {
                ["useMaxWidth"] = true,
                ["titleTopMargin"] = 25.0,
                ["subGraphTitleMargin"] = new JsonObject { ["top"] = 0.0, ["bottom"] = 0.0 },
                ["diagramPadding"] = 8.0,
                ["htmlLabels"] = null,
                ["nodeSpacing"] = 50.0,
                ["rankSpacing"] = 50.0,
                ["curve"] = "basis",
                ["padding"] = 15.0,
                ["defaultRenderer"] = "dagre-wrapper",
                ["wrappingWidth"] = 200.0,
                ["inheritDir"] = false,
            },
            ["sequence"] = CreateSequenceDefaults(),

            // defaultConfig.ts replaces the schema's class section; padding is deliberately left undefined
            ["class"] = new JsonObject
            {
                ["defaultRenderer"] = "dagre-wrapper",
                ["hideEmptyMembersBox"] = false,
                ["hierarchicalNamespaces"] = true,
            },
            ["state"] = CreateStateDefaults(),
            ["er"] = CreateErDefaults(),
            ["pie"] = CreatePieDefaults(),
            ["gantt"] = CreateGanttDefaults(),
            ["theme"] = "default",
            ["look"] = "classic",
            ["handDrawnSeed"] = 0.0,
            ["layout"] = "dagre",
            ["maxTextSize"] = 50000.0,
            ["maxEdges"] = 500.0,
            ["darkMode"] = false,
            ["fontFamily"] = DefaultFontFamily,
            ["logLevel"] = 5.0,
            ["securityLevel"] = "strict",
            ["startOnLoad"] = true,
            ["arrowMarkerAbsolute"] = false,
            ["secure"] = secure,
            ["legacyMathML"] = false,
            ["forceLegacyMathML"] = false,
            ["deterministicIds"] = false,
            ["fontSize"] = 16.0,
            ["markdownAutoWrap"] = true,
            ["suppressErrorRendering"] = false,
            ["themeVariables"] = ThemeResolver.GetThemeVariables("default", null),
        };
    }

    /// <summary>
    ///     Builds Mermaid's default sequence section (the defaults of <c>SequenceDiagramConfig</c>; the
    ///     <c>messageFont</c>, <c>noteFont</c> and <c>actorFont</c> functions of <c>defaultConfig.ts</c> have no JSON form).
    /// </summary>
    /// <returns>The default sequence section.</returns>
    private static JsonObject CreateSequenceDefaults() => new()
    {
        ["useMaxWidth"] = true,
        ["hideUnusedParticipants"] = false,
        ["activationWidth"] = 10.0,
        ["diagramMarginX"] = 50.0,
        ["diagramMarginY"] = 10.0,
        ["actorMargin"] = 50.0,
        ["width"] = 150.0,
        ["height"] = 65.0,
        ["boxMargin"] = 10.0,
        ["boxTextMargin"] = 5.0,
        ["noteMargin"] = 10.0,
        ["messageMargin"] = 35.0,
        ["messageAlign"] = "center",
        ["mirrorActors"] = true,
        ["forceMenus"] = false,
        ["bottomMarginAdj"] = 1.0,
        ["rightAngles"] = false,
        ["showSequenceNumbers"] = false,
        ["actorFontSize"] = 14.0,
        ["actorFontFamily"] = SequenceConfig.DefaultActorFontFamily,
        ["actorFontWeight"] = 400.0,
        ["noteFontSize"] = 14.0,
        ["noteFontFamily"] = SequenceConfig.DefaultTextFontFamily,
        ["noteFontWeight"] = 400.0,
        ["noteAlign"] = "center",
        ["messageFontSize"] = 16.0,
        ["messageFontFamily"] = SequenceConfig.DefaultTextFontFamily,
        ["messageFontWeight"] = 400.0,
        ["wrap"] = false,
        ["wrapPadding"] = 10.0,
        ["labelBoxWidth"] = 50.0,
        ["labelBoxHeight"] = 20.0,
    };

    /// <summary>
    ///     Builds Mermaid's default state section (the defaults of <c>StateDiagramConfig</c>; <c>nodeSpacing</c> and
    ///     <c>rankSpacing</c> have none).
    /// </summary>
    /// <returns>The default state section.</returns>
    private static JsonObject CreateStateDefaults() => new()
    {
        ["useMaxWidth"] = true,
        ["titleTopMargin"] = 25.0,
        ["dividerMargin"] = 10.0,
        ["sizeUnit"] = 5.0,
        ["padding"] = 8.0,
        ["textHeight"] = 10.0,
        ["titleShift"] = -15.0,
        ["noteMargin"] = 10.0,
        ["forkWidth"] = 70.0,
        ["forkHeight"] = 7.0,
        ["miniPadding"] = 2.0,
        ["fontSizeFactor"] = 5.02,
        ["fontSize"] = 24.0,
        ["labelHeight"] = 16.0,
        ["edgeLengthFactor"] = "20",
        ["compositTitleSize"] = 35.0,
        ["radius"] = 5.0,
        ["defaultRenderer"] = "dagre-wrapper",
    };

    /// <summary>
    ///     Builds Mermaid's default entity relationship section (the defaults of <c>ErDiagramConfig</c>).
    /// </summary>
    /// <returns>The default entity relationship section.</returns>
    private static JsonObject CreateErDefaults() => new()
    {
        ["useMaxWidth"] = true,
        ["titleTopMargin"] = 25.0,
        ["diagramPadding"] = 20.0,
        ["layoutDirection"] = "TB",
        ["minEntityWidth"] = 100.0,
        ["minEntityHeight"] = 75.0,
        ["entityPadding"] = 15.0,
        ["nodeSpacing"] = 140.0,
        ["rankSpacing"] = 80.0,
        ["stroke"] = "gray",
        ["fill"] = "honeydew",
        ["fontSize"] = 12.0,
    };

    /// <summary>
    ///     Builds Mermaid's default pie section (the defaults of <c>PieDiagramConfig</c>).
    /// </summary>
    /// <returns>The default pie section.</returns>
    private static JsonObject CreatePieDefaults() => new()
    {
        ["useMaxWidth"] = true,
        ["textPosition"] = 0.75,
        ["donutHole"] = 0.0,
        ["legendPosition"] = "right",
        ["highlightSlice"] = string.Empty,
    };

    /// <summary>
    ///     Builds Mermaid's default gantt section (the defaults of <c>GanttDiagramConfig</c>; <c>tickInterval</c> and
    ///     <c>useWidth</c> are undefined).
    /// </summary>
    /// <returns>The default gantt section.</returns>
    private static JsonObject CreateGanttDefaults() => new()
    {
        ["useMaxWidth"] = true,
        ["titleTopMargin"] = 25.0,
        ["barHeight"] = 20.0,
        ["barGap"] = 4.0,
        ["topPadding"] = 50.0,
        ["rightPadding"] = 75.0,
        ["leftPadding"] = 75.0,
        ["gridLineStartPadding"] = 35.0,
        ["fontSize"] = 11.0,
        ["sectionFontSize"] = 11.0,
        ["numberSectionStyles"] = 4.0,
        ["axisFormat"] = "%Y-%m-%d",
        ["topAxis"] = false,
        ["displayMode"] = string.Empty,
        ["weekday"] = "sunday",
    };
}
