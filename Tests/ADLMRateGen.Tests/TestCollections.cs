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
}

[CollectionDefinition(TestCollections.LibraryState, DisableParallelization = true)]
public class LibraryStateCollection
{
}
