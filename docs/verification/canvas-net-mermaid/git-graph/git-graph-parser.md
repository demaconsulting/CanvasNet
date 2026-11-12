### GitGraphParser Unit Verification Design

This document describes the unit-level verification strategy for the `GitGraphParser` unit in the
`DemaConsulting.CanvasNet.Mermaid.GitGraph` namespace: the internal sealed `GitGraphParser` (the lexer, the grammar,
and the database operations, in three partial files), the statements `GitStatement`, the records `GitCommit` and
`GitBranch`, the database `GitGraphDatabase` with its generated ids, and the diagram `GitGraphDiagram` with the database
of a render's configuration.

#### Verification Approach

The unit is verified in two ways. `GitGraphVectorTests` parses the 297 cases of
`Fixtures/upstream-vectors/gitgraph-parser.json`, recorded from the pinned Mermaid release in headless Chrome by
`tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type gitgraph` from the texts of `gitgraph-vector-cases.mjs`: the
inputs of Mermaid's git graph spec (`diagrams/git/gitGraph.spec.ts`, 65 cases) and of the git graph tests of
`@mermaid-js/parser` (`tests/gitGraph.test.ts`, 27 cases), 187 original texts that cover the header forms, the
keywords and their longer alternatives, the options of every statement, the strings and their escapes, the
sanitizing, the titles and accessibility texts, the comments and directives, every error of the database (E1 to E14,
each at least once), and the main branch name and order of 16 site configurations, and the 18 git graph fixtures. The
generator runs Mermaid with the fixed pseudo-random sequence of `seeded-random.mjs`, restarted for each text, so the
ids Mermaid generates are recorded. Each text goes through Mermaid's preprocessing and detection, as a diagram does,
and is parsed with the case's site configuration. For each of the 196 valid texts the facts of Mermaid's git database
(the type, the direction, the title, the accessible title and description, the effective gitGraph configuration,
every commit with its id, message, sequence number, type, tags, parents, branch, and custom type and id, every branch
with its head, the drawing order of the branches, the checked-out branch, and the head) must equal the facts of the
parsed diagram. For each of the 101 invalid texts the error must be the first one Mermaid reports: for the 41 database
errors a `mermaid-syntax-error` whose message holds Mermaid's message; for the 6 lexical errors a located
`unrecognized text` error at the line and column of Mermaid's first lexer error; and for the 54 grammar errors a
located error at the line and column of Mermaid's first parser error with exactly the expected tokens chevrotain
lists. `GitGraphParserTests` verifies the header forms, the keywords, the grammar errors, the commit options, the
generated ids, the operations Mermaid rejects and those it accepts, the composed texts, the sanitizing, the branch
order, the metadata, the limits, and the database of a render's configuration on short hand-written texts, asserting
the commits, the branches, the texts, the metadata, the expected tokens, the locations (line and column in the
original text), the messages, and the hints.

No mocking or stubbing is used; the preprocessor, the detector, the configuration resolver, and the sanitizer are real
(each test preprocesses and detects its text, then calls `Parse`, as the document does). The internal types are
reached through the test assembly's `InternalsVisibleTo` access.

Unit tests reside in `GitGraphParserTests.cs` and `GitGraphVectorTests.cs` (with the shared helper
`UpstreamVectors.cs`) within the `DemaConsulting.CanvasNet.Mermaid.Tests` project, in the `GitGraph` folder.

#### Test Environment

- **Framework**: xUnit v3 running under the .NET SDK
- **Execution**: `dotnet test` invoked by `build.ps1` and the CI pipeline
- **Mocking**: None required
- **Fixtures**: `Fixtures/upstream-vectors/gitgraph-parser.json`, generated from the pinned Mermaid by
  `tools/visual-diff/mermaid/gen-diagram-vectors.mjs --type gitgraph` from
  `tools/visual-diff/mermaid/gitgraph-vector-cases.mjs`, with the ids of `tools/visual-diff/mermaid/seeded-random.mjs`
- **Isolation**: Each test parses its own text; the fixture is read only

#### Acceptance Criteria

A unit-level test run passes when every scenario below passes without error or exception beyond those explicitly
asserted. Any database fact, generated id, expected token, location, message, or error that differs from the recorded
or asserted Mermaid value constitutes a failure.

#### Unit-Level Test Scenarios

##### CanvasNetMermaid-GitGraph-GitGraphParser-Tokens: Tokens As Mermaid's Lexer Makes Them

