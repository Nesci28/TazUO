// SPDX-License-Identifier: BSD-2-Clause

using System.Linq;
using System.Xml;
using ClassicUO.Assets;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.UI.Gumps
{
    public class HealthbarGrabberGump : Gump
    {
        private const int WIDTH = 560;
        private const int HEIGHT = 132;
        private const int BORDER_WIDTH = 2;
        private const int TITLE_HEIGHT = 28;
        private const int COLUMN_WIDTH = 180;
        private const int CENTER_WIDTH = 150;
        private const int COLUMN_Y = 34;
        private const int COLUMN_HEIGHT = 92;
        private const int HARMFUL_X = 12;
        private const int SELF_X = 205;
        private const int BENEFICIAL_X = 368;
        private const int LEFT_SEPARATOR_X = 196;
        private const int RIGHT_SEPARATOR_X = 356;

        private readonly World _world;
        private readonly GrabberColumn _harmfulColumn;
        private readonly GrabberColumn _selfColumn;
        private readonly GrabberColumn _beneficialColumn;

        public HealthbarGrabberGump(World world) : base(world, 0, 0)
        {
            _world = world;

            Width = WIDTH;
            Height = HEIGHT;

            CanMove = true;
            CanCloseWithRightClick = true;
            AcceptMouseInput = true;

            Add(new AlphaBlendControl(0.78f)
            {
                Width = WIDTH,
                Height = HEIGHT
            });

            Add(new Label(TazLang.Get("healthbargrabber_title", "Healthbar Grabber"), true, 0x0481, WIDTH, font: 1, style: FontStyle.BlackBorder, align: TEXT_ALIGN_TYPE.TS_CENTER)
            {
                Y = 7
            });

            _harmfulColumn = new GrabberColumn(
                world,
                TazLang.Get("healthbargrabber_harmful", "Last Harmful Target"),
                Color.IndianRed,
                0x0021,
                showPlayerResources: false
            )
            {
                X = HARMFUL_X,
                Y = COLUMN_Y,
                Width = COLUMN_WIDTH,
                Height = COLUMN_HEIGHT
            };

            _selfColumn = new GrabberColumn(
                world,
                TazLang.Get("healthbargrabber_you", "You"),
                Color.DodgerBlue,
                0x0481,
                showPlayerResources: true
            )
            {
                X = SELF_X,
                Y = COLUMN_Y,
                Width = CENTER_WIDTH,
                Height = COLUMN_HEIGHT
            };

            _beneficialColumn = new GrabberColumn(
                world,
                TazLang.Get("healthbargrabber_beneficial", "Last Beneficial Target"),
                Color.ForestGreen,
                0x0044,
                showPlayerResources: false
            )
            {
                X = BENEFICIAL_X,
                Y = COLUMN_Y,
                Width = COLUMN_WIDTH,
                Height = COLUMN_HEIGHT
            };

            Add(_harmfulColumn);
            Add(_selfColumn);
            Add(_beneficialColumn);

            if (world.Player != null)
            {
                _selfColumn.SetSerial(world.Player.Serial);
            }
        }

        public override GumpType GumpType => GumpType.HealthBarGrabber;

        public static void OnTargetSelected(World world, uint serial, TargetType targetType)
        {
            if (world?.Player == null || serial == world.Player.Serial || !SerialHelper.IsMobile(serial))
            {
                return;
            }

            if (targetType != TargetType.Harmful && targetType != TargetType.Beneficial)
            {
                return;
            }

            Entity entity = world.Get(serial);

            if (entity is not Mobile mobile || mobile.IsDestroyed)
            {
                return;
            }

            GameActions.RequestMobileStatus(world, serial);

            foreach (HealthbarGrabberGump grabberGump in UIManager.Gumps.OfType<HealthbarGrabberGump>())
            {
                grabberGump.SetTarget(serial, targetType);
            }
        }

        public static void MobileDestroyed(uint serial)
        {
            foreach (HealthbarGrabberGump grabberGump in UIManager.Gumps.OfType<HealthbarGrabberGump>())
            {
                grabberGump.ClearSerial(serial);
            }
        }

        private void SetTarget(uint serial, TargetType targetType)
        {
            switch (targetType)
            {
                case TargetType.Harmful:
                    _harmfulColumn.SetSerial(serial);
                    break;

                case TargetType.Beneficial:
                    _beneficialColumn.SetSerial(serial);
                    break;
            }
        }

        private void ClearSerial(uint serial)
        {
            if (_harmfulColumn.Serial == serial)
            {
                _harmfulColumn.ClearSerial();
            }

            if (_beneficialColumn.Serial == serial)
            {
                _beneficialColumn.ClearSerial();
            }
        }

        public override void Restore(XmlElement xml)
        {
            base.Restore(xml);

            if (_world.Player != null)
            {
                _selfColumn.SetSerial(_world.Player.Serial);
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            if (!base.Draw(batcher, x, y))
            {
                return false;
            }

            DrawRect(batcher, x, y, Width, BORDER_WIDTH, Color.DimGray);
            DrawRect(batcher, x, y + Height - BORDER_WIDTH, Width, BORDER_WIDTH, Color.DimGray);
            DrawRect(batcher, x, y, BORDER_WIDTH, Height, Color.DimGray);
            DrawRect(batcher, x + Width - BORDER_WIDTH, y, BORDER_WIDTH, Height, Color.DimGray);

            DrawRect(batcher, x + LEFT_SEPARATOR_X, y + 36, 1, 82, Color.DimGray);
            DrawRect(batcher, x + RIGHT_SEPARATOR_X, y + 36, 1, 82, Color.DimGray);
            DrawRect(batcher, x + 8, y + TITLE_HEIGHT, Width - 16, 1, Color.Black);

            return true;
        }

        private static void DrawRect(UltimaBatcher2D batcher, int x, int y, int width, int height, Color color)
        {
            batcher.Draw(
                SolidColorTextureCache.GetTexture(color),
                new Rectangle(x, y, width, height),
                ShaderHueTranslator.GetHueVector(0, false, 1f)
            );
        }

        private sealed class GrabberColumn : Control
        {
            private const int BAR_HEIGHT = 12;
            private const int BAR_GAP = 5;
            private const int LABEL_Y = 5;
            private const int NAME_Y = 29;
            private const int FIRST_BAR_Y = 48;

            private readonly World _world;
            private readonly Color _accentColor;
            private readonly bool _showPlayerResources;
            private readonly Label _titleLabel;
            private readonly Label _nameLabel;
            private readonly ResourceBar _healthBar;
            private readonly ResourceBar _manaBar;
            private readonly ResourceBar _staminaBar;
            private Mobile _mobile;

            public GrabberColumn(World world, string title, Color accentColor, ushort titleHue, bool showPlayerResources)
            {
                _world = world;
                _accentColor = accentColor;
                _showPlayerResources = showPlayerResources;

                CanMove = true;
                AcceptMouseInput = true;

                _titleLabel = new Label(title, true, titleHue, 200, font: 1, style: FontStyle.BlackBorder, align: TEXT_ALIGN_TYPE.TS_CENTER)
                {
                    Y = LABEL_Y
                };

                _nameLabel = new Label(string.Empty, true, 0x0481, 200, font: 1, style: FontStyle.BlackBorder, align: TEXT_ALIGN_TYPE.TS_CENTER)
                {
                    Y = NAME_Y
                };

                _healthBar = new ResourceBar(Color.DarkRed, accentColor);
                _manaBar = new ResourceBar(Color.MidnightBlue, Color.DodgerBlue);
                _staminaBar = new ResourceBar(Color.DarkSlateBlue, Color.MediumPurple);

                Add(_titleLabel);
                Add(_nameLabel);
                Add(_healthBar);
                Add(_manaBar);
                Add(_staminaBar);
            }

            public uint Serial { get; private set; }

            public override bool AcceptMouseInput { get; set; } = true;

            public void SetSerial(uint serial)
            {
                Entity entity = _world.Get(serial);

                if (entity is not Mobile mobile || mobile.IsDestroyed)
                {
                    ClearSerial();
                    return;
                }

                Serial = serial;
                _mobile = mobile;
                SetTooltip(serial);
                UpdateLayout();
                UpdateText();

                if (!_showPlayerResources)
                {
                    GameActions.RequestMobileStatus(_world, serial);
                }
            }

            public void ClearSerial()
            {
                Serial = 0;
                _mobile = null;
                ClearTooltip();
                UpdateLayout();
                UpdateText();
            }

            public override void Update()
            {
                base.Update();
                UpdateLayout();
            }

            public override void PreDraw()
            {
                base.PreDraw();

                if (Serial != 0)
                {
                    Entity entity = _world.Get(Serial);

                    if (entity is not Mobile mobile || mobile.IsDestroyed)
                    {
                        ClearSerial();
                        return;
                    }

                    _mobile = mobile;
                }

                UpdateText();
                UpdateBars();
            }

            public override void OnMouseDown(int x, int y, MouseButtonType button)
            {
                if (button == MouseButtonType.Left && Serial != 0)
                {
                    if (_world.TargetManager.IsTargeting)
                    {
                        _world.TargetManager.Target(Serial);
                    }
                    else if (Keyboard.Alt && !ProfileManager.CurrentProfile.DisableAutoFollowAlt && !_showPlayerResources)
                    {
                        ProfileManager.CurrentProfile.FollowingMode = true;
                        ProfileManager.CurrentProfile.FollowingTarget = Serial;
                    }
                    else if (!_world.Player.InWarMode)
                    {
                        _world.DelayedObjectClickManager.Set(
                            Serial,
                            Mouse.Position.X,
                            Mouse.Position.Y,
                            Time.Ticks + Mouse.MOUSE_DELAY_DOUBLE_CLICK
                        );
                    }

                    if (ProfileManager.CurrentProfile.SingleClickMobileSetsLastTarget && !_showPlayerResources)
                    {
                        _world.TargetManager.LastTargetInfo.SetEntity(Serial);
                    }
                }

                base.OnMouseDown(x, y, button);
            }

            protected override void OnMouseEnter(int x, int y)
            {
                if (_mobile != null && !_mobile.IsDestroyed)
                {
                    SelectedObject.HealthbarObject = _mobile;
                    SelectedObject.Object = _mobile;
                }

                base.OnMouseEnter(x, y);
            }

            public override bool OnMouseDoubleClick(int x, int y, MouseButtonType button)
            {
                if (button != MouseButtonType.Left || Serial == 0)
                {
                    return false;
                }

                Entity entity = _world.Get(Serial);

                if (entity != null)
                {
                    if (entity != _world.Player)
                    {
                        if (_world.Player.InWarMode)
                        {
                            GameActions.Attack(_world, entity);
                        }
                        else if (!GameActions.OpenCorpse(_world, entity))
                        {
                            GameActions.DoubleClick(_world, entity);
                        }
                    }
                    else
                    {
                        GameActions.DoubleClick(_world, entity);
                    }
                }

                return true;
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                if (MouseIsOver)
                {
                    DrawRect(batcher, x, y, Width, Height, new Color(32, 32, 32));
                }

                DrawRect(batcher, x, y + 24, Width, 1, _accentColor);

                return base.Draw(batcher, x, y);
            }

            private void UpdateLayout()
            {
                _titleLabel.X = (Width - _titleLabel.Width) / 2;
                _nameLabel.X = (Width - _nameLabel.Width) / 2;

                _healthBar.X = 8;
                _healthBar.Y = FIRST_BAR_Y;
                _healthBar.Width = Width - 16;
                _healthBar.Height = BAR_HEIGHT;

                _manaBar.X = 8;
                _manaBar.Y = FIRST_BAR_Y + BAR_HEIGHT + BAR_GAP;
                _manaBar.Width = Width - 16;
                _manaBar.Height = BAR_HEIGHT;

                _staminaBar.X = 8;
                _staminaBar.Y = FIRST_BAR_Y + (BAR_HEIGHT + BAR_GAP) * 2;
                _staminaBar.Width = Width - 16;
                _staminaBar.Height = BAR_HEIGHT;

                _healthBar.IsVisible = Serial != 0;
                _manaBar.IsVisible = _showPlayerResources && Serial != 0;
                _staminaBar.IsVisible = _showPlayerResources && Serial != 0;
            }

            private void UpdateText()
            {
                if (_mobile == null || _mobile.IsDestroyed)
                {
                    _nameLabel.Text = "-";
                    _nameLabel.Hue = 0x0386;
                    _nameLabel.X = (Width - _nameLabel.Width) / 2;
                    return;
                }

                string name = _showPlayerResources ? _mobile.Name : $"{_mobile.Name} ({_mobile.Distance})";
                _nameLabel.Text = string.IsNullOrWhiteSpace(name) ? "-" : name;
                _nameLabel.Hue = _showPlayerResources ? (ushort)0x0481 : Notoriety.GetHue(_mobile.NotorietyFlag);
                _nameLabel.X = (Width - _nameLabel.Width) / 2;
            }

            private void UpdateBars()
            {
                if (_mobile == null || _mobile.IsDestroyed)
                {
                    _healthBar.SetValue(0, 0, string.Empty);
                    _manaBar.SetValue(0, 0, string.Empty);
                    _staminaBar.SetValue(0, 0, string.Empty);
                    return;
                }

                _healthBar.SetValue(_mobile.Hits, _mobile.HitsMax, ToPercentText(_mobile.Hits, _mobile.HitsMax));

                if (_showPlayerResources)
                {
                    _manaBar.SetValue(_mobile.Mana, _mobile.ManaMax, ToPercentText(_mobile.Mana, _mobile.ManaMax));
                    _staminaBar.SetValue(_mobile.Stamina, _mobile.StaminaMax, ToPercentText(_mobile.Stamina, _mobile.StaminaMax));
                }
            }

            private static string ToPercentText(int current, int max)
            {
                if (max <= 0)
                {
                    return string.Empty;
                }

                int percent = current * 100 / max;

                if (percent > 100)
                {
                    percent = 100;
                }

                return percent + "%";
            }
        }

        private sealed class ResourceBar : Control
        {
            private readonly Color _backgroundColor;
            private readonly Color _foregroundColor;
            private readonly Label _percentLabel;
            private int _current;
            private int _max;

            public ResourceBar(Color backgroundColor, Color foregroundColor)
            {
                _backgroundColor = backgroundColor;
                _foregroundColor = foregroundColor;

                CanMove = true;
                AcceptMouseInput = false;

                _percentLabel = new Label(string.Empty, true, 0xFFFF, 160, font: 1, style: FontStyle.BlackBorder, align: TEXT_ALIGN_TYPE.TS_CENTER)
                {
                    Y = -3
                };

                Add(_percentLabel);
            }

            public void SetValue(int current, int max, string text)
            {
                _current = current;
                _max = max;
                _percentLabel.Text = text;
                _percentLabel.X = (Width - _percentLabel.Width) / 2;
            }

            public override void Update()
            {
                base.Update();
                _percentLabel.X = (Width - _percentLabel.Width) / 2;
            }

            public override bool Draw(UltimaBatcher2D batcher, int x, int y)
            {
                int fillWidth = 0;

                if (_max > 0)
                {
                    fillWidth = Width * _current / _max;

                    if (fillWidth > Width)
                    {
                        fillWidth = Width;
                    }
                    else if (fillWidth < 0)
                    {
                        fillWidth = 0;
                    }
                }

                DrawRect(batcher, x, y, Width, Height, Color.Black);
                DrawRect(batcher, x + 1, y + 1, Width - 2, Height - 2, _backgroundColor);

                if (fillWidth > 2)
                {
                    DrawRect(batcher, x + 1, y + 1, fillWidth - 2, Height - 2, _foregroundColor);
                }

                return base.Draw(batcher, x, y);
            }
        }
    }
}
