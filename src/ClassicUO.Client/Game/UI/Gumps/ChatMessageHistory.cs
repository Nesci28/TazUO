using System.Collections.Generic;

namespace ClassicUO.Game.UI.Gumps;

internal readonly record struct ChatHistoryEntry(ChatMode Mode, string Text);

internal sealed class ChatMessageHistory
{
    private readonly List<ChatHistoryEntry> _entries = [];
    private ChatHistoryEntry _draft;
    private int _index;

    internal int Count => _entries.Count;

    internal void Add(ChatMode mode, string text)
    {
        var entry = new ChatHistoryEntry(mode, text);

        if (_entries.Count == 0 || _entries[^1] != entry)
        {
            _entries.Add(entry);
        }

        ResetNavigation();
    }

    internal ChatHistoryEntry? Previous(ChatMode currentMode, string currentText)
    {
        if (_entries.Count == 0)
        {
            return null;
        }

        if (_index >= _entries.Count)
        {
            _index = _entries.Count;
            _draft = new ChatHistoryEntry(currentMode, currentText);
        }

        if (_index > 0)
        {
            _index--;
        }

        return _entries[_index];
    }

    internal ChatHistoryEntry? Next()
    {
        if (_entries.Count == 0 || _index >= _entries.Count)
        {
            return null;
        }

        _index++;

        return _index < _entries.Count ? _entries[_index] : _draft;
    }

    internal void ResetNavigation()
    {
        _index = _entries.Count;
        _draft = default;
    }
}
