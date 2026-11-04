namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <content>
///     The compilation of the <c>labelExpr</c> of axes, legends and headers (a partial file of the specification parser unit).
/// </content>
internal sealed partial class SpecParser
{
    /// <summary>
    ///     Compiles the <c>labelExpr</c> of an <c>axis</c>, <c>legend</c> or <c>header</c> object, so that a malformed expression fails when the document is opened.
    /// </summary>
    /// <param name="guide">The guide object (or a configuration object that has the property).</param>
    /// <exception cref="VegaLiteSpecException">Thrown for a syntax error, a limit overflow or a reference to an unknown parameter.</exception>
    private void CompileLabelExpression(SpecNode guide)
    {
        if (guide["labelExpr"] is { IsString: true } text)
        {
            CompileExpression(text);
        }
    }

    /// <summary>
    ///     Compiles the <c>labelExpr</c> of the guide configurations of <c>config</c>: <c>axis</c> and the axis configurations (<c>axisX</c>, <c>axisBand</c> and so on), and
    ///     <c>header</c>, <c>headerRow</c>, <c>headerColumn</c> and <c>headerFacet</c>. Vega-Lite has no <c>labelExpr</c> for <c>config.legend</c>.
    /// </summary>
    /// <param name="config">The <c>config</c> object.</param>
    /// <exception cref="VegaLiteSpecException">Thrown for an invalid expression.</exception>
    private void CompileConfigLabelExpressions(SpecNode config)
    {
        if (!config.IsObject)
        {
            return;
        }

        foreach (var property in config.Properties)
        {
            if (property.IsObject && property.Name is { } name && (name.StartsWith("axis", StringComparison.Ordinal) || name is "header" or "headerRow" or "headerColumn" or "headerFacet"))
            {
                CompileLabelExpression(property);
            }
        }
    }
}
