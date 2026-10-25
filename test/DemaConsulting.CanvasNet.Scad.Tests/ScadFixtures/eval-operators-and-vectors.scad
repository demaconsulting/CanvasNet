// Evaluation fixture: operators, vectors and matrices.

echo("arithmetic", 7 + 3, 7 - 3, 7 * 3, 7 / 2, 7 % 3, 2 ^ 10, -7 % 3, -(2 + 3));
echo("division", 1 / 0, -1 / 0, 0 / 0);
echo("comparison", 1 < 2, 2 <= 2, 3 > 4, 3 >= 3, "a" < "b", [1, 2] < [1, 3], true < false);
echo("equality", true == 1, "x" == "x", [1, [2]] == [1, [2]], 1 != 2, undef == undef);
echo("not", !0, !"", !"a", !undef, !0.1, ![], ![0]);
echo("logic yields booleans", true && "x", false || 0, 1 && 2, 0 || "");
echo("ternary", 1 ? "yes" : "no", 0 ? "yes" : "no", "" ? "yes" : "no");

echo("vector arithmetic", [1, 2, 3] + [10, 20, 30], [5, 5] - [1, 2]);
echo("scaling", [1, 2, 3] * 2, 2 * [1, 2], [2, 4] / 2);
echo("dot product", [1, 2, 3] * [4, 5, 6]);
echo("matrix times vector", [[1, 2], [3, 4]] * [1, 1]);
echo("matrix times matrix", [[1, 2], [3, 4]] * [[1, 0], [0, 1]], [[1, 2], [3, 4]] * [[0, 1], [1, 0]]);

v = [10, 20, 30];
echo("indexing", v[0], v[2], v[3], v[-1], v[1.5]);
echo("string indexing", "hello"[1], "hello"[9]);
echo("members", v.x, v.y, v.z, [1, 2].z);
echo("nested index", [[1, 2], [3, 4]][1][0]);

r = [1 : 2 : 9];
echo("range as value", r, [for (i = r) i]);
echo("precedence", 2 + 3 * 4, (2 + 3) * 4, 2 ^ 3 ^ 2, -2 ^ 2, 10 - 4 - 3, 1 + 2 < 4);
