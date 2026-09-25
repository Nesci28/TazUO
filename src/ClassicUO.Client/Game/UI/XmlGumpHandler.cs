using ClassicUO.Assets;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.Scenes;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.Network;
using ClassicUO.Renderer;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using ClassicUO.Input;
using static ClassicUO.Game.UI.XmlGumpHandler;

namespace ClassicUO.Game.UI
{
    public class XmlGumpHandler
    {
        public static string XmlGumpPath => Path.Combine(CUOEnviroment.ExecutablePath, "Data", "XmlGumps");

        public static XmlGump CreateGumpFromFile(World world, string filePath)
        {
            var gump = new XmlGump(world);
            gump.CanCloseWithRightClick = true;
            gump.AcceptMouseInput = true;
            gump.CanMove = true;

            if (File.Exists(filePath))
            {
                gump.FilePath = filePath;

                var xmlDoc = new XmlDocument();
                try
                {
                    xmlDoc.LoadXml(File.ReadAllText(filePath));
                }
                catch (Exception e) { GameActions.Print(world, e.Message); }

                if (xmlDoc.DocumentElement != null)
                {
                    XmlElement root = xmlDoc.DocumentElement;

                    foreach (XmlAttribute attr in root.Attributes)
                    {
                        switch (attr.Name.ToLower())
                        {
                            case "x":
                                int.TryParse(attr.Value, out gump.X);
                                break;
                            case "y":
                                int.TryParse(attr.Value, out gump.Y);
                                break;
                            case "locked":
                                if (bool.TryParse(attr.Value, out bool locked))
                                {
                                    gump.IsLocked = locked;
                                }
                                break;
                            case "saveposition":
                                if (bool.TryParse(attr.Value, out bool savePos))
                                {
                                    gump.SavePosition = savePos;
                                }
                                break;
                            case "acceptmouseinput":
                                if (bool.TryParse(attr.Value, out bool b))
                                {
                                    gump.AcceptMouseInput = b;
                                }
                                break;
                            case "fontsizeoffset":
                                if (int.TryParse(attr.Value, out int fontSizeOffset))
                                {
                                    gump.FontSizeOffset = Math.Clamp(fontSizeOffset, -8, 24);
                                }
                                break;
                            case "nativefont":
                                if (bool.TryParse(attr.Value, out bool nativeFont))
                                {
                                    gump.UseNativeFont = nativeFont;
                                }
                                break;
                        }
                    }

                    ProcessChildNodes(world, gump, root);
                }
                gump.ForceSizeUpdate();
            }

            return gump;
        }

        public static void TryAutoOpenByName(World world, string name)
        {
            string fullFile = Path.Combine(XmlGumpPath, name + ".xml");
            if (File.Exists(fullFile))
            {
                UIManager.Add(CreateGumpFromFile(world, fullFile));
            }
        }

        public static string[] GetAllXmlGumps()
        {
            var fileList = new List<string>();

            try
            {
                if (Directory.Exists(XmlGumpPath))
                {
                    string[] allFiles = Directory.GetFiles(XmlGumpPath, "*.xml");
                    foreach (string file in allFiles)
                    {
                        fileList.Add(Path.GetFileNameWithoutExtension(file));
                    }
                }
                else
                {
                    Directory.CreateDirectory(XmlGumpPath);
                }
            }
            catch { }

            return fileList.ToArray();
        }

