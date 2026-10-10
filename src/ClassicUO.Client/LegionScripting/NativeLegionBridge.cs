namespace ClassicUO.LegionScripting;

/// <summary>
/// Small public boundary used by native hosts to trigger the same Legion
/// script runner that the in-game Script Manager uses. The implementation
/// remains internal so the desktop client keeps its existing surface area.
/// </summary>
public static class NativeLegionBridge
{
    public static ScriptFile? FindLoadedScript(string fileName)
        => LegionScripting.LoadedScripts.Find(item => item.FileName == fileName);

    public static bool Play(ScriptFile? script)
    {
        if (script is null)
            return false;

        LegionScripting.PlayScript(script);
        return true;
    }
}
