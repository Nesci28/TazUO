// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.Managers;
using ClassicUO.Game.UI.Controls;
using ClassicUO.Input;

namespace ClassicUO.Game.UI.Gumps;

/// <summary>
/// Creates the side-by-side item comparison shared by grid containers, paperdolls, and server-sent
/// item gumps.
/// </summary>
internal static class ItemComparisonTooltips
{
    public static MultipleToolTipGump Create(World world, Item candidate, Control hoverReference)
    {
        if (candidate == null)
            return null;

        return Create(world, candidate.Serial, candidate.ItemData.Layer, hoverReference);
    }

    public static MultipleToolTipGump Create(World world, uint candidateSerial, byte candidateLayer, Control hoverReference)
        => Create(world, candidateSerial, null, candidateLayer, hoverReference);

    /// <summary>
    /// Creates a comparison for server gumps that provide the complete tooltip text without an
    /// itemproperty serial. OSI Vendor Search can use this representation for ButtonTileArt rows.
    /// </summary>
    public static MultipleToolTipGump Create(
        World world,
        string candidateTooltip,
        byte candidateLayer,
        Control hoverReference
    ) => Create(world, 0, candidateTooltip, candidateLayer, hoverReference);

    private static MultipleToolTipGump Create(
        World world,
        uint candidateSerial,
        string candidateTooltipText,
        byte candidateLayer,
        Control hoverReference
    )
    {
        if (world?.Player == null || hoverReference == null || candidateLayer == 0)
            return null;

        Layer itemLayer = (Layer)candidateLayer;
        if (itemLayer == Layer.Backpack)
            return null;

        Item equipped = FindEquippedItem(world, itemLayer);
        Item secondEquipped = null;

        if (itemLayer == Layer.OneHanded || itemLayer == Layer.TwoHanded)
        {
            Layer otherHand = itemLayer == Layer.OneHanded ? Layer.TwoHanded : Layer.OneHanded;
            secondEquipped = FindEquippedItem(world, otherHand);

            if (equipped == null)
            {
                equipped = secondEquipped;
                secondEquipped = null;
            }
        }

        if (equipped == null)
            return null;

        // Equipped items are not directly hovered during a comparison, so request their OPL for
        // both the side tooltip and the stat-change summary.
        world.OPL.Contains(equipped.Serial);
        if (secondEquipped != null)
            world.OPL.Contains(secondEquipped.Serial);

        CustomToolTip candidateTooltip = string.IsNullOrWhiteSpace(candidateTooltipText)
            ? new CustomToolTip(
                world,
                candidateSerial,
                candidateLayer,
                Mouse.Position.X + 5,
                Mouse.Position.Y + 5,
                hoverReference,
                compareTo: equipped
            )
            : new CustomToolTip(
                world,
                candidateTooltipText,
                candidateLayer,
                Mouse.Position.X + 5,
                Mouse.Position.Y + 5,
                hoverReference,
                compareTo: equipped
            );

        var equippedTooltip = new CustomToolTip(
            world,
            equipped,
            candidateTooltip.X + candidateTooltip.Width + 10,
            candidateTooltip.Y,
            hoverReference,
            "<basefont color=\"orange\">Equipped Item<br>"
        );
        equippedTooltip.OnOPLLoaded += candidateTooltip.RefreshData;

        var tooltips = new List<CustomToolTip> { candidateTooltip, equippedTooltip };

        if (CUOEnviroment.Debug)
        {
            ItemPropertiesData candidateProperties = string.IsNullOrWhiteSpace(candidateTooltipText)
                ? new ItemPropertiesData(world, candidateSerial, candidateLayer)
                : new ItemPropertiesData(world, candidateTooltipText, candidateLayer);
            var equippedProperties = new ItemPropertiesData(world, equipped);

            if (candidateProperties.GenerateComparisonTooltip(equippedProperties, out string compiledTooltip))
                GameActions.Print(world, compiledTooltip);
        }

        if (secondEquipped != null)
        {
            tooltips.Add(
                new CustomToolTip(
                    world,
                    secondEquipped,
                    equippedTooltip.X + equippedTooltip.Width + 10,
                    equippedTooltip.Y,
                    hoverReference,
                    "<basefont color=\"orange\">Equipped Item<br>"
                )
            );
        }

        return new MultipleToolTipGump(
            world,
            Mouse.Position.X + 10,
            Mouse.Position.Y + 10,
            tooltips.ToArray(),
            hoverReference
        );
    }

    /// <summary>
    /// Finds the equipped item by its network layer first, then by the static tile-data layer.
    /// The fallback handles equipment packets that leave <see cref="Item.Layer"/> unset even though
    /// the item is linked directly to the player and its art identifies the correct wearable slot.
    /// </summary>
    internal static Item FindEquippedItem(World world, Layer layer)
    {
        Item equipped = world?.Player?.FindItemByLayer(layer);
        if (equipped != null || world?.Player == null)
            return equipped;

        for (var current = world.Player.Items; current != null; current = current.Next)
        {
            var item = (Item)current;
            if (
                !item.IsDestroyed
                && item.Container == world.Player.Serial
                && item.ItemData.Layer == (byte)layer
            )
            {
                return item;
            }
        }

        return null;
    }
}
