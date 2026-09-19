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

    view CanvasView {
        expose Canvas;
    }

    view CodecsView {
        expose Codecs;
    }
}
