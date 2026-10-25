using DemaConsulting.CanvasNet.Scad.Language;

namespace DemaConsulting.CanvasNet.Scad.Evaluation;

/// <summary>
///     The evaluation state of a <see cref="ScadCell"/>.
/// </summary>
internal enum ScadCellState
{
    /// <summary>The expression has not been evaluated yet.</summary>
    Unevaluated,

    /// <summary>The expression is being evaluated (reading the cell now is a circular reference).</summary>
    Evaluating,

    /// <summary>The value is known.</summary>
    Done,
}

/// <summary>
///     A variable binding: a lazily evaluated, memoized assignment, or an already known value.
/// </summary>
internal sealed class ScadCell
{
    /// <summary>
    ///     Initializes a lazy cell whose expression is evaluated in its defining scope on first use.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="expression">The expression.</param>
    /// <param name="scope">The scope the expression is evaluated in.</param>
    /// <param name="location">The location of the assignment.</param>
    public ScadCell(string name, ScadExpression expression, ScadScope scope, ScadSourceLocation location)
    {
        Name = name;
        Expression = expression;
        Scope = scope;
        Location = location;
    }

    /// <summary>
    ///     Initializes a cell with a known value.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value.</param>
    /// <param name="location">The location of the binding.</param>
    public ScadCell(string name, ScadValue value, ScadSourceLocation location)
    {
        Name = name;
        Value = value;
        State = ScadCellState.Done;
        Location = location;
    }

    /// <summary>Gets the variable name.</summary>
    public string Name { get; }

    /// <summary>Gets the expression of a lazy cell, or <see langword="null"/>.</summary>
    public ScadExpression? Expression { get; }

    /// <summary>Gets the scope a lazy cell is evaluated in, or <see langword="null"/>.</summary>
    public ScadScope? Scope { get; }

    /// <summary>Gets the location of the binding.</summary>
    public ScadSourceLocation Location { get; }

    /// <summary>Gets or sets the value once <see cref="State"/> is <see cref="ScadCellState.Done"/>.</summary>
    public ScadValue Value { get; set; }

    /// <summary>Gets or sets the evaluation state.</summary>
    public ScadCellState State { get; set; }

    /// <summary>Gets or sets a value indicating whether a later assignment in the same scope replaced this one.</summary>
    public bool Superseded { get; set; }
}

/// <summary>
///     One link of the dynamic <c>$</c> variable chain: the special variables bound by one scope.
/// </summary>
internal sealed class ScadSpecialFrame
{
    /// <summary>The bindings of this frame.</summary>
    private readonly Dictionary<string, ScadCell> _cells = [];

    /// <summary>
    ///     Initializes a frame.
    /// </summary>
    /// <param name="next">The dynamically enclosing frame, or <see langword="null"/>.</param>
    public ScadSpecialFrame(ScadSpecialFrame? next)
    {
        Next = next;
        Depth = (next?.Depth ?? 0) + 1;
    }

    /// <summary>Gets the dynamically enclosing frame, or <see langword="null"/> for the outermost.</summary>
    public ScadSpecialFrame? Next { get; }

    /// <summary>Gets the number of frames in the chain starting here.</summary>
    public int Depth { get; }

    /// <summary>
    ///     Binds a name in this frame, replacing a previous binding of the same name.
    /// </summary>
    /// <param name="cell">The binding.</param>
    /// <returns>The replaced binding, or <see langword="null"/>.</returns>
    public ScadCell? Bind(ScadCell cell)
    {
        _cells.TryGetValue(cell.Name, out var previous);
        _cells[cell.Name] = cell;
        return previous;
    }

    /// <summary>
    ///     Looks a name up in this frame only.
    /// </summary>
    /// <param name="name">The special variable name.</param>
    /// <param name="cell">The binding when found.</param>
    /// <returns><see langword="true"/> when the frame binds the name.</returns>
    public bool TryGet(string name, out ScadCell cell) => _cells.TryGetValue(name, out cell!);

    /// <summary>
    ///     Creates a frame with the same bindings (the very same, already evaluated cells) in front of another chain.
    /// </summary>
    /// <param name="next">The chain the copy is put in front of, or <see langword="null"/>.</param>
    /// <returns>The new frame; this frame is unchanged.</returns>
    public ScadSpecialFrame CopyOnto(ScadSpecialFrame? next)
    {
        var copy = new ScadSpecialFrame(next);
        foreach (var cell in _cells.Values)
        {
            copy._cells[cell.Name] = cell;
        }

        return copy;
    }
}

/// <summary>
///     A module definition together with the scope it was defined in.
/// </summary>
/// <param name="Definition">The definition.</param>
/// <param name="Scope">The defining scope (the lexical parent of every call).</param>
internal sealed record ScadModuleBinding(ScadModuleDefinition Definition, ScadScope Scope);

