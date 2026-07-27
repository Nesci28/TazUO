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
/// Creates the side-by-side item comparison shared by grid containers and server-sent item gumps.
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
    {
        if (world?.Player == null || hoverReference == null || candidateLayer == 0)
            return null;

        Layer itemLayer = (Layer)candidateLayer;
        if (itemLayer == Layer.Backpack)
            return null;

        Item equipped = world.Player.FindItemByLayer(itemLayer);
        Item secondEquipped = null;

        if (itemLayer == Layer.OneHanded || itemLayer == Layer.TwoHanded)
        {
            Layer otherHand = itemLayer == Layer.OneHanded ? Layer.TwoHanded : Layer.OneHanded;
            secondEquipped = world.Player.FindItemByLayer(otherHand);

            if (equipped == null)
            {
                equipped = secondEquipped;
                secondEquipped = null;
            }
        }

        if (equipped == null)
            return null;

        var candidateTooltip = new CustomToolTip(
            world,
            candidateSerial,
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

        var tooltips = new List<CustomToolTip> { candidateTooltip, equippedTooltip };

        if (CUOEnviroment.Debug)
        {
            var candidateProperties = new ItemPropertiesData(world, candidateSerial, candidateLayer);
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
}
