// SPDX-License-Identifier: BSD-2-Clause

using System;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Utility;

namespace ClassicUO.Game.UI.Gumps
{
    public class PopupMenuGump : Gump
    {
        private const ushort ClientActionHue = 0x03B2;

        public static uint CloseNext = uint.MaxValue;

        private Action _selectedAction;
        private readonly PopupMenuData _data;

        public PopupMenuData Data => _data;

        public PopupMenuGump(World world, PopupMenuData data) : base(world, 0, 0)
        {
            if (CloseNext != uint.MaxValue && data.Serial == CloseNext)
            {
                Dispose();
                CloseNext = uint.MaxValue;
                return;
            }

            CanMove = false;
            CanCloseWithRightClick = true;
            _data = data;

            double scale = ProfileManager.CurrentProfile?.ContextMenuScale ?? 1.0;

            var pic = new ResizePic(0x0A3C)
            {
                Alpha = 0.75f
            };

            Add(pic);

            int offsetY = ScaleHelper.Scaled(10, scale);
            int width = 0, height = ScaleHelper.Scaled(20, scale);
            bool arrowAdded = false;

            int AddRow(string text, ushort hue, ushort replacedHue, Action action)
            {
                int rowY = offsetY;

                if (replacedHue != 0)
                {
                    uint h = (HuesHelper.Color16To32(replacedHue) << 8) | 0xFF;
                    Client.Game.UO.FileManager.Fonts.SetUseHTML(true, h);
                }

                var label = new Label(text, true, hue, font: 1);
                label.ApplyScale(scale, scalePosition: false);
                label.X = ScaleHelper.Scaled(10, scale);
                label.Y = offsetY;

                Client.Game.UO.FileManager.Fonts.SetUseHTML(false);

                var box = new HitBox(ScaleHelper.Scaled(10, scale), offsetY, label.Width, label.Height)
                {
                    Tag = action
                };

                box.MouseEnter += (sender, e) =>
                {
                    _selectedAction = (Action)(sender as HitBox).Tag;
                };

                box.MouseUp += (sender, e) =>
                {
                    _selectedAction = (Action)(sender as HitBox).Tag;
                };

                Add(box);
                Add(label);

                offsetY += label.Height;
                height += label.Height;

                if (width < label.Width)
                {
                    width = label.Width;
                }

                return rowY;
            }

            for (int i = 0; i < data.Items.Length; i++)
            {
                ref PopupMenuItem item = ref data.Items[i];
                ushort index = item.Index;
                string text = MobileDefaultContextActionManager.GetActionName(item.Cliloc);

                int rowY = AddRow(
                    text,
                    item.Hue,
                    item.ReplacedHue,
                    () =>
                    {
                        if (CUOEnviroment.Debug)
                        {
                            GameActions.Print(World, $"Popup menu [{_data.Serial}] response: {index}");
                        }

                        GameActions.ResponsePopupMenu(_data.Serial, index);
                    }
                );

                if ((item.Flags & 0x02) != 0 && !arrowAdded)
                {
                    arrowAdded = true;

                    var arrow = new Button(0, 0x15E6, 0x15E2, 0x15E2)
                    {
                        X = ScaleHelper.Scaled(20, scale),
                        Y = rowY
                    };

                    arrow.ApplyScale(scale, scalePosition: false);
                    Add(arrow);
                }
            }

            if (World.Get(data.Serial) is Mobile mobile)
            {
                for (int i = 0; i < data.Items.Length; i++)
                {
                    PopupMenuItem item = data.Items[i];
                    string actionName = MobileDefaultContextActionManager.GetActionName(item.Cliloc);

                    AddRow(
                        $"Set default: {actionName}",
                        ClientActionHue,
                        0,
                        () => MobileDefaultContextActionManager.SetDefault(mobile, item)
                    );
                }

                if (MobileDefaultContextActionManager.TryGetDefault(mobile, out _))
                {
                    AddRow(
                        "Clear default action",
                        ClientActionHue,
                        0,
                        () => MobileDefaultContextActionManager.ClearDefault(mobile)
                    );
                }
            }

            width += ScaleHelper.Scaled(20, scale);

            if (height <= ScaleHelper.Scaled(10, scale) || width <= ScaleHelper.Scaled(20, scale))
            {
                Dispose();
            }
            else
            {
                pic.Width = width;
                pic.Height = height;

                foreach (HitBox box in FindControls<HitBox>())
                {
                    box.Width = width - ScaleHelper.Scaled(20, scale);
                }
            }
        }

        public override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
            {
                _selectedAction?.Invoke();
                Dispose();
            }
        }
    }
}