/// <summary>
///     The children passed to a user module call, ready for <c>children()</c>.
/// </summary>
/// <param name="Slots">The child slots: every statement of the call's child list that is not an assignment or a definition, nested braces flattened.</param>
/// <param name="ListScope">The scope slots execute in: the call site, extended by assignments and definitions of the child list.</param>
/// <param name="Location">The location of the call.</param>
/// <param name="ListSpecials">
///     The frame of the <c>$</c> variables assigned by the child list itself, or <see langword="null"/> when it assigns
///     none. A slot runs with a copy of this frame in front of the dynamic chain in effect where <c>children()</c> executes.
/// </param>
internal sealed record ScadChildren(IReadOnlyList<ScadStatement> Slots, ScadScope ListScope, ScadSourceLocation Location, ScadSpecialFrame? ListSpecials = null);

/// <summary>
///     A function value: a named function definition or a function literal together with the scope it closes over.
/// </summary>
internal sealed class ScadClosure : IScadFunctionValue
{
    /// <summary>The parameter names, computed once.</summary>
    private readonly string[] _parameterNames;

    /// <summary>
    ///     Initializes a closure.
    /// </summary>
    /// <param name="name">The function name, or <see langword="null"/> for a function literal.</param>
    /// <param name="parameters">The formal parameters.</param>
    /// <param name="body">The result expression.</param>
    /// <param name="scope">The scope the function closes over (its lexical parent when called).</param>
    /// <param name="location">The location of the definition.</param>
    public ScadClosure(string? name, IReadOnlyList<ScadParameter> parameters, ScadExpression body, ScadScope scope, ScadSourceLocation location)
    {
        Name = name;
        Parameters = parameters;
        Body = body;
        Scope = scope;
        Location = location;
        _parameterNames = parameters.Select(p => p.Name).ToArray();
    }

    /// <summary>Gets the function name, or <see langword="null"/> for a function literal.</summary>
    public string? Name { get; }

    /// <summary>Gets the formal parameters.</summary>
    public IReadOnlyList<ScadParameter> Parameters { get; }

    /// <summary>Gets the result expression.</summary>
    public ScadExpression Body { get; }

    /// <summary>Gets the scope the function closes over.</summary>
    public ScadScope Scope { get; }

    /// <summary>Gets the location of the definition.</summary>
    public ScadSourceLocation Location { get; }

    /// <inheritdoc />
    IReadOnlyList<string> IScadFunctionValue.ParameterNames => _parameterNames;
}

/// <summary>
///     A lexical scope: variable, function and module bindings of one block, call or loop iteration, plus the
///     pointer into the dynamic <c>$</c> variable chain that applies inside it.
/// </summary>
/// <remarks>
///     <para>
///         A scope holds three independent namespaces. Variables are lazily evaluated memoized cells, so definitions
///         are position independent. The scope's <see cref="Specials"/> is a persistent linked list shared with the
///         dynamically enclosing scope until the scope binds a <c>$</c> name itself, at which point it gets its own
///         frame in front of the inherited chain; scopes that bind no <c>$</c> name allocate nothing.
///     </para>
///     <para>
///         Scopes are single-threaded and live only for one evaluation pass.
///     </para>
/// </remarks>
internal sealed class ScadScope
{
    /// <summary>The variable cells by name.</summary>
    private Dictionary<string, ScadCell>? _variables;

    /// <summary>The function definitions by name.</summary>
    private Dictionary<string, ScadClosure>? _functions;

    /// <summary>The module definitions by name.</summary>
    private Dictionary<string, ScadModuleBinding>? _modules;

    /// <summary>The cells to force in source order at scope entry.</summary>
    private List<ScadCell>? _lazyCells;

    /// <summary>Whether <see cref="Specials"/> starts with a frame owned by this scope.</summary>
    private bool _ownsFrame;

    /// <summary>
    ///     Initializes a scope.
    /// </summary>
    /// <param name="parent">The lexically enclosing scope, or <see langword="null"/>.</param>
    /// <param name="specials">The dynamic <c>$</c> chain in effect (the dynamic parent's pointer).</param>
    public ScadScope(ScadScope? parent, ScadSpecialFrame? specials)
    {
        Parent = parent;
        Specials = specials;
    }

    /// <summary>Gets the lexically enclosing scope.</summary>
    public ScadScope? Parent { get; }

    /// <summary>Gets the head of the dynamic <c>$</c> chain that applies inside this scope.</summary>
    public ScadSpecialFrame? Specials { get; private set; }

    /// <summary>Gets the frame this scope created for its own <c>$</c> bindings, or <see langword="null"/> when it binds none.</summary>
    public ScadSpecialFrame? OwnedFrame => _ownsFrame ? Specials : null;

    /// <summary>Gets or sets the children of the module call this scope belongs to (set on a module call scope).</summary>
    public ScadChildren? Children { get; set; }

    /// <summary>Gets the lazily evaluated cells in source order (those replaced by a later assignment are flagged <see cref="ScadCell.Superseded"/>).</summary>
    public IReadOnlyList<ScadCell> LazyCells => (IReadOnlyList<ScadCell>?)_lazyCells ?? [];

