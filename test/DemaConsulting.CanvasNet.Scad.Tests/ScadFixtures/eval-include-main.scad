// Evaluation fixture: include and use. Run through the file system and through an in-memory resolver.

include <libs/defs.scad>
use <libs/shapes.scad>

echo("main", unit, scale_factor, inner_note);
echo("library function", peg_count(3), lib_value_plus(8));
echo("library value is private", is_undef(lib_value));

peg_ring(3);
translate([0, 0, scale_factor]) peg(h = 2);
