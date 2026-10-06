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
public sealed record GuideValue(string Key, string Label, string Value, GuideValueKind Kind, string Guidance)
{
    public bool CopyInForm { get; init; }
    public bool ReferenceOnly { get; init; }
    public bool IsTechnical { get; init; } = true;
    public bool CanCopy { get; init; } = true;
}
public sealed record ImplementationFacts(ImmutableArray<GuideValue> Values);
public sealed record CodeArtifact(string Id, string ComponentId, string Language, string DestinationFile, string Content) { public string? ExecutionLocation { get; init; } }
public sealed record StepCompletion(string StepId, CompletionState State, string? BlockerExplanation = null);

/// <summary>Typed content is shared by the UI and Markdown export.</summary>
public abstract record GuideContent { public string GroupTitle { get; init; } = ""; public string GroupSystem { get; init; } = ""; public ImmutableArray<string> GroupLocation { get; init; } = []; }
public sealed record InstructionContent(string Text) : GuideContent { public string Title { get; init; } = ""; public ImmutableArray<string> Location { get; init; } = []; public string LocationLabel { get; init; } = "Application configuration"; }
public sealed record PortalActionContent(ImmutableArray<string> Breadcrumbs, string Action) : GuideContent { public string Title { get; init; } = "Configure in Entra"; }
public sealed record CopyableValueContent(GuideValue Value) : GuideContent;
public sealed record ConfigurationRowsContent(string Title, ImmutableArray<string> Breadcrumbs, ImmutableArray<GuideValue> Values, string Instruction) : GuideContent { public string Introduction { get; init; } = ""; }
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
