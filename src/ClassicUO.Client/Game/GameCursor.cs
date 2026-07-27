// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Managers.Hotkeys;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Input;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using SDL3;

namespace ClassicUO.Game
{
    public sealed class GameCursor
    {
        private static readonly ushort[,] _cursorData = new ushort[2, 16]
        {
            {
                0x206A,
                0x206B,
                0x206C,
                0x206D,
                0x206E,
                0x206F,
                0x2070,
                0x2071,
                0x2072,
                0x2073,
                0x2074,
                0x2075,
                0x2076,
                0x2077,
                0x2078,
                0x2079
            },
            {
                0x2053,
                0x2054,
                0x2055,
                0x2056,
                0x2057,
                0x2058,
                0x2059,
                0x205A,
                0x205B,
                0x205C,
                0x205D,
                0x205E,
                0x205F,
                0x2060,
                0x2061,
                0x2062
            }
        };

        private readonly Aura _aura;
        private readonly List<CustomBuildObject> _componentsList = new ();
        private readonly int[,] _cursorOffset = new int[2, 16];
        private readonly IntPtr[,] _cursors_ptr = new IntPtr[2, 16];
        private ushort _graphic = 0x2073;
        private bool _needGraphicUpdate = true;
        private readonly List<Multi> _temp = new List<Multi>();
        private readonly Tooltip _tooltip;
        private readonly World _world;
        private HotKeyEntry _compareItemHotkey;
        private MultipleToolTipGump _comparisonTooltip;
        private string _lastItemComparisonFailure;

        /// <summary>
        ///     The game cursor's visual style override.
        ///     When set to a value other than null, the cursor's visual style will be overridden.
        /// </summary>
        private GameCursorVisualType? _cursorVisual;

        public GameCursor(World world)
        {
            _world = world;
            _tooltip = new Tooltip(world);
            _aura = new Aura(30);

            for (int i = 0; i < 2; i++)
            {
                for (int j = 0; j < 16; j++)
                {
                    ushort id = _cursorData[i, j];

                    nint surface = Client.Game.UO.Arts.CreateCursorSurfacePtr(
                        id,
                        0,
                        out int hotX,
                        out int hotY
                    );

                    if (surface != IntPtr.Zero)
                    {
                        if (hotX != 0 || hotY != 0)
                        {
                            _cursorOffset[0, j] = hotX;
                            _cursorOffset[1, j] = hotY;
                        }

                        _cursors_ptr[i, j] = SDL.SDL_CreateColorCursor(surface, hotX, hotY);
                    }
                }
            }
        }

        public ushort Graphic
        {
            get => _graphic;
            set
            {
                if (_graphic != value)
                {
                    _graphic = value;
                    _needGraphicUpdate = true;
                }
            }
        }

        public bool IsLoading { get; set; }
        public bool IsDraggingCursorForced { get; set; }
        public bool AllowDrawSDLCursor { get; set; } = true;
        public bool IsVisible { get; set; } = true;

        public ItemHold ItemHold { get; } = new ItemHold();

        private ushort GetDraggingItemGraphic()
        {
            if (ItemHold.Enabled)
            {
                if (ItemHold.IsGumpTexture)
                {
                    return (ushort)(ItemHold.DisplayedGraphic - Constants.ITEM_GUMP_TEXTURE_OFFSET);
                }

                return ItemHold.DisplayedGraphic;
            }

            return 0xFFFF;
        }

        private Point GetDraggingItemOffset()
        {
            ushort graphic = GetDraggingItemGraphic();

            if (graphic == 0xFFFF)
            {
                return Point.Zero;
            }

            ref readonly SpriteInfo artInfo = ref (ItemHold.IsGumpTexture ? ref Client.Game.UO.Gumps.GetGump(graphic) : ref Client.Game.UO.Arts.GetArt(graphic));

            float scale = 1;

            if (
                ProfileManager.CurrentProfile != null
                && ProfileManager.CurrentProfile.ScaleItemsInsideContainers
            )
            {
                scale = UIManager.ContainerScale;
            }

            return new Point(
                (int)((artInfo.UV.Width >> 1) * scale) - ItemHold.MouseOffset.X,
                (int)((artInfo.UV.Height >> 1) * scale) - ItemHold.MouseOffset.Y
            );
        }

