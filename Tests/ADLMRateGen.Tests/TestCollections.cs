using Xunit;

namespace ADLMRateGen.Tests;

/// <summary>
/// MaterialLibraryService and LabourLibraryService hold static state and raise a
/// static LibraryChanged event. Tests that reload them must therefore run one at
/// a time, or one test's reload fires into another's live RateEntryItem objects
/// and the results are non-deterministic.
/// </summary>
public static class TestCollections
{
    public const string LibraryState = "library-state";

    /// <summary>
    /// Tests that set or clear process environment variables. The environment is
    /// process-wide, so two of them running at once read each other's values.
    /// </summary>
    public const string ProcessEnvironment = "process-environment";
}

[CollectionDefinition(TestCollections.LibraryState, DisableParallelization = true)]
public class LibraryStateCollection
{
}

[CollectionDefinition(TestCollections.ProcessEnvironment, DisableParallelization = true)]
public class ProcessEnvironmentCollection
{
}
