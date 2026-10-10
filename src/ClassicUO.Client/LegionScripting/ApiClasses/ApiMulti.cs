using ClassicUO.Game.GameObjects;

namespace ClassicUO.LegionScripting.ApiClasses;

/// <summary>
/// Represents a Python-accessible multi-tile structure (e.g., player buildings or player ships) in the game world.
/// Inherits spatial and visual data from <see cref="ApiGameObject"/>.
/// </summary>
public class ApiMulti : ApiGameObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApiMulti"/> class from a <see cref="Multi"/> object.
    /// </summary>
    /// <param name="multi">The multi-tile object to wrap.</param>
    internal ApiMulti(Multi multi) : this(multi, 0)
    {
    }

    /// <summary>
    /// Initializes a multi wrapper and associates it with a known client-side house.
    /// </summary>
    internal ApiMulti(Multi multi, uint houseSerial) : base(multi)
    {
        HouseSerial = houseSerial;
    }

    /// <summary>Serial of the known house containing this component, or zero.</summary>
    public uint HouseSerial { get; }

    /// <summary>Hexadecimal representation of <see cref="HouseSerial"/>.</summary>
    public string HouseSerialHex => HouseSerial == 0 ? string.Empty : $"0x{HouseSerial:X8}";

    /// <summary>
    /// The Python-visible class name of this object.
    /// Accessible in Python as <c>obj.__class__</c>.
    /// </summary>
    public override string __class__ => "ApiMulti";
}
