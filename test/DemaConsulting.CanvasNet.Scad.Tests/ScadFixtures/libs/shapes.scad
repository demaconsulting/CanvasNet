// Used by eval-include-main.scad: only the definitions are imported. The echo and the geometry below are
// never run, and lib_value is reachable only through the library's own functions.

echo("shapes top level is not run by use");
cube(99);

lib_value = 42;
function lib_value_plus(k) = lib_value + k;
function peg_count(n) = n * 2;

module peg(h) {
    cylinder(h = h, r = 1);
}

module peg_ring(n) {
    for (i = [0 : n - 1])
        rotate([0, 0, i * 360 / n])
            translate([5, 0, 0])
                peg(h = 2 + i);
}
