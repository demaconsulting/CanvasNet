// cspell:ignore SFNT Sfnt sfnt glyf Glyf cmap Cmap loca Loca hmtx Hmtx hhea Hhea
// cspell:ignore maxp Maxp notdef codepoint codepoints subtable subtables subsetted
// cspell:ignore subsetting PPEM OTTO CFF charstring charstrings subr subrs
// cspell:ignore hintmask cntrmask rmoveto hmoveto vmoveto rlineto hlineto vlineto
// cspell:ignore rrcurveto hhcurveto vvcurveto hvcurveto vhcurveto callsubr callgsubr
// cspell:ignore endchar seac hstem vstem hstemhm vstemhm nominalWidthX defaultWidthX
// cspell:ignore ISOAdobe charsets psnames pstables bchar achar
// cspell:ignore quoteright exclamdown quoteleft quotedblleft guillemotleft guilsinglleft guilsinglright
// cspell:ignore endash daggerdbl periodcentered quotesinglbase quotedblbase quotedblright
// cspell:ignore guillemotright perthousand questiondown dotaccent hungarumlaut ogonek caron
// cspell:ignore emdash ordfeminine Lslash Oslash ordmasculine dotlessi lslash oslash
// cspell:ignore germandbls onesuperior logicalnot onehalf plusminus onequarter brokenbar
// cspell:ignore threequarters twosuperior threesuperior Aacute Acircumflex Adieresis Agrave
// cspell:ignore Aring Atilde Ccedilla Eacute Ecircumflex Edieresis Egrave Iacute Icircumflex
// cspell:ignore Idieresis Igrave Ntilde Oacute Ocircumflex Odieresis Ograve Otilde Scaron
// cspell:ignore Uacute Ucircumflex Udieresis Ugrave Yacute Ydieresis Zcaron aacute acircumflex
// cspell:ignore adieresis agrave aring atilde ccedilla eacute ecircumflex edieresis egrave
// cspell:ignore iacute icircumflex idieresis igrave ntilde oacute ocircumflex odieresis
// cspell:ignore ograve otilde scaron uacute ucircumflex udieresis ugrave yacute ydieresis
// cspell:ignore zcaron exclamsmall Hungarumlautsmall dollaroldstyle dollarsuperior
// cspell:ignore ampersandsmall Acutesmall parenleftsuperior parenrightsuperior twodotenleader
// cspell:ignore onedotenleader zerooldstyle oneoldstyle twooldstyle threeoldstyle fouroldstyle
// cspell:ignore fiveoldstyle sixoldstyle sevenoldstyle eightoldstyle nineoldstyle
// cspell:ignore commasuperior threequartersemdash periodsuperior questionsmall asuperior
// cspell:ignore bsuperior centsuperior dsuperior esuperior isuperior lsuperior msuperior
// cspell:ignore nsuperior osuperior rsuperior ssuperior tsuperior parenleftinferior
// cspell:ignore parenrightinferior Circumflexsmall hyphensuperior Gravesmall Asmall Bsmall
// cspell:ignore Csmall Dsmall Esmall Fsmall Gsmall Hsmall Ismall Jsmall Ksmall Lsmall Msmall
// cspell:ignore Nsmall Osmall Psmall Qsmall Rsmall Ssmall Tsmall Usmall Vsmall Wsmall Xsmall
// cspell:ignore Ysmall Zsmall colonmonetary onefitted Tildesmall exclamdownsmall centoldstyle
// cspell:ignore Lslashsmall Scaronsmall Zcaronsmall Dieresissmall Brevesmall Caronsmall
// cspell:ignore Dotaccentsmall Macronsmall figuredash hypheninferior Ogoneksmall Ringsmall
// cspell:ignore Cedillasmall questiondownsmall oneeighth threeeighths fiveeighths seveneighths
// cspell:ignore onethird twothirds zerosuperior foursuperior fivesuperior sixsuperior
// cspell:ignore sevensuperior eightsuperior ninesuperior zeroinferior oneinferior twoinferior
// cspell:ignore threeinferior fourinferior fiveinferior sixinferior seveninferior
// cspell:ignore eightinferior nineinferior centinferior dollarinferior periodinferior
// cspell:ignore commainferior Agravesmall Aacutesmall Acircumflexsmall Atildesmall
// cspell:ignore Adieresissmall Aringsmall Ccedillasmall Egravesmall Eacutesmall
// cspell:ignore Ecircumflexsmall Edieresissmall Igravesmall Iacutesmall Icircumflexsmall
// cspell:ignore Idieresissmall Ethsmall Ntildesmall Ogravesmall Oacutesmall Ocircumflexsmall
// cspell:ignore Otildesmall Odieresissmall Oslashsmall Ugravesmall Uacutesmall
// cspell:ignore Ucircumflexsmall Udieresissmall Yacutesmall Thornsmall Ydieresissmall
using System.Text;
using Path = DemaConsulting.CanvasNet.Geometry.Path;

namespace DemaConsulting.CanvasNet.Fonts;

