using EntraAdvisor.Guide.Contracts;
namespace EntraAdvisor.Guide;
/// <summary>Concrete sample file destinations; existing application paths are not known.</summary>
public static class BrowserConfigurationContent {
 public static List<GuideContent> Create(string component,string name,string target) {
  GuideContent Action(string title,string text,string group,string system,params string[] location) => new InstructionContent(text) { Title=title,GroupTitle=group,GroupSystem=system,GroupLocation=[..location] };
  GuideContent Code(string id,string file,string text,string group,string system,string language="typescript") => new CodeContent(new(id,component,language,file,text)) { GroupTitle=group,GroupSystem=system,GroupLocation=[name,group=="Configure sign-in" ? "src/authConfig.ts + redirect.html (sample files to create)" : file+" (sample file to create)"] };
  return [
   new CopyableValueContent(new(component+".clientId","Copy the application identifiers","__"+component.Replace('-', '_').ToUpperInvariant()+"_CLIENTID__",GuideValueKind.DeveloperSupplied,"Copy Application (client) ID and Directory (tenant) ID from this app’s registration. Enter them in the next group; do not use the Backend API client ID as the browser client ID.") { ReferenceOnly=true,CanCopy=false }) { GroupTitle="Collect application identifiers",GroupSystem="Entra",GroupLocation=["App registrations",name,"Overview"] },
   Action("Install MSAL Browser","Run `npm install @azure/msal-browser@5.24.0` in the browser project. The following sample uses popup sign-in; invoke interactive methods from user actions and handle cancellation in your UI.","Configure sign-in","Code",name,"src/authConfig.ts + redirect.html (sample files to create)"),
   Action("Set identifiers and the callback","Create `src/authConfig.ts`. Replace the browser client ID, tenant ID and redirect URI with your Entra values. The redirect URI must exactly match the registered SPA callback. These file names are proposed sample destinations; adapt imports to your project.","Configure sign-in","Code",name,"src/authConfig.ts + redirect.html (sample files to create)"),
   Code("browser-auth","src/authConfig.ts",Auth,"Configure sign-in","Code"),
   Action("Create the callback bridge","Add `redirect.html` to the build as a separate bundled entry. Serve this page at the registered SPA redirect URI. It must run only the MSAL redirect bridge, without the app router or other app scripts. The example assumes a bundler such as Vite or Webpack.","Configure sign-in","Code",name,"src/authConfig.ts + redirect.html (sample files to create)"),
   Code("browser-bridge","redirect.html",Bridge,"Configure sign-in","Code","html"),
   Action("Request the API scope","Create `src/backendApi.ts`. Replace `__BACKEND_SCOPE__` with the scope granted for this connection, for example `api://<backend-client-id>/Orders.Read`. No operation scope has been selected by this guide. Set `__BACKEND_API_URL__` to your protected endpoint.","Call "+target,"Api",name,"src/backendApi.ts (sample file to create)"),
   Action("Acquire and attach an access token","Attempt silent acquisition using the signed-in account. If MSAL requires interaction, acquire interactively from the API-call button. Send the access token as `Authorization: Bearer`; do not use the ID token. Handle cancellation and failed HTTP responses in the UI.","Call "+target,"Api",name,"src/backendApi.ts (sample file to create)"),
   Code("browser-api","src/backendApi.ts",Api,"Call "+target,"Api")
  ];
 }
 private const string Bridge = """
<!doctype html>
<html><head><title>Authentication callback</title></head>
<body><p>Completing sign-in...</p>
<script type="module">
  import { broadcastResponseToMainFrame } from "@azure/msal-browser/redirect-bridge";
  broadcastResponseToMainFrame();
</script>
</body></html>
""";
 private const string Auth = """
import { PublicClientApplication } from "@azure/msal-browser";

export const auth = new PublicClientApplication({
  auth: {
    clientId: "__BROWSER_CLIENT_ID__",
    authority: "https://login.microsoftonline.com/__TENANT_ID__",
    redirectUri: "__REGISTERED_REDIRECT_URI__"
  }
});
export const authReady = auth.initialize();

// Call from the sign-in button; let the UI handle errors/cancellation.
export async function signIn() {
  await authReady;
  const result = await auth.loginPopup({ scopes: ["openid", "profile"] });
  auth.setActiveAccount(result.account);
  return result.account;
}
""";
 private const string Api = """
import { InteractionRequiredAuthError } from "@azure/msal-browser";
import { auth, authReady } from "./authConfig";

// Call from an API-call button after signing in.
export async function callBackend() {
  await authReady;
  const account = auth.getActiveAccount();
  if (!account) throw new Error("Sign in before calling the API.");
  const request = { account, scopes: ["__BACKEND_SCOPE__"] };
  let result;
  try {
    result = await auth.acquireTokenSilent(request);
  } catch (error) {
    if (!(error instanceof InteractionRequiredAuthError)) throw error;
    result = await auth.acquireTokenPopup(request);
  }
  const response = await fetch("__BACKEND_API_URL__", {
    headers: { Authorization: `Bearer ${result.accessToken}` }
  });
  if (!response.ok) throw new Error(`API returned ${response.status}`);
  return response.json();
}
""";
}
