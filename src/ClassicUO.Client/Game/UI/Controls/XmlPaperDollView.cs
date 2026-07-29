using System;
using System.Collections.Generic;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using Microsoft.Xna.Framework;

namespace ClassicUO.Game.UI.Controls
{
    internal sealed class XmlPaperDollView : StaticPaperDollView
    {
        private static readonly Layer[] _equipmentLayers =
        {
            Layer.Cloak,
            Layer.Shirt,
            Layer.Pants,
            Layer.Shoes,
            Layer.Legs,
            Layer.Arms,
            Layer.Torso,
            Layer.Tunic,
            Layer.Ring,
            Layer.Bracelet,
            Layer.Face,
            Layer.Gloves,
            Layer.Skirt,
            Layer.Robe,
            Layer.Waist,
            Layer.Necklace,
            Layer.Hair,
            Layer.Beard,
            Layer.Earrings,
            Layer.Helmet,
            Layer.OneHanded,
            Layer.TwoHanded,
            Layer.Talisman
        };

        private readonly World _world;
        private readonly bool _updates;
        private int _appearanceHash;
        private uint _nextUpdate;

        public XmlPaperDollView(
            World world,
            Mobile mobile,
            int width,
            int height,
            bool updates,
            bool background
        ) : base(
            mobile.Graphic,
            mobile.Hue,
            mobile.IsFemale,
            CreateEquipment(mobile),
            new Vector2(width, height),
            background
        )
        {
            _world = world;
            _updates = updates;
            CenterContent = true;
            LocalSerial = mobile.Serial;
            WantUpdateSize = false;
            _appearanceHash = GetAppearanceHash(mobile);
            _nextUpdate = Time.Ticks + XmlGump.UpdateFrequency;
        }

        public override void Update()
        {
            if (_updates && Time.Ticks >= _nextUpdate)
            {
                _nextUpdate = Time.Ticks + XmlGump.UpdateFrequency;

                Mobile mobile = _world.Mobiles.Get(LocalSerial);

                if (mobile == null || mobile.IsDestroyed)
                {
                    Dispose();
                    return;
                }

                int appearanceHash = GetAppearanceHash(mobile);

                if (appearanceHash != _appearanceHash)
                {
                    _appearanceHash = appearanceHash;
                    BodyGraphic = mobile.Graphic;
                    BodyHue = mobile.Hue;
                    IsFemale = mobile.IsFemale;
                    SetEquipment(CreateEquipment(mobile));
                }
            }

            base.Update();
        }

        private static Dictionary<Layer, EquipmentEntry> CreateEquipment(Mobile mobile)
        {
            Dictionary<Layer, EquipmentEntry> equipment = new();

            foreach (Layer layer in _equipmentLayers)
            {
                Item item = mobile.FindItemByLayer(layer);

                if (item == null || item.IsDestroyed || Mobile.IsCovered(mobile, layer))
                {
                    continue;
                }

                equipment[layer] = new EquipmentEntry(
                    item.ItemData.AnimID,
                    (ushort)(item.Hue & 0x3FFF),
                    item.ItemData.IsPartialHue
                );
            }

            return equipment;
        }

        private static int GetAppearanceHash(Mobile mobile)
        {
            HashCode hash = new();
            hash.Add(mobile.Graphic);
            hash.Add(mobile.Hue);
            hash.Add(mobile.IsFemale);

            foreach (Layer layer in _equipmentLayers)
            {
                Item item = mobile.FindItemByLayer(layer);

                if (item == null || item.IsDestroyed)
                {
                    continue;
                }

                hash.Add(layer);
                hash.Add(item.Serial);
                hash.Add(item.Graphic);
                hash.Add(item.Hue);
                hash.Add(item.ItemData.AnimID);
                hash.Add(item.ItemData.IsPartialHue);
            }

            return hash.ToHashCode();
        }
    }
}
