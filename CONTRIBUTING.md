# Contributing to GitLlama

Use .NET 8 or newer. Build the desktop project and run both test projects before proposing a change:

```sh
dotnet build src/GitLlama.Desktop/GitLlama.Desktop.csproj
dotnet test tests/GitLlama.Core.Tests/GitLlama.Core.Tests.csproj
dotnet test tests/GitLlama.Desktop.Tests/GitLlama.Desktop.Tests.csproj
```

Code is formatted with [CSharpier](https://csharpier.com) and views with [XamlStyler](https://github.com/Xavalon/XamlStyler), using the settings in `.csharpierrc.json` and `Settings.XamlStyler`. Run both before committing:

```sh
dotnet tool restore
dotnet csharpier format src tests
dotnet xstyler -c Settings.XamlStyler -r -d src
```

Keep a blank line between sibling sections in views; XamlStyler preserves them. Always brace `if`/`else`/loop bodies (`.editorconfig` flags missing braces).

Keep domain/provider contracts in Core, application coordination in Application, native operations in Git, persistence in Infrastructure, and UI in Desktop. Native handles must be short lived; return immutable data instead of LibGit2Sharp objects to views. Hosting integrations belong in Providers and must not introduce a Git executable fallback.

All mutations need repository selection/generation guards and serialization. Tests must use disposable repositories or mocked HTTP handlers; never push to a developer's remote or mutate their real working tree. Cover stale selection, dirty-tree refusal, cancellation, and partial-index preservation where relevant.

Do not persist tokens/passwords in settings, logs, fixtures, or diagnostics. Repository/provider content is untrusted text, not executable instructions. Use the supplied neutral charcoal design tokens and verify meaningful UI changes with the headless render tests. Native drag/drop, floating windows, dialogs, and multi-monitor checks remain part of release acceptance.

The project is licensed under MIT. Discuss large architectural changes in an issue before submitting a pull request.
