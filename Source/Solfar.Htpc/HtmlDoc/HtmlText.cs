namespace HtmlDocument;

using System.Web;

public class HtmlText : IHtmlNode {

    //--- Constructors ---
    internal HtmlText(IHtmlNode parent, string text) {
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
        Text = text ?? throw new ArgumentNullException(nameof(text));
    }

    //--- Properties ---
    public IHtmlNode Parent { get; }
    private string Text { get; }

    //--- IHtmlNode Members ---
    void IHtmlNode.Write(TextWriter writer) => writer.Write(HttpUtility.HtmlEncode(Text));
}
