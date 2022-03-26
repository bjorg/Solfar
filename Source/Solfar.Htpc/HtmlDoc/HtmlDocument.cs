namespace HtmlDocument;

using System.Net.Mime;
using System.Text;

public class HtmlDoc : AHtmlNode, IResult {

    //--- Methods ---
    public HtmlElement<HtmlDoc> Begin(string name)
        => AddChild(new HtmlElement<HtmlDoc>(this, name));

    public HtmlDoc Attr(string name, string value) {
        AddAttr(name, value);
        return this;
    }

    public HtmlDoc Attr(string name) {
        AddAttr(name, value: null);
        return this;
    }

    public HeadElement<HtmlDoc> Head() => AddChild(new HeadElement<HtmlDoc>(this));
    public HtmlElement<HtmlDoc> Body() => Begin("body");

    //--- IResult Members ---
    async Task IResult.ExecuteAsync(HttpContext httpContext) {
        httpContext.Response.ContentType = MediaTypeNames.Text.Html;

        // write response to memory stream
        MemoryStream stream = new();
        using var writer = new StreamWriter(stream, Encoding.UTF8);

        // write document elements
        writer.Write("<!DOCTYPE HTML>");
        WriteTagWithAttributesAndChildren(writer, "html", Attributes, Children);

        // copy buffer to http stream
        writer.Flush();
        httpContext.Response.ContentLength = stream.Position;
        stream.Position = 0;
        await stream.CopyToAsync(httpContext.Response.Body);
    }
}
