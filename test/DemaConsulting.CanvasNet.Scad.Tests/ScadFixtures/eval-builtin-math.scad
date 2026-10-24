// Evaluation fixture: numeric built-in functions. Transcendental results are chosen so that the six
// significant digits printed are stable on every platform.

echo("abs sign", abs(-3.5), abs(2), sign(-9), sign(0), sign(4));
echo("rounding", floor(2.7), floor(-2.7), ceil(2.1), ceil(-2.1), round(2.5), round(-2.5), round(2.4));
echo("roots and powers", sqrt(16), sqrt(2), pow(2, 8), pow(9, 0.5), exp(0), ln(1));
echo("logarithm", log(1000), log(1), sqrt(-1));
echo("trigonometry", sin(0), sin(30), sin(90), cos(0), cos(60), cos(90), tan(45));
echo("inverse", asin(0.5), acos(0.5), atan(1), atan2(1, 1), atan2(1, -1), atan2(-1, -1));
echo("min max", min(3, 1, 2), max(3, 1, 2), min([4, 2, 8]), max([4, 2, 8]), min(5), max(-1, -7));
echo("norm", norm([3, 4]), norm([1, 2, 2]), norm([0]));
echo("cross", cross([1, 0, 0], [0, 1, 0]), cross([0, 1, 0], [1, 0, 0]), cross([2, 3], [4, 5]));
echo("precision", 1 / 3, 2 / 3, 22 / 7, 100 / 7);
echo("wrong types", sqrt("x"), abs(undef), floor([1]));
