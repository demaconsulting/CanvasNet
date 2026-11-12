namespace DemaConsulting.CanvasNet.Mermaid;

/// <summary>
///     Options that control how a <see cref="MermaidDocument"/> is opened.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="SourceName"/> appears in every <see cref="MermaidLocation"/> of the document, and
///         <see cref="Limits"/> bound what is checked while the text is read (<see cref="MermaidLimits.MaxTextSize"/>,
///         <see cref="MermaidLimits.MaxYamlDepth"/> and the structural limits of the diagram parser). Opening never
///         accesses another file and never uses the network.
///     </para>
///     <para>
///         Every property is validated when it is set (fail-closed) and the instance is immutable after
///         initialization, so an options object can be shared between threads and documents.
///     </para>
/// </remarks>
public sealed class MermaidOpenOptions
{
    /// <summary>The validated source name.</summary>
    private readonly string? _sourceName;

    /// <summary>
    ///     Gets the name placed in every <see cref="MermaidLocation.File"/> and diagnostic, or <see langword="null"/>
    ///     to leave locations unnamed (opening a file defaults it to the path).
    /// </summary>
    /// <exception cref="ArgumentException">Thrown on initialization when the value is empty or whitespace.</exception>
    public string? SourceName
    {
        get => _sourceName;
        init
        {
            if (value is not null && string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The value must be null or contain a non-whitespace character.", nameof(SourceName));
            }

            _sourceName = value;
        }
    }

    /// <summary>
    ///     Gets the limits that apply while the text is read, or <see langword="null"/> to use
    ///     <see cref="MermaidLimits.Default"/>.
    /// </summary>
    /// <remarks>
    ///     The limits of measuring and rendering are set separately in <see cref="MermaidRenderOptions.Limits"/>,
    ///     because the same document can be rendered under different budgets.
    /// </remarks>
    public MermaidLimits? Limits { get; init; }

    /// <summary>
    ///     Gets the limits that are in effect: <see cref="Limits"/>, or the defaults when none were given.
    /// </summary>
    internal MermaidLimits EffectiveLimits => Limits ?? MermaidLimits.Default;
}
