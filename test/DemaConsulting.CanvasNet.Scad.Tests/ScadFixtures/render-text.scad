// Original CanvasNet test fixture: text() with the default (bundled) font, written for this repository.
// Everything is extruded (a flat 2D shape next to solids is a mixed-dimension union), and the first line is the
// reference: a capital H is exactly size tall, so the extruded "HIT" is 10 tall with its ink flush with x = 0.
// Line 7: centered text with glyphs that have holes. Line 8: a line with a descender. Line 9: a 2D difference of text.
// Line 10: a hull of text. Line 11: a rotate_extrude of a glyph.
linear_extrude(height = 2) text("HIT", size = 10);
translate([0, -20, 0]) linear_extrude(height = 2) text("OBA", size = 10, halign = "center", valign = "center");
translate([0, -40, 0]) linear_extrude(height = 4) text("RgO", size = 10);
translate([40, -20, 0]) linear_extrude(height = 2) difference() { text("HE", size = 10); translate([1, 4]) square([12, 1.5]); }
translate([40, -45, 0]) linear_extrude(height = 1) hull() text("AV", size = 8);
translate([80, -10, 0]) rotate_extrude($fn = 24) translate([6, 0]) text("l", size = 6);
