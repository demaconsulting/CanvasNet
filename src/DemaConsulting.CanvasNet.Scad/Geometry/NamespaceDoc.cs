namespace DemaConsulting.CanvasNet.Scad.Geometry;

/// <summary>
///     The <c>Geometry</c> namespace turns the geometry-agnostic instantiation tree produced by evaluation into a
///     Render3D scene: <c>ScadGeometryBuilder</c> walks the tree with an explicit stack (the engine never
///     recurses, so a model nested thousands of levels deep cannot overflow the caller's stack), combines the
///     children of implicit unions, applies the modifiers and reports everything it cannot build, and the primitive
///     builders (<c>ScadPrimitives</c>, with <c>ScadArguments</c> and <c>ScadFragmentRule</c>) create the meshes and
///     2D regions of <c>cube</c>, <c>sphere</c>, <c>cylinder</c>, <c>polyhedron</c>, <c>square</c>, <c>circle</c>
///     and <c>polygon</c>.
/// </summary>
/// <remarks>
///     <para>
///         The subsystem is internal. It depends one way on the <c>Evaluation</c> subsystem (instances, values,
///         the report sink) and on the public Render3D and core types; it never touches the syntax tree. Invalid
///         arguments are reported as warnings and yield nothing for that instance; things the language allows but
///         this implementation cannot build are reported as unsupported features; resource limits are errors. In no
///         case is a module silently skipped.
///     </para>
/// </remarks>
internal static class NamespaceDoc
{
}
