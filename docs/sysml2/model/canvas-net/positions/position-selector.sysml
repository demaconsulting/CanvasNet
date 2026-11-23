package CanvasNet_PositionSelector {
    private import SoftwareItemMetadata::*;

    part def PositionSelector {
        doc /* The position selector grammar and its matching: the immutable, thread-safe PositionSelector parses one selector text (Parse throws a FormatException that names the selector and the problem, TryParse reports it) - all, a kind (an ASCII letter, then letters, digits, and hyphens), a kind with an id glob (* any run, ? exactly one character), a kind with a quoted JSON string as an exact id, or a kind with predicates joined by + (name=value, each name once; a word naming a channel or a data field, or one of the @id, @view, @from, @to, and @index facts) - with case-insensitive keywords and case-sensitive ids, channel and field names, and text values. A bare channel value is read like a --param value through ImageParameterValues (true, false, null, a finite invariant number, else text with one pair of single quotes removed; a value starting with [ or { is refused), a JSON string value is always text, @id, @view, @from, and @to take the text as written, and @index a whole number that is not negative. Matches tests a position: all matches every position, a kind alone every position of that kind, an id pattern is matched by an iterative two-pointer glob with a single backtrack point (at most pattern length times id length steps), and predicates are ANDed with typed equality (a missing name or fact never matches); a word name in the position's ChannelNames, or one its Values holds, compares with Values, and any other word with Data, so a data field named like a channel is matched only through that channel. A selector is at most MaxSelectorLength (256) characters with at most MaxPredicates (16) predicates. PositionSelection is the union of several selectors: Parse(texts) keeps identical texts once in their first order and accepts at most MaxSelectors (32), All wants every position, IncludesKind tells whether a kind can be wanted, and Matches whether any selector matches. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasNet/Positions/PositionSelector.cs",
                "src/DemaConsulting.CanvasNet/Positions/PositionSelection.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasNet.Tests/Positions/PositionSelectorTests.cs");
            designFiles = ("docs/design/canvas-net/positions/position-selector.md");
            verificationFiles = ("docs/verification/canvas-net/positions/position-selector.md");
            requirementFiles = ("docs/reqstream/canvas-net/positions/position-selector.yaml");
        }
    }
}
