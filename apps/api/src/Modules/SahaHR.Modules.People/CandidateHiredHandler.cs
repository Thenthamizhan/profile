using System.Text.Json;
using SahaHR.Common.Eventing;

namespace SahaHR.Modules.People;

/// Consumes recruitment.CandidateHired and provisions the employee record — the §5.2 "a hire becomes
/// an employee" seam.
///
/// Boundary note (FF-1): People must NOT reference the Recruitment module. So this handler subscribes
/// by the topic STRING and deserializes the JSON payload into a LOCAL shape, exactly as a separate
/// Kafka consumer service would. There is no shared C# event type across the boundary.
public sealed class CandidateHiredHandler : IDomainEventHandler
{
    public string EventType => "recruitment.CandidateHired";

    private readonly EmployeeService _employees;
    public CandidateHiredHandler(EmployeeService employees) => _employees = employees;

    // Local mirror of the payload — intentionally decoupled from Recruitment's CandidateHired record.
    private sealed record HiredPayload(
        Guid ApplicationId, Guid CompanyId, string? CandidateName, string? CandidateEmail);

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task HandleAsync(string payload, CancellationToken ct)
    {
        var e = JsonSerializer.Deserialize<HiredPayload>(payload, Json)
            ?? throw new InvalidOperationException("CandidateHired payload was empty.");

        if (e.CompanyId == Guid.Empty)
            throw new InvalidOperationException("CandidateHired is missing CompanyId; cannot provision employee.");

        await _employees.CreateFromHiredCandidateAsync(
            e.ApplicationId, e.CompanyId, e.CandidateName, e.CandidateEmail, ct);
    }
}
