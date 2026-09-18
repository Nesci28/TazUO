// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using static SDL3.SDL;

namespace ClassicUO.Input;

/// <summary>Copies SDL callbacks for processing on the game thread, outside SDL's event lock.</summary>
internal sealed unsafe class MobileSdlEventQueue
{
    private readonly ConcurrentQueue<(SDL_Event Event, byte[] Text)> _events = new();

    internal bool Enqueue(IntPtr userdata, SDL_Event* source)
    {
        if (source == null)
            return false;

        SDL_Event copy = *source;
        byte[] text = null;
        if ((SDL_EventType)copy.type == SDL_EventType.SDL_EVENT_TEXT_INPUT)
        {
            // SDL owns this pointer only during event delivery. Keep our own
            // bytes until the game consumes the event, including its terminator.
            string value = Marshal.PtrToStringUTF8((IntPtr)copy.text.text);
            text = Encoding.UTF8.GetBytes((value ?? string.Empty) + '\0');
            copy.text.text = null;
        }

        _events.Enqueue((copy, text));
        return true;
    }

    internal void Drain(SDL_EventFilter handler)
    {
        while (_events.TryDequeue(out var item))
        {
            SDL_Event current = item.Event;
            fixed (byte* text = item.Text)
            {
                if (item.Text != null)
                    current.text.text = text;
                handler(IntPtr.Zero, &current);
            }
        }
    }
}
