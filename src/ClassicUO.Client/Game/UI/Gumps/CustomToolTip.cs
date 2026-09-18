using ClassicUO.Configuration;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Renderer;
using ClassicUO.Utility;
using Microsoft.Xna.Framework;
using System.Threading.Tasks;

namespace ClassicUO.Game.UI.Gumps
{
    public class CustomToolTip : Gump
    {
        private readonly uint itemSerial;
        private readonly byte itemLayer;
        private Control hoverReference;
        private readonly string prepend;
        private readonly string append;
        private readonly Item compareTo;
        private readonly string rawTooltip;
        private TextBox text;
        private readonly uint hue = 0xFFFF;

        // Border hue requested by a matched tooltip override (-1 = default border).
        private int borderHueOverride = -1;

        public event FinishedLoadingEvent OnOPLLoaded;

        public CustomToolTip(World world, Item item, int x, int y, Control hoverReference, string prepend = "", string append = "", Item compareTo = null)
            : this(world, item?.Serial ?? 0, item?.ItemData.Layer ?? 0, x, y, hoverReference, prepend, append, compareTo)
        {
        }

        /// <summary>
        /// Creates a tooltip from an item serial and explicit equipment layer. This supports both
        /// OPL-only Vendor Search results and real items whose network layer must be preserved.
        /// </summary>
        public CustomToolTip(World world, uint serial, byte layer, int x, int y, Control hoverReference, string prepend = "", string append = "", Item compareTo = null) : base(world, 0, 0)
        {
            itemSerial = serial;
            itemLayer = layer;
            this.hoverReference = hoverReference;
            this.prepend = prepend;
            this.append = append;
            this.compareTo = compareTo;
            X = x;
            Y = y;
            if (ProfileManager.CurrentProfile != null)
            {
                hue = ProfileManager.CurrentProfile.TooltipTextHue;
            }
            BuildGump();
        }

        /// <summary>
        /// Creates a comparison tooltip from text embedded directly in a server gump. Some OSI
        /// Vendor Search rows expose no itemproperty serial but still provide the complete item
        /// tooltip on their ButtonTileArt control.
        /// </summary>
        public CustomToolTip(World world, string rawTooltip, byte layer, int x, int y, Control hoverReference, string prepend = "", string append = "", Item compareTo = null) : base(world, 0, 0)
        {
            this.rawTooltip = rawTooltip;
            itemLayer = layer;
            this.hoverReference = hoverReference;
            this.prepend = prepend;
            this.append = append;
            this.compareTo = compareTo;
            X = x;
            Y = y;
            if (ProfileManager.CurrentProfile != null)
            {
                hue = ProfileManager.CurrentProfile.TooltipTextHue;
            }
            BuildGump();
        }

        public void RemoveHoverReference() => hoverReference = null;

        internal void RefreshData()
        {
            if (IsDisposed)
                return;

            LoadOPLData(0);
        }

        private static TextBox.RTLOptions ToolTipOptions => new TextBox.RTLOptions() { Align = ProfileManager.CurrentProfile.LeftAlignToolTips ? FontStashSharp.RichText.TextHorizontalAlignment.Left : FontStashSharp.RichText.TextHorizontalAlignment.Center };

        private void BuildGump()
        {
            text = TextBox.GetOne("Loading item data...", ProfileManager.CurrentProfile.SelectedToolTipFont, ProfileManager.CurrentProfile.SelectedToolTipFontSize, (int)hue, ToolTipOptions);
            text.Width = 150;

            Height = text.Height;
            Width = text.Width;

            if (string.IsNullOrWhiteSpace(rawTooltip))
                LoadOPLData(0);
            else
                LoadRawTooltipData();
        }

        private void LoadRawTooltipData()
        {
            string finalString = Managers.ToolTipOverrideData.ProcessTooltipText(
                World,
                rawTooltip,
                itemLayer,
                out borderHueOverride,
                compareTo
            );

            if (string.IsNullOrWhiteSpace(finalString))
                finalString = rawTooltip;

            SetTooltipText(prepend + finalString + append);
        }

