## ResultWriter

### Verification Approach

`ResultWriter` is verified with unit tests in `ResultWriterTests.cs`. The documents are compared byte for byte with
literal expected text (a golden comparison), which is what makes the schema a verified contract; escaping is verified
by a round trip through `System.Text.Json`. No mocking is needed.

### Test Environment

N/A - standard test environment. The tests belong to the "Sequential" collection because one test changes the
current culture.

### Acceptance Criteria

- All unit tests pass with zero failures.
- Every document is exactly the expected single line of ASCII text.
- The output does not depend on the current culture.

### Test Scenarios

**ResultWriter_SchemaVersion_IsOne**: the released schema version is 1.

**ResultWriter_Success_WithFields_WritesEnvelopeThenFields** and **ResultWriter_Success_NoFields_WritesEnvelopeOnly**:
a success document is the envelope followed by the fields the command wrote (string, integer, fraction, boolean,
array), exactly; with no fields it is the envelope alone.

**ResultWriter_NullArguments_ThrowsArgumentNullException**: each null argument of both methods is rejected.

**ResultWriter_Failure_WithHint_WritesErrorObject**: the `error` object carries code, message, and hint; there is no
exit-code field.

**ResultWriter_Failure_NoCommandNoHint_WritesNullCommandAndOmitsHint**: a failure before a command was selected has
`"command":null`, and a failure without a hint has no `hint` key.

**ResultWriter_Failure_FromException_MapsExceptionToCode**: a `ToolException` keeps its code and hint, an
`ArgumentException` is `usage-error`, and another exception is `operation-failed`.

**ResultWriter_Success_GermanCulture_WritesInvariantNumbers**: under `de-DE` a fraction is written with a decimal
point and an integer without a thousands separator.

**ResultWriter_Success_SameInput_ProducesIdenticalOutput**: two writes of the same document are equal.

**ResultWriter_Failure_SpecialCharacters_AreEscapedAndRoundTrip**: a message with quotes, a backslash, a newline, a
tab, HTML characters, an accented letter, and an emoji (a surrogate pair) yields ASCII-only single-line text that
parses back to the original message.