        public void Update()
        {
            Graphic = AssignGraphicByState();

            if (_needGraphicUpdate)
            {
                _needGraphicUpdate = false;

                if (AllowDrawSDLCursor && Settings.GlobalSettings.RunMouseInASeparateThread)
                {
                    ushort id = Graphic;

                    if (id < 0x206A)
                    {
                        id -= 0x2053;
                    }
                    else
                    {
                        id -= 0x206A;
                    }

                    int war = _world.InGame && _world.Player.InWarMode ? 1 : 0;

                    ref IntPtr ptrCursor = ref _cursors_ptr[war, id];

                    if (ptrCursor != IntPtr.Zero)
                    {
                        SDL.SDL_SetCursor(ptrCursor);
                    }
                }
            }

            if (!ItemHold.Enabled)
            {
                return;
            }
            ushort draggingGraphic = GetDraggingItemGraphic();

            if (draggingGraphic == 0xFFFF || !ItemHold.IsFixedPosition || UIManager.IsDragging)
            {
                return;
            }
            ref readonly SpriteInfo artInfo = ref (ItemHold.IsGumpTexture ? ref Client.Game.UO.Gumps.GetGump(draggingGraphic) : ref Client.Game.UO.Arts.GetArt(draggingGraphic));

            Point offset = GetDraggingItemOffset();

            int x = ItemHold.FixedX - offset.X;
            int y = ItemHold.FixedY - offset.Y;

            if (
                Mouse.Position.X >= x
                && Mouse.Position.X < x + artInfo.UV.Width
                && Mouse.Position.Y >= y
                && Mouse.Position.Y < y + artInfo.UV.Height
            )
            {
                if (!ItemHold.IgnoreFixedPosition)
                {
                    ItemHold.IsFixedPosition = false;
                    ItemHold.FixedX = 0;
                    ItemHold.FixedY = 0;
                }
            }
            else if (ItemHold.IgnoreFixedPosition)
            {
                ItemHold.IgnoreFixedPosition = false;
            }
        }

