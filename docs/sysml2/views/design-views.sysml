// Views rendered into docs/design/generated/ for the Design document.
// Reopens the CanvasNet package so each 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content. Every view is typed by a
// standard view definition (sysml2tools 0.3.0-beta.3+): an untyped view has its notation
// inferred from its name, so a name ending in 'SequenceView' or 'StateView' (such as
// 'MermaidSequenceView' and 'MermaidStateView') was drawn as a behavior diagram and rendered
// empty for a structural part def.
package CanvasNet {
    private import StandardViewDefinitions::*;

    // Several systems, so a general view draws each system with its members. Documentation and the
    // artifact-location comments are filtered out so the overview stays readable.
    view SoftwareStructureView : GeneralView {
        expose CanvasNetSystem::**;
        expose CanvasNetSvgSystem::**;
        expose CanvasNetPdfSystem::**;
        expose CanvasNetPptxSystem::**;
        expose CanvasNetChartsSystem::**;
        expose CanvasNetVsdxSystem::**;
        expose CanvasNetRender3DSystem::**;
        expose CanvasNetScadSystem::**;
        expose CanvasNetVegaLiteSystem::**;
        expose CanvasNetViewerSystem::**;
        expose CanvasNetMermaidSystem::**;
        expose CanvasNetToolSystem::**;
        filter not @SysML::Documentation;
        filter not @SysML::Comment;
    }

    view CanvasNetView : InterconnectionView {
        expose CanvasNetSystem::**;
    }

    view CanvasNetSvgView : InterconnectionView {
        expose CanvasNetSvgSystem::**;
    }

    view CanvasNetPdfView : InterconnectionView {
        expose CanvasNetPdfSystem::**;
    }

    view CanvasNetPptxView : InterconnectionView {
        expose CanvasNetPptxSystem::**;
    }

    view CanvasNetChartsView : InterconnectionView {
        expose CanvasNetChartsSystem::**;
    }

    view CanvasNetVsdxView : InterconnectionView {
        expose CanvasNetVsdxSystem::**;
    }

    view CanvasNetRender3DView : InterconnectionView {
        expose CanvasNetRender3DSystem::**;
    }

    view SceneModelView : InterconnectionView {
        expose SceneModel::**;
    }

    view TessellationView : InterconnectionView {
        expose Tessellation::**;
    }

    view SceneRenderingView : InterconnectionView {
        expose SceneRendering::**;
    }

    view CanvasNetScadView : InterconnectionView {
        expose CanvasNetScadSystem::**;
    }

    view CanvasNetVegaLiteView : InterconnectionView {
        expose CanvasNetVegaLiteSystem::**;
    }

    view CanvasNetViewerView : InterconnectionView {
        expose CanvasNetViewerSystem::**;
    }

    view VegaLiteSpecModelView : InterconnectionView {
        expose VegaLiteSpecModel::**;
    }

    view VegaLiteDataView : InterconnectionView {
        expose VegaLiteData::**;
    }

    view VegaLiteExpressionsView : InterconnectionView {
        expose VegaLiteExpressions::**;
    }

    view VegaLiteTransformsView : InterconnectionView {
        expose VegaLiteTransforms::**;
    }

    view VegaLiteScalesView : InterconnectionView {
        expose VegaLiteScales::**;
    }

    view VegaLiteGuidesView : InterconnectionView {
        expose VegaLiteGuides::**;
    }

    view VegaLiteLayoutView : InterconnectionView {
        expose VegaLiteLayout::**;
    }

    view VegaLiteMarksView : InterconnectionView {
        expose VegaLiteMarks::**;
    }

    view VegaLiteScenegraphView : InterconnectionView {
        expose VegaLiteScenegraph::**;
    }

    view CanvasNetMermaidView : InterconnectionView {
        expose CanvasNetMermaidSystem::**;
    }

    view MermaidSyntaxView : InterconnectionView {
        expose MermaidSyntax::**;
    }

    view MermaidConfigurationView : InterconnectionView {
        expose MermaidConfiguration::**;
    }

    view MermaidGraphlibView : InterconnectionView {
        expose MermaidGraphlib::**;
    }

    view MermaidDagreView : InterconnectionView {
        expose MermaidDagre::**;
    }

    view MermaidRenderingView : InterconnectionView {
        expose MermaidRendering::**;
    }

    view MermaidScenegraphView : InterconnectionView {
        expose MermaidScenegraph::**;
    }

    view MermaidFlowchartView : InterconnectionView {
        expose MermaidFlowchart::**;
    }

    view MermaidSequenceView : InterconnectionView {
        expose MermaidSequence::**;
    }

    view MermaidClassView : InterconnectionView {
        expose MermaidClass::**;
    }

    view MermaidStateView : InterconnectionView {
        expose MermaidState::**;
    }

    view MermaidErView : InterconnectionView {
        expose MermaidEr::**;
    }

    view MermaidPieView : InterconnectionView {
        expose MermaidPie::**;
    }

    view MermaidGanttView : InterconnectionView {
        expose MermaidGantt::**;
    }

    view MermaidMindmapView : InterconnectionView {
        expose MermaidMindmap::**;
    }

    view MermaidCoseView : InterconnectionView {
        expose MermaidCose::**;
    }

    view MermaidPacketView : InterconnectionView {
        expose MermaidPacket::**;
    }

    view MermaidGitGraphView : InterconnectionView {
        expose MermaidGitGraph::**;
    }

    view CanvasNetToolView : InterconnectionView {
        expose CanvasNetToolSystem::**;
    }

    view CommandsView : InterconnectionView {
        expose Commands::**;
    }

    view ImagingView : InterconnectionView {
        expose Imaging::**;
    }

    view ScadLanguageView : InterconnectionView {
        expose ScadLanguage::**;
    }

    view ScadEvaluationView : InterconnectionView {
        expose ScadEvaluation::**;
    }

    view ScadGeometryView : InterconnectionView {
        expose ScadGeometry::**;
    }

    view ChartModelView : InterconnectionView {
        expose ChartModel::**;
    }

    view OpenXmlChartView : InterconnectionView {
        expose OpenXmlChart::**;
    }

    view CanvasView : InterconnectionView {
        expose Canvas::**;
    }

    view CodecsView : InterconnectionView {
        expose Codecs::**;
    }

    view GeometryView : InterconnectionView {
        expose Geometry::**;
    }

    view DrawingView : InterconnectionView {
        expose Drawing::**;
    }

    view FontsView : InterconnectionView {
        expose Fonts::**;
    }

    view RenderingView : InterconnectionView {
        expose Rendering::**;
    }

    view FilesView : InterconnectionView {
        expose Files::**;
    }

    view ImageSourcesView : InterconnectionView {
        expose ImageSources::**;
    }

    view PackagingView : InterconnectionView {
        expose Packaging::**;
    }
}
