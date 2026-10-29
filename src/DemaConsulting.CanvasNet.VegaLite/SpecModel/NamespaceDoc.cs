namespace DemaConsulting.CanvasNet.VegaLite.SpecModel;

/// <summary>
///     The <c>SpecModel</c> namespace is everything about the text of a Vega-Lite specification and nothing about data
///     or pixels: it turns UTF-8 JSON into an immutable tree whose nodes know their JSON pointer, line and column
///     (<c>SpecParser</c>, <c>SpecNode</c>), checks the shape of the properties that the renderer depends on, lists
///     the recognized-but-unsupported constructs with stable <c>vl-*</c> identifiers (<c>FeatureCatalog</c>), and
///     resolves the effective configuration from the Vega-Lite version 6 defaults, the specification's
///     <c>config</c> and the caller's configuration (<c>ConfigResolver</c>).
/// </summary>
/// <remarks>
///     The namespace is internal to the package: the public surface is <c>VegaLiteDocument</c>. Every type is
///     immutable once built and safe for concurrent use, performs no I/O and never touches the network. The specification
///     parser also compiles the expressions of the supported sites (static parameters, <c>calculate</c> and <c>filter</c>
///     transforms and row-bound mark values) once, when the document is opened, and validates the shape of the supported
///     transforms and parameters (<c>SpecExpressions</c>, <c>ParameterGraph</c>); the catalog reports what is left out.
/// </remarks>
internal static class NamespaceDoc
{
}
