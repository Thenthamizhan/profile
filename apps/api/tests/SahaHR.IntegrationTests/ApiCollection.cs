using Xunit;

namespace SahaHR.IntegrationTests;

/// One Postgres container + one API host for the WHOLE test assembly.
///
/// Why this matters: SahaHrApiFactory pins the API to its container by writing the connection
/// string to a process-global environment variable (Program.cs reads it at startup). If each test
/// class had its OWN factory, the second factory's env-var write would clobber the first, pointing
/// one class's API at the wrong container -> 500s. A single shared collection fixture guarantees
/// exactly one container and one env-var value for every test.
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<SahaHrApiFactory>
{
    public const string Name = "sahahr-api";
}
