// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Reference.Add;

internal static class ReferenceAddCommandParser
{
    public static readonly CliArgument<IEnumerable<string>> ProjectPathArgument = new(CliCommandStrings.ReferenceAddProjectPathArgumentName)
    {
        Description = CliCommandStrings.ReferenceAddProjectPathArgumentDescription,
        Arity = ArgumentArity.OneOrMore,
        CustomParser = arguments =>
        {
            var result = arguments.Tokens.TakeWhile(t => !t.Value.StartsWith("-"));
            arguments.OnlyTake(result.Count());
            return result.Select(t => t.Value);
        }
    };

    public static readonly CliOption<string> FrameworkOption = new DynamicOption<string>("--framework", "-f")
    {
        Description = CliCommandStrings.ReferenceAddCmdFrameworkDescription,
        HelpName = CliStrings.CommonCmdFramework

    }.AddCompletions(CliCompletion.TargetFrameworksFromProjectFile);

    public static readonly CliOption<bool> InteractiveOption = CommonOptions.InteractiveOption();

    private static readonly CliCommand Command = ConstructCommand();

    public static CliCommand GetCommand()
    {
        return Command;
    }

    private static CliCommand ConstructCommand()
    {
        CliCommand command = new("add", CliCommandStrings.ReferenceAddAppFullName);

        command.Arguments.Add(ProjectPathArgument);
        command.Options.Add(FrameworkOption);
        command.Options.Add(InteractiveOption);

        command.SetAction((parseResult) => new ReferenceAddCommand(parseResult).Execute());

        return command;
    }
}
