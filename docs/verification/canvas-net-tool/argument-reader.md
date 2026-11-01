## ArgumentReader

<!-- cspell:ignore rgba dpi rrggbbaa dwidth outptu ouput -->

### Verification Approach

`ArgumentReader` is verified with unit tests in `ArgumentReaderTests.cs`. It is a pure function of its inputs (the
argument list, the option table, and the value text), so no mocking and no console are needed: the tests build
readers from arrays of tokens and an `OptionSpec` table, and call the value grammars directly. The invariant-culture
guarantee is verified by running a test under a culture whose decimal separator is a comma. One system-level test
(`CanvasNetToolTests.cs`) passes real option spellings through `Context`, `CommandRegistry`, and a command.

### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because one test changes the current
culture.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Every documented spelling of an option reads to the same canonical name and value.
- Every malformed input is rejected with a `usage-error` (or `size-limit` for an oversized size), never silently
  accepted, and the message and hint name the option, the text, and an example.
- Numbers parse the same under every culture.

### Test Scenarios

**ArgumentReader_Parse_SeparateValue_ReadsValue**, **ArgumentReader_Parse_EqualsValue_ReadsValue**,
**ArgumentReader_Parse_ShortAlias_RecordsCanonicalName**, and **ArgumentReader_Parse_AttachedShortValue_ReadsValue**:
`--output a.png`, `--output=a=b.png` (the value may contain `=`), `-o a.png` (recorded as `--output`), and the
attached forms `-oa.png`, `-o=a.png`, and `-Dwidth=20`.

**ArgumentReader_Parse_ValueStartingWithDash_IsTheValue**: `--azimuth -45` reads `-45` as the value.

**ArgumentReader_Parse_Flag_IsPresentWithoutValue**: a flag is present and has no value.

**ArgumentReader_Parse_Positionals_KeptInOrder**: positional arguments around an option keep their order and do not
include the option's value.

**ArgumentReader_Parse_DashAndNegativeNumber_ArePositional** and **ArgumentReader_Parse_DoubleDash_EndsOptions**: a
lone dash, `-90`, and `-.5` are positional; everything after `--` is positional.

**ArgumentReader_Parse_RepeatedOptions_KeptInCommandLineOrder**: interleaved repeated options are kept in order in
`Ordered`; `GetAll` returns the values in order and `Get` the last.

**ArgumentReader_Lookup_UndeclaredOption_ThrowsKeyNotFoundException**: `Has`, `Get`, and `GetAll` of an undeclared
name throw `KeyNotFoundException`, which is not an expected-failure type.

**ArgumentReader_Parse_UnknownOption_SuggestsClosestOption** and
**ArgumentReader_Parse_UnknownOptionNoSuggestion_PointsAtHelp**: `--outptu`, `--out`, and `--ouput` get a
suggestion of `--output`; an unrelated option gets only the pointer to the command's help.

**ArgumentReader_Parse_MissingValue_ThrowsUsageError**, **ArgumentReader_Parse_FlagWithValue_ThrowsUsageError**, and
**ArgumentReader_Parse_NullArguments_ThrowsArgumentNullException**: a value option as the last token, a flag with an
inline value, and null arguments are rejected.

**ArgumentReader_RequirePositionals_CountInRange_DoesNotThrow** and
**ArgumentReader_RequirePositionals_CountOutOfRange_ThrowsUsageError**: a count in range passes; too few, too many,
and "takes no arguments" have their messages.

**ArgumentReader_GetInt_ValidValue_ReturnsValue**, **ArgumentReader_GetInt_InvalidValue_ThrowsUsageError**,
**ArgumentReader_GetDouble_ValidValue_ReturnsValue**, and **ArgumentReader_GetDouble_InvalidValue_ThrowsUsageError**:
typed getters return the value or `null` when absent, and reject text that is not a number, is not finite, or is out
of range.

**ArgumentReader_ParseInt_ValidAndInvalid_ParsesOrThrows**, **ArgumentReader_ParseNumber_GermanCulture_UsesInvariantCulture**,
and **ArgumentReader_ParseDpiAndPage_ValidAndInvalid_ParsesOrThrows**: range checks; under `de-DE` `1.5` parses and
`1,5` does not; resolutions and pages are range checked.

**ArgumentReader_ParseSize_ValidSize_ReturnsWidthAndHeight**, **ArgumentReader_ParseSize_Malformed_ThrowsUsageError**,
and **ArgumentReader_ParseSize_BeyondMaxDimension_ThrowsSizeLimit**: `WxH` forms, malformed sizes (missing side, zero,
negative, fraction, extra part, empty), and a side one pixel over the maximum.

**ArgumentReader_ParseGeometry_ValidForms_ReturnsRectangle** and
**ArgumentReader_ParseGeometry_Malformed_ThrowsUsageError**: the comma form and the `WxH+X+Y` form with pixels,
`px`, and percent (including `WxH` without an offset, and a `px` unit directly before the separating `x`);
malformed forms (wrong number of parts, zero width or height, negative or fractional pixels, percent over 100,
missing offset parts).

**ArgumentReader_ParseColor_HexForms_ReturnsColorWithAlphaLast**,
**ArgumentReader_ParseColor_NamesAndTransparent_ReturnsColor**, and **ArgumentReader_ParseColor_Invalid_ThrowsUsageError**:
`#RGB`, `#RGBA`, `#RRGGBB`, `#RRGGBBAA` (the alpha pair is last), a bare hex value, surrounding whitespace, names in
any case (the basic ones and extended ones such as `steelblue` and `rebeccapurple`, which come from the core
`CssColor` table), `transparent`, and invalid text (including the `rgb()` and `hsl()` function forms, `none`, and
names that are not CSS colors).

**ArgumentReader_ParseDegrees_ValidAngle_ReturnsNormalizedAngle** and **ArgumentReader_ParseDegrees_Invalid_ThrowsUsageError**:
`-90` becomes 270, `450` becomes 90, `360` becomes 0, a `deg` suffix is accepted; text, `NaN`, and `Infinity` are
rejected.

**ArgumentReader_ParseFlip_ValidAxis_ReturnsAxis** and **ArgumentReader_ParseFlip_Invalid_ThrowsUsageError**: short
and long axis names in any case; anything else lists the choices.

**ArgumentReader_ParsePad_ValidForms_ReturnsWidths**, **ArgumentReader_ParsePad_Malformed_ThrowsUsageError**, and
**ArgumentReader_ParsePad_BeyondMaxDimension_ThrowsSizeLimit**: `10` for every side, `1,2,3,4` as left, top, right,
bottom; wrong counts, negatives, and fractions are rejected; one pixel over the maximum is a size-limit error.

**ArgumentReader_ParseDefines_LiteralTypes_ParsedByType**, **ArgumentReader_ParseDefines_NonFiniteNumber_IsString**,
and **ArgumentReader_ParseDefines_Malformed_ThrowsUsageError**: numbers become `double`, `true`/`false` booleans,
the rest strings (quotes removed); a later definition replaces an earlier one; `Infinity` stays text; missing `=`,
invalid names, and vector values are rejected.

**ArgumentReader_Parsers_NullText_ThrowsArgumentNullException**: every parser rejects null text.