        public void Draw(UltimaBatcher2D sb)
        {
            // Check if mouse should be hidden via Hide HUD system
            if (!IsVisible)
            {
                return;
            }

            if (_world.InGame && _world.TargetManager.IsTargeting && ProfileManager.CurrentProfile != null)
            {
                if (_world.TargetManager.TargetingState == CursorTarget.MultiPlacement)
                {
                    if (
                        _world.CustomHouseManager != null
                        && _world.CustomHouseManager.SelectedGraphic != 0
                    )
                    {
                        ushort hue = 0;

                        _componentsList.Clear();
                        if (
                            !_world.CustomHouseManager.CanBuildHere(
                                _componentsList,
                                out CUSTOM_HOUSE_BUILD_TYPE type
                            )
                        )
                        {
                            hue = 0x0021;
                        }

                        //if (_temp.Count != list.Count)
                        {
                            _temp.ForEach(s => s.Destroy());
                            _temp.Clear();

                            for (int i = 0; i < _componentsList.Count; i++)
                            {
                                var m = Multi.Create(_world, _componentsList[i].Graphic);
                                m.AlphaHue = 0xFF;
                                m.Hue = hue;
                                m.State = CUSTOM_HOUSE_MULTI_OBJECT_FLAGS.CHMOF_PREVIEW;
                                _temp.Add(m);
                            }
                        }

                        if (_componentsList.Count != 0)
                        {
                            if (SelectedObject.Object is GameObject selectedObj)
                            {
                                int z = 0;

                                if (selectedObj.Z < _world.CustomHouseManager.MinHouseZ)
                                {
                                    if (
                                        selectedObj.X >= _world.CustomHouseManager.StartPos.X - 1
                                        && selectedObj.X <= _world.CustomHouseManager.EndPos.X - 1
                                        && selectedObj.Y >= _world.CustomHouseManager.StartPos.Y - 1
                                        && selectedObj.Y <= _world.CustomHouseManager.EndPos.Y - 1
                                    )
                                    {
                                        if (type != CUSTOM_HOUSE_BUILD_TYPE.CHBT_STAIR)
                                        {
                                            z += 7;
                                        }
                                    }
                                }

                                for (int i = 0; i < _componentsList.Count; i++)
                                {
                                    CustomBuildObject item = _componentsList[i];

                                    _temp[i].SetInWorldTile(
                                        (ushort)(selectedObj.X + item.X),
                                        (ushort)(selectedObj.Y + item.Y),
                                        (sbyte)(_world.Player.Z + item.Z)
                                    );
                                }
                            }
                        }
                    }
                    else if (_temp.Count != 0)
                    {
                        _temp.ForEach(s => s.Destroy());
                        _temp.Clear();
                    }
                }
                else if (_temp.Count != 0)
                {
                    _temp.ForEach(s => s.Destroy());
                    _temp.Clear();
                }

                if (ProfileManager.CurrentProfile.AuraOnMouse)
                {
                    ushort id = Graphic;

                    if (id < 0x206A)
                    {
                        id -= 0x2053;
                    }
                    else
                    {
                        id -= 0x206A;
                    }

                    ushort hue = 0;

                    switch (_world.TargetManager.TargetingType)
                    {
                        case TargetType.Neutral:
                            hue = 0x03b2;

                            break;

                        case TargetType.Harmful:
                            hue = 0x0023;

                            break;

                        case TargetType.Beneficial:
                            hue = 0x005A;

                            break;
                    }

                    _aura.Draw(sb, Mouse.Position.X, Mouse.Position.Y, hue, 0f);
                }

                if (ProfileManager.CurrentProfile.ShowTargetRangeIndicator)
                {
                    if (UIManager.IsMouseOverWorld)
                    {
                        if (SelectedObject.Object is GameObject obj)
                        {
                            string dist = obj.Distance.ToString();

                            var hue = new Vector3(0, 1, 1f);
                            sb.DrawString(
                                Fonts.Bold,
                                dist,
                                Mouse.Position.X - 26,
                                Mouse.Position.Y - 21,
                                hue
                            );

                            hue.Y = 0;
                            sb.DrawString(
                                Fonts.Bold,
                                dist,
                                Mouse.Position.X - 25,
                                Mouse.Position.Y - 20,
                                hue
                            );
                        }
                    }
                }
            }
            else if (_temp.Count != 0)
            {
                _temp.ForEach(s => s.Destroy());
                _temp.Clear();
            }

            if (ItemHold.Enabled && !ItemHold.Dropped)
            {
                float scale = Client.Game.RenderScale;

                if (
                    ProfileManager.CurrentProfile != null
                    && ProfileManager.CurrentProfile.ScaleItemsInsideContainers
                )
                {
                    scale = UIManager.ContainerScale;
                }

                ushort draggingGraphic = GetDraggingItemGraphic();

                ref readonly SpriteInfo artInfo = ref (ItemHold.IsGumpTexture ? ref Client.Game.UO.Gumps.GetGump(draggingGraphic) : ref Client.Game.UO.Arts.GetArt(draggingGraphic));

                if (artInfo.Texture != null)
                {
                    Point offset = GetDraggingItemOffset();

                    int x =
                        (ItemHold.IsFixedPosition ? ItemHold.FixedX : Mouse.Position.X) - offset.X;
                    int y =
                        (ItemHold.IsFixedPosition ? ItemHold.FixedY : Mouse.Position.Y) - offset.Y;

                    Vector3 hue = ShaderHueTranslator.GetHueVector(
                        ItemHold.Hue,
                        ItemHold.IsPartialHue,
                        ItemHold.HasAlpha ? .5f : 1f
                    );

                    var rect = new Rectangle(
                        x,
                        y,
                        (int)(artInfo.UV.Width * scale),
                        (int)(artInfo.UV.Height * scale)
                    );

                    sb.Draw(artInfo.Texture, rect, artInfo.UV, hue);

                    if (
                        ItemHold.Amount > 1
                        && ItemHold.DisplayedGraphic == ItemHold.Graphic
                        && ItemHold.IsStackable
                    )
                    {
                        rect.X += 5;
                        rect.Y += 5;

                        sb.Draw(artInfo.Texture, rect, artInfo.UV, hue);
                    }
                }
            }

            DrawToolTip(sb, Mouse.Position);

            if (!Settings.GlobalSettings.RunMouseInASeparateThread)
            {
                Graphic = AssignGraphicByState();

                ushort graphic = Graphic;

                if (graphic < 0x206A)
                {
                    graphic -= 0x2053;
                }
                else
                {
                    graphic -= 0x206A;
                }

                int offX = _cursorOffset[0, graphic];
                int offY = _cursorOffset[1, graphic];

                Vector3 hueVec = ShaderHueTranslator.GetHueVector(0);

                ref readonly SpriteInfo artInfo = ref Client.Game.UO.Arts.GetArt(Graphic);

                Rectangle rect = artInfo.UV;

                const int BORDER_SIZE = 1;
                rect.X += BORDER_SIZE;
                rect.Y += BORDER_SIZE;
                rect.Width -= BORDER_SIZE * 2;
                rect.Height -= BORDER_SIZE * 2;

                sb.Draw(
                    artInfo.Texture,
                    new Vector2(Mouse.Position.X - offX, Mouse.Position.Y - offY),
                    rect,
                    hueVec
                );
            }
        }

