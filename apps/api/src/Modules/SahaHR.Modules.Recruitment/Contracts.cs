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
