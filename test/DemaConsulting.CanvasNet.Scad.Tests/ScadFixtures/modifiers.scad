// Original CanvasNet test fixture: statement modifiers.
*cube(10);
!sphere(5);
#cylinder(h = 10, r = 2);
%translate([20, 0, 0]) cube(5);
#!cube(3);
translate([0, 30, 0]) #cube(4);
*for (i = [0 : 2]) translate([i * 6, 0, 0]) cube(2);

%{
  cube(1);
  sphere(1);
}

#if (true) cube(2);
!let (k = 2) cube(k);

union() {
  *cube(1);
  #sphere(2);
}
