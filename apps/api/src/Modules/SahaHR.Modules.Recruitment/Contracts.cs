namespace SahaHR.Modules.Recruitment.Contracts;

public sealed record CreateJobRequest(Guid CompanyId, Guid PipelineId, string Title, string? Location, string? EmploymentType);
public sealed record JobResponse(Guid Id, string Title, string Status, string? Location, string? EmploymentType, Guid PipelineId);

public sealed record CreateCandidateRequest(string FullName, string? Email, string? Source);
public sealed record CandidateResponse(Guid Id, string? FullName, string? Email, string? Source);

public sealed record CreateApplicationRequest(Guid JobId, Guid CandidateId, decimal? MatchScore);
public sealed record MoveApplicationRequest(string ToStage);
public sealed record ApplicationResponse(Guid Id, Guid JobId, Guid CandidateId, string CurrentStage, decimal? MatchScore, string Status);

// Kanban board projection: ordered stage columns, each holding its applications + candidate name.
public sealed record StageColumn(string Key, string Name, IReadOnlyList<KanbanCard> Cards);
public sealed record KanbanCard(Guid ApplicationId, Guid CandidateId, string CandidateName, decimal? MatchScore, string Stage);
public sealed record BoardResponse(Guid JobId, string JobTitle, IReadOnlyList<StageColumn> Columns);

// ---- Offers (money is decimal) ----
public sealed record CreateOfferRequest(decimal? Salary, string? Currency);
public sealed record RespondOfferRequest(string Decision);   // accepted|declined
public sealed record OfferResponse(
    Guid Id, Guid ApplicationId, decimal? Salary, string? Currency, string Status,
    DateTimeOffset? SentAt, DateTimeOffset? RespondedAt);

// ---- Scorecards ----
public sealed record ScheduleInterviewRequest(DateTimeOffset? ScheduledAt, Guid[]? Interviewers);
public sealed record CompetencyScore(string Name, double Weight, int Score);   // Score 1..5
public sealed record SubmitScorecardRequest(IReadOnlyList<CompetencyScore> Competencies, string? Recommendation, string? Notes);
public sealed record InterviewResponse(
    Guid Id, Guid ApplicationId, DateTimeOffset? ScheduledAt, Guid[] Interviewers,
    double? RollupScore, string? Recommendation);