        /// <summary>
        /// Overrides the game cursor visual style.
        /// Set to null to remove the override and revert to the standard behavior.
        /// <para>
        /// <b>This is a global override; Discretion is required.</b>
        /// </para>
        /// </summary>
        /// <param name="visual">
        /// The visual style to use.
        /// <para>
        /// Note that additional styles are available but not listed here. See <see cref="_cursorData"/> for a complete list
        /// </para>
        /// </param>
        public void ForceSetCursorVisualStyle(GameCursorVisualType? visual)
        {
            if (visual is null)
            {
                _cursorVisual = null;
                return;
            }

            int index = (int)visual.Value;
            if (index < 0 || (uint)index >= (uint)_cursorData.GetLength(1))
            {
                Log.Warn($"Method was called with an out-of-bounds cursor visual '{visual}'. Ignoring");
                return; // This is a pretty 'low-value' method, so it's honestly better to ignore than crash here.
            }

            _cursorVisual = visual;
        }

        private void DrawToolTip(UltimaBatcher2D batcher, Point position)
        {
            if (Client.Game.Scene is GameScene gs)
            {
                if (
                    (!_world.ClientFeatures.TooltipsEnabled && ProfileManager.CurrentProfile != null && !ProfileManager.CurrentProfile.ForceTooltipsOnOldClients)
                    || (
                        SelectedObject.Object is Item selectedItem
                        && selectedItem.IsLocked
                        && selectedItem.ItemData.Weight == 255
                        && !selectedItem.ItemData.IsContainer
                        &&
                        // We need to check if OPL contains data.
                        // If not we can ignore tooltip.
                        !_world.OPL.Contains(selectedItem)
                    )
                    || (ItemHold.Enabled && !ItemHold.IsFixedPosition)
                )
                {
                    if (
                        !_tooltip.IsEmpty
                        && (UIManager.MouseOverControl == null || UIManager.IsMouseOverWorld)
                    )
                    {
                        _tooltip.Clear();
                    }
                }
                else
                {
                    if (TryShowItemComparison())
                    {
                        return;
                    }

                    if (
                        UIManager.IsMouseOverWorld
                        && SelectedObject.Object is Entity item
                        && _world.OPL.Contains(item)
                    )
                    {
                        if (_tooltip.IsEmpty || item != _tooltip.Serial)
                        {
                            _tooltip.SetGameObject(item);
                        }

                        _tooltip.Draw(batcher, position.X, position.Y + 24);

                        return;
                    }

                    if (
                        UIManager.MouseOverControl != null
                        && UIManager.MouseOverControl.Tooltip is uint serial
                    )
                    {
                        if (SerialHelper.IsValid(serial) && _world.OPL.Contains(serial))
                        {
                            if (_tooltip.IsEmpty || serial != _tooltip.Serial)
                            {
                                _tooltip.SetGameObject(serial);
                            }

                            _tooltip.Draw(batcher, position.X, position.Y + 24);

                            return;
                        }
                    }

                    else if (UIManager.MouseOverControl != null && UIManager.MouseOverControl.Tooltip is Control c)
                    {
                        c.Draw(batcher, position.X, position.Y + 24);
                        return;
                    }
                }
            }

            if (
                UIManager.MouseOverControl != null
                && UIManager.MouseOverControl.HasTooltip
                && !Mouse.IsDragging
            )
            {
                if (UIManager.MouseOverControl.Tooltip is string text)
                {
                    if (_tooltip.IsEmpty || _tooltip.Text != text)
                    {
                        _tooltip.SetText(text);
                    }

                    _tooltip.Draw(batcher, position.X, position.Y + 24);
                }
                else if (UIManager.MouseOverControl.Tooltip is Control c)
                {
                    c.Draw(batcher, position.X, position.Y + 24);
                }
            }
            else if (!_tooltip.IsEmpty)
            {
                _tooltip.Clear();
            }
        }

