// Evaluation fixture: functions, recursion, named arguments and the echo, let and assert expression forms.

function fact(n) = n <= 1 ? 1 : n * fact(n - 1);
function fib(n) = n < 2 ? n : fib(n - 1) + fib(n - 2);
function sum_to(n, acc = 0) = n == 0 ? acc : sum_to(n - 1, acc + n);
function is_even(n) = n == 0 ? true : is_odd(n - 1);
function is_odd(n) = n == 0 ? false : is_even(n - 1);

echo("fact", fact(5), fact(10));
echo("fib", fib(15));
echo("tail recursion", sum_to(100000));
echo("mutual", is_even(10), is_odd(7), is_even(7));

function named(a, b = 10, c = 100) = [a, b, c];
echo("named", named(1), named(1, c = 3), named(c = 5, a = 2), named(1, 2, 3));

function describe(v) = let(d = v * 2) echo("describing", v) d + 1;
echo("expression forms", describe(4));

function positive(v) = assert(v > 0, "value must be positive") v;
echo("assert passes", positive(3));

function clamp(v, lo, hi) = max(lo, min(hi, v));
echo("clamp", clamp(15, 0, 10), clamp(-3, 0, 10), clamp(4, 0, 10));

function repeat(s, n) = n <= 0 ? "" : str(s, repeat(s, n - 1));
echo("repeat", repeat("ab", 3));

function min(a, b) = a < b ? a : b;
echo("user min shadows the built-in", min(3, 9));
