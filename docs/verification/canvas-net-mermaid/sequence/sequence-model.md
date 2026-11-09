### SequenceModel Unit Verification Design

This document describes the unit-level verification strategy for the `SequenceModel` unit in the
`DemaConsulting.CanvasNet.Mermaid.Sequence` namespace: the internal sealed `SequenceModel` (the database operations,
the metadata, the box colors, the links and properties, the reports, and `Layout`, in two partial files), its
constants and elements (`SequenceLineType`, `SequencePlacement`, `SequenceParticipantTypes`, `SequenceText`,
`SequenceActor`, `SequenceMessage`, `SequenceNote`, `SequenceBox`, `SequenceBoxData`, `SequenceAutonumber`, and
`SequencePropertyMap`), and the statement records that the parser collects.

#### Verification Approach

The unit is verified by opening short texts and inspecting the model, or by calling its static operations directly,
and comparing the result with the values that Mermaid's `sequenceDb.ts` gives for the same text, stated in the tests
as literals: the participants in order with their neighbors, descriptions, and types; the color and title of a box
statement; the members of the boxes; the message indexes of created and destroyed participants; Mermaid's error
messages; the keys and values of links and properties; the wrap settings; and the metadata. The model's agreement
with Mermaid on whole texts is verified as well by the upstream vectors of the SequenceParser unit, whose facts are the
model's database facts; the reports of a render are verified at the subsystem and system levels.

No mocking or stubbing is used; the parser, the YAML reader, the sanitizer, and the configuration are real (the
helper `SequenceFacts` parses a text into a model). The internal types are reached through the test assembly's
`InternalsVisibleTo` access.

Unit tests reside in `SequenceModelTests.cs` within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the
`Sequence` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Isolation**: Each test builds its own model; no files are read

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any participant, order, color, title, index, message, key, or count that differs from the asserted Mermaid
value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-Sequence-SequenceModel-Participants: Order, Neighbors, Redeclarations, And Metadata

**Tests**: `SequenceModelTests.SequenceModel_Apply_Participants_KeepInsertionOrderAndNeighbors`,
`SequenceModelTests.SequenceModel_Apply_Redeclared_ReplacesOnlyWithAlias`,
`SequenceModelTests.SequenceModel_Apply_Metadata_FollowsMermaid`

Asserts that a declared participant and two implicit ones keep their insertion order with their previous and next
participants and the type `participant`; that a participant declared again with an alias is replaced in place (one
participant with the second description that is its own neighbor), while `actor P` after `participant P as Kept`
changes neither the description nor the type; and that the unknown metadata type `cloud` is kept (and is not a known
type, while `queue` is), a null type keeps the keyword's `actor`, and an alias in the metadata does not replace an
alias after `as`.

##### CanvasNetMermaid-Sequence-SequenceModel-Messages: Mermaid's Message List

