namespace DemaConsulting.CanvasNet.VegaLite.Data;

/// <summary>
///     The <c>Data</c> namespace holds the rows a chart is drawn from: an immutable table of scalar values
///     (<c>DataTable</c>), the loader that fills it from the data of a specification (<c>DataLoader</c>), and the
///     file and text readers behind it (<c>DataFileResolver</c>, <c>DelimitedTextParser</c>).
/// </summary>
/// <remarks>
///     The namespace is internal to the package and never uses the network. The only file access is the sandboxed
///     reading of the local data files a chart names (<c>DataFileResolver</c>): relative paths under the base directory
///     and data paths of the open options, or the caller's resolver callback, with every link rejected and every read
///     bounded; remote data (a URL with a scheme or an absolute path) is never opened and yields an empty table that the
///     feature catalog reports. Inline rows, local CSV, TSV, DSV and JSON files, named datasets and generated sequences
///     are read, with <c>format.parse</c> conversions and per-column type inference for delimited text. The table types are immutable and
///     safe for concurrent use once built; a loader and a resolver serve one call on one thread. All text formatting is
///     culture invariant and all dates are UTC.
/// </remarks>
internal static class NamespaceDoc
{
}
