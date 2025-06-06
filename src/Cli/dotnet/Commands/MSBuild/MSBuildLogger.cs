﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;

namespace Microsoft.DotNet.Cli.Commands.MSBuild;

public sealed class MSBuildLogger : INodeLogger
{
    private readonly IFirstTimeUseNoticeSentinel _sentinel =
        new FirstTimeUseNoticeSentinel();
    private readonly ITelemetry _telemetry;

    internal const string TargetFrameworkTelemetryEventName = "targetframeworkeval";
    internal const string BuildTelemetryEventName = "build";
    internal const string LoggingConfigurationTelemetryEventName = "loggingConfiguration";
    internal const string BuildcheckAcquisitionFailureEventName = "buildcheck/acquisitionfailure";
    internal const string BuildcheckRunEventName = "buildcheck/run";
    internal const string BuildcheckRuleStatsEventName = "buildcheck/rule";

    internal const string SdkTaskBaseCatchExceptionTelemetryEventName = "taskBaseCatchException";
    internal const string PublishPropertiesTelemetryEventName = "PublishProperties";
    internal const string WorkloadPublishPropertiesTelemetryEventName = "WorkloadPublishProperties";
    internal const string ReadyToRunTelemetryEventName = "ReadyToRun";

    internal const string TargetFrameworkVersionTelemetryPropertyKey = "TargetFrameworkVersion";
    internal const string RuntimeIdentifierTelemetryPropertyKey = "RuntimeIdentifier";
    internal const string SelfContainedTelemetryPropertyKey = "SelfContained";
    internal const string UseApphostTelemetryPropertyKey = "UseApphost";
    internal const string OutputTypeTelemetryPropertyKey = "OutputType";
    internal const string UseArtifactsOutputTelemetryPropertyKey = "UseArtifactsOutput";
    internal const string ArtifactsPathLocationTypeTelemetryPropertyKey = "ArtifactsPathLocationType";

    /// <summary>
    /// This is defined in <see cref="ComputeDotnetBaseImageAndTag.cs"/>
    /// </summary>
    internal const string SdkContainerPublishBaseImageInferenceEventName = "sdk/container/inference";
    /// <summary>
    /// This is defined in <see cref="CreateNewImage.cs"/>
    /// </summary>
    internal const string SdkContainerPublishSuccessEventName = "sdk/container/publish/success";
    /// <summary>
    /// This is defined in <see cref="CreateNewImage.cs"/>
    /// </summary>
    internal const string SdkContainerPublishErrorEventName = "sdk/container/publish/error";

    public MSBuildLogger()
    {
        try
        {
            string sessionId =
                Environment.GetEnvironmentVariable(MSBuildForwardingApp.TelemetrySessionIdEnvironmentVariableName);

            if (sessionId != null)
            {
                // senderCount: 0 to disable sender.
                // When senders in different process running at the same
                // time they will read from the same global queue and cause
                // sending duplicated events. Disable sender to reduce it.
                _telemetry = new Telemetry.Telemetry(
                    _sentinel,
                    sessionId,
                    senderCount: 0);
            }
        }
        catch (Exception)
        {
            // Exceptions during telemetry shouldn't cause anything else to fail
        }
    }

    public void Initialize(IEventSource eventSource, int nodeCount)
    {
        Initialize(eventSource);
    }

    public void Initialize(IEventSource eventSource)
    {
        // Declare lack of dependency on having properties/items in ProjectStarted events
        // (since this logger doesn't ever care about those events it's irrelevant)
        if (eventSource is IEventSource4 eventSource4)
        {
            eventSource4.IncludeEvaluationPropertiesAndItems();
        }

        try
        {
            if (_telemetry != null && _telemetry.Enabled)
            {
                if (eventSource is IEventSource2 eventSource2)
                {
                    eventSource2.TelemetryLogged += OnTelemetryLogged;
                }
            }
        }
        catch (Exception)
        {
            // Exceptions during telemetry shouldn't cause anything else to fail
        }
    }

