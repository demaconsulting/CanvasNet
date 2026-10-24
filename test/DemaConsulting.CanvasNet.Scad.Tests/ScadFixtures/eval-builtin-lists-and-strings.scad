// Evaluation fixture: list and string built-in functions.

echo("len", len([1, 2, 3]), len([]), len("hello"), len([[1, 2], [3]]), len(5));
echo("concat", concat([1], [2, 3], 4, [[5]]), concat(), concat("a", ["b"]));
echo("str", str(1, "a", [1, "b"], true, undef), str(), str(1 / 3), str(0.1 + 0.2));
echo("chr ord", chr(65), chr(72, 105), chr([97, 98]), ord("A"), ord("abc"), ord(""));

table = [[0, 0], [10, 100], [20, 400]];
echo("lookup", lookup(5, table), lookup(15, table), lookup(-5, table), lookup(99, table));

names = ["red", "green", "blue", "green"];
echo("search list", search(["green"], names), search(["green", "blue"], names, 1), search(["x"], names));
echo("search chars", search("lo", "hello"), search("l", "hello", 0));
echo("search table", search(20, [[10, "a"], [20, "b"], [30, "c"]], 1, 0));

echo("build", [for (i = [0 : 3]) chr(97 + i)], str([for (i = [1 : 3]) i * i]));
echo("seeded random", len(rands(0, 1, 5, 7)), rands(0, 1, 4, 7) == rands(0, 1, 4, 7), min(rands(5, 6, 20, 3)) >= 5, max(rands(5, 6, 20, 3)) < 6);
echo("version", version());
echo("wrong types", len(undef), chr(), concat(undef, undef));