/// <summary>
///     Parses a font's <c>CFF </c> (Compact Font Format) table - header, Name/Top DICT/String/
///     Global Subr INDEXes, Private DICT, Local Subr INDEX, and CharStrings INDEX - and decodes
///     individual glyph outlines from Type 2 charstring bytecode into <see cref="Path"/> geometry
///     via <see cref="CffCharstringInterpreter"/>.
/// </summary>
/// <remarks>
///     <para>
///     Every structural offset inside a CFF table is relative to the start of the CFF table's own
///     byte range, never the overall font file - this type therefore slices the CFF table's bytes
///     into their own private array at construction time, so every offset it subsequently reads
///     or hands to <see cref="CffCharstringInterpreter"/> is relative to that array's start
///     (position <c>0</c>), matching the format's own convention exactly.
///     </para>
///     <para>
///     Only non-CID-keyed CFF fonts are supported: a Top DICT declaring the <c>ROS</c> operator
///     (CID-keyed font identification) is rejected with <see cref="InvalidDataException"/> rather
///     than attempting unsupported <c>FDArray</c>/<c>FDSelect</c> parsing - mirroring
///     <see cref="TrueTypeFont"/>'s existing "reject rather than silently mis-parse" posture for
///     unsupported <c>maxp</c> versions. A Top DICT is also required to resolve to exactly one
///     entry (the non-CID-keyed case always has exactly one).
///     </para>
///     <para>
///     Like <see cref="GlyfLocaReader"/>, no individual glyph's charstring is decoded until
///     <see cref="GetGlyphOutline"/> is called for that specific index - one corrupt glyph's
///     charstring bytecode does not prevent using every other, otherwise well-formed, glyph in
///     the font. <see cref="GetAdvanceWidth"/> is equally lazy (it decodes the same charstring a
///     second time, discarding the outline), deliberately trading a little redundant work for
///     keeping this "nothing decoded until asked for" posture uniform across both members.
///     </para>
///     <para>
///     The Top DICT's <c>charset</c> operator (operator <c>15</c>) - which maps each glyph index
///     to a String ID (SID) identifying its PostScript glyph name - is resolved by
///     <see cref="TryGetGlyphIndex"/> for exactly three cases: the operator is absent, or its
///     value is the predefined ID <c>0</c> (both mean the ISOAdobe/Standard charset: glyph
///     <c>i</c>'s SID is simply <c>i</c>, for <c>i</c> in <c>1..GlyphCount-1</c>); or its value is
///     greater than <c>2</c>, a byte offset (relative to this table's own start) to a custom
///     charset table in format <c>0</c> (a flat array of 2-byte SIDs), format <c>1</c> (ranges of
///     <c>(first SID: 2 bytes, nLeft: 1 byte)</c>), or format <c>2</c> (the same as format
///     <c>1</c>, but <c>nLeft</c> is 2 bytes). The predefined Expert (<c>1</c>) and ExpertSubset
///     (<c>2</c>) charsets are a deliberate, documented scope boundary: a glyph index is still
///     decodable via <see cref="GetGlyphOutline"/>, but <see cref="TryGetGlyphIndex"/> can never
///     resolve a glyph name against either of them, returning <see langword="false"/> rather than
///     throwing (the same "absent optional data => benign negative result" contract
///     <see cref="TrueTypeFont.GetKerning"/> documents for its own optional tables). A custom
///     charset table in any other format byte is rejected with <see cref="InvalidDataException"/>.
///     Every SID a resolved charset produces is itself resolved to a glyph name using either the
///     fixed Standard Strings table (SID <c>0</c>-<c>390</c>, see <see cref="StandardStrings"/>)
///     or the font's own String INDEX (SID <c>391</c> and above, indexed as <c>SID - 391</c>).
///     </para>
///     <para>
///     The Private DICT's <c>defaultWidthX</c> (operator <c>20</c>) and <c>nominalWidthX</c>
///     (operator <c>21</c>) operands - both <c>0</c> when no Private DICT is present, or when
///     present but the operator itself is absent, per the CFF specification's own documented
///     defaults - combine with each glyph's own charstring-encoded width delta (see
///     <see cref="CffCharstringInterpreter"/>) to resolve <see cref="GetAdvanceWidth"/>.
///     </para>
///     <para>
///     A glyph whose charstring uses the deprecated 4-operand <c>seac</c>-style form of
///     <c>endchar</c> is supported: both <see cref="GetGlyphOutline"/> and
///     <see cref="GetAdvanceWidth"/> pass <see cref="ResolveSeacComponent"/> into
///     <see cref="CffCharstringInterpreter.Decode"/> as its resolver callback, so
///     <see cref="CffCharstringInterpreter"/> can compose the base/accent outline itself.
///     <see cref="ResolveSeacComponent"/> maps an Adobe StandardEncoding code to a glyph name via
///     <see cref="CffStandardEncoding"/>, resolves that name to a glyph index in <em>this same
///     font</em> via <see cref="TryGetGlyphIndex"/>, and decodes that glyph's own charstring
///     <em>without</em> supplying a resolver of its own - so a seac component glyph that is
///     itself (illegally) seac-style fails with <see cref="InvalidDataException"/> rather than
///     recursing further; no separate depth counter is needed for this, since the omission is
///     structural. An undefined StandardEncoding code, or one whose glyph name is not present in
///     this font's own charset, is likewise rejected with <see cref="InvalidDataException"/>
///     rather than silently dropping the accent or producing a partial outline.
///     </para>
/// </remarks>
internal sealed class CffTable : IGlyphOutlineSource
{
    private readonly byte[] _cffData;
    private readonly (int Offset, int Length)[] _charStrings;
    private readonly (int Offset, int Length)[] _globalSubrs;
    private readonly (int Offset, int Length)[] _localSubrs;
    private readonly double _defaultWidthX;
    private readonly double _nominalWidthX;
    private readonly IReadOnlyDictionary<string, int> _nameToGlyphIndex;

    /// <summary>
    ///     The <c>ROS</c> Top DICT operator (escape operator <c>12 30</c>), identifying a
    ///     CID-keyed CFF font - explicitly rejected (see this class's remarks).
    /// </summary>
    private const int RosOperator = 1230;

