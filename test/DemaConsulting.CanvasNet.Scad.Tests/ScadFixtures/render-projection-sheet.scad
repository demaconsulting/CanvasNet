// CanvasNet test fixture: a drawing sheet made of 2D shapes only. Two projections of a cube (from above and from the
// front), a border, and two lines of text. The model is entirely 2D, so it is drawn flat from the top. The sheet was
// supplied with the feature request for projection(); the cube, the sizes and the labels are the requester's own.
union() {
    translate([0, 40]) projection() my_model();
    translate([0, 0]) projection() rotate([90, 0, 0]) my_model();
    difference() { square([120, 80]); translate([2, 2]) square([116, 76]); }
    translate([5, 5]) text("SCALE: 1:1", size = 3);
    translate([40, 5]) text("TITLE: PROJECTION SHEET", size = 4, font = "Liberation Sans:style=Bold");
}
module my_model() { cube([30, 20, 15], center = true); }
