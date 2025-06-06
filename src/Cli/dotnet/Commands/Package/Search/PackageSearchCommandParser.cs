﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Package.Search;

internal static class PackageSearchCommandParser
{
    public static readonly CliArgument<string> SearchTermArgument = new CliArgument<string>("SearchTerm")
    {
        HelpName = CliCommandStrings.PackageSearchSearchTermArgumentName,
        Description = CliCommandStrings.PackageSearchSearchTermDescription,
        Arity = ArgumentArity.ZeroOrOne
    };

    public static readonly CliOption Sources = new ForwardedOption<IEnumerable<string>>("--source")
    {
        Description = CliCommandStrings.SourceDescription,
        HelpName = CliCommandStrings.SourceArgumentName
    }.ForwardAsManyArgumentsEachPrefixedByOption("--source")
    .AllowSingleArgPerToken();

    public static readonly CliOption<string> Take = new ForwardedOption<string>("--take")
    {
        Description = CliCommandStrings.PackageSearchTakeDescription,
        HelpName = CliCommandStrings.PackageSearchTakeArgumentName
    }.ForwardAsSingle(o => $"--take:{o}");

    public static readonly CliOption<string> Skip = new ForwardedOption<string>("--skip")
    {
        Description = CliCommandStrings.PackageSearchSkipDescription,
        HelpName = CliCommandStrings.PackageSearchSkipArgumentName
    }.ForwardAsSingle(o => $"--skip:{o}");

    public static readonly CliOption<bool> ExactMatch = new ForwardedOption<bool>("--exact-match")
    {
        Description = CliCommandStrings.ExactMatchDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--exact-match");

    public static readonly CliOption<bool> Interactive = CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    public static readonly CliOption<bool> Prerelease = new ForwardedOption<bool>("--prerelease")
    {
        Description = CliCommandStrings.PackageSearchPrereleaseDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--prerelease");

    public static readonly CliOption<string> ConfigFile = new ForwardedOption<string>("--configfile")
    {
        Description = CliCommandStrings.ConfigFileDescription,
        HelpName = CliCommandStrings.ConfigFileArgumentName
    }.ForwardAsSingle(o => $"--configfile:{o}");

    public static readonly CliOption<string> Format = new ForwardedOption<string>("--format")
    {
        Description = CliCommandStrings.FormatDescription,
        HelpName = CliCommandStrings.FormatArgumentName
    }.ForwardAsSingle(o => $"--format:{o}");

    public static readonly CliOption<string> Verbosity = new ForwardedOption<string>("--verbosity")
    {
        Description = CliCommandStrings.VerbosityDescription,
        HelpName = CliCommandStrings.VerbosityArgumentName
    }.ForwardAsSingle(o => $"--verbosity:{o}");

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand searchCommand = new("search", CliCommandStrings.PackageSearchCommandDescription);

        searchCommand.Arguments.Add(SearchTermArgument);
        searchCommand.Options.Add(Sources);
        searchCommand.Options.Add(Take);
        searchCommand.Options.Add(Skip);
        searchCommand.Options.Add(ExactMatch);
        searchCommand.Options.Add(Interactive);
        searchCommand.Options.Add(Prerelease);
        searchCommand.Options.Add(ConfigFile);
        searchCommand.Options.Add(Format);
        searchCommand.Options.Add(Verbosity);

        searchCommand.SetAction((parseResult) =>
        {
            var command = new PackageSearchCommand(parseResult);
            int exitCode = command.Execute();

            if (exitCode == 1)
            {
                parseResult.ShowHelp();
            }
            // Only return 1 or 0
            return exitCode == 0 ? 0 : 1;
        });

        return searchCommand;
    }
}