    /// <summary>
    ///     The <c>CharStrings</c> Top DICT operator, giving the offset (relative to the start of
    ///     the CFF table) of the CharStrings INDEX.
    /// </summary>
    private const int CharStringsOperator = 17;

    /// <summary>
    ///     The <c>Private</c> Top DICT operator, giving the Private DICT's <c>(size, offset)</c>
    ///     pair (offset relative to the start of the CFF table).
    /// </summary>
    private const int PrivateOperator = 18;

    /// <summary>
    ///     The <c>Subrs</c> Private DICT operator, giving the Local Subr INDEX offset relative to
    ///     the start of the Private DICT itself (not the CFF table).
    /// </summary>
    private const int SubrsOperator = 19;

    /// <summary>
    ///     The <c>defaultWidthX</c> Private DICT operator - see this class's remarks.
    /// </summary>
    private const int DefaultWidthXOperator = 20;

    /// <summary>
    ///     The <c>nominalWidthX</c> Private DICT operator - see this class's remarks.
    /// </summary>
    private const int NominalWidthXOperator = 21;

    /// <summary>
    ///     The <c>charset</c> Top DICT operator, giving either a predefined charset ID
    ///     (<c>0</c>/<c>1</c>/<c>2</c>) or a byte offset (relative to the start of the CFF table)
    ///     to a custom charset table - see this class's remarks.
    /// </summary>
    private const int CharsetOperator = 15;

    /// <summary>
    ///     The predefined ISOAdobe/Standard charset ID: glyph <c>i</c>'s SID is <c>i</c>, for
    ///     <c>i</c> in <c>1..GlyphCount-1</c> - see this class's remarks.
    /// </summary>
    private const int IsoAdobeCharsetId = 0;

    /// <summary>
    ///     The predefined Expert charset ID - a deliberate, documented scope boundary (see this
    ///     class's remarks); glyphs using it are decodable by index but never resolvable by name.
    /// </summary>
    private const int ExpertCharsetId = 1;

    /// <summary>
    ///     The predefined ExpertSubset charset ID - see <see cref="ExpertCharsetId"/>'s remarks.
    /// </summary>
    private const int ExpertSubsetCharsetId = 2;

    /// <summary>
    ///     The number of standard (predefined, universally-known) CFF/Type 2 glyph name strings -
    ///     SIDs <c>0</c>-<c>390</c>. A SID of <see cref="StandardStrings"/>.Length or greater
    ///     instead indexes the font's own String INDEX, at <c>SID - StandardStrings.Length</c>.
    /// </summary>
    /// <remarks>
    ///     Transcribed from Adobe Technical Note #5176 (The Compact Font Format Specification)
    ///     Appendix A, "Standard Strings" - cross-checked against FreeType's independently
    ///     published <c>ft_standard_glyph_names</c>/<c>ft_sid_names</c> tables
    ///     (<c>src/psnames/pstables.h</c>), itself a direct encoding of the same specification
    ///     appendix - used here only as a cross-check source, not copied code (these are the
    ///     specification's own fixed, universally-known string values, not copyrightable
    ///     expression).
    /// </remarks>
    internal static readonly string[] StandardStrings =
    [
        ".notdef", "space", "exclam", "quotedbl", "numbersign", "dollar", "percent", "ampersand", "quoteright",
        "parenleft", "parenright", "asterisk", "plus", "comma", "hyphen", "period", "slash", "zero", "one", "two",
        "three", "four", "five", "six", "seven", "eight", "nine", "colon", "semicolon", "less", "equal", "greater",
        "question", "at", "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R",
        "S", "T", "U", "V", "W", "X", "Y", "Z", "bracketleft", "backslash", "bracketright", "asciicircum",
        "underscore", "quoteleft", "a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k", "l", "m", "n", "o", "p",
        "q", "r", "s", "t", "u", "v", "w", "x", "y", "z", "braceleft", "bar", "braceright", "asciitilde",
        "exclamdown", "cent", "sterling", "fraction", "yen", "florin", "section", "currency", "quotesingle",
        "quotedblleft", "guillemotleft", "guilsinglleft", "guilsinglright", "fi", "fl", "endash", "dagger",
        "daggerdbl", "periodcentered", "paragraph", "bullet", "quotesinglbase", "quotedblbase", "quotedblright",
        "guillemotright", "ellipsis", "perthousand", "questiondown", "grave", "acute", "circumflex", "tilde",
        "macron", "breve", "dotaccent", "dieresis", "ring", "cedilla", "hungarumlaut", "ogonek", "caron", "emdash",
        "AE", "ordfeminine", "Lslash", "Oslash", "OE", "ordmasculine", "ae", "dotlessi", "lslash", "oslash", "oe",
        "germandbls", "onesuperior", "logicalnot", "mu", "trademark", "Eth", "onehalf", "plusminus", "Thorn",
        "onequarter", "divide", "brokenbar", "degree", "thorn", "threequarters", "twosuperior", "registered",
        "minus", "eth", "multiply", "threesuperior", "copyright", "Aacute", "Acircumflex", "Adieresis", "Agrave",
        "Aring", "Atilde", "Ccedilla", "Eacute", "Ecircumflex", "Edieresis", "Egrave", "Iacute", "Icircumflex",
        "Idieresis", "Igrave", "Ntilde", "Oacute", "Ocircumflex", "Odieresis", "Ograve", "Otilde", "Scaron",
        "Uacute", "Ucircumflex", "Udieresis", "Ugrave", "Yacute", "Ydieresis", "Zcaron", "aacute", "acircumflex",
        "adieresis", "agrave", "aring", "atilde", "ccedilla", "eacute", "ecircumflex", "edieresis", "egrave",
        "iacute", "icircumflex", "idieresis", "igrave", "ntilde", "oacute", "ocircumflex", "odieresis", "ograve",
        "otilde", "scaron", "uacute", "ucircumflex", "udieresis", "ugrave", "yacute", "ydieresis", "zcaron",
        "exclamsmall", "Hungarumlautsmall", "dollaroldstyle", "dollarsuperior", "ampersandsmall", "Acutesmall",
        "parenleftsuperior", "parenrightsuperior", "twodotenleader", "onedotenleader", "zerooldstyle",
        "oneoldstyle", "twooldstyle", "threeoldstyle", "fouroldstyle", "fiveoldstyle", "sixoldstyle",
        "sevenoldstyle", "eightoldstyle", "nineoldstyle", "commasuperior", "threequartersemdash",
        "periodsuperior", "questionsmall", "asuperior", "bsuperior", "centsuperior", "dsuperior", "esuperior",
        "isuperior", "lsuperior", "msuperior", "nsuperior", "osuperior", "rsuperior", "ssuperior", "tsuperior",
        "ff", "ffi", "ffl", "parenleftinferior", "parenrightinferior", "Circumflexsmall", "hyphensuperior",
        "Gravesmall", "Asmall", "Bsmall", "Csmall", "Dsmall", "Esmall", "Fsmall", "Gsmall", "Hsmall", "Ismall",
        "Jsmall", "Ksmall", "Lsmall", "Msmall", "Nsmall", "Osmall", "Psmall", "Qsmall", "Rsmall", "Ssmall",
        "Tsmall", "Usmall", "Vsmall", "Wsmall", "Xsmall", "Ysmall", "Zsmall", "colonmonetary", "onefitted",
        "rupiah", "Tildesmall", "exclamdownsmall", "centoldstyle", "Lslashsmall", "Scaronsmall", "Zcaronsmall",
        "Dieresissmall", "Brevesmall", "Caronsmall", "Dotaccentsmall", "Macronsmall", "figuredash",
        "hypheninferior", "Ogoneksmall", "Ringsmall", "Cedillasmall", "questiondownsmall", "oneeighth",
        "threeeighths", "fiveeighths", "seveneighths", "onethird", "twothirds", "zerosuperior", "foursuperior",
        "fivesuperior", "sixsuperior", "sevensuperior", "eightsuperior", "ninesuperior", "zeroinferior",
        "oneinferior", "twoinferior", "threeinferior", "fourinferior", "fiveinferior", "sixinferior",
        "seveninferior", "eightinferior", "nineinferior", "centinferior", "dollarinferior", "periodinferior",
        "commainferior", "Agravesmall", "Aacutesmall", "Acircumflexsmall", "Atildesmall", "Adieresissmall",
        "Aringsmall", "AEsmall", "Ccedillasmall", "Egravesmall", "Eacutesmall", "Ecircumflexsmall",
        "Edieresissmall", "Igravesmall", "Iacutesmall", "Icircumflexsmall", "Idieresissmall", "Ethsmall",
        "Ntildesmall", "Ogravesmall", "Oacutesmall", "Ocircumflexsmall", "Otildesmall", "Odieresissmall",
        "OEsmall", "Oslashsmall", "Ugravesmall", "Uacutesmall", "Ucircumflexsmall", "Udieresissmall",
        "Yacutesmall", "Thornsmall", "Ydieresissmall", "001.000", "001.001", "001.002", "001.003", "Black",
        "Bold", "Book", "Light", "Medium", "Regular", "Roman", "Semibold",
    ];