        private static void ProcessChildNodes(World world, XmlGump gump, XmlNode node)
        {
            foreach (XmlNode child in node.ChildNodes)
            {
                switch (child.NodeType)
                {
                    case XmlNodeType.Element:
                        if (child.Name.ToLower().Equals("text"))
                        {
                            HandleTextTag(world, gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("colorbox"))
                        {
                            HandleColorBox(gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("image"))
                        {
                            HandleImage(gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("embedded_image"))
                        {
                            HandleEmbeddedImage(gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("nine_slice"))
                        {
                            HandleNineSlice(gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("player_paperdoll"))
                        {
                            HandlePlayerPaperDoll(world, gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("image_progress_bar"))
                        {
                            HandleImageProgressBar(world, gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("color_progress_bar"))
                        {
                            HandleColorProgressBar(world, gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("control"))
                        {
                            HandleControl(world, gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("simple_border"))
                        {
                            HandleBorder(gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("macro_button_color"))
                        {
                            HandleMacroButton(world, gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("macro_button_graphic"))
                        {
                            HandleMacroButtonGraphic(world, gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("hp_bar_color"))
                        {
                            HandleColorHPBar(world, gump, child);
                            break;
                        }
                        if (child.Name.ToLower().Equals("hp_bar_image"))
                        {
                            HandleImageHPBar(world, gump, child);
                            break;
                        }
                        break;
                }
            }
        }

        private static void HandleEmbeddedImage(XmlGump gump, XmlNode node)
        {
            EmbeddedTextureSettings settings = ParseEmbeddedTextureSettings(node);

            if (
                string.IsNullOrWhiteSpace(settings.Texture)
                || !ExternalImageLoader.Instance.TryGetEmbeddedTexture(settings.Texture, out var texture)
                || texture == null
                || texture.IsDisposed
            )
            {
                return;
            }

            var image = new EmbeddedGumpPic(0, 0, texture, settings.Hue)
            {
                Alpha = settings.Alpha,
                AcceptMouseInput = false,
                CanMove = false
            };
            gump.Add(ApplyBasicAttributes(image, node));
        }

        private static void HandleNineSlice(XmlGump gump, XmlNode node)
        {
            EmbeddedTextureSettings settings = ParseEmbeddedTextureSettings(node, 8);

            if (
                string.IsNullOrWhiteSpace(settings.Texture)
                || !ExternalImageLoader.Instance.TryGetEmbeddedTexture(settings.Texture, out var texture)
                || texture == null
                || texture.IsDisposed
            )
            {
                return;
            }

            int maximumBorder = Math.Max(1, Math.Min(texture.Width, texture.Height) / 2);
            int border = Math.Clamp(settings.Border, 1, maximumBorder);
            var panel = new NineSliceControl(texture.Width, texture.Height, texture, border)
            {
                Alpha = settings.Alpha,
                Hue = settings.Hue,
                AcceptMouseInput = false,
                CanMove = false
            };
            gump.Add(ApplyBasicAttributes(panel, node));
        }

        internal static EmbeddedTextureSettings ParseEmbeddedTextureSettings(
            XmlNode node,
            int defaultBorder = 0
        )
        {
            string texture = string.Empty;
            int border = defaultBorder;
            ushort hue = 0;
            float alpha = 1f;

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "texture":
                    case "name":
                        texture = attr.Value.Trim();
                        break;
                    case "border":
                        if (int.TryParse(attr.Value, out int parsedBorder) && parsedBorder > 0)
                        {
                            border = parsedBorder;
                        }
                        break;
                    case "hue":
                        ushort.TryParse(attr.Value, out hue);
                        break;
                    case "alpha":
                        if (
                            float.TryParse(
                                attr.Value,
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out float parsedAlpha
                            )
                        )
                        {
                            alpha = Math.Clamp(parsedAlpha, 0f, 1f);
                        }
                        break;
                }
            }

            return new EmbeddedTextureSettings(texture, border, hue, alpha);
        }

        private static void HandlePlayerPaperDoll(World world, XmlGump gump, XmlNode node)
        {
            if (world.Player == null)
            {
                return;
            }

            PlayerPaperDollSettings settings = ParsePlayerPaperDollSettings(node);
            var paperDoll = new XmlPaperDollView(
                world,
                world.Player,
                settings.Width,
                settings.Height,
                settings.Updates,
                settings.Background
            );

            ApplyBasicAttributes(paperDoll, node);
            paperDoll.TargetSize = new Vector2(settings.Width, settings.Height);
            paperDoll.Alpha = settings.Alpha;
            gump.Add(paperDoll);
        }

        internal static PlayerPaperDollSettings ParsePlayerPaperDollSettings(XmlNode node)
        {
            int width = PlayerPaperDollSettings.DefaultWidth;
            int height = PlayerPaperDollSettings.DefaultHeight;
            bool updates = true;
            bool background = false;
            float alpha = 1f;

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "width":
                        if (int.TryParse(attr.Value, out int parsedWidth) && parsedWidth > 0)
                        {
                            width = parsedWidth;
                        }
                        break;
                    case "height":
                        if (int.TryParse(attr.Value, out int parsedHeight) && parsedHeight > 0)
                        {
                            height = parsedHeight;
                        }
                        break;
                    case "updates":
                        if (bool.TryParse(attr.Value, out bool parsedUpdates))
                        {
                            updates = parsedUpdates;
                        }
                        break;
                    case "background":
                        if (bool.TryParse(attr.Value, out bool parsedBackground))
                        {
                            background = parsedBackground;
                        }
                        break;
                    case "alpha":
                        if (
                            float.TryParse(
                                attr.Value,
                                NumberStyles.Float,
                                CultureInfo.InvariantCulture,
                                out float parsedAlpha
                            )
                        )
                        {
                            alpha = Math.Clamp(parsedAlpha, 0f, 1f);
                        }
                        break;
                }
            }

            return new PlayerPaperDollSettings(width, height, updates, background, alpha);
        }

        private static void HandleImageHPBar(World world, XmlGump gump, XmlNode node)
        {
            var hpBar = new XmlHealthBar(world, world.Player);
            ApplyBasicAttributes(hpBar, node);
            hpBar.SetImageType();

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "hue_normal":
                        if (ushort.TryParse(attr.Value, out ushort h))
                        {
                            hpBar.NormalHue = h;
                        }
                        break;
                    case "hue_background":
                        if (ushort.TryParse(attr.Value, out ushort hh))
                        {
                            hpBar.BackgroundHue = hh;
                        }
                        break;
                    case "hue_poisoned":
                        if (ushort.TryParse(attr.Value, out ushort hp))
                        {
                            hpBar.PoisonedHue = hp;
                        }
                        break;
                    case "direction":
                        if (attr.Value == "right")
                        {
                            hpBar.BarDirection = XmlHealthBar.Direction.LeftToRight;
                        }
                        else if (attr.Value == "up")
                        {
                            hpBar.BarDirection = XmlHealthBar.Direction.BottomToTop;
                        }
                        break;
                    case "image_background":
                        if (ushort.TryParse(attr.Value, out ushort bg))
                        {
                            hpBar.BackgroundImage = bg;
                        }
                        break;
                    case "image_foreground":
                        if (ushort.TryParse(attr.Value, out ushort fg))
                        {
                            hpBar.ForegroundImage = fg;
                        }
                        break;
                    case "image_foreground_poisoned":
                        if (ushort.TryParse(attr.Value, out ushort fgp))
                        {
                            hpBar.ForegroundImagePoisoned = fgp;
                        }
                        break;

                }
            }

            gump.Add(hpBar);
        }

        private static void HandleColorHPBar(World world, XmlGump gump, XmlNode node)
        {
            var hpBar = new XmlHealthBar(world, world.Player);
            ApplyBasicAttributes(hpBar, node);
            hpBar.SetColoredType();

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "hue_normal":
                        if (ushort.TryParse(attr.Value, out ushort h))
                        {
                            hpBar.NormalHue = h;
                        }
                        break;
                    case "hue_background":
                        if (ushort.TryParse(attr.Value, out ushort hh))
                        {
                            hpBar.BackgroundHue = hh;
                        }
                        break;
                    case "hue_poisoned":
                        if (ushort.TryParse(attr.Value, out ushort hp))
                        {
                            hpBar.PoisonedHue = hp;
                        }
                        break;
                    case "direction":
                        if (attr.Value == "right")
                        {
                            hpBar.BarDirection = XmlHealthBar.Direction.LeftToRight;
                        }
                        else if (attr.Value == "up")
                        {
                            hpBar.BarDirection = XmlHealthBar.Direction.BottomToTop;
                        }
                        break;
                }
            }

            gump.Add(hpBar);
        }

        private static void HandleMacroButtonGraphic(World world, XmlGump gump, XmlNode node)
        {
            var hb = new HitBox(0, 0, 0, 0, null, 0);
            ApplyBasicAttributes(hb, node);


            var bg = new GumpPic(0, 0, 0, 0);

            hb.Add(bg);

            ushort hue = 0, graphic = 0;

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "id":
                        if (ushort.TryParse(attr.Value, out ushort gra))
                        {
                            bg.Graphic = graphic = gra;
                            if (hb.Width < 1)
                            {
                                hb.ForceSizeUpdate();
                            }
                        }
                        break;
                    case "id_hover":
                        if (ushort.TryParse(attr.Value, out ushort gra_hover))
                        {
                            hb.MouseEnter += (s, e) => { bg.Graphic = gra_hover; };
                            hb.MouseExit += (s, e) => { bg.Graphic = graphic; };
                        }
                        break;
                    case "alpha":
                        if (float.TryParse(attr.Value, out float a))
                        {
                            bg.Alpha = a;
                        }
                        break;
                    case "hue":
                        if (ushort.TryParse(attr.Value, out ushort h))
                        {
                            bg.Hue = hue = h;
                        }
                        break;
                    case "hue_hover":
                        if (ushort.TryParse(attr.Value, out ushort hh))
                        {
                            hb.MouseEnter += (s, e) => { bg.Hue = hh; };
                            hb.MouseExit += (s, e) => { bg.Hue = hue; };
                        }
                        break;
                    case "macro":
                        MacroManager manager = world.Macros;
                        Macro m = manager.FindMacro(attr.Value);
                        if (m != null)
                        {
                            hb.MouseUp += (s, e) => { if (e.Button == Input.MouseButtonType.Left) { manager.SetMacroToExecute(m.Items as MacroObject); } };
                        }
                        break;
                }
            }

            gump.Add(hb);
        }

        private static void HandleMacroButton(World world, XmlGump gump, XmlNode node)
        {
            var hb = new HitBox(0, 0, 0, 0, null, 0);
            ApplyBasicAttributes(hb, node);

            var bg = new AlphaBlendControl() { Width = hb.Width, Height = hb.Height };
            hb.Add(bg);

            ushort hue = 0;

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {

                    case "alpha":
                        if (float.TryParse(attr.Value, out float a))
                        {
                            bg.Alpha = a;
                        }
                        break;
                    case "hue":
                        if (ushort.TryParse(attr.Value, out ushort h))
                        {
                            bg.Hue = hue = h;
                        }
                        break;
                    case "hue_hover":
                        if (ushort.TryParse(attr.Value, out ushort hh))
                        {
                            hb.MouseEnter += (s, e) => { bg.Hue = hh; };
                            hb.MouseExit += (s, e) => { bg.Hue = hue; };
                        }
                        break;
                    case "macro":
                        MacroManager manager = world.Macros;
                        Macro m = manager.FindMacro(attr.Value);
                        if (m != null)
                        {
                            hb.MouseUp += (s, e) => { if (e.Button == Input.MouseButtonType.Left) { manager.SetMacroToExecute(m.Items as MacroObject); } };
                        }
                        break;
                }
            }

            gump.Add(hb);
        }

        private static void HandleBorder(XmlGump gump, XmlNode node)
        {
            ushort hue = 0;
            int width = 0, height = 0;

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "hue":
                        ushort.TryParse(attr.Value, out hue);
                        break;
                    case "width":
                        int.TryParse(attr.Value, out width); //Special handling of width and height required for this tag
                        break;
                    case "height":
                        int.TryParse(attr.Value, out height);
                        break;
                }
            }

