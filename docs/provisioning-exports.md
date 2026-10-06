# Provisioning exports

The implementation page offers **Download Bicep** and **Download setup PowerShell**. Both open a brief coverage summary and download a ZIP; the static application never connects to a tenant or provisions resources.

## Data and files

`ProvisioningPlanBuilder` projects the existing `OAuthPlan` into typed application, permission and access records, retaining architecture prerequisites. It does not parse displayed instructions or alter engine decisions. Both exporters use that same plan and parameter schema.

Each ZIP includes `plan.json`, editable `settings.json`, a README, shared preflight/lookup helpers and a separate `Grant-Consent.ps1`. PowerShell adds `Setup-Entra.ps1`. Bicep adds `create.bicep`, `configure.bicep`, a pinned Microsoft Graph v1.0 extension configuration and an Az PowerShell deployment wrapper.

The PowerShell implementation uses Microsoft.Graph.Authentication with Graph v1.0 requests. Bicep provisions Graph resources through the Microsoft Graph extension. Its wrapper resolves external permission IDs and supplies template parameters, rather than embedding example permissions.

## Operation

1. Extract the ZIP and fill the required names, tenant IDs, exact redirect URIs and actual permission definitions. Certificate-authenticated applications require public `.cer` files; private keys never belong in the export.
2. Run **Register** separately in every application's home tenant, sharing `state.json` and keeping the deployment key unchanged. Managed identities reference an existing Azure host identity instead of creating another registration.
3. Run **Configure** in each application/API home tenant. Custom API scopes and roles use stable typed IDs, including across tenant stages. External resource permissions are resolved by their actual enabled definitions; missing permissions fail rather than being guessed.
4. If tenant-wide administrator consent is desired and authorized, run the separate consent script in the resource tenant with its explicit switch. Permission configuration alone does not grant consent. User consent is not automated by this helper.
5. Apply the exported identifiers to application code and follow the guide's runtime verification steps.

Reruns find registrations by stable `uniqueName`, never display name. The state and tenant checks prevent accidental reuse of another plan. These fresh-start exports own the scope/role/redirect/certificate collections of their registrations: manually reconcile changes if someone subsequently modifies those collections. They are not general migration tools for arbitrary existing registrations.

## Coverage boundaries

App registrations, service principals, platform callbacks, v2 API settings, custom scopes/app roles and requested permissions are generated from the selected decisions. Consent supports delegated tenant-wide grants and application role assignments, preserving existing delegated grants on reruns.

Managed identity host enablement, Azure RBAC/data-plane authorization, federation issuer/subject configuration, guest invitations and tenant policies remain explicitly documented follow-up actions. Customer tenant onboarding requires separate tenant stages and compatible multitenant applications. Missing external resource service principals must be onboarded before their permissions can be resolved. No secrets are generated, no private credentials are exported, and no application code is deployed.

Bicep needs an existing resource group/subscription in the selected tenant. Graph Bicep resources do not support normal ARM what-if. Use the README shipped with the download for commands, modules and privileges.

## Validation

The provisioning matrix test covers all terminating concrete questionnaire paths and checks that both formats share identical plan/settings data. Focused tests cover decision preservation, stable permission IDs, managed identity boundaries, ZIP contents and keeping scenario strings out of executable code.

Bicep templates are compiled with the official compiler and pinned Graph extension. Exported PowerShell can be checked with mocked Graph responses:

```powershell
./tests/scripts/Test-ProvisioningExport.ps1 -BundleDirectory '<extracted-browser-and-api-powershell-bundle>'
```

This check performs no authentication or network writes. A live tenant deployment requires the user's own permissions and is separate from these checks.

## Microsoft references

- [Graph Bicep setup](https://learn.microsoft.com/en-us/graph/templates/bicep/quickstart-create-bicep-interactive-mode)
- [Manage applications with Graph](https://learn.microsoft.com/en-us/graph/tutorial-applications-basics)
- [Graph Bicep limitations](https://learn.microsoft.com/en-us/graph/templates/bicep/limitations)
