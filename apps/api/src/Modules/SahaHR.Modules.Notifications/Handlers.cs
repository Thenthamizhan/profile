using System.Text.Json;
using SahaHR.Common.Eventing;

namespace SahaHR.Modules.Notifications;

// Notifications consumes events by TOPIC STRING and deserializes payloads into LOCAL DTOs — no
// reference to People or Recruitment (FF-1). This is the second consumer in the system; together
// with People's CandidateHiredHandler it proves event FAN-OUT: one recruitment.CandidateHired
// event drives BOTH an employee auto-provision AND a welcome notification, independently.

internal static class Json
{
    public static readonly JsonSerializerOptions Opts = new() { PropertyNameCaseInsensitive = true };
}

/// On a new employee (whether created directly or auto-provisioned from a hire), record a welcome.
public sealed class EmployeeHiredNotifier : IDomainEventHandler
{
    public string EventType => "people.EmployeeHired";

    private readonly NotificationService _notifications;
    public EmployeeHiredNotifier(NotificationService notifications) => _notifications = notifications;

    private sealed record Payload(Guid EmployeeId, Guid CompanyId, string? EmployeeNo);

    public async Task HandleAsync(string payload, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<Payload>(payload, Json.Opts)
            ?? throw new InvalidOperationException("EmployeeHired payload was empty.");
        await _notifications.RecordAsync(
            topic: EventType,
            subject: $"Welcome aboard — employee {e.EmployeeNo}",
            body: "Onboarding tasks have been queued.",
            recipient: null,
            ct);
    }
}

/// On a candidate hire, notify the recruiting team that the seat is filled.
public sealed class CandidateHiredNotifier : IDomainEventHandler
{
    public string EventType => "recruitment.CandidateHired";

    private readonly NotificationService _notifications;
    public CandidateHiredNotifier(NotificationService notifications) => _notifications = notifications;

    private sealed record Payload(Guid ApplicationId, Guid JobId, string? CandidateName);

    public async Task HandleAsync(string payload, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<Payload>(payload, Json.Opts)
            ?? throw new InvalidOperationException("CandidateHired payload was empty.");
        await _notifications.RecordAsync(
            topic: EventType,
            subject: $"Candidate hired: {e.CandidateName ?? "(unnamed)"}",
            body: $"Application {e.ApplicationId} closed as hired.",
            recipient: null,
            ct);
    }
}