            gump.Add(ApplyBasicAttributes(new SimpleBorder() { Hue = hue, Width = width, Height = height }, node));
        }

        private static void HandleControl(World world, XmlGump gump, XmlNode node)
        {
            var newControl = new XmlGump(world);
            ApplyBasicAttributes(newControl, node);

            ProcessChildNodes(world, newControl, node);

            if (newControl.Width < 1)
            {
                newControl.ForceSizeUpdate();
            }

            gump.Add(newControl);
        }

        private static void HandleColorProgressBar(World world, XmlGump gump, XmlNode node)
        {
            ushort bg_hue = 0, fg_hue = 0;
            int value = 0, maxval = 0;
            bool needsUpdates = false;
            string originalValue = string.Empty, originalMaxVal = string.Empty;
            bool vertical = false;

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "background_hue":
                        ushort.TryParse(attr.Value, out bg_hue);
                        break;
                    case "foreground_hue":
                        ushort.TryParse(attr.Value, out fg_hue);
                        break;
                    case "value":
                        originalValue = attr.Value;
                        if (!int.TryParse(attr.Value, out value))
                        {
                            int.TryParse(FormatText(world, attr.Value), out value);
                        }
                        break;
                    case "max_value":
                        originalMaxVal = attr.Value;
                        if (!int.TryParse(attr.Value, out maxval))
                        {
                            int.TryParse(FormatText(world, attr.Value), out maxval);
                        }
                        break;
                    case "updates":
                        bool.TryParse(attr.Value, out needsUpdates);
                        break;
                    case "direction":
                        if (attr.Value == "up")
                        {
                            vertical = true;
                        }
                        else if (attr.Value == "right")
                        {
                            vertical = false;
                        }
                        break;
                }
            }
            Control c;
            gump.Add(c = ApplyBasicAttributes(new ColorBox(0, 0, bg_hue) { AcceptMouseInput = false }, node));
            int maxWidth = c.Width;
            int maxHeight = c.Height;
            gump.Add(c = ApplyBasicAttributes(new ColorBox(0, 0, fg_hue) { AcceptMouseInput = false }, node));
            c.Width = (int)(GetPercentage(value, maxval) * c.Width);

            if (needsUpdates)
            {
                if (vertical)
                {
                    gump.VerticalProgressBarUpdates.Add(new XmlProgressBarInfo(c, maxHeight, originalValue, originalMaxVal));
                }
                else
                {
                    gump.ProgressBarUpdates.Add(new XmlProgressBarInfo(c, maxWidth, originalValue, originalMaxVal));
                }
            }
        }

        private static void HandleImageProgressBar(World world, XmlGump gump, XmlNode node)
        {
            ushort bg_graphic = 0, fg_graphic = 0, bg_hue = 0, fg_hue = 0;
            int value = 0, maxval = 0;
            bool needsUpdates = false;
            string originalValue = string.Empty, originalMaxVal = string.Empty;
            bool vertical = false;

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "image_background":
                        ushort.TryParse(attr.Value, out bg_graphic);
                        break;
                    case "image_foreground":
                        ushort.TryParse(attr.Value, out fg_graphic);
                        break;
                    case "background_hue":
                        ushort.TryParse(attr.Value, out bg_hue);
                        break;
                    case "foreground_hue":
                        ushort.TryParse(attr.Value, out fg_hue);
                        break;
                    case "value":
                        originalValue = attr.Value;
                        if (!int.TryParse(attr.Value, out value))
                        {
                            int.TryParse(FormatText(world, attr.Value), out value);
                        }
                        break;
                    case "max_value":
                        originalMaxVal = attr.Value;
                        if (!int.TryParse(attr.Value, out maxval))
                        {
                            int.TryParse(FormatText(world, attr.Value), out maxval);
                        }
                        break;
                    case "updates":
                        bool.TryParse(attr.Value, out needsUpdates);
                        break;
                    case "direction":
                        if (attr.Value == "up")
                        {
                            vertical = true;
                        }
                        else if (attr.Value == "right")
                        {
                            vertical = false;
                        }
                        break;
                }
            }
            Control c;
            gump.Add(c = ApplyBasicAttributes(new GumpPic(0, 0, bg_graphic, bg_hue), node));
            int maxWidth = c.Width;
            int maxHeight = c.Height;
            if (vertical)
            {
                gump.Add(c = ApplyBasicAttributes(new GumpPicInPic(0, 0, fg_graphic, 0, 0, (ushort)maxWidth, (ushort)maxHeight) { Hue = fg_hue }, node));
            }
            else
            {
                gump.Add(c = ApplyBasicAttributes(new GumpPicTiled(fg_graphic) { Hue = fg_hue }, node));
                c.Width = (int)(GetPercentage(value, maxval) * c.Width);
            }

            if (needsUpdates)
            {
                if (vertical)
                {
                    gump.VerticalProgressBarUpdates.Add(new XmlProgressBarInfo(c, maxHeight, originalValue, originalMaxVal));
                }
                else
                {
                    gump.ProgressBarUpdates.Add(new XmlProgressBarInfo(c, maxWidth, originalValue, originalMaxVal));
                }
            }
        }

        private static void HandleImage(XmlGump gump, XmlNode node)
        {
            ushort graphic = 0, hue = 0;
            Rectangle picinpic = Rectangle.Empty;
            bool isPicInPic = false;

            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "id":
                        ushort.TryParse(attr.Value, out graphic);
                        break;
                    case "hue":
                        ushort.TryParse(attr.Value, out hue);
                        break;
                    case "rect":
                        string[] parts = attr.Value.Split(';');
                        if (parts.Length == 4)
                        {
                            if (int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y) && int.TryParse(parts[2], out int w) && int.TryParse(parts[3], out int h))
                            {
                                picinpic = new Rectangle(x, y, w, h);
                                isPicInPic = true;
                            }
                        }
                        break;
                }
            }

            if (isPicInPic)
            {
                gump.Add(ApplyBasicAttributes(new GumpPicInPic(0, 0, graphic, (ushort)picinpic.X, (ushort)picinpic.Y, (ushort)picinpic.Width, (ushort)picinpic.Height), node));
            }
            else
            {
                gump.Add(ApplyBasicAttributes(new GumpPic(0, 0, graphic, hue), node));
            }
        }

        private static void HandleColorBox(XmlGump gump, XmlNode colorNode)
        {
            ushort hue = 0;
            float alpha = 1;

            foreach (XmlAttribute attr in colorNode.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "hue":
                        ushort.TryParse(attr.Value, out hue);
                        break;
                    case "alpha":
                        float.TryParse(attr.Value, out alpha);
                        break;

                }
            }

            gump.Add(ApplyBasicAttributes(new ColorBox(0, 0, hue) { Alpha = alpha, AcceptMouseInput = true, CanCloseWithRightClick = true, CanMove = true }, colorNode));
        }

        private static void HandleTextTag(World world, XmlGump gump, XmlNode textNode)
        {
            string font = TrueTypeLoader.EMBEDDED_FONT;
            int x = 0, y = 0, fontSize = 16, width = 0, hue = 997;
            bool needsUpdates = false;
            bool useNativeFont = gump.UseNativeFont;
            XmlTextStyleSettings style = ParseXmlTextStyleSettings(textNode);
            FontStashSharp.RichText.TextHorizontalAlignment align = FontStashSharp.RichText.TextHorizontalAlignment.Left;
            TEXT_ALIGN_TYPE nativeAlign = TEXT_ALIGN_TYPE.TS_LEFT;

            foreach (XmlAttribute attr in textNode.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "x":
                        int.TryParse(attr.Value, out x);
                        break;
                    case "y":
                        int.TryParse(attr.Value, out y);
                        break;
                    case "font":
                        font = attr.Value;
                        break;
                    case "size":
                        int.TryParse(attr.Value, out fontSize);
                        break;
                    case "width":
                        int.TryParse(attr.Value, out width);
                        break;
                    case "hue":
                        int.TryParse(attr.Value, out hue);
                        break;
                    case "color":
                    case "stroke":
                        // Parsed together below so hue-only XML remains backward compatible.
                        break;
                    case "updates":
                        bool.TryParse(attr.Value, out needsUpdates);
                        break;
                    case "native":
                        bool.TryParse(attr.Value, out useNativeFont);
                        break;
                    case "align":
                        switch (attr.Value.ToLower())
                        {
                            case "left":
                                align = FontStashSharp.RichText.TextHorizontalAlignment.Left;
                                nativeAlign = TEXT_ALIGN_TYPE.TS_LEFT;
                                break;
                            case "center":
                                align = FontStashSharp.RichText.TextHorizontalAlignment.Center;
                                nativeAlign = TEXT_ALIGN_TYPE.TS_CENTER;
                                break;
                            case "right":
                                align = FontStashSharp.RichText.TextHorizontalAlignment.Right;
                                nativeAlign = TEXT_ALIGN_TYPE.TS_RIGHT;
                                break;
                        }
                        break;
                }
            }

            string text = FormatText(world, textNode.InnerText);
            if (useNativeFont)
            {
                byte nativeFont = byte.TryParse(font, out byte parsedFont) ? parsedFont : byte.MaxValue;
                ushort nativeHue = (ushort)Math.Clamp(hue, ushort.MinValue, ushort.MaxValue);
                // Script gump labels are rendered without a max width. Do the same here so width is
                // an alignment region rather than a word-wrap boundary that can stack into the next row.
                var label = new Label(text, true, nativeHue, font: nativeFont)
                {
                    X = x,
                    Y = y,
                    AcceptMouseInput = false
                };
                label.X = GetNativeTextX(x, width, label.Width, nativeAlign);

                gump.Add(label);

                if (needsUpdates)
                {
                    gump.TextBoxUpdates.Add(
                        new XmlTextUpdateInfo(label, textNode.InnerText, text, x, width, nativeAlign)
                    );
                }

                return;
            }

            fontSize = Math.Max(1, fontSize + gump.FontSizeOffset);

            TextBox.RTLOptions textboxOptions = new()
            {
                Width = width > 0 ? width : null,
                Align = align,
                StrokeEffect = style.Stroke
            };
            TextBox t = style.Color.HasValue
                ? TextBox.GetOne(text, font, fontSize, style.Color.Value, textboxOptions)
                : TextBox.GetOne(text, font, fontSize, hue, textboxOptions);
            gump.Add(t);
            t.X = x;
            t.Y = y;
            t.AcceptMouseInput = false;

            if (needsUpdates)
            {
                gump.TextBoxUpdates.Add(new XmlTextUpdateInfo(t, textNode.InnerText, text));
            }
        }

        internal static int GetNativeTextX(
            int x,
            int width,
            int labelWidth,
            TEXT_ALIGN_TYPE align
        )
        {
            if (width <= 0 || labelWidth >= width)
            {
                return x;
            }

            return align switch
            {
                TEXT_ALIGN_TYPE.TS_CENTER => x + (width - labelWidth) / 2,
                TEXT_ALIGN_TYPE.TS_RIGHT => x + width - labelWidth,
                _ => x
            };
        }

        internal static XmlTextStyleSettings ParseXmlTextStyleSettings(XmlNode textNode)
        {
            Color? color = null;
            bool stroke = false;

            foreach (XmlAttribute attr in textNode.Attributes)
            {
                switch (attr.Name.ToLowerInvariant())
                {
                    case "color":
                        string hex = attr.Value.Trim();
                        if (hex.StartsWith("#", StringComparison.Ordinal))
                        {
                            hex = hex.Substring(1);
                        }

                        if (
                            hex.Length == 6
                            && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb)
                        )
                        {
                            color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                        }
                        break;
                    case "stroke":
                        bool.TryParse(attr.Value, out stroke);
                        break;
                }
            }

            return new XmlTextStyleSettings(color, stroke);
        }

        private static Control ApplyBasicAttributes(Control c, XmlNode node)
        {
            foreach (XmlAttribute attr in node.Attributes)
            {
                switch (attr.Name.ToLower())
                {
                    case "x":
                        int.TryParse(attr.Value, out c.X);
                        break;
                    case "y":
                        int.TryParse(attr.Value, out c.Y);
                        break;
                    case "acceptmouseinput":
                        if (bool.TryParse(attr.Value, out bool b))
                        {
                            c.AcceptMouseInput = b;
                        }
                        break;
                    case "canmove":
                        if (bool.TryParse(attr.Value, out bool cm))
                        {
                            c.CanMove = cm;
                        }
                        break;
                    case "width":
                        int.TryParse(attr.Value, out c.Width);
                        break;
                    case "height":
                        int.TryParse(attr.Value, out c.Height);
                        break;
                }
            }

            return c;
        }

        public static float GetPercentage(double value, double max) => (float)(value / max);

        public static string FormatText(World world, string text)
        {
            text = text.Replace("{charname}", world.Player.Name);
            text = text.Replace("{hp}", world.Player.Hits.ToString());
            text = text.Replace("{maxhp}", world.Player.HitsMax.ToString());
            text = text.Replace("{mana}", world.Player.Mana.ToString());
            text = text.Replace("{maxmana}", world.Player.ManaMax.ToString());
            text = text.Replace("{stam}", world.Player.Stamina.ToString());
            text = text.Replace("{maxstam}", world.Player.StaminaMax.ToString());
            text = text.Replace("{weight}", world.Player.Weight.ToString());
            text = text.Replace("{maxweight}", world.Player.WeightMax.ToString());
            text = text.Replace("{str}", world.Player.Strength.ToString());
            text = text.Replace("{dex}", world.Player.Dexterity.ToString());
            text = text.Replace("{int}", world.Player.Intelligence.ToString());
            text = text.Replace("{damagemin}", world.Player.DamageMin.ToString());
            text = text.Replace("{damagemax}", world.Player.DamageMax.ToString());
            text = text.Replace("{hci}", world.Player.HitChanceIncrease.ToString());
            text = text.Replace("{di}", world.Player.DamageIncrease.ToString());
            text = text.Replace("{ssi}", world.Player.SwingSpeedIncrease.ToString());
            text = text.Replace("{defchance}", world.Player.DefenseChanceIncrease.ToString());
            text = text.Replace("{defchancemax}", world.Player.MaxDefenseChanceIncrease.ToString());
            text = text.Replace("{sdi}", world.Player.SpellDamageIncrease.ToString());
            CombatDamageSnapshot combatDps = world.CombatDamageTracker.GetActiveSnapshot();
            text = text.Replace("{dpsmine}", combatDps.MineDps.ToString("0.0"));
            text = text.Replace("{dpsothers}", combatDps.OthersDps.ToString("0.0"));
            text = text.Replace("{dpsunknown}", combatDps.UnknownDps.ToString("0.0"));
            text = text.Replace("{dpstotal}", combatDps.TotalDps.ToString("0.0"));
            text = text.Replace("{dpsminedamage}", combatDps.MineDamage.ToString());
            text = text.Replace("{dpsothersdamage}", combatDps.OthersDamage.ToString());
            text = text.Replace("{dpsunknowndamage}", combatDps.UnknownDamage.ToString());
            text = text.Replace("{dpstotaldamage}", combatDps.TotalDamage.ToString());
            text = text.Replace("{dpshits}", combatDps.HitCount.ToString());
            text = text.Replace("{dpsseconds}", combatDps.ElapsedSeconds.ToString("0.0"));
            text = text.Replace("{dpscoverage}", (combatDps.AttributionCoverage * 100).ToString("0"));
            text = text.Replace("{fc}", world.Player.FasterCasting.ToString());
            text = text.Replace("{fcr}", world.Player.FasterCastRecovery.ToString());
            text = text.Replace("{lmc}", world.Player.LowerManaCost.ToString());
            text = text.Replace("{lrc}", world.Player.LowerReagentCost.ToString());
            text = text.Replace("{phyres}", world.Player.PhysicalResistance.ToString());
            text = text.Replace("{phyresmax}", world.Player.MaxPhysicResistence.ToString());
            text = text.Replace("{fireres}", world.Player.FireResistance.ToString());
            text = text.Replace("{fireresmax}", world.Player.MaxFireResistence.ToString());
            text = text.Replace("{coldres}", world.Player.ColdResistance.ToString());
            text = text.Replace("{coldresmax}", world.Player.MaxColdResistence.ToString());
            text = text.Replace("{poisonres}", world.Player.PoisonResistance.ToString());
            text = text.Replace("{poisonresmax}", world.Player.MaxPoisonResistence.ToString());
            text = text.Replace("{energyres}", world.Player.EnergyResistance.ToString());
            text = text.Replace("{energyresmax}", world.Player.MaxEnergyResistence.ToString());
            text = text.Replace("{maxstats}", world.Player.StatsCap.ToString());
            text = text.Replace("{luck}", world.Player.Luck.ToString());
            text = text.Replace("{gold}", world.Player.Gold.ToString());
            text = text.Replace("{pets}", world.Player.Followers.ToString());
            text = text.Replace("{petsmax}", world.Player.FollowersMax.ToString());

            NetStatistics statistics = AsyncNetClient.Socket?.Statistics;
            text = FormatNetworkText(
                text,
                statistics?.Ping ?? 0,
                statistics?.DeltaBytesReceived ?? 0,
                statistics?.DeltaBytesSent ?? 0
            );

            return text;
        }

        internal static string FormatNetworkText(
            string text,
            uint ping,
            uint bytesReceived,
            uint bytesSent
        )
        {
            text = text.Replace("{ping}", ping.ToString());
            text = text.Replace("{bytesreceived}", NetStatistics.GetSizeAdaptive(bytesReceived));
            text = text.Replace("{bytessent}", NetStatistics.GetSizeAdaptive(bytesSent));

            return text;
        }

        public class XmlProgressBarInfo
        {
            public XmlProgressBarInfo(Control control, int maxSize, string value, string maxValue)
            {
                Control = control;
                MaxSize = maxSize;
                Value = value;
                MaxValue = maxValue;
            }

            public Control Control { get; }
            public int MaxSize { get; }
            public string Value { get; }
            public string MaxValue { get; }
        }

        public sealed class XmlTextUpdateInfo
        {
            public XmlTextUpdateInfo(
                Control control,
                string template,
                string initialText,
                int x = 0,
                int width = 0,
                TEXT_ALIGN_TYPE align = TEXT_ALIGN_TYPE.TS_LEFT
            )
            {
                Control = control;
                Template = template;
                LastText = initialText;
                X = x;
                Width = width;
                Align = align;
            }

            public Control Control { get; }
            public string Template { get; }
            public string LastText { get; private set; }
            private int X { get; }
            private int Width { get; }
            private TEXT_ALIGN_TYPE Align { get; }

            internal void Apply(string text)
            {
                switch (Control)
                {
                    case TextBox textBox:
                        textBox.Text = text;
                        // TextBox applies its stroke during Update. Rebuild now so a changed
                        // value never renders for one frame without its configured outline.
                        textBox.Update();
                        break;
                    case Label label:
                        label.Text = text;
                        label.X = GetNativeTextX(X, Width, label.Width, Align);
                        break;
                }
            }

            internal bool ShouldApply(string text)
            {
                if (LastText == text)
                {
                    return false;
                }

                LastText = text;
                return true;
            }
        }
    }

    internal readonly struct PlayerPaperDollSettings
    {
        public const int DefaultWidth = 190;
        public const int DefaultHeight = 250;

        public PlayerPaperDollSettings(int width, int height, bool updates, bool background, float alpha)
        {
            Width = width;
            Height = height;
            Updates = updates;
            Background = background;
            Alpha = alpha;
        }

        public int Width { get; }
        public int Height { get; }
        public bool Updates { get; }
        public bool Background { get; }
        public float Alpha { get; }
    }

    internal readonly struct EmbeddedTextureSettings
    {
        public EmbeddedTextureSettings(string texture, int border, ushort hue, float alpha)
        {
            Texture = texture;
            Border = border;
            Hue = hue;
            Alpha = alpha;
        }

        public string Texture { get; }
        public int Border { get; }
        public ushort Hue { get; }
        public float Alpha { get; }
    }

    internal readonly struct XmlTextStyleSettings
    {
        public XmlTextStyleSettings(Color? color, bool stroke)
        {
            Color = color;
            Stroke = stroke;
        }

        public Color? Color { get; }
        public bool Stroke { get; }
    }

    public class XmlGump : Gump
    {
        /// <summary>
        /// The frequency of UI updates for Xml Gumps for text/progress bar changes. This affects all xml gumps, it is not set individually.
        /// </summary>
        public static uint UpdateFrequency { get; set; } = 250;
        public List<XmlTextUpdateInfo> TextBoxUpdates { get; set; } = new List<XmlTextUpdateInfo>();
        public List<XmlProgressBarInfo> ProgressBarUpdates { get; set; } = new List<XmlProgressBarInfo>();
        public List<XmlProgressBarInfo> VerticalProgressBarUpdates { get; set; } = new List<XmlProgressBarInfo>();
        public bool SavePosition { get; set; }
        public string FilePath { get; set; }
        public int FontSizeOffset { get; set; }
        public bool UseNativeFont { get; set; }

        private uint nextUpdate = 0;
        private bool savingFile = false;
        private uint saveFileAfter = uint.MaxValue;

        public XmlGump(World world) : base(world, 0, 0)
        {
        }

        public override void Update()
        {
            base.Update();

            if (Time.Ticks >= nextUpdate)
            {
                foreach (XmlTextUpdateInfo update in TextBoxUpdates)
                {
                    if (update.Control != null && !update.Control.IsDisposed)
                    {
                        string newString = XmlGumpHandler.FormatText(World, update.Template);
                        if (update.ShouldApply(newString))
                        {
                            update.Apply(newString);
                        }
                    }
                }

                foreach (XmlProgressBarInfo p in ProgressBarUpdates)
                {
                    if (p.Control != null && !p.Control.IsDisposed)
                    {
                        if (int.TryParse(XmlGumpHandler.FormatText(World, p.Value), out int val))
                        {
                            if (int.TryParse(XmlGumpHandler.FormatText(World, p.MaxValue), out int max))
                            {
                                p.Control.Width = (int)(XmlGumpHandler.GetPercentage(val, max) * p.MaxSize);
                            }
                        }
                    }
                }

                foreach (XmlProgressBarInfo p in VerticalProgressBarUpdates)
                {
                    if (p.Control != null && !p.Control.IsDisposed)
                    {
                        if (int.TryParse(XmlGumpHandler.FormatText(World, p.Value), out int val))
                        {
                            if (int.TryParse(XmlGumpHandler.FormatText(World, p.MaxValue), out int max))
                            {
                                int newHeight = (int)(XmlGumpHandler.GetPercentage(val, max) * p.MaxSize);
                                if (p.Control is GumpPicInPic picnpic)
                                {
                                    picnpic.PicInPicBounds = new Rectangle(0, p.MaxSize - newHeight, picnpic.Width, p.MaxSize - (p.MaxSize - newHeight));
                                    picnpic.DrawOffset = new Vector2(0, p.MaxSize - newHeight);
                                }
                                else
                                {
                                    p.Control.Height = newHeight;
                                    p.Control.Y = p.MaxSize - newHeight;
                                }
                            }
                        }
                    }
                }

                nextUpdate = Time.Ticks + UpdateFrequency;
            }

            if (Time.Ticks > saveFileAfter)
            {
                saveFileAfter = uint.MaxValue;
                Task.Run(SaveFile);
            }
        }

        protected override void OnMove(int x, int y)
        {
            base.OnMove(x, y);

            if (SavePosition)
            {
                saveFileAfter = Time.Ticks + 5000;
            }
        }

        public override void OnMouseUp(int x, int y, MouseButtonType button)
        {
            base.OnMouseUp(x, y, button);

            if (World.TargetManager.IsTargeting)
            {
                World.TargetManager.Target(World.Player);
                Mouse.LastLeftButtonClickTime = 0;
            }
        }

        protected override void OnLockedChanged()
        {
            base.OnLockedChanged();
            saveFileAfter = Time.Ticks + 5000;
        }

        private void SaveFile()
        {
            if (!savingFile)
            {
                savingFile = true;

                if (!string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
                {
                    var xmlDoc = new XmlDocument();
                    try
                    {
                        xmlDoc.LoadXml(File.ReadAllText(FilePath));

                        if (xmlDoc.DocumentElement != null)
                        {
                            XmlElement root = xmlDoc.DocumentElement;
                            root.SetAttribute("x", X.ToString());
                            root.SetAttribute("y", Y.ToString());
                            root.SetAttribute("locked", IsLocked.ToString());

                            xmlDoc.Save(FilePath);
                        }
                    }
                    catch (Exception e) { GameActions.Print(World, e.Message); }

                }

                savingFile = false;
            }
        }
    }

    public class XmlHealthBar : Control
    {
        private ColorBox color_background, color_foreground;
        private GumpPic image_background;
        private GumpPicInPic image_foreground;
        private Mobile mobile;

        private ushort backgroundHue, backgroundImage, foregroundImage, foregroundImagePoisoned = 0;

        public ushort NormalHue = 97;
        public ushort PoisonedHue = 62;
        public ushort BackgroundHue
        {
            get => backgroundHue; set
            {
                backgroundHue = value;
                if (color_background != null)
                {
                    color_background.Hue = value;
                }
                if (image_background != null)
                {
                    image_background.Hue = value;
                }
            }
        }
        public Direction BarDirection { get; set; } = Direction.LeftToRight;

        public ushort BackgroundImage
        {
            get => backgroundImage;
            set
            {
                backgroundImage = value;
                if (image_background != null)
                {
                    image_background.Graphic = value;
                    Width = image_background.Width;
                    Height = image_background.Height;
                }
            }
        }

        public ushort ForegroundImage
        {
            get => foregroundImage;
            set
            {
                foregroundImage = value;
                if (image_background != null)
                {
                    image_foreground.Graphic = value;
                }
            }
        }

        public ushort ForegroundImagePoisoned
        {
            get { return foregroundImagePoisoned == 0 ? foregroundImage : foregroundImagePoisoned; }
            set
            {
                foregroundImagePoisoned = value;
            }
        }

        public XmlHealthBar(World world, uint serial)
        {
            LocalSerial = serial;
            if (SerialHelper.IsMobile(serial))
            {
                mobile = world.Get(serial) as Mobile;
            }
            AcceptMouseInput = false;
        }

        public void SetImageType()
        {
            color_background = null;
            color_foreground = null;

            image_background = new GumpPic(0, 0, 0, BackgroundHue);
            image_foreground = new GumpPicInPic(0, 0, 0, 0, 0, 0, 0);
        }

        public void SetColoredType()
        {
            image_background = null;
            image_foreground = null;

            color_background = new ColorBox(Width, Height, BackgroundHue);
            color_foreground = new ColorBox(Width, Height, NormalHue);
        }

        public override void Update()
        {
            base.Update();

            if (mobile != null)
            {
                if (mobile.IsPoisoned)
                {
                    if (color_foreground != null)
                    {
                        color_foreground.Hue = PoisonedHue;
                    }
                    if (image_foreground != null)
                    {
                        image_foreground.Hue = PoisonedHue;
                        if (foregroundImagePoisoned != 0)
                        {
                            image_foreground.Graphic = foregroundImagePoisoned;
                        }
                    }
                }
                else
                {
                    if (color_foreground != null)
                    {
                        color_foreground.Hue = NormalHue;
                    }
                    if (image_foreground != null)
                    {
                        image_foreground.Hue = NormalHue;

                        if (image_foreground.Graphic != foregroundImage)
                        {
                            image_foreground.Graphic = foregroundImage;
                        }
                    }
                }

                UpdateForegroundSizeForPercent();
            }
        }

        private void UpdateForegroundSizeForPercent()
        {
            switch (BarDirection)
            {
                case Direction.LeftToRight:
                    if (color_foreground != null)
                    {
                        color_foreground.Width = (int)(Width * healthPercent());
                    }

                    if (image_foreground != null)
                    {
                        int widthPerc = (int)(Width * healthPercent());
                        image_foreground.PicInPicBounds = new Rectangle(0, 0, widthPerc, Height);
                        image_foreground.Width = widthPerc;
                    }
                    break;
                case Direction.BottomToTop:
                    if (color_foreground != null)
                    {
                        color_foreground.Height = (int)(Height * healthPercent());
                        color_foreground.Y = Height - color_foreground.Height;
                    }

                    if (image_foreground != null)
                    {
                        int heightPerc = (int)(Height * healthPercent());
                        image_foreground.PicInPicBounds = new Rectangle(0, Height - heightPerc, Width, Height - (Height - heightPerc));
                        image_foreground.Y = Height - heightPerc;
                    }
                    break;
            }
        }

        public override bool Draw(UltimaBatcher2D batcher, int x, int y)
        {
            base.Draw(batcher, x, y);

            color_background?.Draw(batcher, x, y);
            color_foreground?.Draw(batcher, x, y + color_foreground.Y);
            image_background?.Draw(batcher, x, y);
            image_foreground?.Draw(batcher, x, y + image_foreground.Y);

            return true;
        }

        private float healthPercent()
        {
            if (mobile == null)
            {
                return 0f;
            }

            return (float)((double)mobile.Hits / (double)mobile.HitsMax);
        }

        public enum Direction
        {
            LeftToRight,
            BottomToTop
        }
    }
}
