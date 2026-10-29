using DemaConsulting.CanvasNet.VegaLite.Data;

namespace DemaConsulting.CanvasNet.VegaLite.Expressions;

/// <summary>
///     The operation of one instruction of a compiled expression.
/// </summary>
/// <remarks>
///     A compiled expression is a postfix program for a stack machine: operands are pushed, operators replace their
///     operands by the result, and the forward jumps implement the short-circuit operators and the ternary
///     operator, so evaluation is a loop without recursion whatever the shape of the expression.
/// </remarks>
internal enum ExpressionOpcode
{
    /// <summary>Pushes the constant at <c>Operand</c>.</summary>
    PushConstant,

    /// <summary>Pushes the whole datum.</summary>
    PushDatum,

    /// <summary>Pushes the field of the datum named by the name at <c>Operand</c>.</summary>
    DatumField,

    /// <summary>Pops a key and pushes the field of the datum with that name.</summary>
    DatumIndex,

    /// <summary>Pushes the parameter named by the name at <c>Operand</c> (null when it is not defined).</summary>
    PushParameter,

    /// <summary>Pops a value and pushes its member named by the name at <c>Operand</c>.</summary>
    Member,

    /// <summary>Pops a key and a value and pushes the member of the value with that key.</summary>
    Index,

    /// <summary>Unary minus.</summary>
    Negate,

    /// <summary>Unary plus.</summary>
    Plus,

    /// <summary>Logical not.</summary>
    Not,

    /// <summary>Bitwise not (<c>~</c>).</summary>
    BitNot,

    /// <summary>Addition or string concatenation.</summary>
    Add,

    /// <summary>Subtraction.</summary>
    Subtract,

    /// <summary>Multiplication.</summary>
    Multiply,

    /// <summary>Division.</summary>
    Divide,

    /// <summary>Remainder.</summary>
    Modulo,

    /// <summary>Strict equality (<c>===</c>).</summary>
    StrictEqual,

    /// <summary>Strict inequality (<c>!==</c>).</summary>
    StrictNotEqual,

    /// <summary>Loose equality (<c>==</c>).</summary>
    LooseEqual,

    /// <summary>Loose inequality (<c>!=</c>).</summary>
    LooseNotEqual,

    /// <summary>Less than.</summary>
    Less,

    /// <summary>Less than or equal.</summary>
    LessEqual,

    /// <summary>Greater than.</summary>
    Greater,

    /// <summary>Greater than or equal.</summary>
    GreaterEqual,

    /// <summary>When the top of the stack is falsy jumps to <c>Operand</c> keeping it, otherwise pops it (<c>&amp;&amp;</c>).</summary>
    JumpIfFalsyKeep,

    /// <summary>When the top of the stack is truthy jumps to <c>Operand</c> keeping it, otherwise pops it (<c>||</c>).</summary>
    JumpIfTruthyKeep,

    /// <summary>Pops the top of the stack and jumps to <c>Operand</c> when it is falsy (the test of <c>?:</c>).</summary>
    JumpIfFalsy,

    /// <summary>Jumps to <c>Operand</c>.</summary>
    Jump,

    /// <summary>Pops <c>Argument</c> values and pushes an array of them.</summary>
    MakeArray,

    /// <summary>Pops <c>Argument</c> key/value pairs and pushes an object.</summary>
    MakeObject,

    /// <summary>Calls the function at <c>Operand</c> of the function table with <c>Argument</c> arguments.</summary>
    Call,

    /// <summary>Pops <c>Argument</c> values of an unsupported construct and pushes null.</summary>
    Unsupported,
}

/// <summary>
///     One instruction of a compiled expression.
/// </summary>
/// <param name="Op">The operation.</param>
/// <param name="Operand">The constant, name, function or jump target index, depending on the operation.</param>
/// <param name="Argument">The number of values the operation consumes (arrays, objects, calls and unsupported constructs).</param>
/// <param name="Offset">The zero-based UTF-16 offset in the expression text of the construct that produced the instruction.</param>
internal readonly record struct ExpressionInstruction(ExpressionOpcode Op, int Operand, int Argument, int Offset);

