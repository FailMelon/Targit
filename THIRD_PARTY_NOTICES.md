# Third-party notices

The portable distribution includes verbatim dependency licenses and native third-party notices in `licenses/`. [The inventory](licenses/README.md) records the resolved package versions, declared licenses and their included notice files. Upstream notices remain applicable to their respective components.

Components include Avalonia, AvaloniaEdit, Dock, CommunityToolkit.Mvvm, LibGit2Sharp and libgit2, SSH.NET, BouncyCastle.Cryptography, MicroCom, Microsoft.Extensions.DependencyInjection, System.Diagnostics.DiagnosticSource, Newtonsoft.Json, SkiaSharp/Skia, HarfBuzzSharp/HarfBuzz, System.IO.Pipelines, Tmds.DBus and the native ANGLE dependencies. Self-contained distributions also include the .NET runtime license and its third-party notices.

Package-supplied licenses were copied from the resolved NuGet packages. Avalonia and Dock licenses were fetched at the NuGet repository commits; Tmds.DBus at its declared commit. MicroCom's upstream MIT license is retained alongside its NuGet copyright metadata. BuildServices is a build-time dependency whose upstream license is also included. Test-only packages are not shipped with the app.

Application code is licensed under the root `LICENSE`. Signing, notarization and installer creation are separate release operations.
