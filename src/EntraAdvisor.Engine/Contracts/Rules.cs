using System.Collections.Immutable;

namespace EntraAdvisor.Engine.Contracts;

public enum RulePhase { InputValidation, SupportedBoundary, RequiredFacts, Classification, IdentityAndFlow, Authorization, PlanAssembly }

/// <summary>Metadata only; evaluator execution is implemented in milestone 2.</summary>
public sealed record RuleDefinition(
    string Id,
    RulePhase Phase,
    int Priority,
    string Rationale,
    ImmutableArray<DocumentationSource> Sources);
