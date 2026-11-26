## Help

### Verification Approach

The `Help` subsystem is verified at two tiers. The unit tests of `CanvasNetHelp` (see *CanvasNetHelp Unit Verification
Design* under `help/`) verify the unit and the embedded library cards against the code, the renderer, and the user
guides, and validate every card with the validator of `DemaConsulting.HelpCards` and CanvasNet's strict options. The
subsystem tests in `HelpSubsystemTests.cs` verify the unit and HelpCards working together the way a host uses them:
every embedded card is loaded and validated, resolved by its path, and rendered; a filtered view of the real library
catalog hides the cards it excludes everywhere a reader could meet them; a host's overlay and own card (in-memory card
files in a source that imports the library source) are layered on the library cards, with the host writing the links;
and a host card with a broken link fails with the file and the line. Nothing is mocked: the library catalog is the real
embedded content.

### Test Environment

N/A - standard test environment; the subsystem tests read only the embedded resources of the Viewer assembly and
in-memory card texts.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- Every library card resolves by its path (written with spaces and in upper case) and renders as text that starts with
  its title and contains its body, and as JSON with its identifier and tags, without an unresolved token.
- A view that excludes the `file-access` and `hosting` tags shows no card with those tags, finds none of them, lists
  only visible cards among the children and related cards, writes no library link to a hidden card, and never asks the
  host to write a link to one.
- A host's overlay is appended to the library card, its own card is a new root after the library roots, the host's
  link formatter receives the visible links, a hidden host card is not linked in the text, and the library catalog is
  unchanged.
- A host card with a link to a card that does not exist fails the catalog with one problem naming the file and the line.

### Test Scenarios

**HelpSubsystem_DefaultCatalog_EveryCardResolvesByItsPathAndRenders**: for every card of `CanvasNetHelp.Catalog`, `Find`
with the identifier written with spaces and in upper case returns the same instance; `Render` starts with `# <title>`
and a line feed, contains the `Body`, and has no `{{`; and the JSON rendering parses with the card's `id` and `tags`.

**HelpSubsystem_FilteredView_HidesExcludedCardsFromTreeLookupAndLinks**: the view `Filter(null, [file-access,
hosting])` of the library is not empty, has no card with either tag, does not find `files` or `svg/options`, and finds
`svg/style`; for every visible card the child and related identifiers are visible, the `Body` does not contain
"the card `files`", and `FormatBody` passes only visible targets to the host's link formatter.

**HelpSubsystem_HostSource_IsLayeredOnTheLibraryCards**: a host source that imports `CanvasNetHelp.Source`, with an
overlay of `limits` (a related card and a section that links `style` and the host card `widgets`) and the host card
`widgets` (tagged `hosting`), is composed with `CanvasNetHelp.Configure`: `widgets` is the last root, the merged
`limits` body starts with the library body and ends with the section in library link form, its related cards include
`widgets`, the host's link formatter writes `<style>` and `<widgets>`, a view without `hosting` does not link
`widgets`, and the library catalog has no `widgets` card and no host section.

**HelpSubsystem_HostSource_BrokenLink_FailsWithTheFileAndTheLine**: a host card whose body links the unknown card
`nothing` fails the catalog with one problem in `widgets.md`, line 8, that names the link.