    /// <summary>
    ///     The total number of glyphs described by the CharStrings INDEX.
    /// </summary>
    public int GlyphCount => _charStrings.Length;

    private CffTable(
        byte[] cffData,
        (int Offset, int Length)[] charStrings,
        (int Offset, int Length)[] globalSubrs,
        (int Offset, int Length)[] localSubrs,
        double defaultWidthX,
        double nominalWidthX,
        IReadOnlyDictionary<string, int> nameToGlyphIndex)
    {
        _cffData = cffData;
        _charStrings = charStrings;
        _globalSubrs = globalSubrs;
        _localSubrs = localSubrs;
        _defaultWidthX = defaultWidthX;
        _nominalWidthX = nominalWidthX;
        _nameToGlyphIndex = nameToGlyphIndex;
    }

    /// <summary>
    ///     Reports whether <paramref name="data"/> begins with a structurally plausible bare CFF
    ///     header - exactly the same bounds checks <see cref="Parse(byte[], int, int)"/> itself
    ///     applies to its own leading header bytes (major version <c>1</c>, and a declared
    ///     <c>headerSize</c> not exceeding the data's own length), but without parsing any
    ///     further INDEX/DICT structure. A lightweight, non-throwing shape sniff distinguishing a
    ///     bare (standalone) CFF program from an SFNT-wrapped one (see
    ///     <see cref="SfntContainer.LooksLikeSfnt"/>), used by <c>Pdf.PdfDocument</c>'s own
    ///     <c>/FontFile3</c> shape-sniffing dispatch.
    /// </summary>
    /// <param name="data">
    ///     The candidate font program bytes (the complete bare-CFF stream, not an SFNT table
    ///     directory entry's own sliced-out range).
    /// </param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="data"/> is at least 4 bytes long, its first
    ///     byte (the CFF header's major version) is <c>1</c>, and its third byte (the header's
    ///     own declared <c>headerSize</c>) does not exceed <paramref name="data"/>'s length;
    ///     otherwise, <see langword="false"/>. Deliberately shallow - a value recognized here can
    ///     still fail <see cref="Parse(byte[], int, int)"/>'s own, stricter validation (for
    ///     example a malformed INDEX/DICT, or a CID-keyed Top DICT).
    /// </returns>
    public static bool LooksLikeCffHeader(byte[] data) =>
        data.Length >= 4 && data[0] == 1 && data[2] <= data.Length;

