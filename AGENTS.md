## Learned User Preferences
- When implementing an attached plan, do not edit the plan file; the plan todos already exist, so update their statuses in order and continue until they are complete.
- For backoffice or server integration work, include concrete validation steps using the browser Network/Console and server logs so the user can confirm what is loading or executing.

## Learned Workspace Facts
- This workspace is an Umbraco 17.3.5 / .NET 10 solution with host project `MySite`, alt-text extension projects `AltTextGen.Backoffice` and `AltTextGen.Server`, and test project `AltTextGen.Tests`.
- `MySite/Program.cs` uses the standard Umbraco builder with `.AddComposers()`, so server-side extension DI should be registered through Umbraco composers in referenced assemblies.
- The AltTextGen backoffice UI is registered by `AltTextGen.Backoffice/wwwroot/App_Plugins/AltTextGen/umbraco-package.json` as a media `workspaceAction`; in Umbraco 17 the workspace condition alias must be `Umb.Condition.WorkspaceAlias` with `match: "Umb.Workspace.Media"`.
- Umbraco backoffice manifests are exposed through `/umbraco/management/api/v1/manifest/manifest/private`; manifest discovery happens at application startup, so `dotnet watch` hot reload is not enough after changing referenced backoffice extension projects.
- The AltTextGen action appears on saved media item editor screens, typically under the workspace `Actions` menu, not during the initial upload flow.
- The AltTextGen backoffice JavaScript must include the Umbraco backoffice bearer token when calling protected server endpoints, otherwise the request returns 401.
- The AltTextGen server flow sends only the media key from the browser; the server loads media/file bytes itself and then uses either the mock generator or the configured Umbraco.AI/Microsoft Foundry provider.
