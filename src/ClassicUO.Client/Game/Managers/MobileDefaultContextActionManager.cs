// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers.Hotkeys;

namespace ClassicUO.Game.Managers
{
    public static class MobileDefaultContextActionManager
    {
        private const uint PendingTimeout = 5000;

        private static readonly Dictionary<uint, PendingAction> _pendingActions = new();

        public static bool TryGetDefault(Mobile mobile, out int cliloc)
        {
            cliloc = 0;

            if (mobile == null || ProfileManager.CurrentProfile?.MobileDefaultContextActions == null)
            {
                return false;
            }

            return ProfileManager.CurrentProfile.MobileDefaultContextActions.TryGetValue(GetKey(mobile), out cliloc);
        }

        public static void SetDefault(Mobile mobile, PopupMenuItem item)
        {
            if (mobile == null || ProfileManager.CurrentProfile == null)
            {
                return;
            }

            Dictionary<ushort, int> defaults = ProfileManager.CurrentProfile.MobileDefaultContextActions != null
                ? new Dictionary<ushort, int>(ProfileManager.CurrentProfile.MobileDefaultContextActions)
                : new Dictionary<ushort, int>();

            defaults[GetKey(mobile)] = item.Cliloc;
            ProfileManager.CurrentProfile.MobileDefaultContextActions = defaults;

            GameActions.Print(
                mobile.World,
                $"Default context action set to {GetActionName(item.Cliloc)} for this mobile type.",
                Constants.HUE_SUCCESS
            );
        }

        public static void ClearDefault(Mobile mobile)
        {
            if (mobile == null || ProfileManager.CurrentProfile?.MobileDefaultContextActions == null)
            {
                return;
            }

            Dictionary<ushort, int> defaults = new(ProfileManager.CurrentProfile.MobileDefaultContextActions);

            if (!defaults.Remove(GetKey(mobile)))
            {
                return;
            }

            ProfileManager.CurrentProfile.MobileDefaultContextActions = defaults;
            GameActions.Print(mobile.World, "Default context action cleared for this mobile type.", Constants.HUE_SUCCESS);
        }

        public static bool TryStartDefaultAction(Mobile mobile)
        {
            if (
                mobile == null
                || !HotKeys.IsPressed(HotKeyRegistrar.MobileDefaultContextActionId)
                || !mobile.World.ClientFeatures.PopupEnabled
                || !TryGetDefault(mobile, out int cliloc)
            )
            {
                return false;
            }

            _pendingActions[mobile.Serial] = new PendingAction(cliloc, Time.Ticks + PendingTimeout);
            GameActions.OpenPopupMenu(mobile.Serial, shift: true);

            return true;
        }

        public static bool TryHandlePopupResponse(World world, PopupMenuData data)
        {
            if (data == null || !_pendingActions.TryGetValue(data.Serial, out PendingAction pending))
            {
                return false;
            }

            _pendingActions.Remove(data.Serial);

            if (Time.Ticks > pending.Expires)
            {
                return false;
            }

            for (int i = 0; i < data.Items.Length; i++)
            {
                if (data.Items[i].Cliloc == pending.Cliloc)
                {
                    GameActions.ResponsePopupMenu(data.Serial, data.Items[i].Index);
                    return true;
                }
            }

            GameActions.Print(
                world,
                $"Default context action {GetActionName(pending.Cliloc)} is not available for this mobile.",
                Constants.HUE_ERROR
            );

            return true;
        }

        public static string GetActionName(int cliloc)
        {
            string text = Client.Game.UO.FileManager.Clilocs.GetString(cliloc);

            return string.IsNullOrWhiteSpace(text) ? cliloc.ToString() : text;
        }

        private static ushort GetKey(Mobile mobile) => mobile.OriginalGraphic;

        private readonly struct PendingAction
        {
            public PendingAction(int cliloc, uint expires)
            {
                Cliloc = cliloc;
                Expires = expires;
            }

            public int Cliloc { get; }
            public uint Expires { get; }
        }
    }
}
