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
///     safe for concurrent use once built; a loader and a resolver serve one call on one thread. The loader of a call is
///     also the context of the row transforms: a lookup loads its source through it (same sandbox, file cache and limits) and
///     the rows of derived tables are reserved in its row budget. A value can also be an immutable array or object, the
///     result of an expression. All text formatting is culture invariant. Calendar work (<c>TimeCalendar</c>, its d3-time
///     intervals, the Vega-Lite time units <c>TimeUnitSpec</c> and the datetime objects <c>DateTimeObject</c>) uses only the
///     time zone it is given (UTC by default), never the host's zone, clock or culture, and every date outside years 1 to
///     9999 is invalid.
/// </remarks>
internal static class NamespaceDoc
{
}
