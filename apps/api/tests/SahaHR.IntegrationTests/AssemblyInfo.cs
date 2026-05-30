// These integration tests each boot a dedicated Postgres container + API host and override the
// connection via a process-global environment variable. Running test classes in parallel would let
// those env-var writes clobber each other (writes land in the wrong container). Serialize them —
// standard practice for container-backed integration suites.
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]
