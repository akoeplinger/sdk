// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Workload.Install;
using Microsoft.DotNet.Cli.Commands.Workload.Install.WorkloadInstallRecords;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using Microsoft.TemplateEngine.Cli.Commands;

namespace Microsoft.DotNet.Cli.Commands.Workload.List;

internal class WorkloadListCommand : WorkloadCommandBase
{
    private readonly bool _includePreviews;
    private readonly bool _machineReadableOption;
    private readonly IWorkloadManifestUpdater _workloadManifestUpdater;
    private readonly WorkloadInfoHelper _workloadListHelper;

    public WorkloadListCommand(
        ParseResult parseResult,
        IReporter reporter = null,
        IWorkloadInstallationRecordRepository workloadRecordRepo = null,
        string currentSdkVersion = null,
        string dotnetDir = null,
        string userProfileDir = null,
        string tempDirPath = null,
        INuGetPackageDownloader nugetPackageDownloader = null,
        IWorkloadManifestUpdater workloadManifestUpdater = null,
        IWorkloadResolver workloadResolver = null
    ) : base(parseResult, CommonOptions.HiddenVerbosityOption, reporter, tempDirPath, nugetPackageDownloader)
    {
        _machineReadableOption = parseResult.GetValue(WorkloadListCommandParser.MachineReadableOption);

        var resolvedReporter = _machineReadableOption ? NullReporter.Instance : Reporter;
        _workloadListHelper = new WorkloadInfoHelper(
            parseResult.HasOption(SharedOptions.InteractiveOption),
            Verbosity,
            parseResult?.GetValue(WorkloadListCommandParser.VersionOption) ?? null,
            VerifySignatures,
            resolvedReporter,
            workloadRecordRepo,
            currentSdkVersion,
            dotnetDir,
            userProfileDir,
            workloadResolver
        );

        _includePreviews = parseResult.GetValue(WorkloadListCommandParser.IncludePreviewsOption);
        string userProfileDir1 = userProfileDir ?? CliFolderPathCalculator.DotnetUserProfileFolderPath;

        _workloadManifestUpdater = workloadManifestUpdater ?? new WorkloadManifestUpdater(resolvedReporter,
            _workloadListHelper.WorkloadResolver, PackageDownloader, userProfileDir1, _workloadListHelper.WorkloadRecordRepo, _workloadListHelper.Installer);
    }

    public override int Execute()
    {
        IEnumerable<WorkloadId> installedList = _workloadListHelper.InstalledSdkWorkloadIds;

        if (_machineReadableOption)
        {
            _workloadListHelper.CheckTargetSdkVersionIsValid();

            var updateAvailable = GetUpdateAvailable(installedList);
            var installed = installedList.Select(id => id.ToString()).ToArray();
            ListOutput listOutput = new(installed, [.. updateAvailable]);

            Reporter.WriteLine(JsonSerializer.Serialize(listOutput, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
        else
        {
            var versionInfo = _workloadListHelper.ManifestProvider.GetWorkloadVersion();
            Reporter.WriteLine();
            var shouldPrintTable = versionInfo.IsInstalled;
            var shouldShowWorkloadSetVersion = versionInfo.GlobalJsonPath is not null ||
                versionInfo.GlobalJsonSpecifiesWorkloadSets == true ||
                WorkloadManifestUpdater.ShouldUseWorkloadSetMode(_workloadListHelper._currentSdkFeatureBand, _workloadListHelper.UserLocalPath);

            if (shouldShowWorkloadSetVersion)
            {
                if (versionInfo.GlobalJsonPath is not null)
                {
                    Reporter.WriteLine(string.Format(
                        versionInfo.IsInstalled ?
                            CliCommandStrings.WorkloadSetFromGlobalJsonInstalled :
                            CliCommandStrings.WorkloadSetFromGlobalJsonNotInstalled,
                        versionInfo.Version,
                        versionInfo.GlobalJsonPath));
                }
                else
                {
                    Reporter.WriteLine(string.Format(CliCommandStrings.WorkloadListWorkloadSetVersion, _workloadListHelper.WorkloadResolver.GetWorkloadVersion().Version ?? "unknown"));
                }

                Reporter.WriteLine();
            }

            if (shouldPrintTable)
            {
                var manifestInfoDict = _workloadListHelper.WorkloadResolver.GetInstalledManifests().ToDictionary(info => info.Id, StringComparer.OrdinalIgnoreCase);
                InstalledWorkloadsCollection installedWorkloads = _workloadListHelper.AddInstalledVsWorkloads(installedList);
                PrintableTable<KeyValuePair<string, string>> table = new();
                table.AddColumn(CliCommandStrings.WorkloadIdColumn, workload => workload.Key);
                table.AddColumn(CliCommandStrings.WorkloadManifestVersionColumn, workload =>
                {
                    var m = _workloadListHelper.WorkloadResolver.GetManifestFromWorkload(new WorkloadId(workload.Key));
                    var manifestInfo = manifestInfoDict[m.Id];
                    return m.Version + "/" + manifestInfo.ManifestFeatureBand;
                });

                table.AddColumn(CliCommandStrings.WorkloadSourceColumn, workload => workload.Value);

                table.PrintRows(installedWorkloads.AsEnumerable().OrderBy(workload => workload.Key), l => Reporter.WriteLine(l));
            }

            Reporter.WriteLine();
            Reporter.WriteLine(CliCommandStrings.WorkloadListFooter);
            Reporter.WriteLine();

            var updatableWorkloads = _workloadManifestUpdater.GetUpdatableWorkloadsToAdvertise(installedList).Select(workloadId => workloadId.ToString());
            if (updatableWorkloads.Any())
            {
                Reporter.WriteLine(string.Format(CliCommandStrings.WorkloadListWorkloadUpdatesAvailable, string.Join(" ", updatableWorkloads)));
                Reporter.WriteLine();
            }
        }

        return 0;
    }

    internal IEnumerable<UpdateAvailableEntry> GetUpdateAvailable(IEnumerable<WorkloadId> installedList)
    {
        // This was an internal partner ask, and they do not need to support workload sets.
        _workloadManifestUpdater.UpdateAdvertisingManifestsAsync(_includePreviews).Wait();
        var manifestsToUpdate = _workloadManifestUpdater.CalculateManifestUpdates();

        foreach ((ManifestVersionUpdate manifestUpdate, WorkloadCollection workloads) in manifestsToUpdate)
        {
            foreach ((WorkloadId workloadId, WorkloadDefinition workloadDefinition) in workloads)
            {
                if (installedList.Contains(workloadId))
                {
                    var existingVersion = _workloadListHelper.WorkloadResolver.GetManifestVersion(manifestUpdate.ManifestId.ToString());

                    yield return new UpdateAvailableEntry(existingVersion,
                        manifestUpdate.NewVersion.ToString(),
                        workloadDefinition.Description, workloadId.ToString());
                }
            }
        }
    }

    internal record ListOutput(string[] Installed, UpdateAvailableEntry[] UpdateAvailable);

    internal record UpdateAvailableEntry(string ExistingManifestVersion, string AvailableUpdateManifestVersion,
        string Description, string WorkloadId);
}