        private bool TryShowItemComparison()
        {
            if (UIManager.MouseOverControl is not Control hoveredControl)
            {
                _lastItemComparisonFailure = null;
                return false;
            }

            if (hoveredControl.RootParent is not Gump { IsFromServer: true })
            {
                _lastItemComparisonFailure = null;
                return false;
            }

            if (_comparisonTooltip is { IsDisposed: false })
            {
                _tooltip.Clear();
                return true;
            }

            _compareItemHotkey ??= HotKeys.Get(HotKeyRegistrar.GridCompareId);
            // Vendor Search is expected to support Ctrl+hover even when the shared grid hotkey
            // registry has not been initialized yet or a profile failed to restore that binding.
            if (!IsItemComparisonPressed(_compareItemHotkey))
            {
                _lastItemComparisonFailure = null;
                return false;
            }

            if (!TryResolveItemProperty(hoveredControl, out uint serial, out ushort graphic))
            {
                string controlType = hoveredControl.GetType().Name;
                ReportItemComparisonFailure(
                    serial,
                    serial == 0
                        ? $"item serial could not be resolved (control {controlType})"
                        : $"item art could not be resolved (serial 0x{serial:X8}, control {controlType})"
                );
                return false;
            }

            if (!_world.OPL.TryGetNameAndData(serial, out _, out _))
            {
                ReportItemComparisonFailure(serial, "item properties are not loaded");
                return false;
            }

            byte layer = Client.Game.UO.FileManager.TileData.StaticData[graphic].Layer;
            if (layer == 0)
            {
                ReportItemComparisonFailure(serial, $"graphic 0x{graphic:X4} has no equipment layer");
                return false;
            }

            MultipleToolTipGump tooltip = ItemComparisonTooltips.Create(
                _world,
                serial,
                layer,
                hoveredControl
            );

            if (tooltip == null)
            {
                ReportItemComparisonFailure(serial, $"no equipped item was found on layer {(Layer)layer}");
                return false;
            }

            _lastItemComparisonFailure = null;
            _tooltip.Clear();
            _comparisonTooltip = tooltip;
            UIManager.Add(_comparisonTooltip);
            return true;
        }

        private void ReportItemComparisonFailure(uint serial, string reason)
        {
            string failure = $"{serial:X8}:{reason}";
            if (_lastItemComparisonFailure == failure)
                return;

            _lastItemComparisonFailure = failure;
            GameActions.PrintUserWarn(_world, $"Vendor comparison unavailable: {reason}.");
        }

        /// <summary>
        /// Resolves the serial and art attached to an itemproperty entry. Server gumps do not have
        /// to place the itemproperty command directly on the tile-art control, so the normal uint
        /// tooltip and the nearest preceding item art are also accepted as fallbacks.
        /// </summary>
        internal static bool TryResolveItemProperty(
            Control hoveredControl,
            out uint serial,
            out ushort graphic
        )
        {
            serial = hoveredControl?.ItemPropertySerial ?? 0;
            graphic = hoveredControl?.ItemPropertyGraphic ?? 0;

            if (hoveredControl == null)
                return false;

            if (serial == 0 && hoveredControl.Tooltip is uint tooltipSerial)
                serial = tooltipSerial;

            if (graphic == 0)
                graphic = GetItemArtGraphic(hoveredControl);

            IGui current = hoveredControl;

            while ((serial == 0 || graphic == 0) && current.Parent != null)
            {
                List<IGui> siblings = current.Parent.Children;
                int currentIndex = siblings.IndexOf(current);

                for (int distance = 0; distance < siblings.Count; distance++)
                {
                    int beforeIndex = currentIndex - distance;
                    int afterIndex = currentIndex + distance;

                    if (beforeIndex >= 0 && siblings[beforeIndex] is Control beforeControl)
                        ResolveItemPropertyFromSibling(beforeControl, ref serial, ref graphic);

                    if (
                        distance != 0
                        && afterIndex < siblings.Count
                        && siblings[afterIndex] is Control afterControl
                    )
                        ResolveItemPropertyFromSibling(afterControl, ref serial, ref graphic);

                    if (serial != 0 && graphic != 0)
                        break;
                }

                current = current.Parent;
            }

            if (graphic == 0 && serial != 0 && hoveredControl.RootParent is Gump rootGump)
            {
                graphic = FindAssociatedItemPropertyGraphic(rootGump, serial);

                if (graphic == 0)
                {
                    TryResolveItemGraphicFromGumpText(
                        rootGump.PacketGumpText,
                        serial,
                        out graphic
                    );
                }
            }

            return serial != 0 && graphic != 0;
        }

