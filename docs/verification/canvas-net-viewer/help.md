## Help

### Verification Approach

The `Help` subsystem is verified at two tiers. The unit tests of `HelpCatalog` and `HelpFrontMatter` (see *HelpCatalog
Unit Verification Design* and *HelpFrontMatter Unit Verification Design* under `help/`) verify the units in isolation,
mostly on small in-memory catalogs built through the internal seams `HelpCatalog.Create` and `HelpCatalog.ExtendWith`
(the test project has access to the internals), and verify the embedded library cards against the code, the
renderer, and the user guides. The subsystem tests in `HelpSubsystemTests.cs` verify the two units working together
the way a host uses them: every embedded card is parsed by `HelpFrontMatter`, loaded and validated by `HelpCatalog`,
resolved by its path, and rendered; a filtered view of the real library catalog hides the cards it excludes
everywhere a reader could meet them; a host's overlay and own card (in-memory card files) are parsed and layered on
the library catalog, with the host writing the links; and a malformed host card fails with the file and the line,
from the front matter and from the body. Nothing is mocked: the library catalog is the real embedded content.

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
  link writer receives the visible links, a hidden host card becomes its title in the text, and the library catalog is
  unchanged.
- A malformed host card fails with `Help card '<file>', line <n>:` for a front-matter error and for a link that names
  no card.

### Test Scenarios

**HelpSubsystem_DefaultCatalog_EveryCardResolvesByItsPathAndRenders**: for every card of `HelpCatalog.Default`, `Find`
with the identifier written with spaces and in upper case returns the same instance; `RenderText` starts with
`# <title>` and a line feed, contains the `Markdown`, and has no `{{`; and `RenderJson` parses with the card's `id`
and `tags`.

**HelpSubsystem_FilteredView_HidesExcludedCardsFromTreeLookupAndLinks**: the view `Filter(null, [file-access,
hosting])` of the library is not empty, has no card with either tag, does not find `files` or `svg/options`, and finds
`svg/style`; for every visible card the child and related identifiers are visible, the `Markdown` does not contain
"the card `files`", and `FormatMarkdown` passes only visible targets to the host.

**HelpSubsystem_Extend_HostCardsAreLayeredOnTheLibraryCards**: an overlay of `limits` (a related card and a section
that links `style` and the host card `widgets`) and the host card `widgets` (tagged `hosting`) are layered on the
library: `widgets` is the last root, the merged `limits` body starts with the library body and ends with the section
in library link form, its related cards include `widgets`, the host's link writer writes `<style>` and `<widgets>`, a
view without `hosting` writes the link to `widgets` as its title "Widgets", and the library catalog has no `widgets`
card and no host section.

**HelpSubsystem_Extend_MalformedHostCard_FailsWithTheFileAndTheLine**: a host card with a tab in its front matter
fails with `Help card 'widgets.md', line 2:`, and a host card whose body links the unknown card `nothing` fails with
`Help card 'widgets.md', line 8:` and names the link.
