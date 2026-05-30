using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SahaHR.Common.Domain;

namespace SahaHR.Modules.Recruitment.Domain;

public sealed class Pipeline : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public string Name { get; set; } = default!;
    public string Stages { get; set; } = "[]";   // jsonb: ordered [{key,name}]
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class Job : Entity, ITenantScoped, ISoftDelete
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PipelineId { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string Status { get; set; } = "open";   // draft|open|on_hold|closed
    public string? Location { get; set; }
    public string? EmploymentType { get; set; }
    public DateTimeOffset? PostedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class Candidate : Entity, ITenantScoped, ISoftDelete
{
    public Guid TenantId { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Source { get; set; }
    public string? ResumeS3Key { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class Application : Entity, ITenantScoped
{
    public Guid TenantId { get; set; }
    public Guid JobId { get; set; }
    public Guid CandidateId { get; set; }
    public string CurrentStage { get; set; } = "applied";
    public decimal? MatchScore { get; set; }       // numeric(5,2) — never float
    public string Status { get; set; } = "active";  // active|rejected|hired|withdrawn
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

// ---- EF configurations ----

public sealed class PipelineConfiguration : IEntityTypeConfiguration<Pipeline>
{
    public void Configure(EntityTypeBuilder<Pipeline> b)
    {
        b.ToTable("pipeline");
        b.HasKey(x => x.Id);
        b.Property(x => x.Stages).HasColumnName("stages").HasColumnType("jsonb");
    }
}

public sealed class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> b)
    {
        b.ToTable("job");
        b.HasKey(x => x.Id);
    }
}

public sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> b)
    {
        b.ToTable("candidate");
        b.HasKey(x => x.Id);
        // snake_case convention yields "resume_s3key"; the column is "resume_s3_key". Map explicitly.
        b.Property(x => x.ResumeS3Key).HasColumnName("resume_s3_key");
    }
}

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<Application>
{
    public void Configure(EntityTypeBuilder<Application> b)
    {
        b.ToTable("application");
        b.HasKey(x => x.Id);
        b.Property(x => x.MatchScore).HasColumnType("numeric(5,2)");
        b.HasIndex(x => new { x.JobId, x.CandidateId }).IsUnique();
    }
}
