// A plate with a round hole and three callout anchors (see 'canvasnet help scad')
difference() {
    cube([30, 20, 5]);
    translate([15, 10, -1]) cylinder(h = 7, r = 4, $fn = 48);
}

echo("anchor", "corner", [0, 0, 5]);

translate([15, 10, 5]) {
    echo("anchor", "hole-center", [0, 0, 0]);
    rotate([0, 0, 45]) {
        echo("anchor", "hole-edge", [4, 0, 0]);
    }
}