/// <summary>
///     The immutable result of parsing an expression: a postfix program plus everything the caller must know about the
///     expression before it runs.
/// </summary>
/// <remarks>
///     <para>
///         A compiled expression is safe for concurrent use; every <see cref="ExpressionEvaluator"/> that evaluates it
///         keeps its own mutable state. Besides the program, the parser reports the unsupported constructs found in the
///         text (<see cref="UnsupportedConstructs"/>), the parameters the text refers to
///         (<see cref="ParameterReferences"/>) and whether it reads the datum (<see cref="ReferencesDatum"/>).
///     </para>
///     <para>
///         The step cost of an evaluation is the number of instructions executed (see <see cref="ExpressionEvaluator"/>),
///         so a program of <see cref="InstructionCount"/> instructions without jumps costs exactly that many steps.
///     </para>
/// </remarks>
internal sealed class CompiledExpression
{
    /// <summary>
    ///     Initializes a compiled expression.
    /// </summary>
    /// <param name="source">The expression text.</param>
    /// <param name="location">The location of the expression text in the specification.</param>
    /// <param name="program">The instructions.</param>
    /// <param name="tables">The constant, name and function tables of the program.</param>
    /// <param name="findings">What the parser found out about the expression.</param>
    internal CompiledExpression(
        string source,
        VegaLiteLocation location,
        ExpressionInstruction[] program,
        ExpressionTables tables,
        ExpressionFindings findings)
    {
        Source = source;
        Location = location;
        Program = program;
        Tables = tables;
        UnsupportedConstructs = findings.Unsupported;
        ParameterReferences = findings.Parameters;
        ReferencesDatum = findings.ReferencesDatum;
        Depth = findings.Depth;
        MaxStack = findings.MaxStack;
    }

    /// <summary>Gets the expression text.</summary>
    public string Source { get; }

    /// <summary>Gets the location of the expression text (the JSON pointer and the line and column of the string value).</summary>
    public VegaLiteLocation Location { get; }

    /// <summary>Gets the unsupported constructs in order of appearance; empty when the expression is fully supported.</summary>
    public IReadOnlyList<ExpressionConstruct> UnsupportedConstructs { get; }

    /// <summary>Gets the parameters the expression refers to, one entry per name, in order of first use.</summary>
    public IReadOnlyList<ExpressionReference> ParameterReferences { get; }

    /// <summary>Gets a value indicating whether the expression reads the datum.</summary>
    public bool ReferencesDatum { get; }

    /// <summary>Gets the deepest nesting of groups, brackets, arguments, branches and unary operands in the text.</summary>
    public int Depth { get; }

    /// <summary>Gets the number of instructions of the program.</summary>
    public int InstructionCount => Program.Length;

    /// <summary>Gets the largest stack height the program reaches.</summary>
    internal int MaxStack { get; }

    /// <summary>Gets the instructions.</summary>
    internal ExpressionInstruction[] Program { get; }

    /// <summary>Gets the constants, names and functions the program refers to.</summary>
    internal ExpressionTables Tables { get; }
}

/// <summary>
///     The constant, name and function tables that the instructions of a program index into.
/// </summary>
/// <param name="Constants">The constants the program pushes.</param>
/// <param name="Names">The member, field and parameter names the program uses.</param>
/// <param name="Functions">The functions the program calls.</param>
internal sealed record ExpressionTables(DataValue[] Constants, string[] Names, ExpressionFunction[] Functions);

/// <summary>
///     What the parser found out about an expression besides its program.
/// </summary>
/// <param name="Unsupported">The unsupported constructs in order of appearance.</param>
/// <param name="Parameters">The parameter references, one per name.</param>
/// <param name="ReferencesDatum">Whether the expression reads the datum.</param>
/// <param name="Depth">The deepest nesting reached.</param>
/// <param name="MaxStack">The largest stack height the program reaches.</param>
internal sealed record ExpressionFindings(
    ExpressionConstruct[] Unsupported,
    ExpressionReference[] Parameters,
    bool ReferencesDatum,
    int Depth,
    int MaxStack);
