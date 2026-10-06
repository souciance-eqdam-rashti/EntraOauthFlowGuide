using System.Collections.Immutable;
using EntraAdvisor.Engine.Contracts;

namespace EntraAdvisor.Guide.Contracts;

public enum GuideSection
{
    Prerequisites, ResourceRegistration, ClientRegistration, PermissionsAndConsent,
    Manifest, Dependencies, Configuration, AuthenticationAndAuthorization, ApiCalls, TestAndTroubleshoot
}
public enum CompletionState { Pending, InProgress, Complete, Blocked }
public enum GuideValueKind { DeveloperSupplied, Sample, Derived }

public sealed record GuideVersions(string Schema, string Rules, string Templates);
public sealed record GuideValue(string Key, string Label, string Value, GuideValueKind Kind, string Guidance);
public sealed record ImplementationFacts(ImmutableArray<GuideValue> Values);
public sealed record CodeArtifact(string Id, string ComponentId, string Language, string DestinationFile, string Content);
public sealed record StepCompletion(string StepId, CompletionState State, string? BlockerExplanation = null);

/// <summary>Typed content is shared by the UI and Markdown export.</summary>
public abstract record GuideContent;
public sealed record InstructionContent(string Text) : GuideContent;
public sealed record PortalActionContent(ImmutableArray<string> Breadcrumbs, string Action) : GuideContent;
public sealed record CopyableValueContent(GuideValue Value) : GuideContent;
public sealed record CodeContent(CodeArtifact Artifact) : GuideContent;
public sealed record ExplanationContent(string Title, string Text) : GuideContent;

public sealed record GuideStep
{
    public required string Id { get; init; }
    public required GuideSection Section { get; init; }
    public required string Title { get; init; }
    public required string Purpose { get; init; }
    public required string ComponentId { get; init; }
    public required string Action { get; init; }
    public required string ExpectedResult { get; init; }
    public ImmutableArray<string> DependsOnStepIds { get; init; } = [];
    public ImmutableArray<string> RelatedRelationshipIds { get; init; } = [];
    public ImmutableArray<GuideContent> Content { get; init; } = [];
    public ImmutableArray<DocumentationSource> Sources { get; init; } = [];
}

public sealed record ImplementationGuide
{
    public required string PlanId { get; init; }
    public required GuideVersions Versions { get; init; }
    public required OAuthPlan Architecture { get; init; }
    public ImmutableArray<GuideStep> Steps { get; init; } = [];
    public ImmutableArray<string> Assumptions { get; init; } = [];
    public ImmutableArray<DocumentationSource> Sources { get; init; } = [];
}

public interface IGuideGenerator
{
    ImplementationGuide Generate(OAuthPlan validatedPlan, ImplementationFacts facts);
}

public interface IGuideExporter
{
    string ExportMarkdown(ImplementationGuide guide);
}
