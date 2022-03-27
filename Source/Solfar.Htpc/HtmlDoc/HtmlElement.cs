namespace HtmlDocument;

public class HtmlElement<TOuter> : AHtmlNode, IHtmlNode where TOuter : class {

    //--- Constructors ---
    internal HtmlElement(TOuter parent, string name) {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Parent = parent ?? throw new ArgumentNullException(nameof(parent));
    }

    //--- Properties ---
    public TOuter Parent { get; }
    public string Name { get; }

    //--- Methods ---
    public HtmlElement<HtmlElement<TOuter>> Begin(string name)
        => AddChild(new HtmlElement<HtmlElement<TOuter>>(this, name));

    public HtmlElement<TOuter> Attr(string name, string value) {
        AddAttr(name, value);
        return this;
    }

    public HtmlElement<TOuter> Attr(string name) {
        AddAttr(name, value: null);
        return this;
    }

    public TOuter End() => Parent;
    public HtmlElement<TOuter> Elem(string name, string text) => Begin(name).Value(text).End();

    public HtmlElement<TOuter> Build(Func<HtmlElement<TOuter>, HtmlElement<TOuter>> builder) => builder(this);

    public HtmlElement<TOuter> Value(string text) {
        AddChild(new HtmlText(this, text));
        return this;
    }

    //--- IHtmlNode Members ---
    void IHtmlNode.Write(TextWriter writer)
        => WriteTagWithAttributesAndChildren(writer, Name, Attributes, Children);
}