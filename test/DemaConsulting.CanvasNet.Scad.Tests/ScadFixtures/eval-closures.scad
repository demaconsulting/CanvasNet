// Evaluation fixture: function literals and closures.

function adder(n) = function(x) x + n;
add5 = adder(5);
echo("closure", add5(10), adder(1)(2));

inc = function(x) x + 1;
dbl = function(x) x * 2;
function compose(f, g) = function(x) f(g(x));
echo("compose", compose(inc, dbl)(5), compose(dbl, inc)(5));

function apply_all(fs, v) = [for (f = fs) f(v)];
echo("apply all", apply_all([inc, dbl, function(x) x * x], 6));

echo("immediate", (function(a, b) a - b)(10, 4));

function make_getters(n) = [for (i = [1 : n]) function() i * 10];
echo("captured loop variable", [for (g = make_getters(3)) g()]);

function fold(f, list, start) =
    len(list) == 0 ? start : fold(f, [for (i = [1 : len(list) - 1]) list[i]], f(start, list[0]));
echo("fold", fold(function(acc, v) acc + v, [1, 2, 3, 4], 0));

echo("type tests", is_function(inc), is_function(3), is_undef(undef), is_num(1), is_string("s"), is_list([]), is_bool(false));
echo("function as text", str(inc));
