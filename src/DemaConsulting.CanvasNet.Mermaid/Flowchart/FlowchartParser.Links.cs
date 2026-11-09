// Portions derived from mermaid (MIT); see third-party/README.md
using System.Text;

namespace DemaConsulting.CanvasNet.Mermaid.Flowchart;

/// <content>
///     The links of the grammar: a link token with an optional <c>|text|</c>, or a link with its text between the start
///     and the arrow, each with an optional id.
/// </content>
internal sealed partial class FlowchartParser
{
    /// <summary>
    ///     Parses a link (<c>link</c>): <c>[id@]--&gt;[|text|]</c> or <c>[id@]-- text --&gt;</c>, in every stroke.
    /// </summary>
    /// <returns>The link.</returns>
    private FlowLink ParseLink()
    {
        // An optional id before the link
        string? id = null;
        if (Is(FlowchartTokens.LinkId))
        {
            id = Advance().Text;
            if (!Is(FlowchartTokens.StartLink, FlowchartTokens.Link))
            {
                throw Fail("write the link right after its id, for example A e1@--> B");
            }
        }

        // -- text -->: the text between the start and the arrow
        if (Is(FlowchartTokens.StartLink))
        {
            var start = Advance().Text;
            var text = ParseEdgeText();
            var end = Expect(FlowchartTokens.Link).Text;
            return FlowchartModel.DestructLink(end, start, text, id);
        }

        // -->|text|, with one optional space after the text
        var link = Expect(FlowchartTokens.Link).Text;
        FlowText? arrowText = null;
        if (Is(FlowchartTokens.Pipe, FlowchartTokens.TestStr))
        {
            Advance();
            arrowText = ParseText();
            Expect(FlowchartTokens.Pipe);
            if (Is(FlowchartTokens.Space))
            {
                Advance();
            }
        }

        return FlowchartModel.DestructLink(link, null, arrowText, id);
    }

    /// <summary>
    ///     Parses the text of a link between its start and its arrow (<c>edgeText</c>): a quoted or markdown string, or
    ///     characters, possibly followed by more characters.
    /// </summary>
    /// <returns>The text.</returns>
    private FlowText ParseEdgeText()
    {
        var builder = new StringBuilder();
        string type;
        if (Is(FlowchartTokens.Str))
        {
            builder.Append(Advance().Text);
            type = "string";
        }
        else if (Is(FlowchartTokens.MdStr))
        {
            builder.Append(Advance().Text);
            type = "markdown";
        }
        else if (Is(FlowchartTokens.EdgeText, FlowchartTokens.UnicodeText))
        {
            builder.Append(Advance().Text);
            type = "text";
        }
        else
        {
            throw Fail("write the link text after the start of the link, for example A-- text -->B");
        }

        while (Is(FlowchartTokens.EdgeText, FlowchartTokens.UnicodeText))
        {
            builder.Append(Advance().Text);
        }

        return new FlowText(builder.ToString(), type);
    }
}
