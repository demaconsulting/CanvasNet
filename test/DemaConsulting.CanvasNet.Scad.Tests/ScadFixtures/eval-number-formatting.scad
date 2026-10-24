// Evaluation fixture: how numbers and other values print. Six significant digits, fixed notation for
// exponents from -4 to 5, scientific otherwise, and identical output under every culture.

echo("integers", 0, 1, -1, 100, 100000, 123456, 1234567);
echo("fractions", 0.1, 0.5, 1.5, -2.5, 1 / 3, 2 / 3, 3.14159265, 12345.67);
echo("small", 0.0001, 0.00001, 0.000012345678, 0.00123);
echo("large", 999999, 999999.5, 1e6, 1e10, 1e100, 1.5e20);
echo("special", -0, 1 / 0, -1 / 0, 0 / 0);
echo("rounded up", 0.9999995, 9.999995, 99999.95, 0.0001234565);
echo("booleans and undef", true, false, undef);
echo("strings", "plain", "quote \" inside", "tab\there", "back\\slash", "line\nbreak");
echo("lists", [], [1, "two", [3, [4]]], [[]], [true, undef, 1e-5]);
echo("ranges", [0 : 5], [0 : 0.5 : 2], [10 : -3 : 0]);
echo("named", a = 1, "mixed", b = [1, 2], c = "text");
echo("str forms", str("a", 1.5, [1, "x"], true), str([0 : 3]));
echo("function text", str(function(a, b) a + b));
