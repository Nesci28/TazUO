using Xunit;

namespace ClassicUO.UnitTests.Game;

/// <summary>
/// Serializes tests that create and clear a World because those operations also mutate global UI state.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class WorldStateTestCollection
{
    public const string Name = "World state collection";
}
