// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Commands.Workload.Install;

namespace Microsoft.DotNet.Cli.Commands.Workload.Uninstall;

internal static class WorkloadUninstallCommandParser
{
    public static readonly CliArgument<IEnumerable<string>> WorkloadIdArgument = WorkloadInstallCommandParser.WorkloadIdArgument;

    public static readonly CliOption<string> VersionOption = InstallingWorkloadCommandParser.VersionOption;

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("uninstall", CliCommandStrings.WorkloadUninstallCommandDescription);
        command.Arguments.Add(WorkloadIdArgument);
        command.Options.Add(WorkloadInstallCommandParser.SkipSignCheckOption);
        command.Options.Add(CommonOptions.VerbosityOption);

        command.SetAction((parseResult) => new WorkloadUninstallCommand(parseResult).Execute());

        return command;
    }
}
