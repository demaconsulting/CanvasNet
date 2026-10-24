// Original CanvasNet test fixture: list comprehensions.
squares = [for (i = [1 : 5]) i * i];
grid = [for (x = [0 : 2]) for (y = [0 : 2]) [x, y]];
pairs = [for (x = [0 : 2], y = [0 : 2]) if (x != y) [x, y]];
signs = [for (v = [-2, -1, 1, 2]) if (v < 0) -1 else 1];
shifted = [for (v = squares) let (w = v + 1) w * 2];
flat = [each [1, 2], each [3, 4], 5];
powers = [for (n = 1; n < 100; n = n * 3) n];
mixed = [0, for (i = [1 : 3]) i * 10, 99];
counters = [for (a = 0, b = 10; a < b; a = a + 3, b = b - 1) [a, b]];
