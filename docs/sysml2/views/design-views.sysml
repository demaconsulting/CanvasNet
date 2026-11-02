// Views rendered into docs/design/generated/ for the Design document.
// Reopens the CanvasNet package so each 'expose' statement can reference
// its target part def directly. Per SysML2Tools syntax, 'expose' is only valid
// inside a named 'view' usage (not a 'view def' definition), and it - not
// 'render' - is what scopes the rendered diagram content.
package CanvasNet {
    view SoftwareStructureView {
        expose CanvasNet;
    }

    view CanvasNetView {
        expose CanvasNetSystem;
    }

    view CanvasNetSvgView {
        expose CanvasNetSvgSystem;
    }

    view CanvasNetPdfView {
        expose CanvasNetPdfSystem;
    }

    view CanvasNetPptxView {
        expose CanvasNetPptxSystem;
    }

    view CanvasNetChartsView {
        expose CanvasNetChartsSystem;
    }

    view CanvasNetVsdxView {
        expose CanvasNetVsdxSystem;
    }

    view CanvasNetRender3DView {
        expose CanvasNetRender3DSystem;
    }

    view SceneModelView {
        expose SceneModel;
    }

    view TessellationView {
        expose Tessellation;
    }

    view SceneRenderingView {
        expose SceneRendering;
    }

    view CanvasNetScadView {
        expose CanvasNetScadSystem;
    }

    view CanvasNetVegaLiteView {
        expose CanvasNetVegaLiteSystem;
    }

    view VegaLiteSpecModelView {
        expose VegaLiteSpecModel;
    }

    view VegaLiteDataView {
        expose VegaLiteData;
    }

    view VegaLiteExpressionsView {
        expose VegaLiteExpressions;
    }

    view VegaLiteTransformsView {
        expose VegaLiteTransforms;
    }

    view VegaLiteScalesView {
        expose VegaLiteScales;
    }

    view VegaLiteGuidesView {
        expose VegaLiteGuides;
    }

    view VegaLiteLayoutView {
        expose VegaLiteLayout;
    }

    view VegaLiteMarksView {
        expose VegaLiteMarks;
    }

    view VegaLiteScenegraphView {
        expose VegaLiteScenegraph;
    }

    view CanvasNetToolView {
        expose CanvasNetToolSystem;
    }

    view CommandsView {
        expose Commands;
    }

    view ImagingView {
        expose Imaging;
    }

    view ScadLanguageView {
        expose ScadLanguage;
    }

    view ScadEvaluationView {
        expose ScadEvaluation;
    }

    view ScadGeometryView {
        expose ScadGeometry;
    }

    view ChartModelView {
        expose ChartModel;
    }

    view OpenXmlChartView {
        expose OpenXmlChart;
    }

    view CanvasView {
        expose Canvas;
    }

    view CodecsView {
        expose Codecs;
    }

    view GeometryView {
        expose Geometry;
    }

    view DrawingView {
        expose Drawing;
    }

    view FontsView {
        expose Fonts;
    }

    view RenderingView {
        expose Rendering;
    }

    view PackagingView {
        expose Packaging;
    }
}
