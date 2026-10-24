// cspell:ignore ANDNOT

namespace DemaConsulting.CanvasNet.Render3D.SceneRendering;

/// <summary>
///     The operation of one <see cref="CsgInstruction"/> of a <see cref="CsgProgram"/> (postfix, stack machine over booleans).
/// </summary>
internal enum CsgOpCode : byte
{
    /// <summary>Pushes whether leaf <see cref="CsgInstruction.Operand"/> is currently inside.</summary>
    PushLeaf,

    /// <summary>Pushes <see langword="false"/> (an empty subtree).</summary>
    PushFalse,

    /// <summary>Pops two values and pushes their disjunction (union).</summary>
    Or,

    /// <summary>Pops two values and pushes their conjunction (intersection).</summary>
    And,

    /// <summary>Pops <c>b</c> then <c>a</c> and pushes <c>a AND NOT b</c> (difference).</summary>
    AndNot,
}

/// <summary>
///     One postfix instruction.
/// </summary>
/// <param name="Op">The operation.</param>
/// <param name="Operand">The leaf index for <see cref="CsgOpCode.PushLeaf"/>; zero otherwise.</param>
internal readonly record struct CsgInstruction(CsgOpCode Op, int Operand);

/// <summary>
///     An immutable, compiled CSG expression: postfix instructions evaluated over a bitset of
///     per-leaf "inside" flags.
/// </summary>
/// <remarks>
///     <para>
///         A <b>leaf</b> is a maximal union-only subtree of the scene whose inside state the resolver
///         tracks with a signed crossing counter (see <c>CsgResolver</c>). The program combines the
///         leaves with <c>Or</c>, <c>And</c> and <c>AndNot</c>; every operator is monotone in the
///         first operand, so an all-outside leaf state always evaluates to outside.
///     </para>
///     <para>
///         The leaf states are a <c>ulong[]</c> bitset (bit <c>i % 64</c> of word <c>i / 64</c>) so any
///         number of leaves is supported. Evaluation is iterative over a caller-supplied boolean stack
///         of at least <see cref="MaxStackDepth"/> entries, so deep expressions never use the call
///         stack. Instances are immutable and may be shared by all workers.
///     </para>
/// </remarks>
internal sealed class CsgProgram
{
    /// <summary>The instructions in postfix order.</summary>
    private readonly CsgInstruction[] _instructions;

    /// <summary>
    ///     Creates a program from validated postfix instructions.
    /// </summary>
    /// <param name="instructions">The postfix instructions; the stack must end with exactly one value.</param>
    /// <param name="leafCount">The number of leaves the instructions may reference.</param>
    /// <exception cref="ArgumentException">Thrown when the instructions are not a well-formed postfix expression over the leaves.</exception>
    public CsgProgram(IEnumerable<CsgInstruction> instructions, int leafCount)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentOutOfRangeException.ThrowIfNegative(leafCount);
        _instructions = instructions.ToArray();
        LeafCount = leafCount;

        // Validate the postfix structure and compute the deepest stack the evaluation needs
        var depth = 0;
        var max = 0;
        foreach (var instruction in _instructions)
        {
            switch (instruction.Op)
            {
                case CsgOpCode.PushLeaf:
                    if (instruction.Operand < 0 || instruction.Operand >= leafCount)
                    {
                        throw new ArgumentException("A PushLeaf operand is outside the leaf range.", nameof(instructions));
                    }

                    depth++;
                    break;
                case CsgOpCode.PushFalse:
                    depth++;
                    break;
                case CsgOpCode.Or:
                case CsgOpCode.And:
                case CsgOpCode.AndNot:
                    if (depth < 2)
                    {
                        throw new ArgumentException("A binary instruction lacks its operands.", nameof(instructions));
                    }

                    depth--;
                    break;
                default:
                    throw new ArgumentException("Unknown instruction.", nameof(instructions));
            }

            max = Math.Max(max, depth);
        }

        if (depth != 1)
        {
            throw new ArgumentException("The instructions must leave exactly one value.", nameof(instructions));
        }

        MaxStackDepth = max;
    }

    /// <summary>Gets the number of leaves.</summary>
    public int LeafCount { get; }

    /// <summary>Gets the number of 64-bit words a leaf-state bitset needs.</summary>
    public int BitsetWords => (LeafCount + 63) >> 6;

    /// <summary>Gets the deepest evaluation stack, i.e. the minimum length of the stack passed to <see cref="Evaluate"/>.</summary>
    public int MaxStackDepth { get; }

    /// <summary>Gets the instructions in postfix order.</summary>
    public ReadOnlySpan<CsgInstruction> Instructions => _instructions;

    /// <summary>
    ///     Evaluates the expression for a leaf state.
    /// </summary>
    /// <param name="leafBits">The leaf states: bit <c>i % 64</c> of word <c>i / 64</c> is set when leaf <c>i</c> is inside (at least <see cref="BitsetWords"/> words).</param>
    /// <param name="stack">Scratch space of at least <see cref="MaxStackDepth"/> entries.</param>
    /// <returns><see langword="true"/> when the point is inside the CSG solid.</returns>
    public bool Evaluate(ReadOnlySpan<ulong> leafBits, Span<bool> stack)
    {
        var top = 0;
        foreach (var instruction in _instructions)
        {
            switch (instruction.Op)
            {
                case CsgOpCode.PushLeaf:
                    stack[top++] = ((leafBits[instruction.Operand >> 6] >> (instruction.Operand & 63)) & 1UL) != 0UL;
                    break;
                case CsgOpCode.PushFalse:
                    stack[top++] = false;
                    break;
                case CsgOpCode.Or:
                    top--;
                    stack[top - 1] |= stack[top];
                    break;
                case CsgOpCode.And:
                    top--;
                    stack[top - 1] &= stack[top];
                    break;
                default:
                    // AndNot: the constructor guarantees only the five opcodes exist
                    top--;
                    stack[top - 1] &= !stack[top];
                    break;
            }
        }

        return stack[0];
    }

    /// <summary>
    ///     Renders the program as text (for diagnostics and tests), e.g. <c>L0 L1 ANDNOT L2 OR</c>.
    /// </summary>
    /// <returns>The space-separated instruction mnemonics.</returns>
    public string Disassemble() =>
        string.Join(
            ' ',
            _instructions.Select(i => i.Op switch
            {
                CsgOpCode.PushLeaf => $"L{i.Operand}",
                CsgOpCode.PushFalse => "FALSE",
                CsgOpCode.Or => "OR",
                CsgOpCode.And => "AND",
                _ => "ANDNOT",
            }));
}