**Tests**: `GitGraphParserTests.GitGraphParser_Parse_KeywordPrefixes_AreReferences`,
`GitGraphVectorTests.GitGraphParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that a keyword followed by more name characters is a branch name: the branches `commits`, `cherry-picked`,
`LRx`, the quoted `"commit"`, and `release/1.0` are created after `main`; that a keyword alone is not a name, so
`branch commit` is refused at line 3, column 10 with the expected tokens `REFERENCE` and `STRING`; that `gitGraphs` is
refused at line 1, column 1 with the expected tokens `gitGraph` and `gitGraph:` (the keyword needs white space or a
comment after it); and that every upstream vector gives Mermaid's facts or Mermaid's first error at its place.

##### CanvasNetMermaid-GitGraph-GitGraphParser-Grammar: The Statements Of Mermaid's Grammar

**Tests**: `GitGraphParserTests.GitGraphParser_Parse_HeaderForms_GiveTheDirection`,
`GitGraphVectorTests.GitGraphParser_Parse_UpstreamVector_MatchesMermaid`,
`GitGraphVectorTests.GitGraphParser_Vectors_FileHasEnoughCases_AtLeast220`

Asserts that `gitGraph`, `gitGraph:`, and `gitGraph :` give the direction `LR`, `gitGraph TB:` the direction `TB`,
`gitGraph BT :` the direction `BT`, and `gitGraph commit` a commit on the header's line, while `gitGraph LR` without its
colon is refused at line 1, column 10 with the expected token `EOF`; that every upstream vector (a theory with one case
for each) gives Mermaid's database facts or Mermaid's error; and that the file holds at least 220 cases, at least 90 of
them from the upstream specs, with between 50 and all but 120 errors.

##### CanvasNetMermaid-GitGraph-GitGraphParser-Values: Values As Mermaid's Converters Give Them

**Tests**: `GitGraphParserTests.GitGraphParser_Parse_CommitOptions_LastWinsAndTagsAddUp`,
`GitGraphParserTests.GitGraphParser_Parse_Sanitizing_EscapesAsMermaid`,
`GitGraphVectorTests.GitGraphParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that a commit with the options `id: "a"`, `id: 'b\"c'`, the bare message `"first"`, `msg: "second"`,
`tag: "x"`, `type: REVERSE`, `tag: "y"`, and `type: HIGHLIGHT` has the id `b"c`, the message `second`, and the type
`HIGHLIGHT` (the last value wins) and the tags `x` and `y` (tags add up); that the id `a<b>c=d&e` is sanitized as
Mermaid sanitizes the texts of a git graph, to `a&lt;b&gt;c&equals;d&e`, the tag `t<br>u` keeps its `<br>` tag, the
branch name `x<y` becomes `x&lt;y` (on the commit made on it), and the tag of a merge, `raw<b>`, is kept as written;
that an empty text sanitizes to empty; and that every upstream vector gives Mermaid's converted texts.

##### CanvasNetMermaid-GitGraph-GitGraphParser-Database: Commits And Branches As Mermaid's Database Keeps Them

**Tests**: `GitGraphParserTests.GitGraphParser_Parse_AcceptedRepeats_FollowMermaid`,
`GitGraphParserTests.GitGraphParser_Parse_ComposedTexts_FollowMermaid`,
`GitGraphParserTests.GitGraphParser_Parse_BranchOrder_SortsStably`,
`GitGraphVectorTests.GitGraphParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that the commits `a`, `b`, and `a` again give the commits `a` (sequence number 2, in the first place, with the
parent `b`) and `b` (sequence number 1): the id used again replaces the earlier commit in its place; that a branch
merged twice without new commits gives two merges; and that the same commit picked twice gives two cherry-picks, both
tagged `cherry-pick:x`. Asserts that the merge `M` of `dev` into `main` with `type: REVERSE` has the message
`merged branch dev into main`, the custom type `REVERSE`, and a custom id; that a cherry-pick of `M` with
`parent: "A"` onto the branch `rel` has the message `cherry-picked merged branch dev into main into rel`, the tag
`cherry-pick:M|parent:A`, and the parents `3-eb28011` (the head of `rel`) and `M`; and that a cherry-pick of `B` (whose
message is empty) with the tags `""` and `kept` has the message `cherry-picked  into rel` and the one tag `kept`.
Asserts that the branches `dev` (order 2), `b` (order 1), `c` (no order), and `d` (order 0) are drawn in the order
`main`, `c`, `d`, `b`, and `dev` (a stable sort, a branch without an order at 0); and that with a configuration whose
main branch is `trunk` with the order 0.5, the branches `a` and `b` (order 1) are drawn as `a`, `trunk`, and `b`, and
`checkout trunk` makes `trunk` the current branch. Every upstream vector gives Mermaid's commits, branches, drawing
order, current branch, and head.

##### CanvasNetMermaid-GitGraph-GitGraphParser-GeneratedIds: Ids From Mermaid's Fixed Sequence

**Tests**: `GitGraphParserTests.GitGraphParser_Parse_GeneratedIds_FollowTheFixedSequence`,
`GitGraphVectorTests.GitGraphParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that a commit without an id, a commit `x` on the branch `dev`, another commit without an id, and a cherry-pick
of `x` onto `main` have the ids `0-eb28011`, `x`, `2-cc99105`, and `3-43d14d3`: the sequence number and seven
hexadecimal digits of the fixed pseudo-random sequence, drawn only when an id is needed, and always for a
cherry-pick; that a second diagram's first commit is again `0-eb28011`, as the sequence restarts for each run; and that
every upstream vector, recorded with the same sequence, gives Mermaid's generated ids.

