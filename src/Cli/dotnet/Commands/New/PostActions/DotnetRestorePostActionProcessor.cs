// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Cli.PostActionProcessors;

namespace Microsoft.DotNet.Cli.Commands.New.PostActions;

internal class DotnetRestorePostActionProcessor(Func<string, bool>? restoreCallback = null) : PostActionProcessorBase
{
    private readonly Func<string, bool> _restoreCallback = restoreCallback ?? DotnetCommandCallbacks.RestoreProject;

    public override Guid Id => ActionProcessorId;

    internal static Guid ActionProcessorId { get; } = new Guid("210D431B-A78B-4D2F-B762-4ED3E3EA9025");

    protected override bool ProcessInternal(IEngineEnvironmentSettings environment, IPostAction actionConfig, ICreationEffects creationEffects, ICreationResult templateCreationResult, string outputBasePath)
    {
        bool allSucceeded = true;
        IEnumerable<string> targetFiles = GetConfiguredFiles(actionConfig.Args, creationEffects, "files", outputBasePath);

        if (!targetFiles.Any())
        {
            //If the author didn't opt in to the new behavior by specifying "projectFiles", use the old behavior - primary outputs
            if (templateCreationResult.PrimaryOutputs.Count == 0)
            {
                Reporter.Output.WriteLine(CliCommandStrings.PostAction_Restore_Error_NoProjectsToRestore);
                return true;
            }
            targetFiles = templateCreationResult.PrimaryOutputs.Select(output => Path.GetFullPath(output.Path, outputBasePath));
        }

        if (!targetFiles.Any())
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.PostAction_Restore_Error_FailedToDetermineProjectToRestore));
            return false;
        }

        foreach (string pathToRestore in targetFiles)
        {
            allSucceeded &= RestoreProject(pathToRestore);
        }
        return allSucceeded;
    }

    private bool RestoreProject(string pathToRestore)
    {
        try
        {
            Reporter.Output.WriteLine(string.Format(CliCommandStrings.PostAction_Restore_Running, pathToRestore));
            bool succeeded = _restoreCallback(pathToRestore);
            if (!succeeded)
            {
                Reporter.Error.WriteLine(CliCommandStrings.PostAction_Restore_Failed);
            }
            else
            {
                Reporter.Output.WriteLine(CliCommandStrings.PostAction_Restore_Succeeded);
            }
            return succeeded;
        }
        catch (Exception e)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.PostAction_Restore_RestoreFailed, e.Message));
            return false;
        }
    }
}
