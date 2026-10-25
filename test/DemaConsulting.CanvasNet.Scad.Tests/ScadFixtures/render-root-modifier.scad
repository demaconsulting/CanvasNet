// Original CanvasNet test fixture: the root modifier. Only the marked subtree is drawn: the second cube and the
// small cube next to it, which is marked for debugging inside the root.
cube(10);
!union() {
  translate([20, 0, 0]) cube(10);
  #translate([35, 0, 0]) cube(5);
}
translate([50, 0, 0]) cube(10);
