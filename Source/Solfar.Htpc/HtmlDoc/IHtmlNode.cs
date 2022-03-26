using System.IO;
using System.Threading.Tasks;

namespace HtmlDocument;

public interface IHtmlNode {

    //--- Methods ---
    void Write(TextWriter writers);
}