    /// <summary>
    ///     Parses a font's <c>CFF </c> table.
    /// </summary>
    /// <param name="data">The complete font file contents.</param>
    /// <param name="tableOffset">The offset of the <c>CFF </c> table within <paramref name="data"/>.</param>
    /// <param name="tableLength">The length of the <c>CFF </c> table.</param>
    /// <returns>A new <see cref="CffTable"/> ready to decode glyph outlines.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the CFF header, an INDEX, or a DICT is malformed or truncated, when the
    ///     header's major version is not <c>1</c>, when the Top DICT INDEX does not resolve to
    ///     exactly one entry, when the Top DICT declares a CID-keyed (<c>ROS</c>) font, when the
    ///     required <c>CharStrings</c> operator is missing, or when the CharStrings INDEX
    ///     contains zero glyphs.
    /// </exception>
    public static CffTable Parse(byte[] data, int tableOffset, int tableLength)
    {
        if (tableLength < 4)
        {
            throw new InvalidDataException("The 'CFF ' table is too short to contain a CFF header.");
        }

        var cffData = new byte[tableLength];
        Array.Copy(data, tableOffset, cffData, 0, tableLength);

        var majorVersion = cffData[0];
        if (majorVersion != 1)
        {
            throw new InvalidDataException($"Unsupported CFF header major version ({majorVersion}).");
        }

        var headerSize = cffData[2];
        if (headerSize > cffData.Length)
        {
            throw new InvalidDataException("The CFF header size exceeds the table bounds.");
        }

        var pos = (int)headerSize;
        ParseIndex(cffData, ref pos); // Name INDEX - parsed only to skip past it; not exposed
        var topDictIndex = ParseIndex(cffData, ref pos);
        if (topDictIndex.Length != 1)
        {
            throw new InvalidDataException("CFF fonts with other than exactly one Top DICT are not supported.");
        }

        var stringIndex = ParseIndex(cffData, ref pos);
        var globalSubrs = ParseIndex(cffData, ref pos);

        var topDict = ParseDict(cffData, topDictIndex[0].Offset, topDictIndex[0].Length);
        if (topDict.ContainsKey(RosOperator))
        {
            throw new InvalidDataException("CID-keyed CFF fonts are not supported.");
        }

        if (!topDict.TryGetValue(CharStringsOperator, out var charStringsOperands) || charStringsOperands.Count == 0)
        {
            throw new InvalidDataException("The CFF Top DICT is missing the required 'CharStrings' operator.");
        }

        var localSubrs = Array.Empty<(int Offset, int Length)>();
        var defaultWidthX = 0.0;
        var nominalWidthX = 0.0;
        if (topDict.TryGetValue(PrivateOperator, out var privateOperands))
        {
            if (privateOperands.Count != 2)
            {
                throw new InvalidDataException("The CFF Top DICT's 'Private' operator must have exactly two operands.");
            }

            var privateSize = (int)privateOperands[0];
            var privateOffset = (int)privateOperands[1];
            if (privateSize < 0 || privateOffset < 0 || checked((long)privateOffset + privateSize) > cffData.Length)
            {
                throw new InvalidDataException("The CFF Private DICT offset/size exceeds the table bounds.");
            }

            var privateDict = ParseDict(cffData, privateOffset, privateSize);
            if (privateDict.TryGetValue(SubrsOperator, out var subrsOperands) && subrsOperands.Count > 0)
            {
                var localSubrPos = privateOffset + (int)subrsOperands[^1];
                localSubrs = ParseIndex(cffData, ref localSubrPos);
            }

            if (privateDict.TryGetValue(DefaultWidthXOperator, out var dwxOperands) && dwxOperands.Count > 0)
            {
                defaultWidthX = dwxOperands[^1];
            }

            if (privateDict.TryGetValue(NominalWidthXOperator, out var nwxOperands) && nwxOperands.Count > 0)
            {
                nominalWidthX = nwxOperands[^1];
            }
        }

        var charStringsPos = (int)charStringsOperands[^1];
        var charStrings = ParseIndex(cffData, ref charStringsPos);
        if (charStrings.Length == 0)
        {
            throw new InvalidDataException("The CFF 'CharStrings' INDEX contains no glyphs.");
        }

        var charsetSids = ParseCharset(topDict, cffData, charStrings.Length);
        var nameToGlyphIndex = BuildNameToGlyphIndex(charsetSids, stringIndex, cffData);

        return new CffTable(cffData, charStrings, globalSubrs, localSubrs, defaultWidthX, nominalWidthX, nameToGlyphIndex);
    }