    /// <summary>
    ///     Binds a variable (or, for a name starting with <c>$</c>, a special variable) in this scope.
    /// </summary>
    /// <param name="cell">The binding.</param>
    /// <returns>The binding of the same name this one replaced, or <see langword="null"/>.</returns>
    public ScadCell? DefineVariable(ScadCell cell)
    {
        if (cell.Expression is not null)
        {
            (_lazyCells ??= []).Add(cell);
        }

        ScadCell? previous;
        if (IsSpecial(cell.Name))
        {
            if (!_ownsFrame)
            {
                Specials = new ScadSpecialFrame(Specials);
                _ownsFrame = true;
            }

            previous = Specials!.Bind(cell);
        }
        else
        {
            _variables ??= [];
            _variables.TryGetValue(cell.Name, out previous);
            _variables[cell.Name] = cell;
        }

        if (previous is not null)
        {
            previous.Superseded = true;
        }

        return previous;
    }

    /// <summary>
    ///     Defines a function in this scope.
    /// </summary>
    /// <param name="function">The function.</param>
    /// <returns>The function of the same name this one replaced, or <see langword="null"/>.</returns>
    public ScadClosure? DefineFunction(ScadClosure function)
    {
        _functions ??= [];
        _functions.TryGetValue(function.Name!, out var previous);
        _functions[function.Name!] = function;
        return previous;
    }

    /// <summary>
    ///     Defines a module in this scope.
    /// </summary>
    /// <param name="name">The module name.</param>
    /// <param name="module">The module.</param>
    /// <returns>The module of the same name this one replaced, or <see langword="null"/>.</returns>
    public ScadModuleBinding? DefineModule(string name, ScadModuleBinding module)
    {
        _modules ??= [];
        _modules.TryGetValue(name, out var previous);
        _modules[name] = module;
        return previous;
    }

    /// <summary>
    ///     Copies the functions and modules defined directly in another scope into this one (the <c>use</c> import).
    /// </summary>
    /// <param name="library">The library's file scope.</param>
    public void ImportDefinitionsFrom(ScadScope library)
    {
        if (library._functions is not null)
        {
            _functions ??= [];
            foreach (var (name, function) in library._functions)
            {
                _functions[name] = function;
            }
        }

        if (library._modules is not null)
        {
            _modules ??= [];
            foreach (var (name, module) in library._modules)
            {
                _modules[name] = module;
            }
        }
    }

    /// <summary>
    ///     Looks a function up in this scope only.
    /// </summary>
    /// <param name="name">The function name.</param>
    /// <returns>The function, or <see langword="null"/>.</returns>
    public ScadClosure? GetFunction(string name) =>
        _functions is not null && _functions.TryGetValue(name, out var function) ? function : null;

    /// <summary>
    ///     Looks a variable up in this scope only.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The cell, or <see langword="null"/>.</returns>
    public ScadCell? GetVariable(string name) =>
        _variables is not null && _variables.TryGetValue(name, out var cell) ? cell : null;

    /// <summary>
    ///     Finds a variable in this scope or an enclosing one.
    /// </summary>
    /// <param name="name">The variable name.</param>
    /// <param name="hops">The number of scopes walked, used to charge lookup work.</param>
    /// <returns>The innermost cell, or <see langword="null"/> when the name is not bound.</returns>
    public ScadCell? FindVariable(string name, out int hops)
    {
        hops = 0;
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            hops++;
            var cell = scope.GetVariable(name);
            if (cell is not null)
            {
                return cell;
            }
        }

        return null;
    }

    /// <summary>
    ///     Finds a module in this scope or an enclosing one.
    /// </summary>
    /// <param name="name">The module name.</param>
    /// <param name="hops">The number of scopes walked.</param>
    /// <returns>The innermost module, or <see langword="null"/>.</returns>
    public ScadModuleBinding? FindModule(string name, out int hops)
    {
        hops = 0;
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            hops++;
            if (scope._modules is not null && scope._modules.TryGetValue(name, out var module))
            {
                return module;
            }
        }

        return null;
    }

    /// <summary>
    ///     Finds a special variable by walking the dynamic chain.
    /// </summary>
    /// <param name="name">The special variable name (including <c>$</c>).</param>
    /// <param name="hops">The number of frames walked.</param>
    /// <returns>The innermost binding, or <see langword="null"/>.</returns>
    public ScadCell? FindSpecial(string name, out int hops)
    {
        hops = 0;
        for (var frame = Specials; frame is not null; frame = frame.Next)
        {
            hops++;
            if (frame.TryGet(name, out var cell))
            {
                return cell;
            }
        }

        return null;
    }

    /// <summary>
    ///     Finds the children of the innermost module call that encloses this scope.
    /// </summary>
    /// <returns>The children, or <see langword="null"/> when the scope is not inside a module call.</returns>
    public ScadChildren? FindChildren()
    {
        for (var scope = this; scope is not null; scope = scope.Parent)
        {
            if (scope.Children is not null)
            {
                return scope.Children;
            }
        }

        return null;
    }

    /// <summary>
    ///     Determines whether a name is a special variable.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the name starts with <c>$</c>.</returns>
    public static bool IsSpecial(string name) => name.Length > 0 && name[0] == '$';
}