        private static void ResolveItemPropertyFromSibling(
            Control control,
            ref uint serial,
            ref ushort graphic
        )
        {
            uint candidateSerial = control.ItemPropertySerial;

            if (candidateSerial == 0 && control.Tooltip is uint tooltipSerial)
                candidateSerial = tooltipSerial;

            if (serial == 0 && candidateSerial != 0)
                serial = candidateSerial;

            if (graphic != 0)
                return;

            if (
                control.ItemPropertyGraphic != 0
                && (serial == 0 || candidateSerial == 0 || candidateSerial == serial)
            )
            {
                graphic = control.ItemPropertyGraphic;
                return;
            }

            graphic = GetItemArtGraphic(control);
        }

        private static ushort FindAssociatedItemPropertyGraphic(IGui root, uint serial)
        {
            for (int i = 0; i < root.Children.Count; i++)
            {
                IGui child = root.Children[i];

                if (
                    child is Control control
                    && control.ItemPropertySerial == serial
                    && control.ItemPropertyGraphic != 0
                )
                {
                    return control.ItemPropertyGraphic;
                }

                ushort nestedGraphic = FindAssociatedItemPropertyGraphic(child, serial);
                if (nestedGraphic != 0)
                    return nestedGraphic;
            }

            return 0;
        }