    /// <summary>
    ///     Resolves the Top DICT's <c>charset</c> operator (operator <c>15</c>) to a per-glyph
    ///     array of String IDs (SIDs) - see this class's remarks for the complete set of
    ///     supported predefined IDs and custom table formats.
    /// </summary>
    /// <param name="topDict">The parsed Top DICT.</param>
    /// <param name="cffData">The complete CFF table bytes.</param>
    /// <param name="glyphCount">The number of glyphs declared by the CharStrings INDEX.</param>
    /// <returns>
    ///     An array of length <paramref name="glyphCount"/>, where element <c>0</c> is always
    ///     <c>0</c> (the <c>.notdef</c> SID) and each other element is either the glyph's
    ///     resolved SID, or <c>-1</c> if the glyph's name cannot be resolved (always and only for
    ///     the predefined Expert/ExpertSubset charsets - see this class's remarks).
    /// </returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when a custom charset table's byte offset is out of bounds, or its format byte
    ///     is not <c>0</c>, <c>1</c>, or <c>2</c>.
    /// </exception>
    private static int[] ParseCharset(Dictionary<int, List<double>> topDict, byte[] cffData, int glyphCount)
    {
        var sids = new int[glyphCount];

        var charsetId = IsoAdobeCharsetId;
        if (topDict.TryGetValue(CharsetOperator, out var charsetOperands) && charsetOperands.Count > 0)
        {
            charsetId = (int)charsetOperands[^1];
        }

        switch (charsetId)
        {
            case IsoAdobeCharsetId:
                for (var i = 1; i < glyphCount; i++)
                {
                    sids[i] = i;
                }

                return sids;

            case ExpertCharsetId:
            case ExpertSubsetCharsetId:
                // Deliberate, documented scope boundary - see this class's remarks.
                for (var i = 1; i < glyphCount; i++)
                {
                    sids[i] = -1;
                }

                return sids;
        }

        var pos = charsetId;
        EnsureAvailable(cffData, pos, 1);
        var format = cffData[pos];
        pos++;

        var glyphIndex = 1;
        switch (format)
        {
            case 0:
                while (glyphIndex < glyphCount)
                {
                    EnsureAvailable(cffData, pos, 2);
                    sids[glyphIndex] = SfntContainer.ReadUInt16(cffData, pos);
                    pos += 2;
                    glyphIndex++;
                }

                break;

            case 1:
                while (glyphIndex < glyphCount)
                {
                    EnsureAvailable(cffData, pos, 3);
                    var first = SfntContainer.ReadUInt16(cffData, pos);
                    var nLeft = cffData[pos + 2];
                    pos += 3;
                    for (var i = 0; i <= nLeft && glyphIndex < glyphCount; i++)
                    {
                        sids[glyphIndex] = first + i;
                        glyphIndex++;
                    }
                }

                break;

            case 2:
                while (glyphIndex < glyphCount)
                {
                    EnsureAvailable(cffData, pos, 4);
                    var first = SfntContainer.ReadUInt16(cffData, pos);
                    var nLeft = SfntContainer.ReadUInt16(cffData, pos + 2);
                    pos += 4;
                    for (var i = 0; i <= nLeft && glyphIndex < glyphCount; i++)
                    {
                        sids[glyphIndex] = first + i;
                        glyphIndex++;
                    }
                }

                break;

            default:
                throw new InvalidDataException($"Unsupported CFF charset table format ({format}).");
        }

        return sids;
    }

    /// <summary>
    ///     Builds a map from PostScript glyph name to glyph index, by resolving each glyph's SID
    ///     (from <paramref name="charsetSids"/>) to a name using either the fixed
    ///     <see cref="StandardStrings"/> table (SID <c>0</c>-<c>390</c>) or
    ///     <paramref name="stringIndex"/> (SID <c>391</c> and above). A SID of <c>-1</c> (see
    ///     <see cref="ParseCharset"/>) is skipped - that glyph simply cannot be resolved by name.
    /// </summary>
    private static Dictionary<string, int> BuildNameToGlyphIndex(
        int[] charsetSids,
        (int Offset, int Length)[] stringIndex,
        byte[] cffData)
    {
        var nameToGlyphIndex = new Dictionary<string, int>();
        for (var glyphIndex = 0; glyphIndex < charsetSids.Length; glyphIndex++)
        {
            var sid = charsetSids[glyphIndex];
            if (sid < 0)
            {
                continue;
            }

            string name;
            if (sid < StandardStrings.Length)
            {
                name = StandardStrings[sid];
            }
            else
            {
                var customIndex = sid - StandardStrings.Length;
                if (customIndex >= stringIndex.Length)
                {
                    continue;
                }

                var entry = stringIndex[customIndex];
                name = Encoding.Latin1.GetString(cffData, entry.Offset, entry.Length);
            }

            nameToGlyphIndex.TryAdd(name, glyphIndex);
        }

        return nameToGlyphIndex;
    }

    /// <summary>
    ///     Attempts to resolve a PostScript glyph name to its glyph index, using the Top DICT's
    ///     <c>charset</c> operator - see this class's remarks for the complete set of supported
    ///     predefined IDs and custom table formats.
    /// </summary>
    /// <param name="glyphName">The PostScript glyph name to resolve (for example <c>A</c>).</param>
    /// <param name="glyphIndex">The resolved glyph index, or <c>0</c> if not found.</param>
    /// <returns>
    ///     <see langword="true"/> if <paramref name="glyphName"/> was resolved; otherwise
    ///     <see langword="false"/> (including, always, for any glyph whose charset is the
    ///     predefined Expert or ExpertSubset charset - see this class's remarks).
    /// </returns>
    internal bool TryGetGlyphIndex(string glyphName, out int glyphIndex) =>
        _nameToGlyphIndex.TryGetValue(glyphName, out glyphIndex);

