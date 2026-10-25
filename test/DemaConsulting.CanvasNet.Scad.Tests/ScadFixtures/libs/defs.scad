// Included by eval-include-main.scad: its statements run in the including file, in place.

unit = 5;
scale_factor = unit * 2;
include <nested/inner.scad>
echo("defs included", unit);
