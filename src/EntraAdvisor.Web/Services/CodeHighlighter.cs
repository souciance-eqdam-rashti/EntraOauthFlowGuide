using System.Net;
using System.Text;
using System.Text.RegularExpressions;
namespace EntraAdvisor.Web.Services;
/// <summary>Escapes every token before adding fixed markup. No external scripts or HTML from artifacts.</summary>
public static partial class CodeHighlighter
{
    [GeneratedRegex("//[^\\r\\n]*|#[^\\r\\n]*|<!--[\\s\\S]*?-->|\"(?:\\\\.|[^\"\\\\])*\"|'(?:\\\\.|[^'\\\\])*'|\\b\\d+\\b|\\b[A-Za-z_][A-Za-z_0-9]*\\b")]
    private static partial Regex Tokens();
    private static readonly HashSet<string> Keywords = new("using namespace public private internal static sealed class var new return if else foreach in await async Task true false null void string int catch try throw".Split(' '),StringComparer.Ordinal);
    public static string Render(string content)
    {
        var b=new StringBuilder();var offset=0;
        foreach(Match match in Tokens().Matches(content)) {
            b.Append(WebUtility.HtmlEncode(content[offset..match.Index]));
            var value=match.Value;var style=value.StartsWith("//",StringComparison.Ordinal)||value.StartsWith('#')||value.StartsWith("<!--",StringComparison.Ordinal)?"comment":value.StartsWith('"')||value.StartsWith('\'')?"string":char.IsDigit(value[0])?"number":Keywords.Contains(value)?"keyword":null;
            var escaped=WebUtility.HtmlEncode(value);
            b.Append(style is null ? escaped : $"<span class=\"syntax-{style}\">{escaped}</span>");offset=match.Index+match.Length;
        }
        return b.Append(WebUtility.HtmlEncode(content[offset..])).ToString();
    }
}