    /// <summary>
    ///     Resolves a glyph's advance width, decoding its Type 2 charstring (see
    ///     <see cref="CffCharstringInterpreter"/>) a second time - deliberately, to keep this
    ///     table's "nothing decoded until asked for" laziness uniform with
    ///     <see cref="GetGlyphOutline"/> - and combining its charstring-encoded width delta (if
    ///     any) with the Private DICT's <c>defaultWidthX</c>/<c>nominalWidthX</c> operands.
    /// </summary>
    /// <param name="glyphIndex">The glyph index to resolve.</param>
    /// <returns>The glyph's advance width, in raw font design units.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="glyphIndex"/> is outside <c>[0, GlyphCount)</c>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the glyph's charstring bytecode is malformed, truncated, uses an
    ///     unsupported operator, or exceeds an internal recursion/step bound; or, for a seac-style
    ///     glyph, when a StandardEncoding code is undefined, its glyph name is absent from this
    ///     font's own charset, or the resolved component glyph is itself (illegally) seac-style.
    ///     See <see cref="CffCharstringInterpreter"/> and <see cref="ResolveSeacComponent"/> for
    ///     the complete set of rejection conditions.
    /// </exception>
    internal double GetAdvanceWidth(int glyphIndex)
    {
        if (glyphIndex < 0 || glyphIndex >= GlyphCount)
        {
            throw new ArgumentOutOfRangeException(nameof(glyphIndex), glyphIndex, "Glyph index is out of range.");
        }

        return CffCharstringInterpreter.Decode(
            _cffData, _charStrings[glyphIndex], _globalSubrs, _localSubrs, _defaultWidthX, _nominalWidthX,
            ResolveSeacComponent).Width;
    }

    /// <summary>
    ///     Decodes a single glyph's outline from Type 2 charstring bytecode.
    /// </summary>
    /// <param name="glyphIndex">The glyph index to decode.</param>
    /// <returns>
    ///     The glyph's outline in raw font design units, or <see cref="Path.Empty"/> for a glyph
    ///     with no contour data (for example <c>space</c>), or the composed base+accent outline
    ///     for a seac-style glyph (see this class's remarks).
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     Thrown when <paramref name="glyphIndex"/> is outside <c>[0, GlyphCount)</c>.
    /// </exception>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the glyph's charstring bytecode is malformed, truncated, uses an
    ///     unsupported operator, or exceeds an internal recursion/step bound; or, for a seac-style
    ///     glyph, when a StandardEncoding code is undefined, its glyph name is absent from this
    ///     font's own charset, or the resolved component glyph is itself (illegally) seac-style.
    ///     See <see cref="CffCharstringInterpreter"/> and <see cref="ResolveSeacComponent"/> for
    ///     the complete set of rejection conditions.
    /// </exception>
    public Path GetGlyphOutline(int glyphIndex)
    {
        if (glyphIndex < 0 || glyphIndex >= GlyphCount)
        {
            throw new ArgumentOutOfRangeException(nameof(glyphIndex), glyphIndex, "Glyph index is out of range.");
        }

        return CffCharstringInterpreter.Decode(
            _cffData, _charStrings[glyphIndex], _globalSubrs, _localSubrs, _defaultWidthX, _nominalWidthX,
            ResolveSeacComponent).Outline;
    }

    /// <summary>
    ///     Resolves a seac-style <c>endchar</c>'s <c>bchar</c>/<c>achar</c> operand (an Adobe
    ///     StandardEncoding code) to that code's glyph's own already-decoded outline, for use as
    ///     <see cref="CffCharstringInterpreter.Decode"/>'s <c>resolveStandardEncodedGlyph</c>
    ///     callback from both <see cref="GetGlyphOutline"/> and <see cref="GetAdvanceWidth"/>.
    /// </summary>
    /// <param name="standardEncodingCode">The Adobe StandardEncoding code (<c>0</c>-<c>255</c>).</param>
    /// <returns>The resolved component glyph's own decoded outline.</returns>
    /// <exception cref="InvalidDataException">
    ///     Thrown when <paramref name="standardEncodingCode"/> is outside <c>0</c>-<c>255</c> or
    ///     has no defined <see cref="CffStandardEncoding"/> glyph name, when that glyph name is
    ///     not present in this font's own charset (<see cref="TryGetGlyphIndex"/> returns
    ///     <see langword="false"/>), or when the resolved component glyph's own charstring is
    ///     malformed, unsupported, or is itself (illegally) a seac-style <c>endchar</c> - this
    ///     method deliberately omits a resolver on its own recursive
    ///     <see cref="CffCharstringInterpreter.Decode"/> call, so a doubly-nested seac composition
    ///     fails here rather than recursing further.
    /// </exception>
    private Path ResolveSeacComponent(int standardEncodingCode)
    {
        if (standardEncodingCode < 0 || standardEncodingCode >= CffStandardEncoding.CodeToGlyphName.Length)
        {
            throw new InvalidDataException(
                $"CFF seac-style 'endchar' references an out-of-range StandardEncoding code ({standardEncodingCode}).");
        }

        var glyphName = CffStandardEncoding.CodeToGlyphName[standardEncodingCode];
        if (glyphName is null)
        {
            throw new InvalidDataException(
                $"CFF seac-style 'endchar' references a StandardEncoding code ({standardEncodingCode}) with no defined glyph name.");
        }

        if (!TryGetGlyphIndex(glyphName, out var glyphIndex))
        {
            throw new InvalidDataException(
                $"CFF seac-style 'endchar' references glyph '{glyphName}' (StandardEncoding code {standardEncodingCode}), which is not present in this font's charset.");
        }

        return CffCharstringInterpreter.Decode(
            _cffData, _charStrings[glyphIndex], _globalSubrs, _localSubrs, _defaultWidthX, _nominalWidthX).Outline;
    }

