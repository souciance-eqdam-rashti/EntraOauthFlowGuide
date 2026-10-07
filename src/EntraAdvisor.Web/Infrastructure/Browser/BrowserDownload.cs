using Microsoft.JSInterop;

namespace EntraAdvisor.Web.Infrastructure.Browser;

/// <summary>Browser delivery only; feature exporters own artifact content and filenames.</summary>
public sealed class BrowserDownload(IJSRuntime js)
{
    public ValueTask TextAsync(string fileName, string content) =>
        js.InvokeVoidAsync("advisor.download", fileName, content);

    public ValueTask BytesAsync(string fileName, byte[] content) =>
        js.InvokeVoidAsync("advisor.downloadBytes", fileName, content);
}