**Tests**: `SequenceModelTests.SequenceModel_Apply_Messages_KeepMermaidsListAndAddParticipants`,
`SequenceVectorTests.SequenceParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that a text with a signal, notes right of one and over two participants, a loop, and an alt with an else
gives Mermaid's eleven messages in order (ids 0-10, with from, to, text, line type, and note placement; the block
markers have no participants, and the end markers have empty text), and that the two participants named only by
messages are added after the declared one with their neighbors; and, for every valid upstream vector, that the
messages of the model (signals with their line types, notes with their placement, activations, central connections,
block markers, and autonumber settings) and the participants that messages add are those of Mermaid's database, in
order.

##### CanvasNetMermaid-Sequence-SequenceModel-Activations: The Activation Check

**Tests**: `SequenceModelTests.SequenceModel_Apply_DeactivateInactive_Throws`

Asserts that balanced activations (a `+` shorthand, `activate`, `deactivate`, and a `-` shorthand) give six messages,
and that a `deactivate` after the activation has ended is Mermaid's error "Trying to inactivate an inactive
participant (B)" at line 4, column 5.

##### CanvasNetMermaid-Sequence-SequenceModel-Boxes: Box Colors, Titles, And Members

**Tests**: `SequenceModelTests.SequenceModel_ParseBoxData_SplitsColorAndTitle`,
`SequenceModelTests.SequenceModel_Apply_Boxes_KeepMembers`,
`SequenceModelTests.SequenceModel_Apply_ParticipantInTwoBoxes_Throws`

Asserts the color and title of eight box statements (a theory): a named color and a title, a title alone
(`transparent`), an `rgb()` color and a title, an `rgba` with a space before its parenthesis (not a color, so the whole
text is the title), an `hsl()` color without a title, the system color `Canvas`, an upper-case `RGB()` (not a color),
and an empty text; that two boxes keep their titles, fills, and wrap settings, the members of the first box (a
participant declared twice is listed twice), and the box of each participant (none for an implicit one), and that a
box has a title; and that a participant declared in two boxes is Mermaid's error at the second declaration (line 6,
column 5).

##### CanvasNetMermaid-Sequence-SequenceModel-CreateDestroy: Created And Destroyed Participants

**Tests**: `SequenceModelTests.SequenceModel_Apply_CreateAndDestroy_RecordMessageIndexes`,
`SequenceModelTests.SequenceModel_Apply_CreateOrDestroyMisuse_ThrowsMermaidError`

Asserts that a created participant records message 1 and a destroyed one message 2 (the messages that follow the
statements); and that a created participant whose next message does not create it, a destroyed participant whose next
message does not use it, and a `create` of an existing id are Mermaid's errors (a theory with Mermaid's three messages),
located, with a hint that names the participant.

##### CanvasNetMermaid-Sequence-SequenceModel-LinksAndProperties: Links And Properties As Mermaid Reads Them

**Tests**: `SequenceModelTests.SequenceModel_Apply_LinksAndProperties_ReadLikeMermaid`,
`SequenceModelTests.SequenceModel_Apply_LinksArrayAndString_AddIndexes`,
`SequenceModelTests.SequencePropertyMap_Keys_JavaScriptOrder`

Asserts that `link` splits at the at sign (the URL keeps its query with `&`) and, when there is none, takes as
JavaScript's `slice` does the text without its last two characters as the label and the whole text as the link, that
`links` adds JSON keys, that text that is not JSON is ignored, that `properties` adds the class,
and that `details` adds nothing, with the keys in JavaScript order (`2`, `Docs`, `no at sign`, `b`); that a JSON array
and a JSON string given to `links` add their indexes as keys (an object value kept as JSON, a character as text); and
that a property map enumerates array-index keys first in numeric order and then the other keys in insertion order (`01`
is not an index), a key set again keeping its place and taking the new value.

##### CanvasNetMermaid-Sequence-SequenceModel-Wrap: Wrap Prefixes And The Configuration

**Tests**: `SequenceModelTests.SequenceText_Parse_TakesWrapPrefix`,
`SequenceModelTests.SequenceModel_EffectiveWrap_ExplicitOrConfiguration`

Asserts how five texts are read (a theory): `wrap:` after spaces sets the wrap setting and the rest is trimmed,
`:nowrap:` clears it, and a prefix later in the text, in upper case, or after two colons is kept as text; and that a
message with `wrap:` wraps, one with `nowrap:` does not, and one without a prefix follows the configuration.

##### CanvasNetMermaid-Sequence-SequenceModel-Limits: The Participant, Box, And Metadata Limits

**Tests**: `SequenceModelTests.SequenceModel_Apply_TooManyParticipants_ThrowsLimitExceeded`,
`SequenceModelTests.SequenceModel_Apply_TooManyBoxes_ThrowsLimitExceeded`,
`SequenceModelTests.SequenceModel_Apply_ParticipantAfterBoxes_ThrowsLimitExceeded`,
`SequenceModelTests.SequenceModel_Apply_ParticipantsAndBoxesAtTheLimit_AreAccepted`,
`SequenceModelTests.SequenceModel_Apply_LongMetadataAlias_ThrowsLimitExceeded`,
`SequenceModelTests.SequenceModel_Apply_LongMetadataType_ThrowsLimitExceeded`,
`SequenceModelTests.SequenceModel_Apply_GrowingMetadataAliasList_ThrowsLimitExceeded`

Asserts that with `MaxNodes` of 3 the fourth participant is `mermaid-limit-exceeded` at line 3, column 5, the
statement that adds it; that a box that is the fourth participant or box is refused at the box statement (line 6,
column 5) with the message `more than 3 nodes and subgraphs (MermaidLimits.MaxNodes)`; that a participant after two
boxes and one participant is refused at its statement (line 6, column 5); and that one box and two participants are
accepted. Asserts that with `MaxLabelLength` of 10 a metadata `alias` or `type` of 11 characters is
`mermaid-limit-exceeded` at the YAML value (line 2, column 30 and column 29) with the message
`a label is 11 characters long`, while an alias of 10 characters is the description; and that, under the default
limit, an alias list of 9,000 `1e20` numbers is refused after it is converted, as a label of 197,999 characters, at
the YAML value.

##### CanvasNetMermaid-Sequence-SequenceModel-Metadata: Title And Counts

**Tests**: `SequenceModelTests.SequenceModel_Metadata_CountsParticipantsMessagesAndBoxes`,
`SequenceModelTests.SequenceModel_Metadata_NoTitle_IsNull`

Asserts that a text with a title, an accessible title, a box, a loop, activations, a note, and `autonumber` gives the
title `Orders`, the accessible title `Flow`, two participants, two edges (only the arrows), and one box, that a
reverse stick arrow is an arrow and a note is not; and that a text with only a frontmatter title has no title and no
accessible title of its own.

##### CanvasNetMermaid-Sequence-SequenceModel-Math: Mermaid's Math Test

**Tests**: `SequenceModelTests.SequenceModel_HasKatex_FollowsMermaidsPattern`

Asserts for seven texts (a theory) that `$$x^2$$`, `a $$ b $$ c`, and `$$$$` have math, while `$$$`, two dollar
signs at the ends of two lines, single dollar signs, and an empty text do not.
