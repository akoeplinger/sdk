// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.VisualStudio.SolutionPersistence;
using Microsoft.VisualStudio.SolutionPersistence.Model;
using Microsoft.VisualStudio.SolutionPersistence.Serializer.SlnV12;

namespace Microsoft.DotNet.Cli.Commands.Solution.Remove;

internal class SolutionRemoveCommand : CommandBase
{
    private readonly string _fileOrDirectory;
    private readonly IReadOnlyCollection<string> _projects;

    public SolutionRemoveCommand(ParseResult parseResult) : base(parseResult)
    {
        _fileOrDirectory = parseResult.GetValue(SolutionCommandParser.SlnArgument);

        _projects = (parseResult.GetValue(SolutionRemoveCommandParser.ProjectPathArgument) ?? []).ToList().AsReadOnly();

        SolutionArgumentValidator.ParseAndValidateArguments(_fileOrDirectory, _projects, SolutionArgumentValidator.CommandType.Remove);
    }

    public override int Execute()
    {
        string solutionFileFullPath = SlnFileFactory.GetSolutionFileFullPath(_fileOrDirectory);
        if (_projects.Count == 0)
        {
            throw new GracefulException(CliStrings.SpecifyAtLeastOneProjectToRemove);
        }

        try
        {
            var relativeProjectPaths = _projects
                .Select(p => Path.GetFullPath(p))
                .Select(p => Path.GetRelativePath(
                    Path.GetDirectoryName(solutionFileFullPath),
                    Directory.Exists(p)
                        ? MsbuildProject.GetProjectFileFromDirectory(p).FullName
                        : p));

            RemoveProjectsAsync(solutionFileFullPath, relativeProjectPaths, CancellationToken.None).GetAwaiter().GetResult();
            return 0;
        }
        catch (Exception ex) when (ex is not GracefulException)
        {
            if (ex is SolutionException || ex.InnerException is SolutionException)
            {
                throw new GracefulException(CliStrings.InvalidSolutionFormatString, solutionFileFullPath, ex.Message);
            }
            throw new GracefulException(ex.Message, ex);
        }
    }

    private static async Task RemoveProjectsAsync(string solutionFileFullPath, IEnumerable<string> projectPaths, CancellationToken cancellationToken)
    {
        SolutionModel solution = SlnFileFactory.CreateFromFileOrDirectory(solutionFileFullPath);
        ISolutionSerializer serializer = solution.SerializerExtension.Serializer;

        // set UTF-8 BOM encoding for .sln
        if (serializer is ISolutionSerializer<SlnV12SerializerSettings> v12Serializer)
        {
            solution.SerializerExtension = v12Serializer.CreateModelExtension(new()
            {
                Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)
            });
        }

        foreach (var projectPath in projectPaths)
        {
            var project = solution.FindProject(projectPath);
            // If the project is not found, try to find it by name without extension
            if (project is null && !Path.HasExtension(projectPath))
            {
                var projectsMatchByName = solution.SolutionProjects.Where(p => Path.GetFileNameWithoutExtension(p.DisplayName).Equals(projectPath));
                project = projectsMatchByName.Count() == 1 ? projectsMatchByName.First() : null;
            }
            // If project is still not found, print error
            if (project is null)
            {
                Reporter.Output.WriteLine(CliStrings.ProjectNotFoundInTheSolution, projectPath);
            }
            // If project is found, remove it
            else
            {
                solution.RemoveProject(project);
                Reporter.Output.WriteLine(CliStrings.ProjectRemovedFromTheSolution, projectPath);
            }
        }

        for (int i = 0; i < solution.SolutionFolders.Count; i++)
        {
            var folder = solution.SolutionFolders[i];
            int nonFolderDescendants = 0;
            Stack<SolutionFolderModel> stack = new();
            stack.Push(folder);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                nonFolderDescendants += current.Files?.Count ?? 0;
                foreach (var child in solution.SolutionItems)
                {
                    if (child is { Parent: var parent } && parent == current)
                    {
                        if (child is SolutionFolderModel childFolder)
                        {
                            stack.Push(childFolder);
                        }
                        else
                        {
                            nonFolderDescendants++;
                        }
                    }
                }
            }

            if (nonFolderDescendants == 0)
            {
                solution.RemoveFolder(folder);
                // After removal, adjust index and continue to avoid skipping folders after removal
                i--; 
            }
        }

        await serializer.SaveAsync(solutionFileFullPath, solution, cancellationToken);
    }
}
