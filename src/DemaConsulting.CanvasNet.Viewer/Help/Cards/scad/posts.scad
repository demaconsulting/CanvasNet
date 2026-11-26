// A row of posts; override the width with -D width=30
width = 20;
module post(h) { cylinder(h = h, d = width / 2, $fn = 32); }
for (i = [0 : 2]) translate([i * width, 0, 0]) post(10 + 5 * i);