    /// <summary>
    ///     Parses a CFF INDEX structure starting at <paramref name="pos"/>, returning every
    ///     entry's absolute <c>(Offset, Length)</c> within <paramref name="data"/> and advancing
    ///     <paramref name="pos"/> to just past the INDEX.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when the INDEX's count/offset-size header, offset array, or object data region
    ///     is truncated, declares an unsupported offset size, or contains non-monotonically
    ///     non-decreasing offsets.
    /// </exception>
    private static (int Offset, int Length)[] ParseIndex(byte[] data, ref int pos)
    {
        EnsureAvailable(data, pos, 2);
        var count = SfntContainer.ReadUInt16(data, pos);
        pos += 2;
        if (count == 0)
        {
            return [];
        }

        EnsureAvailable(data, pos, 1);
        var offSize = data[pos];
        pos += 1;
        if (offSize is < 1 or > 4)
        {
            throw new InvalidDataException($"CFF INDEX declares an unsupported offset size ({offSize}).");
        }

        var offsetsCount = count + 1;
        EnsureAvailable(data, pos, offsetsCount * offSize);
        var offsets = new int[offsetsCount];
        for (var i = 0; i < offsetsCount; i++)
        {
            long value = 0;
            for (var b = 0; b < offSize; b++)
            {
                value = (value << 8) | data[pos + i * offSize + b];
            }

            offsets[i] = (int)value;
        }

        pos += offsetsCount * offSize;
        var dataStart = pos;

        var result = new (int Offset, int Length)[count];
        for (var i = 0; i < count; i++)
        {
            var start = offsets[i] - 1;
            var end = offsets[i + 1] - 1;
            if (start < 0 || end < start)
            {
                throw new InvalidDataException("CFF INDEX offsets are not monotonically non-decreasing.");
            }

            var absoluteStart = dataStart + start;
            var length = end - start;
            EnsureAvailable(data, absoluteStart, length);
            result[i] = (absoluteStart, length);
        }

        pos = dataStart + (offsets[^1] - 1);
        return result;
    }

    /// <summary>
    ///     Parses a CFF DICT (Top DICT or Private DICT) into a map from operator code to its
    ///     operand list. Two-byte escape operators (<c>12 XX</c>) are keyed as <c>1200 + XX</c>.
    ///     Real-number operands are consumed (so parsing stays correctly positioned) but decoded
    ///     as <c>0</c> rather than interpreted. Every operator this type reads whose value could
    ///     be meaningfully non-zero (<c>CharStrings</c>, <c>Private</c>, <c>Subrs</c>,
    ///     <c>ROS</c>, <c>charset</c>) is always encoded as an integer in practice, never a real
    ///     number, per the CFF specification. <c>defaultWidthX</c>/<c>nominalWidthX</c> are the
    ///     one documented exception: the specification permits (but no known font producer uses)
    ///     a real-number encoding for either, which this method would silently decode as <c>0</c>
    ///     - a deliberate, narrow scope boundary rather than a silent correctness risk in
    ///     practice.
    /// </summary>
    /// <exception cref="InvalidDataException">
    ///     Thrown when an operand or escape operator is truncated, or a reserved/invalid DICT byte
    ///     is encountered.
    /// </exception>
    private static Dictionary<int, List<double>> ParseDict(byte[] data, int offset, int length)
    {
        var dict = new Dictionary<int, List<double>>();
        var operands = new List<double>();
        var end = offset + length;
        if (offset < 0 || length < 0 || end > data.Length)
        {
            throw new InvalidDataException("CFF DICT data exceeds the table bounds.");
        }

        var pos = offset;
        while (pos < end)
        {
            var b0 = data[pos];
            if (b0 <= 21)
            {
                int op = b0;
                pos++;
                if (b0 == 12)
                {
                    if (pos >= end)
                    {
                        throw new InvalidDataException("CFF DICT escape operator is truncated.");
                    }

                    op = 1200 + data[pos];
                    pos++;
                }

                dict[op] = [.. operands];
                operands.Clear();
            }
            else if (b0 == 28)
            {
                if (pos + 3 > end)
                {
                    throw new InvalidDataException("CFF DICT operand is truncated.");
                }

                operands.Add(SfntContainer.ReadInt16(data, pos + 1));
                pos += 3;
            }
            else if (b0 == 29)
            {
                if (pos + 5 > end)
                {
                    throw new InvalidDataException("CFF DICT operand is truncated.");
                }

                operands.Add(SfntContainer.ReadInt32(data, pos + 1));
                pos += 5;
            }
            else if (b0 == 30)
            {
                pos++;
                var terminated = false;
                while (pos < end && !terminated)
                {
                    var nibbles = data[pos];
                    pos++;
                    if ((nibbles >> 4) == 0xF || (nibbles & 0xF) == 0xF)
                    {
                        terminated = true;
                    }
                }

                operands.Add(0); // Real-number value is never interpreted; see this method's summary
            }
            else if (b0 is >= 32 and <= 246)
            {
                operands.Add(b0 - 139);
                pos++;
            }
            else if (b0 is >= 247 and <= 250)
            {
                if (pos + 2 > end)
                {
                    throw new InvalidDataException("CFF DICT operand is truncated.");
                }

                operands.Add(((b0 - 247) * 256) + data[pos + 1] + 108);
                pos += 2;
            }
            else if (b0 is >= 251 and <= 254)
            {
                if (pos + 2 > end)
                {
                    throw new InvalidDataException("CFF DICT operand is truncated.");
                }

                operands.Add((-(b0 - 251) * 256) - data[pos + 1] - 108);
                pos += 2;
            }
            else
            {
                throw new InvalidDataException($"CFF DICT contains a reserved/invalid byte (0x{b0:X2}).");
            }
        }

        return dict;
    }

    /// <summary>
    ///     Validates that <paramref name="length"/> bytes starting at <paramref name="pos"/> lie
    ///     within <paramref name="data"/>'s bounds, throwing otherwise.
    /// </summary>
    private static void EnsureAvailable(byte[] data, int pos, int length)
    {
        if (pos < 0 || length < 0 || checked((long)pos + length) > data.Length)
        {
            throw new InvalidDataException("CFF data is truncated.");
        }
    }
}
