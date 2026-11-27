## DemaConsulting.HelpCards Verification

This document provides the verification evidence for the `DemaConsulting.HelpCards` OTS software item. Requirements
for this OTS item are defined in the DemaConsulting.HelpCards OTS Software Requirements document.

### Required Functionality

`DemaConsulting.HelpCards` is a runtime NuGet dependency of the Viewer library and of the CanvasNet tool. It loads the
help cards embedded in the Viewer and tool assemblies, replaces their tokens, composes the tool's source with the
library source it imports, gives filtered views, renders cards as text and JSON, runs the tool's help command with the
tool's JSON envelope, error object, and exit codes, and validates the cards against CanvasNet's budgets and rules.
Correct behavior is required because hosts of the Viewer and users and agents of the tool read the help, and because
the tool's help documents are part of its JSON contract.

### Verification Approach

`DemaConsulting.HelpCards` has no CLI or self-validation mode of its own in this repository, so it is verified
indirectly, end to end, through its observable effect on the real cards: the CanvasNet tests build the library catalog
and the tool's combined catalog from the embedded resources, validate them, filter and render them, run the help command
in-process and as a process, and compare the output with the contract. A passing test run for each scenario below is
direct evidence that the library behaves as the requirements state. The `CanvasNetTool_HelpExamples` self-test of the
installed tool repeats the strict rendering of the examples and the printing of the tree in the deployment environment.

### Test Scenarios

#### Loading and composition

**Scenarios**: `CanvasNetHelp_Source_LoadsTheEmbeddedCardsAndExamples` (the library source loads the cards and an
example file next to its card), `CanvasNetHelp_Configure_AppliesTheTagVocabulary` (the vocabulary rejects an unknown tag
and a card without a scope tag), `HelpSubsystem_HostSource_IsLayeredOnTheLibraryCards` (a host source that imports the
library source extends a library card and adds a root), `HelpSubsystem_HostSource_BrokenLink_FailsWithTheFileAndTheLine`
(a defect is reported with its file and line), `HelpTopics_OverlayCards_BodyIsTheLibraryBodyThenTheOverlayBody` (every
overlay of the tool is appended to its library card), and
`CanvasNetTool_HelpTopics_MindmapExample_PrintsTheHexagonAndRendersStrict` (the escaped `pkg\{{Package}}` prints as
`pkg{{Package}}` and the example renders in strict mode).

**Requirement coverage**: `CanvasNet-OTS-HelpCards-Load`.

#### Views

**Scenarios**: `CanvasNetHelp_DefaultFiltered_NeverPointsAtAHiddenCard`,
`CanvasNetHelp_DefaultFiltered_VisibleCardBelowAHiddenRoot_IsARootFoundByItsPath`,
`CanvasNetHelp_Catalog_Filter_UnknownTag_ThrowsArgumentException`, and
`HelpSubsystem_FilteredView_HidesExcludedCardsFromTreeLookupAndLinks`: a view never lists, finds, or links a hidden
card, lifts the visible cards below a hidden one, and refuses an unknown tag.

**Requirement coverage**: `CanvasNet-OTS-HelpCards-Views`.

#### Rendering

**Scenarios**: `CanvasNetHelp_Default_RenderTextAndJson_AreDeterministicPlainAndComplete`,
`HelpTopics_RenderText_IsDeterministicPlainAndComplete`, `HelpTopics_RenderJson_HasTheDocumentedShape`, and
`HelpTopics_CommandLineText_WritesLinksAsHelpCommands`: every card renders the same way twice, as plain text with LF
line endings and as one line of ASCII JSON in the tool's envelope, with its links in the plain or the command style.

**Requirement coverage**: `CanvasNet-OTS-HelpCards-Render`.

#### Help command

**Scenarios**: `CommandRegistry_Run_HelpUnknownCard_ListsChildren`,
`CommandRegistry_RenderHelp_Json_Index_ListsCommandsAndTopics`,
`CommandRegistry_RenderHelp_SearchTagAndAll_AnswerInTheToolEnvelope`,
`CommandRegistry_RenderHelp_WrongHelpOptions_AreUsageErrors`, and
`CanvasNetTool_HelpTopics_RunAsProcess_JsonFailures_HaveTheToolShapeAndExitCode1`: the help command answers the index,
cards, examples, searches, and tag lists, offers the tool's own subjects to the fallback resolver, and writes its
documents, errors, and exit codes in the tool's shape.

**Requirement coverage**: `CanvasNet-OTS-HelpCards-Cli`.

#### Validation

**Scenarios**: `CanvasNetHelp_LibraryCards_PassTheStrictValidation`, `HelpTopics_Cards_PassTheStrictValidation`, and
`HelpTopics_Validation_UnquotedPagePattern_IsReported`: the validator applies CanvasNet's budgets, column limits, and
rules to every card, and reports a command example that a shell would expand.

**Requirement coverage**: `CanvasNet-OTS-HelpCards-Validate`.

### Requirements Coverage

- **`CanvasNet-OTS-HelpCards-Load`**: the loading and composition scenarios
- **`CanvasNet-OTS-HelpCards-Views`**: the view scenarios
- **`CanvasNet-OTS-HelpCards-Render`**: the rendering scenarios
- **`CanvasNet-OTS-HelpCards-Cli`**: the help command scenarios
- **`CanvasNet-OTS-HelpCards-Validate`**: the validation scenarios
