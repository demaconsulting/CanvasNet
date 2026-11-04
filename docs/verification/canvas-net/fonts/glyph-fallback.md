### GlyphFallback Unit Verification Design

<!-- cspell:ignore Liberation Noto -->

This document describes the unit-level verification strategy for the `GlyphFallback` class.

#### Verification Approach

`GlyphFallbackTests.cs` uses the real bundled Liberation Sans Regular as the primary font and the
real bundled Noto fonts as the chain, so the tests prove the behavior that users see. The code points
are chosen from a coverage measurement of the bundled files: U+221D and U+226B are absent from
Liberation Sans and present in Noto Sans Math; U+E000 (private use) and U+4E2D (a CJK ideograph) are
absent from all four fonts and are the "still missing" examples. A chain member is also used as the
primary font to prove that it is skipped by reference.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None; the bundled fonts are loaded from the assembly's embedded resources
- **Isolation**: The chain is process-wide and read-only, so tests cannot interfere with each other

#### Acceptance Criteria

A unit test run passes when every scenario below passes, and when every named test method listed for
each requirement ID passes across every target framework and every operating system in this
repository's CI matrix.

#### Test Scenarios

##### CanvasNet-Fonts-GlyphFallback-Resolve: The Chain And Its Order

**Tests**: `GlyphFallback_BundledFonts_IsTheNotoTrioInOrder`,
`GlyphFallback_Resolve_CharacterMissingInPrimary_UsesNotoSansMath`,
`GlyphFallback_Resolve_CharacterCoveredByPrimary_ReturnsPrimaryGlyph`,
`GlyphFallback_Resolve_CharacterCoveredByNoFont_ReturnsPrimaryNotdef`,
`GlyphFallback_Resolve_PrimaryIsChainMember_SkipsItselfAndFindsNextFont`,
`GlyphFallback_Resolve_InvisibleCharacterMissingInPrimary_ReturnsPrimaryNotdef`,
`GlyphFallback_Resolve_ConcurrentCalls_AreDeterministic`

Asserts that the chain is the three Noto fonts, as the very instances of the catalog's cache; that
U+221D and U+226B resolve to Noto Sans Math with a non-zero glyph that has contours; that `A`
resolves to the primary font and its own glyph; that U+E000 and U+4E2D return the primary font with
glyph 0; that Noto Sans as the primary font finds U+221D in the next chain font, not in itself; that
a variation selector the primary font lacks is not looked up; and that 64 concurrent calls return the
same answer.

##### CanvasNet-Fonts-GlyphFallback-NeedsGlyph: Characters Without A Visible Glyph

**Tests**: `GlyphFallback_NeedsGlyph_InvisibleCharacter_ReturnsFalse`,
`GlyphFallback_NeedsGlyph_VisibleAndPrivateUse_ReturnsTrueAndInvalidReturnsFalse`

Asserts `false` for NUL, tab, space, zero-width joiner, left-to-right mark, a variation selector (in the
BMP and the supplement), the line separator, and a non-character; `true` for `A`, U+221D, and U+E000; and
`false` for a negative value, a value above U+10FFFF, and a surrogate.

##### CanvasNet-Fonts-GlyphFallback-FindMissing: Reporting What Stays Missing

**Tests**: `GlyphFallback_FindMissing_ReportsOnlyUncoveredCharactersDistinctAndAscending`,
`GlyphFallback_FindMissing_LoneSurrogate_IsTreatedAsReplacementCharacter`,
`GlyphFallback_NullArguments_ThrowArgumentNullException`

Asserts that text with the math signs and an accented letter reports nothing; that text with U+4E2D,
U+E000 (twice), a zero-width joiner, and a tab reports exactly `[U+4E2D, U+E000]`; that a lone
surrogate is judged as the replacement character; and that a `null` font or text throws.

#### Requirements Coverage

The three unit requirements are verified by the scenarios above; the subsystem requirement
`CanvasNet-Fonts-GlyphFallback` is verified through them and through the `TextRenderer` scenarios
(`CanvasNet-Rendering-TextRenderer-GlyphFallback`) that use the chain.
