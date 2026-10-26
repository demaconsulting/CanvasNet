// cspell:ignore scad openscad
namespace DemaConsulting.CanvasNet.Tool.Tests;

/// <summary>
///     Original OpenSCAD models for the tests, written as text into a directory the caller supplies.
/// </summary>
/// <remarks>
///     Every model is a few lines written for these tests (no text, no font, no file from another project), so
///     there are no licensing constraints beyond the repository's own license. <c>cube.scad</c> in the
///     <c>Fixtures</c> folder is the one model that is a file.
/// </remarks>
internal static partial class FixtureFactory
{
    /// <summary>
    ///     A model that looks different from each of the six sides: a flat slab with a tower near one corner and a
    ///     sphere near the opposite one, so no two of the named views produce the same picture.
    /// </summary>
    public const string AsymmetricModel =
        "cube([20, 10, 5]);\n"
        + "translate([2, 1, 5]) cube([6, 4, 8]);\n"
        + "translate([15, 8, 5]) sphere(r = 2, $fn = 12);\n";

    /// <summary>
    ///     Writes a text file, creating the folders it is in.
    /// </summary>
    /// <param name="directory">The directory.</param>
    /// <param name="name">The path of the file relative to the directory, with <c>/</c> between folders.</param>
    /// <param name="content">The text of the file.</param>
    /// <returns>The full path of the file.</returns>
    public static string WriteText(string directory, string name, string content)
    {
        var path = Path.Combine(directory, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}
