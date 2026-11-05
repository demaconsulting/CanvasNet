// A block with a round hole through its top, for the CanvasNet tool tests.
// An original work of this project: it has no dependencies and uses no text or font.
width = 20;

module block(w) {
    difference() {
        cube([w, 10, 10], center = true);
        cylinder(h = 12, r = 2, center = true, $fn = 16);
    }
}

function half(x) = x / 2;

block(width);
