using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using ClassicUO.Input;
using Xunit;
using static SDL3.SDL;

namespace ClassicUO.UnitTests.Input;

public class MobileSdlEventQueueTests
{
    [Fact]
    public unsafe void BackgroundKeyboardEventsRunOnlyWhenConsumerDrainsInOrder()
    {
        var queue = new MobileSdlEventQueue();
        var received = new List<SDL_EventType>();
        int consumerThread = Environment.CurrentManagedThreadId;
        var producer = new Thread(() =>
        {
            SDL_Event e = new();
            e.type = (uint)SDL_EventType.SDL_EVENT_KEY_DOWN;
            e.key.key = (uint)SDL_Keycode.SDLK_BACKSPACE;
            queue.Enqueue(IntPtr.Zero, &e);
            e.type = (uint)SDL_EventType.SDL_EVENT_KEY_UP;
            queue.Enqueue(IntPtr.Zero, &e);
        });
        producer.Start();
        Assert.True(producer.Join(TimeSpan.FromSeconds(5)));
        Assert.Empty(received);

        queue.Drain((_, e) =>
        {
            Assert.Equal(consumerThread, Environment.CurrentManagedThreadId);
            Assert.Equal((uint)SDL_Keycode.SDLK_BACKSPACE, e->key.key);
            received.Add((SDL_EventType)e->type);
            return true;
        });
        Assert.Equal(new[] { SDL_EventType.SDL_EVENT_KEY_DOWN, SDL_EventType.SDL_EVENT_KEY_UP }, received);
    }

    [Fact]
    public unsafe void TextSurvivesTheNativeEventBufferBeingReused()
    {
        var queue = new MobileSdlEventQueue();
        byte[] bytes = Encoding.UTF8.GetBytes("café\0");
        fixed (byte* text = bytes)
        {
            SDL_Event e = new();
            e.type = (uint)SDL_EventType.SDL_EVENT_TEXT_INPUT;
            e.text.text = text;
            queue.Enqueue(IntPtr.Zero, &e);
            Array.Clear(bytes);
        }

        string received = null;
        queue.Drain((_, e) =>
        {
            received = Marshal.PtrToStringUTF8((IntPtr)e->text.text);
            return true;
        });
        Assert.Equal("café", received);
    }
}
