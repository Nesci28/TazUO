using ClassicUO.Game.UI.Gumps;
using FluentAssertions;
using Xunit;

namespace ClassicUO.UnitTests.Game;

public class ChatMessageHistoryTests
{
    [Fact]
    public void Previous_ShouldReturnMessagesFromNewestToOldest()
    {
        var history = new ChatMessageHistory();
        history.Add(ChatMode.Default, "first");
        history.Add(ChatMode.Party, "second");

        history
            .Previous(ChatMode.Default, string.Empty)
            .Should()
            .Be(new ChatHistoryEntry(ChatMode.Party, "second"));
        history
            .Previous(ChatMode.Default, string.Empty)
            .Should()
            .Be(new ChatHistoryEntry(ChatMode.Default, "first"));
        history
            .Previous(ChatMode.Default, string.Empty)
            .Should()
            .Be(new ChatHistoryEntry(ChatMode.Default, "first"));
    }

    [Fact]
    public void Next_ShouldRestoreDraftAfterNewestMessage()
    {
        var history = new ChatMessageHistory();
        history.Add(ChatMode.Default, "first");
        history.Add(ChatMode.Party, "second");

        history.Previous(ChatMode.Guild, "unfinished draft");
        history.Previous(ChatMode.Guild, "ignored while browsing");

        history.Next().Should().Be(new ChatHistoryEntry(ChatMode.Party, "second"));
        history.Next().Should().Be(new ChatHistoryEntry(ChatMode.Guild, "unfinished draft"));
        history.Next().Should().BeNull();
    }

    [Fact]
    public void Add_ShouldSkipOnlyConsecutiveDuplicates()
    {
        var history = new ChatMessageHistory();
        history.Add(ChatMode.Default, "same");
        history.Add(ChatMode.Default, "same");
        history.Add(ChatMode.Party, "same");

        history.Count.Should().Be(2);
    }
}
