using SahaHR.Common.Eventing;

namespace SahaHR.Modules.Recruitment.Events;

/// Emitted when an application changes stage. Downstream consumers (Notifications, Analytics)
/// react via the event log rather than direct calls (architecture §5.2, §10.1).
public sealed record ApplicationMoved : DomainEvent
{
    public required Guid ApplicationId { get; init; }
    public required Guid JobId { get; init; }
    public required string FromStage { get; init; }
    public required string ToStage { get; init; }
    public override string EventType => "recruitment.ApplicationMoved";
}

/// Emitted when an application reaches the terminal "hired" stage — the seam where People would
/// create the employee record (§5.2 "a hire becomes an employee").
public sealed record CandidateHired : DomainEvent
{
    public required Guid ApplicationId { get; init; }
    public required Guid CandidateId { get; init; }
    public required Guid JobId { get; init; }
    public override string EventType => "recruitment.CandidateHired";
}

/// Offer extended to a candidate (draft → sent).
public sealed record OfferExtended : DomainEvent
{
    public required Guid OfferId { get; init; }
    public required Guid ApplicationId { get; init; }
    public override string EventType => "recruitment.OfferExtended";
}

/// Offer accepted — pairs with CandidateHired (the application is moved to hired on accept).
public sealed record OfferAccepted : DomainEvent
{
    public required Guid OfferId { get; init; }
    public required Guid ApplicationId { get; init; }
    public override string EventType => "recruitment.OfferAccepted";
}

/// Offer declined by the candidate.
public sealed record OfferDeclined : DomainEvent
{
    public required Guid OfferId { get; init; }
    public required Guid ApplicationId { get; init; }
    public override string EventType => "recruitment.OfferDeclined";
}

/// An interview scorecard was submitted; carries the weighted roll-up score for analytics.
public sealed record ScorecardSubmitted : DomainEvent
{
    public required Guid InterviewId { get; init; }
    public required Guid ApplicationId { get; init; }
    public required double RollupScore { get; init; }
    public override string EventType => "recruitment.ScorecardSubmitted";
}
