namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The <c>Data</c> namespace holds the rows a chart is drawn from: an immutable table of scalar values
///     (<c>DataTable</c>) and the loader that fills it from the inline data of a specification (<c>DataLoader</c>).
/// </summary>
/// <remarks>
///     The namespace is internal to the package. It never touches the file system or the network: a data source that
///     is not inline yields an empty table and is reported by the feature catalog instead. Every type is immutable and
///     safe for concurrent use once built, and all text formatting is culture invariant.
/// </remarks>
internal static class NamespaceDoc
{
}