    internal static void FormatAndSend(ITelemetry telemetry, TelemetryEventArgs args)
    {
        switch (args.EventName)
        {
            case TargetFrameworkTelemetryEventName:
                {
                    var newEventName = $"msbuild/{TargetFrameworkTelemetryEventName}";
                    Dictionary<string, string> maskedProperties = [];

                    foreach (var key in new[] {
                        TargetFrameworkVersionTelemetryPropertyKey,
                        RuntimeIdentifierTelemetryPropertyKey,
                        SelfContainedTelemetryPropertyKey,
                        UseApphostTelemetryPropertyKey,
                        OutputTypeTelemetryPropertyKey,
                        UseArtifactsOutputTelemetryPropertyKey,
                        ArtifactsPathLocationTypeTelemetryPropertyKey
                    })
                    {
                        if (args.Properties.TryGetValue(key, out string value))
                        {
                            maskedProperties.Add(key, Sha256Hasher.HashWithNormalizedCasing(value));
                        }
                    }

                    telemetry.TrackEvent(newEventName, maskedProperties, measurements: null);
                    break;
                }
            case BuildTelemetryEventName:
                TrackEvent(telemetry, $"msbuild/{BuildTelemetryEventName}", args.Properties,
                    toBeHashed: ["ProjectPath", "BuildTarget"],
                    toBeMeasured: ["BuildDurationInMilliseconds", "InnerBuildDurationInMilliseconds"]);
                break;
            case LoggingConfigurationTelemetryEventName:
                TrackEvent(telemetry, $"msbuild/{LoggingConfigurationTelemetryEventName}", args.Properties,
                    toBeHashed: [],
                    toBeMeasured: ["FileLoggersCount"]);
                break;
            case BuildcheckAcquisitionFailureEventName:
                TrackEvent(telemetry, $"msbuild/{BuildcheckAcquisitionFailureEventName}", args.Properties,
                    toBeHashed: ["AssemblyName", "ExceptionType", "ExceptionMessage"],
                    toBeMeasured: []);
                break;
            case BuildcheckRunEventName:
                TrackEvent(telemetry, $"msbuild/{BuildcheckRunEventName}", args.Properties,
                    toBeHashed: [],
                    toBeMeasured: ["TotalRuntimeInMilliseconds"]);
                break;
            case BuildcheckRuleStatsEventName:
                TrackEvent(telemetry, $"msbuild/{BuildcheckRuleStatsEventName}", args.Properties,
                    toBeHashed: ["RuleId", "CheckFriendlyName"],
                    toBeMeasured: ["TotalRuntimeInMilliseconds"]);
                break;
            // Pass through events that don't need special handling
            case SdkTaskBaseCatchExceptionTelemetryEventName:
            case PublishPropertiesTelemetryEventName:
            case ReadyToRunTelemetryEventName:
            case WorkloadPublishPropertiesTelemetryEventName:
            case SdkContainerPublishBaseImageInferenceEventName:
            case SdkContainerPublishSuccessEventName:
            case SdkContainerPublishErrorEventName:
                TrackEvent(telemetry, args.EventName, args.Properties, [], []);
                break;
            default:
                // Ignore unknown events
                break;
        }
    }

    private static void TrackEvent(ITelemetry telemetry, string eventName, IDictionary<string, string> eventProperties, string[] toBeHashed, string[] toBeMeasured)
    {
        Dictionary<string, string> properties = null;
        Dictionary<string, double> measurements = null;

        foreach (var propertyToBeHashed in toBeHashed)
        {
            if (eventProperties.TryGetValue(propertyToBeHashed, out string value))
            {
                // Lets lazy allocate in case there is tons of telemetry
                properties ??= new Dictionary<string, string>(eventProperties);
                properties[propertyToBeHashed] = Sha256Hasher.HashWithNormalizedCasing(value);
            }
        }

        foreach (var propertyToBeMeasured in toBeMeasured)
        {
            if (eventProperties.TryGetValue(propertyToBeMeasured, out string value))
            {
                // Lets lazy allocate in case there is tons of telemetry
                properties ??= new Dictionary<string, string>(eventProperties);
                properties.Remove(propertyToBeMeasured);
                if (double.TryParse(value, CultureInfo.InvariantCulture, out double realValue))
                {
                    // Lets lazy allocate in case there is tons of telemetry
                    measurements ??= [];
                    measurements[propertyToBeMeasured] = realValue;
                }
            }
        }

        telemetry.TrackEvent(eventName, properties ?? eventProperties, measurements);
    }

    private void OnTelemetryLogged(object sender, TelemetryEventArgs args)
    {
        FormatAndSend(_telemetry, args);
    }

    public void Shutdown()
    {
        try
        {
            _sentinel?.Dispose();
        }
        catch (Exception)
        {
            // Exceptions during telemetry shouldn't cause anything else to fail
        }
    }

    public LoggerVerbosity Verbosity { get; set; }

    public string Parameters { get; set; }
}
