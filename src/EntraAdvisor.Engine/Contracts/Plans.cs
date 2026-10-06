using System.Collections.Immutable;

namespace EntraAdvisor.Engine.Contracts;

public enum EvaluationStatus { Ready, NeedsClarification, Unsupported, Invalid }
public enum ClientClassification { NotApplicable, Public, Confidential }
public enum SignInApproach { None, OpenIdConnectAuthorizationCode, InteractivePublicClient, DeviceCode }
public enum TokenAcquisition { AuthorizationCode, InteractivePublicClient, DeviceCode, OnBehalfOf, ClientCredentials }
public enum CredentialMechanism { None, ManagedIdentity, Certificate, WorkloadFederation }
public enum PermissionMode { DelegatedScopes, ApplicationPermissions, AzureRoleAssignment, AzureResourceAuthorization }
public enum PrerequisiteKind { ResourceCompatibility, TenantPolicy, Consent, Assignment, DeploymentValue }

public sealed record DocumentationSource(string Title, Uri Url, DateOnly ReviewedOn);
public sealed record PlanVersions(string Schema, string Rules);
public sealed record DecisionIssue(string Code, string Explanation, string NextAction, FactReference? Fact = null);
public sealed record PlanPrerequisite(string Id, PrerequisiteKind Kind, string Explanation, string NextAction, string? RelationshipId = null);
public sealed record DecisionTraceEntry(string RuleId, ImmutableArray<FactReference> RelevantFacts, string Rationale);

public sealed record ComponentDecision(
    string ComponentId,
    ClientClassification Client,
    SignInApproach SignIn,
    bool UsesPkce,
    bool ValidatesIncomingTokens,
    CredentialMechanism Credential = CredentialMechanism.None);

public sealed record AuthorizationRequirement(
    PermissionMode Mode,
    string DeveloperValueKey,
    string SelectionGuidance);

public sealed record RelationshipDecision(
    string RelationshipId,
    ActingIdentity Identity,
    TokenAcquisition Acquisition,
    CredentialMechanism Credential,
    string AudienceValueKey,
    AuthorizationRequirement Authorization,
    ImmutableArray<string> MatchedRuleIds);

public sealed record ApiAuthorizationRequirement(
    string ResourceId,
    ActingIdentity AcceptedIdentity,
    PermissionMode Mode,
    string DeveloperValueKey);

public sealed record ApiValidationDecision(
    string ComponentId,
    string AudienceValueKey,
    string IssuerValueKey,
    ImmutableArray<ApiAuthorizationRequirement> Authorization);

public sealed record RegistrationResponsibility(
    string ComponentId,
    bool CreateRegistration,
    bool CreateServicePrincipal,
    string AccountAudience,
    string Purpose);

public sealed record OAuthPlan
{
    public required string Id { get; init; }
    public required string ScenarioId { get; init; }
    public required PlanVersions Versions { get; init; }
    public required ArchitectureScenario Scenario { get; init; }
    public ImmutableArray<ComponentDecision> Components { get; init; } = [];
    public ImmutableArray<RelationshipDecision> Relationships { get; init; } = [];
    public ImmutableArray<ApiValidationDecision> ApiValidation { get; init; } = [];
    public ImmutableArray<RegistrationResponsibility> Registrations { get; init; } = [];
    public ImmutableArray<PlanPrerequisite> Prerequisites { get; init; } = [];
    public ImmutableArray<string> Assumptions { get; init; } = [];
    public ImmutableArray<DocumentationSource> Sources { get; init; } = [];
    public ImmutableArray<DecisionTraceEntry> Trace { get; init; } = [];
}

/// <summary>Only Ready results carry plans; other statuses retain explicit questions or blockers.</summary>
public sealed record EvaluationResult
{
    public EvaluationStatus Status { get; }
    public OAuthPlan? Plan { get; }
    public ImmutableArray<string> NextQuestionIds { get; }
    public ImmutableArray<DecisionIssue> Issues { get; }

    private EvaluationResult(EvaluationStatus status, OAuthPlan? plan,
        ImmutableArray<string> questions, ImmutableArray<DecisionIssue> issues)
    {
        Status = status;
        Plan = plan;
        NextQuestionIds = questions;
        Issues = issues;
    }

    public static EvaluationResult Ready(OAuthPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(EvaluationStatus.Ready, plan, [], []);
    }

    public static EvaluationResult NeedsClarification(ImmutableArray<string> questions, ImmutableArray<DecisionIssue> issues)
    {
        if (questions.IsDefaultOrEmpty)
            throw new ArgumentException("Clarification requires an applicable question.", nameof(questions));
        return new(EvaluationStatus.NeedsClarification, null, questions, issues);
    }

    public static EvaluationResult Unsupported(ImmutableArray<DecisionIssue> issues) => WithoutPlan(EvaluationStatus.Unsupported, issues);
    public static EvaluationResult Invalid(ImmutableArray<DecisionIssue> issues, ImmutableArray<string> correctiveQuestions)
    {
        if (correctiveQuestions.IsDefaultOrEmpty)
            throw new ArgumentException("Invalid input requires a corrective question.", nameof(correctiveQuestions));
        return WithoutPlan(EvaluationStatus.Invalid, issues, correctiveQuestions);
    }

    private static EvaluationResult WithoutPlan(EvaluationStatus status, ImmutableArray<DecisionIssue> issues,
        ImmutableArray<string> questions = default)
    {
        if (issues.IsDefaultOrEmpty)
            throw new ArgumentException("A blocked result requires an explanation and next action.", nameof(issues));
        return new(status, null, questions.IsDefault ? [] : questions, issues);
    }
}

public interface IArchitectureEvaluator
{
    EvaluationResult Evaluate(ArchitectureScenario scenario);
}