        /// <summary>
        /// Resolves item art directly from a server gump's normalized command text. This is the
        /// final fallback for Vendor Search gumps whose itemproperty tooltip is attached to an
        /// overlay rather than to the control that rendered the item art.
        /// </summary>
        internal static bool TryResolveItemGraphicFromGumpText(
            string packetGumpText,
            uint serial,
            out ushort graphic
        )
        {
            graphic = 0;

            if (serial == 0 || string.IsNullOrWhiteSpace(packetGumpText))
                return false;

            string[] lines = packetGumpText.Split(
                '\n',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

            for (int i = 0; i < lines.Length; i++)
            {
                string[] parts = SplitGumpCommand(lines[i]);

                if (
                    parts.Length < 2
                    || !string.Equals(parts[0], "itemproperty", StringComparison.OrdinalIgnoreCase)
                    || !TryParseSerial(parts[1], out uint itemSerial)
                    || itemSerial != serial
                )
                {
                    continue;
                }

                // itemproperty normally applies to the preceding visual command. Stop at another
                // itemproperty so an adjacent Vendor Search result can never donate its art.
                for (int commandIndex = i - 1; commandIndex >= 0; commandIndex--)
                {
                    string[] command = SplitGumpCommand(lines[commandIndex]);

                    if (IsItemPropertyCommand(command))
                        break;

                    graphic = GetItemGraphicFromGumpCommand(command);
                    if (graphic != 0)
                        return true;
                }

                // A few server implementations emit itemproperty immediately before the visual.
                for (int commandIndex = i + 1; commandIndex < lines.Length; commandIndex++)
                {
                    string[] command = SplitGumpCommand(lines[commandIndex]);

                    if (IsItemPropertyCommand(command))
                        break;

                    graphic = GetItemGraphicFromGumpCommand(command);
                    if (graphic != 0)
                        return true;
                }
            }

            return false;
        }

        private static string[] SplitGumpCommand(string command) =>
            command.Split(
                new[] { ' ', ',' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
            );

        private static bool IsItemPropertyCommand(string[] parts) =>
            parts.Length != 0
            && string.Equals(parts[0], "itemproperty", StringComparison.OrdinalIgnoreCase);

        private static bool TryParseSerial(string value, out uint serial)
        {
            try
            {
                serial = SerialHelper.Parse(value);
                return true;
            }
            catch (FormatException)
            {
                serial = 0;
                return false;
            }
            catch (OverflowException)
            {
                serial = 0;
                return false;
            }
        }

        private static ushort GetItemGraphicFromGumpCommand(string[] parts)
        {
            if (parts.Length == 0)
                return 0;

            if (
                parts.Length > 3
                && (
                    string.Equals(parts[0], "tilepic", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(parts[0], "tilepichue", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        parts[0],
                        "tilepicasgumppic",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            )
            {
                return ClassicUO.Utility.UInt16Converter.Parse(parts[3]);
            }

            if (
                parts.Length > 8
                && string.Equals(parts[0], "buttontileart", StringComparison.OrdinalIgnoreCase)
            )
            {
                return ClassicUO.Utility.UInt16Converter.Parse(parts[8]);
            }

            return 0;
        }

        internal static bool IsItemComparisonPressed(HotKeyEntry compareHotkey) =>
            Keyboard.Ctrl || compareHotkey?.IsPressed() == true;

        private static ushort GetItemArtGraphic(Control control) => control switch
        {
            StaticPic staticPic => staticPic.Graphic,
            ButtonTileArt buttonTileArt => buttonTileArt.Graphic,
            // OSI Vendor Search uses tilepicasgumppic, parsed as a GumpPic. Only treat it as
            // item art when itemproperty has attached a serial tooltip to that same control.
            GumpPic gumpPic when gumpPic.Tooltip is uint => gumpPic.Graphic,
            _ => 0
        };

        private ushort AssignGraphicByState()
        {
            int war = _world.InGame && _world.Player.InWarMode ? 1 : 0;

            if (_cursorVisual != null)
                return _cursorData[war, (ushort)_cursorVisual.Value];

            if (_world.TargetManager.IsTargeting)
            {
                return _cursorData[war, (int)GameCursorVisualType.Targeting];
            }

            if (UIManager.IsDragging || IsDraggingCursorForced)
            {
                return _cursorData[war, (int)GameCursorVisualType.Dragging];
            }

            if (IsLoading)
            {
                return _cursorData[war, (int)GameCursorVisualType.Loading];
            }

            if (
                UIManager.MouseOverControl != null
                && UIManager.MouseOverControl.AcceptKeyboardInput
                && UIManager.MouseOverControl.IsEditable
            )
            {
                return _cursorData[war, (int)GameCursorVisualType.Editing];
            }

            ushort result = _cursorData[war, (int)GameCursorVisualType.FatPointingWest];

            if (!UIManager.IsMouseOverWorld || ProfileManager.CurrentProfile == null)
                return result;

            Camera camera = Client.Game.Scene.Camera;

            int windowCenterX = camera.Bounds.X + (camera.Bounds.Width >> 1);
            int windowCenterY = camera.Bounds.Y + (camera.Bounds.Height >> 1);

            return _cursorData[
                war,
                GetMouseDirection(
                    windowCenterX,
                    windowCenterY,
                    Mouse.Position.X,
                    Mouse.Position.Y,
                    1
                )
            ];
        }

        public static int GetMouseDirection(int x1, int y1, int to_x, int to_y, int current_facing)
        {
            int shiftX = to_x - x1;
            int shiftY = to_y - y1;
            int hashf = 100 * (Sgn(shiftX) + 2) + 10 * (Sgn(shiftY) + 2);

            if (shiftX != 0 && shiftY != 0)
            {
                shiftX = Math.Abs(shiftX);
                shiftY = Math.Abs(shiftY);

                if (shiftY * 5 <= shiftX * 2)
                {
                    hashf = hashf + 1;
                }
                else if (shiftY * 2 >= shiftX * 5)
                {
                    hashf = hashf + 3;
                }
                else
                {
                    hashf = hashf + 2;
                }
            }
            else if (shiftX == 0)
            {
                if (shiftY == 0)
                {
                    return current_facing;
                }
            }

            switch (hashf)
            {
                case 111:
                    return (int)Direction.West; // W

                case 112:
                    return (int)Direction.Up; // NW

                case 113:
                    return (int)Direction.North; // N

                case 120:
                    return (int)Direction.West; // W

                case 131:
                    return (int)Direction.West; // W

                case 132:
                    return (int)Direction.Left; // SW

                case 133:
                    return (int)Direction.South; // S

                case 210:
                    return (int)Direction.North; // N

                case 230:
                    return (int)Direction.South; // S

                case 311:
                    return (int)Direction.East; // E

                case 312:
                    return (int)Direction.Right; // NE

                case 313:
                    return (int)Direction.North; // N

                case 320:
                    return (int)Direction.East; // E

                case 331:
                    return (int)Direction.East; // E

                case 332:
                    return (int)Direction.Down; // SE

                case 333:
                    return (int)Direction.South; // S
            }

            return current_facing;
        }

        private static int Sgn(int val)
        {
            int a = 0 < val ? 1 : 0;
            int b = val < 0 ? 1 : 0;

            return a - b;
        }
    }
}
