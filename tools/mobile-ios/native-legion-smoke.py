# Executed through TazUO's real Legion ScriptFile runner on the connected world.
player = API.Player
assert player is not None, "No player is available to Legion"
serial = player.Serial
assert serial != 0, "Player serial is invalid"
API.Pause(0.1)
assert API.Player.Serial == serial, "Player changed while Legion yielded"
assert not API.StopRequested, "Script was unexpectedly cancelled"

with open("native-legion-smoke-result.txt", "w") as report:
    report.write("Legion live API: PASS player={0} position={1},{2} pause=PASS".format(
        serial, player.X, player.Y))
