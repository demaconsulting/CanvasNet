// Original CanvasNet test fixture: operators, precedence-sensitive expressions, indexing and ranges.
a = 7;
b = 3;
c = [1, 2, 3];

sum = a + b - 2 * 3 / 4 % 5;
power = 2 ^ 3 ^ 2;
neg_power = -2 ^ 2;
flags = !(a > b) || a == b && b != 0;
label = a <= b ? "small" : a >= 10 ? "big" : "medium";
picked = c[1] + c[a - 5];
point = [10, 20, 30];
px = point.x + point.y * point.z;
r1 = [0 : 5];
r2 = [10 : -2 : 0];
r3 = [a : b * 2 : 100];
nested = [[1, 2, [3, 4]], [5, [6, [7]]]];
unary = - -a + +b;
decimal = 1.5e3 + .25 + 4.;
logic = (a < b) == (b > a) && !(a != a);
