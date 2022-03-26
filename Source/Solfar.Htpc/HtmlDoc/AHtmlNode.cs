namespace HtmlDocument;

using System.Web;
using System.Xml;

public abstract class AHtmlNode {

    //--- Class Methods ---
    internal static void WriteTagWithAttributesAndChildren(TextWriter writer, string name, IEnumerable<(string Name, string? Value)> attributes, IEnumerable<IHtmlNode> children) {
        var tagName = XmlConvert.EncodeName(name);

        // write opening tag
        writer.Write("<");
        writer.Write(tagName);
        foreach(var attribute in attributes) {

            // write attribute name
            writer.Write(" ");
            writer.Write(XmlConvert.EncodeName(attribute.Name));

            // write attribute value if it's not null
            if(attribute.Value is not null) {
                writer.Write("=\"");
                writer.Write(HttpUtility.HtmlEncode(attribute.Value));
                writer.Write("\"");
            }
        }
        if(children.Any()) {
            writer.Write(">");

            // write children nodes
            foreach(var child in children) {
                child.Write(writer);
            }

            // write closing tag
            writer.Write("</");
            writer.Write(tagName);
            writer.Write(">");
        } else {
            writer.Write("/>");
        }
    }

    //--- Fields ---
    private readonly List<(string Name, string? Value)> _attributes = new();
    private readonly List<IHtmlNode> _children = new();

    //--- Properties ---
    public IEnumerable<(string Name, string? Value)> Attributes => _attributes;
    public IEnumerable<IHtmlNode> Children => _children;

    //--- Methods ---
    protected T AddChild<T>(T child) where T : IHtmlNode {
        _children.Add(child);
        return child;
    }

    protected void AddAttr(string name, string? value)
        => _attributes.Add((Name: name, Value: value));
}
