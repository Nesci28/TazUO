// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
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
        private const ushort DefaultMarkerEmpty = 0x00D2;
        private const ushort DefaultMarkerSelected = 0x00D3;

        public static uint CloseNext = uint.MaxValue;

        private Action _selectedAction;
        private bool _suppressCloseOnMouseUp;
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

            Mobile menuMobile = World.Get(data.Serial) as Mobile;
            int selectedDefaultCliloc = 0;
            bool hasDefaultAction = menuMobile != null && MobileDefaultContextActionManager.TryGetDefault(menuMobile, out selectedDefaultCliloc);
            var defaultMarkers = new List<(GumpPic Marker, int Cliloc)>();

            int offsetY = ScaleHelper.Scaled(10, scale);
            int padding = ScaleHelper.Scaled(10, scale);
            int markerColumnWidth = menuMobile != null ? ScaleHelper.Scaled(22, scale) : 0;
            int width = 0, height = ScaleHelper.Scaled(20, scale);
            bool arrowAdded = false;

            void SelectDefaultMarker(int cliloc)
            {
                selectedDefaultCliloc = cliloc;
                hasDefaultAction = true;

                foreach ((GumpPic marker, int markerCliloc) in defaultMarkers)
                {
                    marker.Graphic = markerCliloc == selectedDefaultCliloc ? DefaultMarkerSelected : DefaultMarkerEmpty;
                }
            }

            int AddRow(string text, ushort hue, ushort replacedHue, Action action, PopupMenuItem? defaultItem = null)
            {
                int rowY = offsetY;
                int labelX = padding + markerColumnWidth;

                if (replacedHue != 0)
                {
                    uint h = (HuesHelper.Color16To32(replacedHue) << 8) | 0xFF;
                    Client.Game.UO.FileManager.Fonts.SetUseHTML(true, h);
                }

                var label = new Label(text, true, hue, font: 1);
                label.ApplyScale(scale, scalePosition: false);
                label.X = labelX;
                label.Y = rowY;

                Client.Game.UO.FileManager.Fonts.SetUseHTML(false);

                if (menuMobile != null && defaultItem.HasValue)
                {
                    PopupMenuItem itemForDefault = defaultItem.Value;
                    var marker = new GumpPic(
                        padding,
                        rowY,
                        hasDefaultAction && itemForDefault.Cliloc == selectedDefaultCliloc ? DefaultMarkerSelected : DefaultMarkerEmpty,
                        0
                    );

                    marker.ApplyScale(scale, scalePosition: false);
                    marker.Y = rowY + Math.Max(0, (label.Height - marker.Height) >> 1);

                    marker.MouseEnter += (sender, e) =>
                    {
                        _selectedAction = null;
                        _suppressCloseOnMouseUp = true;
                    };

                    marker.MouseUp += (sender, e) =>
                    {
                        if (e.Button != MouseButtonType.Left)
                        {
                            return;
                        }

                        _selectedAction = null;
                        _suppressCloseOnMouseUp = true;
                        MobileDefaultContextActionManager.SetDefault(menuMobile, itemForDefault);
                        SelectDefaultMarker(itemForDefault.Cliloc);
                    };

                    defaultMarkers.Add((marker, itemForDefault.Cliloc));
                    Add(marker);
                }

                var box = new HitBox(labelX, rowY, label.Width, label.Height)
                {
                    Tag = action
                };

                box.MouseEnter += (sender, e) =>
                {
                    _suppressCloseOnMouseUp = false;
                    _selectedAction = (Action)(sender as HitBox).Tag;
                };

                box.MouseUp += (sender, e) =>
                {
                    _suppressCloseOnMouseUp = false;
                    _selectedAction = (Action)(sender as HitBox).Tag;
                };

                Add(box);
                Add(label);

                offsetY += label.Height;
                height += label.Height;

                int rowWidth = markerColumnWidth + label.Width;

                if (width < rowWidth)
                {
                    width = rowWidth;
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
                    },
                    item
                );

                if ((item.Flags & 0x02) != 0 && !arrowAdded)
                {
                    arrowAdded = true;

                    var arrow = new Button(0, 0x15E6, 0x15E2, 0x15E2)
                    {
                        X = labelArrowX(),
                        Y = rowY
                    };

                    arrow.ApplyScale(scale, scalePosition: false);
                    Add(arrow);
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
                    box.Width = width - box.X - padding;
                }
            }

            int labelArrowX() => padding + markerColumnWidth + ScaleHelper.Scaled(10, scale);
        }

        public override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            if (button == MouseButtonType.Left)
            {
                if (_suppressCloseOnMouseUp)
                {
                    _suppressCloseOnMouseUp = false;
                    return;
                }

                _selectedAction?.Invoke();
                Dispose();
            }
        }
    }
}
