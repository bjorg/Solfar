namespace HtmlDocument;

public class HeadElement<TOuter> : HtmlElement<TOuter> where TOuter : class {

    //--- Constructors ---
    internal HeadElement(TOuter parent) : base(parent, "head") { }

    //--- Methods ---
    public HeadElement<TOuter> Title(string value) {
        Elem("title", value);
        return this;
    }

    public HeadElement<TOuter> Style(string value) {
        Elem("style", value);
        return this;
    }

    public HeadElement<TOuter> Script(string value) {
        Elem("script", value);
        return this;
    }

    public HeadElement<TOuter> Link(string rel, string href) {
        Begin("link").Attr("rel", rel).Attr("href", href).End();
        return this;
    }

    public HeadElement<TOuter> Meta(string name, string content) {
        Begin("meta").Attr("name", name).Attr("content", content).End();
        return this;
    }

    public HeadElement<TOuter> Base(string href, string target) {
        Begin("base").Attr("href", href).Attr("target", target).End();
        return this;
    }
}