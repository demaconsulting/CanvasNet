namespace DemaConsulting.CanvasNet.VegaLite;

/// <summary>
///     The ways a Vega-Lite specification can name the data of a view.
/// </summary>
/// <remarks>
///     The kinds mirror the data sources of the Vega-Lite language that can be told apart without evaluating
///     anything, so tools can list what a chart needs (for example which files it refers to) before rendering it.
/// </remarks>
public enum VegaLiteDataKind
{
    /// <summary>Rows written in the specification itself (<c>data.values</c>).</summary>
    Inline = 0,

    /// <summary>A file or remote resource named by <c>data.url</c>.</summary>
    Url = 1,

    /// <summary>A named dataset (<c>data.name</c>) that the caller or the specification's <c>datasets</c> supplies.</summary>
    Name = 2,

    /// <summary>A generated number sequence (<c>data.sequence</c>).</summary>
    Sequence = 3,
}