##### CanvasNetMermaid-GitGraph-GitGraphParser-Metadata: Counts, Direction, And Titles

**Tests**: `GitGraphParserTests.GitGraphParser_Parse_Metadata_CountsCommitsAndParentLinks`

Asserts that a `gitGraph TB:` text under the frontmatter title `Front` with `title History`, `accTitle: Acc`,
`accDescr: Desc`, a commit, the branch `dev` with a commit, and a merge of `dev` into `main` gives a node count of 3
(the commits), an edge count of 3 (the links to their parents, two of them from the merge), a subgraph count of 0, the
direction `TB`, the title `History` (set, and drawn instead of the frontmatter title), the accessible title `Acc`, and
the accessible description `Desc`; and that a bare `title` statement sets no title, so the frontmatter title `Front` is
drawn, as in Mermaid.

##### CanvasNetMermaid-GitGraph-GitGraphParser-Errors: Errors With Chevrotain's Expected Tokens And Mermaid's Messages

**Tests**: `GitGraphParserTests.GitGraphParser_Parse_GrammarErrors_AreLocatedWithTheExpectedTokens`,
`GitGraphParserTests.GitGraphParser_Parse_RejectedOperations_FailWithMermaidsMessage`,
`GitGraphVectorTests.GitGraphParser_Parse_UpstreamVector_MatchesMermaid`

Asserts that eight errors (a theory) are `mermaid-syntax-error` at the refused text with a hint: `commit id: foo` at
line 2, column 14 with `STRING`; `commit id: "a" foo` at line 2, column 18 with `NEWLINE` and `EOF`;
`commit type: normal` at line 2, column 16 with `NORMAL`, `REVERSE`, and `HIGHLIGHT`; a bare `branch` at line 2,
column 9 with `REFERENCE` and `STRING`; `branch dev order: high` at line 2, column 21 with `INT`; `commit2` at line 3,
column 3 with `EOF`; and the lexical errors `branch café` at line 2, column 13 and `branch a.` at line 2, column 11 with
the message `unrecognized text`. Asserts that the fourteen operations Mermaid's database rejects (a theory) are
`mermaid-syntax-error` at their statement, column 3, with a hint and Mermaid's message: a merge of `main` into `dev`
right after `dev` was made from it (`Cannot merge branch 'main' into itself.`), a merge of the current branch, a merge
onto a branch without commits, a merge of a branch that does not exist, of a branch without commits, of a branch with
the same head, and with an id that is already used, the creation of an existing branch, the checkout of a branch that
does not exist, a cherry-pick of a commit that does not exist, with a parent that is not an immediate parent, of a
merge without a parent, of a commit already on the current branch, and onto a branch without commits. Asserts that the
101 errors among the upstream vectors are Mermaid's first error: the database errors with Mermaid's message, and the
lexical and grammar errors at Mermaid's place, the grammar errors with Mermaid's expected tokens.

##### CanvasNetMermaid-GitGraph-GitGraphParser-Limits: Node And Label Limits

**Tests**: `GitGraphParserTests.GitGraphParser_Parse_NodeLimit_CountsBranchesAndCommits`,
`GitGraphParserTests.GitGraphParser_Parse_LabelLimit_CoversEveryText`

Asserts that with a `MaxNodes` of 4 the main branch, a commit, the branch `dev`, and a commit fit (a node count of 2),
while a merge after them is `mermaid-limit-exceeded` at line 5, its statement, with a message that names
`MermaidLimits.MaxNodes`; and that with a `MaxLabelLength` of 30 (a theory) an id of 31 characters, an id of ten `=`
signs (80 characters once sanitized), the message `merged branch abcdefghij into main` that a merge composes, and the
message `cherry-picked abcdefghijklmnop into main` that a cherry-pick composes are `mermaid-limit-exceeded` at their
statement with a message that names `MermaidLimits.MaxLabelLength`.

##### CanvasNetMermaid-GitGraph-GitGraphParser-RenderDatabase: The Database Of A Render's Main Branch

**Tests**: `GitGraphParserTests.GitGraphDiagram_Database_OtherMainBranch_RunsTheStatementsAgain`

Asserts, for a commit, the branch `dev` with a commit, and a merge of `dev` into `main`, that the database of the
default configuration is the database the text opened with; that a main branch order of 5 runs the statements again
into a database with the same commits whose branches are drawn as `dev` and `main`; and that the main branch name
`trunk` runs them again and fails at `checkout main` with `MermaidRenderException`, the code `mermaid-render-failed`,
line 5, and Mermaid's message `Trying to checkout branch which is not yet created. (Help try using "branch main")`.
