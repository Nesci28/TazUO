using ClassicUO.Game.Managers;
using ClassicUO.Game.Managers.Hotkeys;
using FluentAssertions;
using SDL3;
using Xunit;

namespace ClassicUO.UnitTests.Game.Managers
{
    public class CounterBarHotkeysManagerTest
    {
        // High indices unlikely to collide with anything the client registers; always cleaned up.
        private const int Idx = 100000;
        private const string BarA = "test-bar-a";
        private const string BarB = "test-bar-b";

        [Fact]
        public void SetBinding_UsableKey_RoundTripsThroughGet()
        {
            try
            {
                CounterBarHotkeysManager.SetBinding(BarA, Idx, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F1, SDL.SDL_Keymod.SDL_KMOD_CTRL));

                HotkeyBinding got = CounterBarHotkeysManager.GetBinding(BarA, Idx);
                got.HasKey.Should().BeTrue();
                got.Key.Should().Be(SDL.SDL_Keycode.SDLK_F1);
                got.Ctrl.Should().BeTrue();
            }
            finally
            {
                CounterBarHotkeysManager.ClearBinding(BarA, Idx);
            }
        }

        [Fact]
        public void SetBinding_EmptyBinding_ClearsRegistration()
        {
            CounterBarHotkeysManager.SetBinding(BarA, Idx, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F2, SDL.SDL_Keymod.SDL_KMOD_NONE));
            CounterBarHotkeysManager.SetBinding(BarA, Idx, new HotkeyBinding());

            CounterBarHotkeysManager.GetBinding(BarA, Idx).IsEmpty.Should().BeTrue();
        }

        [Fact]
        public void PruneFrom_RemovesCellsAtOrAboveThreshold()
        {
            try
            {
                CounterBarHotkeysManager.SetBinding(BarA, Idx, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F3, SDL.SDL_Keymod.SDL_KMOD_NONE));
                CounterBarHotkeysManager.SetBinding(BarA, Idx + 1, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F4, SDL.SDL_Keymod.SDL_KMOD_NONE));

                CounterBarHotkeysManager.PruneFrom(BarA, Idx + 1);

                CounterBarHotkeysManager.GetBinding(BarA, Idx).IsEmpty.Should().BeFalse();
                CounterBarHotkeysManager.GetBinding(BarA, Idx + 1).IsEmpty.Should().BeTrue();
            }
            finally
            {
                CounterBarHotkeysManager.ClearBinding(BarA, Idx);
                CounterBarHotkeysManager.ClearBinding(BarA, Idx + 1);
            }
        }

        [Fact]
        public void SameCellIndex_OnDifferentBars_HasIndependentBindings()
        {
            try
            {
                CounterBarHotkeysManager.SetBinding(BarA, Idx, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F5, SDL.SDL_Keymod.SDL_KMOD_NONE));
                CounterBarHotkeysManager.SetBinding(BarB, Idx, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F6, SDL.SDL_Keymod.SDL_KMOD_NONE));

                CounterBarHotkeysManager.GetBinding(BarA, Idx).Key.Should().Be(SDL.SDL_Keycode.SDLK_F5);
                CounterBarHotkeysManager.GetBinding(BarB, Idx).Key.Should().Be(SDL.SDL_Keycode.SDLK_F6);
            }
            finally
            {
                CounterBarHotkeysManager.ClearBar(BarA);
                CounterBarHotkeysManager.ClearBar(BarB);
            }
        }

        [Fact]
        public void PruneFrom_OnlyRemovesCellsFromRequestedBar()
        {
            try
            {
                CounterBarHotkeysManager.SetBinding(BarA, Idx, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F7, SDL.SDL_Keymod.SDL_KMOD_NONE));
                CounterBarHotkeysManager.SetBinding(BarB, Idx, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F8, SDL.SDL_Keymod.SDL_KMOD_NONE));

                CounterBarHotkeysManager.PruneFrom(BarA, Idx);

                CounterBarHotkeysManager.GetBinding(BarA, Idx).IsEmpty.Should().BeTrue();
                CounterBarHotkeysManager.GetBinding(BarB, Idx).IsEmpty.Should().BeFalse();
            }
            finally
            {
                CounterBarHotkeysManager.ClearBar(BarA);
                CounterBarHotkeysManager.ClearBar(BarB);
            }
        }

        [Fact]
        public void ClearBar_LeavesOtherBarRegistered()
        {
            try
            {
                CounterBarHotkeysManager.SetBinding(CounterBarHotkeysManager.PrimaryBarId, Idx, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F9, SDL.SDL_Keymod.SDL_KMOD_NONE));
                CounterBarHotkeysManager.SetBinding(BarB, Idx, new HotkeyBinding(SDL.SDL_Keycode.SDLK_F10, SDL.SDL_Keymod.SDL_KMOD_NONE));

                CounterBarHotkeysManager.ClearBar(CounterBarHotkeysManager.PrimaryBarId);

                CounterBarHotkeysManager.GetBinding(CounterBarHotkeysManager.PrimaryBarId, Idx).IsEmpty.Should().BeTrue();
                CounterBarHotkeysManager.GetBinding(BarB, Idx).IsEmpty.Should().BeFalse();
            }
            finally
            {
                CounterBarHotkeysManager.ClearBar(CounterBarHotkeysManager.PrimaryBarId);
                CounterBarHotkeysManager.ClearBar(BarB);
            }
        }

        [Fact]
        public void PrimaryBarId_PreservesLegacyHotkeyId()
        {
            CounterBarHotkeysManager.MakeId(CounterBarHotkeysManager.PrimaryBarId, 3)
                .Should().Be("counterbar:3");
            CounterBarHotkeysManager.MakeId(BarA, 3)
                .Should().Be("counterbar:test-bar-a:3");
        }
    }
}
