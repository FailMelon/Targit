# Azure DevOps personal access tokens

GitLlama's Azure DevOps integration uses personal access tokens (PATs). Microsoft documents that new Entra OAuth apps do not natively support personal Microsoft accounts (MSA) for the Azure DevOps resource. Existing legacy Azure DevOps OAuth authorizations are separate; this does not mean all MSA OAuth has stopped working. GitLlama no longer uses an Azure OAuth registration, device-code flow, discovery step, or token renewal service.

## Connect

1. Open Settings → Integrations → Azure DevOps.
2. Enter an organization name or its `https://dev.azure.com/organization` URL.
3. Select Create / manage tokens to open that organization's token settings. Create an organization-scoped PAT with Code (Read), or Code (Read & write) if you need to push.
4. Paste the token into the masked field and select Connect with token. GitLlama verifies repository read access before saving it. An organization with no repositories can still connect successfully.
5. Add other organizations separately. Open the repository manager to browse repositories from all connected Azure organizations and GitHub. Selecting Clone fills the standard clone dialog with that repository's URL; choose a destination folder to continue. Settings manages connections only.

Connect again with the same organization to replace its token. A failed or canceled check preserves the previous connection. The token entry field is cleared after verification. Tokens do not renew automatically: replace them when expired or revoked. GitLlama does not request full-access tokens or manage token issuance itself. Azure pull requests and Boards remain outside this integration's current scope.

## Storage and removal

On Windows, tokens are encrypted with current-user DPAPI, saved separately from ordinary preferences, and scoped to the active GitLlama profile. Switching profiles clears in-memory tokens and loads that profile's saved connections. If secure storage is unavailable, connections apply only to the current session, with visible feedback. Old Azure OAuth caches are rejected and removed when loaded.

Remove token disconnects only that organization from GitLlama. Remove all Azure connections deletes the active profile's saved Azure connections. These actions do not revoke a PAT at Microsoft; revoke the token in Azure DevOps token settings if it should stop working everywhere.

## Transport

REST calls use HTTPS Basic authentication. Git HTTPS operations use system Git with an organization-scoped Basic header supplied through the child process environment. PATs are never included in command arguments, remote URLs, `.git/config`, ordinary settings, or diagnostic messages. Redirects are disabled and TLS verification remains enabled. Each organization's token is only supplied for that organization; Azure HTTPS operations do not fall back to another host's manual secret or a credential helper. SSH uses the existing key/agent configuration and never receives a PAT.

References: [Microsoft PAT setup](https://learn.microsoft.com/en-us/azure/devops/organizations/accounts/use-personal-access-tokens-to-authenticate?view=azure-devops), [Entra OAuth personal-account limitation](https://learn.microsoft.com/en-us/azure/devops/integrate/get-started/authentication/entra-oauth).
