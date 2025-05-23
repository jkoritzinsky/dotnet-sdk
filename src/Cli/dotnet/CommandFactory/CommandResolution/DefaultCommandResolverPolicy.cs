// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.CommandFactory.CommandResolution;

public class DefaultCommandResolverPolicy : ICommandResolverPolicy
{
    public CompositeCommandResolver CreateCommandResolver(string currentWorkingDirectory = null)
    {
        return Create(currentWorkingDirectory);
    }

    public static CompositeCommandResolver Create(string currentWorkingDirectory = null)
    {
        var environment = new EnvironmentProvider();
        var packagedCommandSpecFactory = new PackagedCommandSpecFactoryWithCliRuntime();
        var publishedPathCommandSpecFactory = new PublishPathCommandSpecFactory();

        IPlatformCommandSpecFactory platformCommandSpecFactory;
        if (OperatingSystem.IsWindows())
        {
            platformCommandSpecFactory = new WindowsExePreferredCommandSpecFactory();
        }
        else
        {
            platformCommandSpecFactory = new GenericPlatformCommandSpecFactory();
        }

        return CreateDefaultCommandResolver(
            environment,
            packagedCommandSpecFactory,
            platformCommandSpecFactory,
            publishedPathCommandSpecFactory,
            currentWorkingDirectory);
    }

    public static CompositeCommandResolver CreateDefaultCommandResolver(
        IEnvironmentProvider environment,
        IPackagedCommandSpecFactory packagedCommandSpecFactory,
        IPlatformCommandSpecFactory platformCommandSpecFactory,
        IPublishedPathCommandSpecFactory publishedPathCommandSpecFactory,
        string currentWorkingDirectory = null)
    {
        var compositeCommandResolver = new CompositeCommandResolver();

        compositeCommandResolver.AddCommandResolver(new MuxerCommandResolver());
        compositeCommandResolver.AddCommandResolver(new DotnetToolsCommandResolver());
        compositeCommandResolver.AddCommandResolver(new LocalToolsCommandResolver(currentWorkingDirectory: currentWorkingDirectory));
        compositeCommandResolver.AddCommandResolver(new RootedCommandResolver());
        compositeCommandResolver.AddCommandResolver(
            new ProjectToolsCommandResolver(packagedCommandSpecFactory, environment));
        compositeCommandResolver.AddCommandResolver(new AppBaseDllCommandResolver());
        compositeCommandResolver.AddCommandResolver(
            new AppBaseCommandResolver(environment, platformCommandSpecFactory));
        compositeCommandResolver.AddCommandResolver(
            new PathCommandResolver(environment, platformCommandSpecFactory));
        compositeCommandResolver.AddCommandResolver(
            new PublishedPathCommandResolver(environment, publishedPathCommandSpecFactory));

        return compositeCommandResolver;
    }
}
