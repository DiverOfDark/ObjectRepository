using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;

class Build : NukeBuild
{
    // Console application entry. Also defines the default target.
    public static int Main() => Execute<Build>(x => x.Compile);

    // Auto-injection fields:

    // [GitVersion] readonly GitVersion GitVersion;
    // Semantic versioning. Must have 'GitVersion.CommandLine' referenced.

    // [GitRepository] readonly GitRepository GitRepository;
    // Parses origin, branch name and head from git config.

    // [Parameter] readonly string MyGetApiKey;
    // Returns command-line arguments and environment variables.

    Target Clean => _ => _
        .OnlyWhen(() => false) // Disabled for safety.
        .Executes(() =>
        {
            DeleteDirectories(GlobDirectories(SourceDirectory, "**/bin", "**/obj"));
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => DefaultDotNetRestore);
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(s => DefaultDotNetBuild
//                .SetAssemblyVersion(GitVersion.GetNormalizedAssemblyVersion())
//                .SetFileVersion(GitVersion.GetNormalizedFileVersion())
//                .SetInformationalVersion(GitVersion.InformationalVersion)
            );
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(x =>
                x.SetNoRestore(true).SetNoBuild(true).SetTestAdapterPath(".").SetLogger("Appveyor").SetProjectFile(
                    SolutionDirectory / "OutCode.EscapeTeams.ObjectRepository.Tests" /
                    "OutCode.EscapeTeams.ObjectRepository.Tests.csproj"));
        });

    Target Publish => _ => _
        .DependsOn(Test)
        .Executes(() =>
        {
            DotNetPack(s => DefaultDotNetPack.SetAuthors("Kirill Orlov").SetNoRestore(true));

            var apikey = Environment.GetEnvironmentVariable("NUGET");
            if (!String.IsNullOrWhiteSpace(apikey))
            {
                GlobFiles(OutputDirectory, "*.nupkg").NotEmpty()
                    .Where(x => !x.EndsWith("symbols.nupkg"))
                    .ForEach(x => DotNetNuGetPush(s => s.SetApiKey(apikey).SetTargetPath(x).SetSource("https://api.nuget.org/v3/index.json")));
            }
        });
}