        private void LoadOPLData(int attempt)
        {
            if (attempt > 4 || IsDisposed)
                return;
            if (!SerialHelper.IsValid(itemSerial))
            {
                Dispose();
                return;
            }

            World.OPL.TryGetNameAndData(itemSerial, out string name, out string data);
            data ??= string.Empty;

            if (name.NotNullNotEmpty())
            {
                string finalString = FormatTooltip(name, data);
                finalString = Managers.ToolTipOverrideData.ProcessTooltipText(World, itemSerial, itemLayer, out borderHueOverride, compareTo);
                if (finalString == null)
                    finalString = FormatTooltip(name, data);
                finalString = prepend + finalString + append;

                SetTooltipText(finalString);
                OnOPLLoaded?.Invoke();
            }
            else
            {
                Task.Factory.StartNew(() =>
                {
                    Task.Delay(1500).Wait();
                    attempt++;
                    // Re-run on the main thread: once the OPL data arrives, LoadOPLData builds and
                    // measures a TextBox through FontStashSharp, whose shared font caches are not
                    // thread-safe. Measuring here (a background task thread) while the main thread
                    // measures/draws the same fonts corrupts those caches and crashes
                    // (IndexOutOfRangeException in FontStashSharp's Int32Map).
                    MainThreadQueue.InvokeOnMainThread(() => LoadOPLData(attempt));
                });
            }



        }

        private void SetTooltipText(string finalString)
        {
            text?.Dispose();
            text = TextBox.GetOne(
                TextBox.ConvertHtmlToFontStashSharpCommand(finalString).Trim(),
                ProfileManager.CurrentProfile.SelectedToolTipFont,
                ProfileManager.CurrentProfile.SelectedToolTipFontSize,
                (int)hue,
                ToolTipOptions
            );
            text.Width = 600;

            if (text.MeasuredSize.X + 10 < 600)
                text.Width = text.MeasuredSize.X + 10;

            Height = text.Height;
            Width = text.Width;
        }

        private string FormatTooltip(string name, string data)
        {
            string text =
                prepend +
                "<basefont color=\"yellow\">" +
                name +
                "\n<basefont color=\"#FFFFFF\">" +
                data +
                append;

            return text;
        }

        public override void Dispose()
        {
            base.Dispose();

            text?.Dispose();
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);

            if (IsDisposed)
                return false;

            if (hoverReference is { MouseIsOver: false })
            {
                Dispose();
                return false;
            }

            float alpha = 0.7f;

            if (ProfileManager.CurrentProfile != null)
            {
                alpha = ProfileManager.CurrentProfile.TooltipBackgroundOpacity / 100f;
                if (float.IsNaN(alpha))
                {
                    alpha = 0f;
                }
            }

            Vector3 hue_vec = ShaderHueTranslator.GetHueVector(1, false, alpha);

            if (ProfileManager.CurrentProfile != null)
                hue_vec.X = ProfileManager.CurrentProfile.ToolTipBGHue;

            batcher.Draw
            (
                SolidColorTextureCache.GetTexture(Color.White),
                new Rectangle
                (
                    x - 4,
                    y - 2,
                    (int)(Width + 8),
                    (int)(Height + 8)
                ),
                hue_vec
            );

            var borderTexture = SolidColorTextureCache.GetTexture(Color.Gray);

            int bgX = x - 4;
            int bgY = y - 2;
            int bgWidth = (int)(Width + 8);
            int bgHeight = (int)(Height + 8);

            // A matched tooltip override draws a colored accent border on the left and top edges only.
            if (borderHueOverride >= 0)
            {
                hue_vec = ShaderHueTranslator.GetHueVector(borderHueOverride, false, alpha);
                borderTexture = SolidColorTextureCache.GetTexture(Color.White);

                const int leftWidth = 2;
                int topHeight = Managers.ToolTipOverrideData.BorderWidth;

                // Both edges sit just outside the background so they don't cover the tooltip text.
                // Top edge spans the width plus the top-left corner.
                batcher.Draw(borderTexture, new Rectangle(bgX - leftWidth, bgY - topHeight, bgWidth + leftWidth, topHeight), hue_vec);
                // Left edge.
                batcher.Draw(borderTexture, new Rectangle(bgX - leftWidth, bgY, leftWidth, bgHeight), hue_vec);
            }
            else
            {
                hue_vec = ShaderHueTranslator.GetHueVector(0, false, alpha);
                batcher.DrawRectangle(borderTexture, bgX, bgY, bgWidth, bgHeight, hue_vec);
            }

            text.Draw(batcher, x, y);

            return true;
        }
    }

    public delegate void FinishedLoadingEvent();
}
