using EntraAdvisor.Web.Infrastructure.Browser;
using Microsoft.JSInterop;

namespace EntraAdvisor.Tests;

public sealed class BrowserDownloadTests
{
    [Fact]
    public async Task Delivery_preserves_export_payloads_and_surfaces_browser_failures()
    {
        var js = new RecordingJs();
        var download = new BrowserDownload(js);
        const string script = "# validation\nWrite-Output '__API_A_SCOPE__'\n";
        await download.TextAsync("validation.ps1", script);
        Assert.Equal("advisor.download", js.Identifier);
        Assert.Equal("validation.ps1", js.Arguments![0]);
        Assert.Equal(script, js.Arguments[1]);

        byte[] zip = [0x50, 0x4b, 0x03, 0x04];
        await download.BytesAsync("setup.zip", zip);
        Assert.Equal("advisor.downloadBytes", js.Identifier);
        Assert.Equal("setup.zip", js.Arguments![0]);
        Assert.Same(zip, js.Arguments[1]);

        js.Fail = true;
        await Assert.ThrowsAsync<JSException>(() => download.TextAsync("validation.ps1", script).AsTask());
    }

    private sealed class RecordingJs : IJSRuntime
    {
        public string? Identifier { get; private set; }
        public object?[]? Arguments { get; private set; }
        public bool Fail { get; set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        {
            if (Fail) throw new JSException("Browser delivery failed.");
            Identifier = identifier;
            Arguments = args;
            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
