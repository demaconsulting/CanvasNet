// Evaluation fixture: list comprehensions.

echo("squares", [for (i = [1 : 5]) i * i]);
echo("filtered", [for (i = [1 : 10]) if (i % 3 == 0) i]);
echo("if else", [for (i = [1 : 4]) if (i % 2 == 0) "even" else "odd"]);
echo("two generators", [for (i = [1 : 2], j = [1 : 2]) [i, j]]);
echo("let", [for (i = [1 : 3]) let(sq = i * i) sq + 1]);
echo("each", [each [1, 2], each [3], 4]);
echo("flatten", [for (row = [[1, 2], [3, 4], []]) each row]);
echo("nested", [for (i = [0 : 2]) [for (j = [0 : i]) j]]);
echo("c-style", [for (i = 0, s = 1; i < 5; i = i + 1, s = s * 2) s]);
echo("parenthesized", [for (x = [1, 2]) (x)]);
echo("mixed", [0, for (i = [1 : 2]) i * 10, 99]);
echo("empty", [for (i = [1 : 0]) i], len([for (i = [1 : 0]) i]));
echo("strings", [for (v = ["a", "b"]) str(v, v)]);

grid = [for (x = [0 : 2]) [for (y = [0 : 1]) x * 10 + y]];
echo("grid", grid, len(grid), len(grid[0]));
