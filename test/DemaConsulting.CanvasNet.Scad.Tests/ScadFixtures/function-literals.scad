// Original CanvasNet test fixture: function literals and calls on arbitrary expressions.
double = function(x) x * 2;
add = function(a, b = 1) a + b;
compose = function(f, g) function(x) f(g(x));
result = (function(x) x + 1)(41);
apply = function(fn, v) fn(v);
value = apply(function(n) n * n, 7);
curried = function(a) function(b) function(c) a + b + c;
sum3 = curried(1)(2)(3);
table = [function(x) x, function(x) x * x];
second = table[1](5);

module use_callback(cb) {
  translate([cb(1), 0, 0]) cube(1);
}

use_callback(function(t) t * 10);
