// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.Utils;
using NuGet.Configuration;

namespace Microsoft.DotNet.Cli.BuildServer;

internal class VBCSCompilerServer(ICommandFactory commandFactory = null) : IBuildServer
{
    private static readonly string s_toolsetPackageName = "microsoft.net.sdk.compilers.toolset";
    private static readonly string s_vbcsCompilerExeFileName = "VBCSCompiler.exe";
    private static readonly string s_shutdownArg = "-shutdown";

    internal static readonly string VBCSCompilerPath = Path.Combine(
            Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
            "Roslyn",
            "bincore",
            "VBCSCompiler.dll");

    private readonly ICommandFactory _commandFactory = commandFactory ?? new DotNetCommandFactory(alwaysRunOutOfProc: true);

    public int ProcessId => 0; // Not yet used

    public string Name => CliStrings.VBCSCompilerServer;

    public void Shutdown()
    {
        List<string> errors = null;

        // Shutdown the compiler from the SDK.
        execute(_commandFactory.Create("exec", [VBCSCompilerPath, s_shutdownArg]), ref errors);

        // Shutdown toolset compilers.
        Reporter.Verbose.WriteLine($"Shutting down '{s_toolsetPackageName}' compilers.");
        var nuGetPackageRoot = SettingsUtility.GetGlobalPackagesFolder(Settings.LoadDefaultSettings(root: null));
        var toolsetPackageDirectory = Path.Join(nuGetPackageRoot, s_toolsetPackageName);
        Reporter.Verbose.WriteLine($"Enumerating '{toolsetPackageDirectory}'.");
        if (Directory.Exists(toolsetPackageDirectory))
        {
            foreach (var versionDirectory in Directory.EnumerateDirectories(toolsetPackageDirectory))
            {
                var vbcsCompilerPath = Path.Join(versionDirectory, s_vbcsCompilerExeFileName);
                Reporter.Verbose.WriteLine($"Found '{vbcsCompilerPath}'.");
                if (File.Exists(vbcsCompilerPath))
                {
                    execute(CommandFactoryUsingResolver.Create(vbcsCompilerPath, [s_shutdownArg]), ref errors);
                }
            }
        }

        if (errors?.Count > 0)
        {
            throw new BuildServerException(
                string.Format(
                    CliStrings.ShutdownCommandFailed,
                    string.Join(Environment.NewLine, errors)));
        }

        static void execute(ICommand command, ref List<string> errors)
        {
            command = command
                .CaptureStdOut()
                .CaptureStdErr();

            var result = command.Execute();
            if (result.ExitCode != 0)
            {
                errors ??= [];
                errors.Add(result.StdErr);
            }
        }
    }
}
