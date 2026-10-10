// SPDX-License-Identifier: BSD-2-Clause

using System.Collections.Generic;
using System.Linq;

namespace ClassicUO.Game.Managers.VendorSearch;

internal static class VendorSearchResponseValidator
{
    public static bool TryValidate(
        VendorSearchSnapshot snapshot,
        VendorSearchResponseRequest request,
        out int statusCode,
        out string message
    )
    {
        statusCode = 400;
        message = null;

        if (request == null)
        {
            message = "A response body is required.";
            return false;
        }

        if (
            snapshot == null
            || snapshot.Kind is VendorSearchGumpKind.Pending or VendorSearchGumpKind.Closed
        )
        {
            statusCode = 410;
            message = "The Vendor Search gump is no longer available.";
            return false;
        }

        if (request.Version != snapshot.Version)
        {
            statusCode = 409;
            message = "Vendor Search changed; refresh before submitting again.";
            return false;
        }

        if (
            request.ButtonID != 0
            && !snapshot.Buttons.Any(
                button => !button.IsPageButton && button.ButtonID == request.ButtonID
            )
        )
        {
            message = "That button does not exist in the current Vendor Search gump.";
            return false;
        }

        var entryIDs = snapshot.Entries.Select(entry => entry.ID).ToHashSet();

        foreach ((int id, string text) in request.Entries ?? new Dictionary<int, string>())
        {
            if (!entryIDs.Contains(id))
            {
                message = $"Text entry {id} does not exist in this gump.";
                return false;
            }

            if ((text?.Length ?? 0) > 239)
            {
                message = $"Text entry {id} exceeds the 239-character packet limit.";
                return false;
            }
        }

        var switchIDs = snapshot.Switches.Select(control => control.ID).ToHashSet();

        foreach (uint switchID in request.Switches ?? [])
        {
            if (!switchIDs.Contains(switchID))
            {
                message = $"Switch {switchID} does not exist in this gump.";
                return false;
            }
        }

        return true;
    }
}
