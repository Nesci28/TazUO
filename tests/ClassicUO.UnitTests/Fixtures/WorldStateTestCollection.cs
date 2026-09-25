using Xunit;

namespace ClassicUO.UnitTests.Fixtures;

/// <summary>Prevents tests that replace process-wide client state from running beside other collections.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorldStateTestCollection
{
    public const string Name = "World state collection";
}
