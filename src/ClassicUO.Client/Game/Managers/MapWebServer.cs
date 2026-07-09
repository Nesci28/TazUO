using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Configuration;
using ClassicUO.Game.Data;
using ClassicUO.Game.GameObjects;
using ClassicUO.Game.UI.Gumps;
using ClassicUO.LegionScripting;
using ClassicUO.Utility.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ClassicUO.Game.Managers
{
    internal class MapWebServer : IDisposable
    {
        private class ClientState
        {
            public HttpListenerResponse Response { get; set; }
            public int LastJournalCount { get; set; }
        }

        private sealed class MarkerManagerRequest
        {
            public string Action { get; set; }
            public int FileIndex { get; set; } = -1;
            public int MarkerIndex { get; set; } = -1;
            public string Name { get; set; }
            public int X { get; set; }
            public int Y { get; set; }
            public int Map { get; set; } = -1;
            public string Color { get; set; }
            public string Icon { get; set; }
        }

        private static readonly JsonSerializerOptions JsonReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly string[] MarkerColors =
        {
            "none",
            "red",
            "green",
            "blue",
            "purple",
            "black",
            "yellow",
            "white"
        };

        private static string UserMarkersFilePath => Path.Combine(
            CUOEnviroment.ExecutablePath,
            "Data",
            "Client",
            $"{WorldMapGump.USER_MARKERS_FILE}.usr"
        );

        private HttpListener _httpListener;
        private Thread _listenerThread;
        private volatile bool _isRunning;
        private int _port = 8088;
        private int _lastMapIndex = -1;
        private readonly object _clientsLock = new object();
        private readonly List<ClientState> _activeClients = new List<ClientState>();
        private byte[] _cachedMapPng = null;
        private readonly object _cacheLock = new object();

        public bool IsRunning => _isRunning;
        public int Port => _port;

        public void SetCachedMapPng(byte[] pngData, int mapIndex)
        {
            lock (_cacheLock)
            {
                _cachedMapPng = pngData;
                _lastMapIndex = mapIndex;
            }
            Log.Info($"Map PNG cached: {pngData?.Length ?? 0} bytes for map {mapIndex}");
        }

        public bool Start(int port = 8088)
        {
            if (_isRunning)
                return false;

            _port = port;

            try
            {
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{_port}/");
                _httpListener.Start();
                _isRunning = true;

                _listenerThread = new Thread(ListenerLoop)
                {
                    IsBackground = true,
                    Name = "MapWebServer"
                };
                _listenerThread.Start();

                Log.Info($"Map Web Server started on http://localhost:{_port}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to start Map Web Server: {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            if (!_isRunning)
                return;

            _isRunning = false;

            try
            {
                _httpListener?.Stop();
                _httpListener?.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"Error stopping Map Web Server: {ex.Message}");
            }

            Log.Info("Map Web Server stopped");
        }

        private void ListenerLoop()
        {
            while (_isRunning)
            {
                try
                {
                    HttpListenerContext context = _httpListener.GetContext();
                    Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Expected when stopping the listener
                    break;
                }
                catch (Exception ex)
                {
                    Log.Error($"Map Web Server error: {ex.Message}");
                }
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            try
            {
                string path = context.Request.Url.AbsolutePath;

                switch (path)
                {
                    case "/":
                        ServeHtmlPage(context.Response);
                        break;
                    case "/api/mapdata":
                        ServeMapData(context.Response);
                        break;
                    case "/api/maptexture":
                        ServeMapTexture(context.Response);
                        break;
                        case "/api/markericon":
                            ServeMarkerIcon(context.Request, context.Response);
                            break;
                        case "/api/markermanager":
                            if (context.Request.HttpMethod == "GET")
                                ServeMarkerManagerData(context.Response);
                            else if (context.Request.HttpMethod == "POST")
                                HandleMarkerManagerRequest(context.Request, context.Response);
                            else
                            {
                                context.Response.StatusCode = 405;
                                context.Response.Close();
                            }
                            break;
                        case "/api/events":
                            ServeEventStream(context.Response);
                            break;
                    case "/api/command":
                        HandleCommand(context.Request, context.Response);
                        break;
                    case "/api/goto":
                        HandleGoto(context.Request, context.Response);
                        break;
                    case "/api/journalsize":
                        if (context.Request.HttpMethod == "GET")
                            GetJournalSize(context.Response);
                        else if (context.Request.HttpMethod == "POST")
                            SetJournalSize(context.Request, context.Response);
                        else
                        {
                            context.Response.StatusCode = 405;
                            context.Response.Close();
                        }
                        break;
                    case "/api/minimizestates":
                        if (context.Request.HttpMethod == "GET")
                            GetMinimizeStates(context.Response);
                        else if (context.Request.HttpMethod == "POST")
                            SetMinimizeStates(context.Request, context.Response);
                        else
                        {
                            context.Response.StatusCode = 405;
                            context.Response.Close();
                        }
                        break;
                    default:
                        context.Response.StatusCode = 404;
                        context.Response.Close();
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling request: {ex.Message}");
                try
                {
                    context.Response.StatusCode = 500;
                    context.Response.Close();
                }
                catch { }
            }
        }

        private void ServeHtmlPage(HttpListenerResponse response)
        {
            string html = GetHtmlPage();
            byte[] buffer = Encoding.UTF8.GetBytes(html);

            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        private void ServeMapData(HttpListenerResponse response)
        {
            if (World.Instance == null || !World.Instance.InGame)
            {
                response.StatusCode = 503;
                response.Close();
                return;
            }

            Texture2D mapTexture = UI.Gumps.WorldMapGump.GetMapTextureForMap();

            var data = new
            {
                mapIndex = World.Instance.MapIndex,
                mapWidth = mapTexture?.Width ?? 0,
                mapHeight = mapTexture?.Height ?? 0,
                player = new
                {
                    x = World.Instance.Player?.X ?? 0,
                    y = World.Instance.Player?.Y ?? 0,
                    name = World.Instance.Player?.Name ?? ""
                },
                party = GetPartyData(),
                guild = GetGuildData(),
                markers = GetMarkersData(),
                mobiles = GetMobilesData()
            };

            string json = JsonSerializer.Serialize(data);
            byte[] buffer = Encoding.UTF8.GetBytes(json);

            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        private void ServeMapTexture(HttpListenerResponse response)
        {
            try
            {
                byte[] imageData = null;
                int currentMapIndex = World.Instance?.MapIndex ?? 0;

                lock (_cacheLock)
                {
                    // Check if cached map is for the current map index
                    if (_lastMapIndex != currentMapIndex)
                    {
                        Log.Warn($"Cached map index ({_lastMapIndex}) doesn't match current map index ({currentMapIndex}). Clearing cache.");
                        _cachedMapPng = null;
                    }

                    imageData = _cachedMapPng;
                }

                if (imageData == null)
                {
                    Log.Warn("Map texture not cached");
                    response.StatusCode = 404;
                    byte[] errorMsg = Encoding.UTF8.GetBytes("Map texture not loaded. Please close and reopen the web map.");
                    response.ContentType = "text/plain";
                    response.ContentLength64 = errorMsg.Length;
                    response.OutputStream.Write(errorMsg, 0, errorMsg.Length);
                    response.Close();
                    return;
                }

                response.ContentType = "image/png";
                response.ContentLength64 = imageData.Length;
                response.OutputStream.Write(imageData, 0, imageData.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"Error serving map texture: {ex.Message}");
                try
                {
                    response.StatusCode = 500;
                    response.Close();
                }
                catch { }
            }
        }

        public void ClearCache()
        {
            lock (_cacheLock)
            {
                _cachedMapPng = null;
                _lastMapIndex = -1;
            }
        }

        // Serves a marker icon by name. Rather than streaming a rendered GPU texture, we look up the
        // original icon file's path on disk and send the file bytes directly. The browser references
        // the icon via a stable URL (/api/markericon?name=...) which it can cache between requests.
        private void ServeMarkerIcon(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                string name = request.QueryString["name"];

                if (string.IsNullOrEmpty(name))
                {
                    response.StatusCode = 400;
                    response.Close();
                    return;
                }

                string iconPath = null;
                UI.Gumps.WorldMapGump._markerIconPaths.TryGetValue(name.ToLower(), out iconPath);

                if (string.IsNullOrEmpty(iconPath) || !File.Exists(iconPath))
                {
                    response.StatusCode = 404;
                    response.Close();
                    return;
                }

                byte[] iconData = File.ReadAllBytes(iconPath);

                response.ContentType = GetIconContentType(iconPath);
                response.Headers.Add("Cache-Control", "public, max-age=86400");
                response.ContentLength64 = iconData.Length;
                response.OutputStream.Write(iconData, 0, iconData.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"Error serving marker icon: {ex.Message}");
                try
                {
                    response.StatusCode = 500;
                    response.Close();
                }
                catch { }
            }
        }

        private void ServeMarkerManagerData(HttpListenerResponse response)
        {
            try
            {
                if (World.Instance == null || !World.Instance.InGame)
                {
                    WriteJson(response, new { error = "Not in game" }, 503);
                    return;
                }

                object data = MainThreadQueue.BubblingInvokeOnMainThread(BuildMarkerManagerData);
                WriteJson(response, data);
            }
            catch (Exception ex)
            {
                Log.Error($"Error serving marker manager data: {ex.Message}");
                WriteJson(response, new { error = "Failed to load marker manager data" }, 500);
            }
        }

        private void HandleMarkerManagerRequest(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (World.Instance == null || !World.Instance.InGame)
                {
                    WriteJson(response, new { error = "Not in game" }, 503);
                    return;
                }

                string body;
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8))
                {
                    body = reader.ReadToEnd();
                }

                MarkerManagerRequest markerRequest = JsonSerializer.Deserialize<MarkerManagerRequest>(body, JsonReadOptions);
                if (markerRequest == null)
                {
                    WriteJson(response, new { error = "Missing marker request" }, 400);
                    return;
                }

                object data = MainThreadQueue.BubblingInvokeOnMainThread(() => ApplyMarkerManagerRequest(markerRequest));
                WriteJson(response, data);
            }
            catch (ArgumentException ex)
            {
                WriteJson(response, new { error = ex.Message }, 400);
            }
            catch (InvalidOperationException ex)
            {
                WriteJson(response, new { error = ex.Message }, 409);
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling marker manager request: {ex.Message}");
                WriteJson(response, new { error = "Failed to update marker" }, 500);
            }
        }

        private object ApplyMarkerManagerRequest(MarkerManagerRequest request)
        {
            string action = request.Action?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(action))
                throw new ArgumentException("Missing marker action");

            WorldMapGump.WMapMarkerFile markerFile;

            switch (action)
            {
                case "add":
                    markerFile = EnsureUserMarkerFile();
                    markerFile.Markers.Add(CreateMarkerFromRequest(request));
                    SaveUserMarkers(markerFile);
                    return BuildMarkerManagerData();

                case "update":
                    markerFile = GetEditableMarkerFile(request.FileIndex);
                    if (request.MarkerIndex < 0 || request.MarkerIndex >= markerFile.Markers.Count)
                        throw new ArgumentException("Invalid marker index");

                    markerFile.Markers[request.MarkerIndex] = CreateMarkerFromRequest(request, markerFile.Markers[request.MarkerIndex]);
                    SaveUserMarkers(markerFile);
                    return BuildMarkerManagerData();

                case "delete":
                    markerFile = GetEditableMarkerFile(request.FileIndex);
                    if (request.MarkerIndex < 0 || request.MarkerIndex >= markerFile.Markers.Count)
                        throw new ArgumentException("Invalid marker index");

                    markerFile.Markers.RemoveAt(request.MarkerIndex);
                    SaveUserMarkers(markerFile);
                    return BuildMarkerManagerData();

                default:
                    throw new ArgumentException("Unknown marker action");
            }
        }

        private object BuildMarkerManagerData()
        {
            WorldMapGump.WMapMarkerFile userFile = EnsureUserMarkerFile();
            var files = new List<object>();
            int userFileIndex = -1;

            for (int fileIndex = 0; fileIndex < WorldMapGump._markerFiles.Count; fileIndex++)
            {
                WorldMapGump.WMapMarkerFile markerFile = WorldMapGump._markerFiles[fileIndex];
                bool editable = IsUserMarkerFile(markerFile);

                if (markerFile == userFile)
                    userFileIndex = fileIndex;

                var markers = new List<object>();
                if (markerFile.Markers != null)
                {
                    for (int markerIndex = 0; markerIndex < markerFile.Markers.Count; markerIndex++)
                    {
                        markers.Add(BuildMarkerManagerEntry(markerFile.Markers[markerIndex], fileIndex, markerIndex, editable));
                    }
                }

                files.Add(new
                {
                    index = fileIndex,
                    name = markerFile.Name,
                    hidden = markerFile.Hidden,
                    editable,
                    markers
                });
            }

            var iconNames = WorldMapGump._markerIcons.Keys
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            iconNames.Insert(0, "");

            return new
            {
                currentMap = World.Instance?.MapIndex ?? 0,
                player = new
                {
                    x = World.Instance?.Player?.X ?? 0,
                    y = World.Instance?.Player?.Y ?? 0
                },
                userFileIndex,
                colors = MarkerColors,
                icons = iconNames,
                files
            };
        }

        private static object BuildMarkerManagerEntry(WorldMapGump.WMapMarker marker, int fileIndex, int markerIndex, bool editable)
        {
            Color color = marker.Color == Color.Transparent ? Color.White : marker.Color;

            return new
            {
                fileIndex,
                markerIndex,
                editable,
                x = marker.X,
                y = marker.Y,
                map = marker.MapId,
                name = marker.Name,
                colorName = marker.ColorName,
                color = new
                {
                    r = color.R,
                    g = color.G,
                    b = color.B,
                    a = color.A
                },
                iconName = marker.MarkerIconName,
                zoomIndex = marker.ZoomIndex
            };
        }

        private static WorldMapGump.WMapMarkerFile EnsureUserMarkerFile()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserMarkersFilePath));

            if (!File.Exists(UserMarkersFilePath))
            {
                using (File.Create(UserMarkersFilePath))
                {
                }
            }

            WorldMapGump.WMapMarkerFile userFile = WorldMapGump._markerFiles.FirstOrDefault(IsUserMarkerFile);
            if (userFile != null)
            {
                userFile.IsEditable = true;
                userFile.Markers ??= new List<WorldMapGump.WMapMarker>();
                return userFile;
            }

            userFile = new WorldMapGump.WMapMarkerFile
            {
                Hidden = false,
                Name = WorldMapGump.USER_MARKERS_FILE,
                FullPath = UserMarkersFilePath,
                IsEditable = true,
                Markers = WorldMapGump.LoadUserMarkers()
            };

            WorldMapGump._markerFiles.Insert(0, userFile);
            return userFile;
        }

        private static WorldMapGump.WMapMarkerFile GetEditableMarkerFile(int fileIndex)
        {
            if (fileIndex < 0 || fileIndex >= WorldMapGump._markerFiles.Count)
                throw new ArgumentException("Invalid marker file");

            WorldMapGump.WMapMarkerFile markerFile = WorldMapGump._markerFiles[fileIndex];
            if (!IsUserMarkerFile(markerFile))
                throw new InvalidOperationException("Only user markers can be edited from web map");

            markerFile.Markers ??= new List<WorldMapGump.WMapMarker>();
            return markerFile;
        }

        private static bool IsUserMarkerFile(WorldMapGump.WMapMarkerFile markerFile)
        {
            return markerFile != null
                   && (string.Equals(markerFile.Name, WorldMapGump.USER_MARKERS_FILE, StringComparison.OrdinalIgnoreCase)
                       || string.Equals(markerFile.FullPath, UserMarkersFilePath, StringComparison.OrdinalIgnoreCase));
        }

        private static WorldMapGump.WMapMarker CreateMarkerFromRequest(
            MarkerManagerRequest request,
            WorldMapGump.WMapMarker existingMarker = null
        )
        {
            int map = request.Map >= 0 ? request.Map : existingMarker?.MapId ?? World.Instance?.MapIndex ?? 0;
            int x = request.X;
            int y = request.Y;

            ValidateMarkerLocation(x, y, map);

            string name = CleanMarkerText(request.Name, 25);
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Marker name is required");

            string colorName = NormalizeMarkerColor(request.Color, existingMarker?.ColorName);
            string iconName = NormalizeMarkerIcon(request.Icon, existingMarker?.MarkerIconName);

            var marker = new WorldMapGump.WMapMarker
            {
                X = x,
                Y = y,
                MapId = map,
                Name = name,
                Color = WorldMapGump.GetColor(colorName),
                ColorName = colorName,
                MarkerIconName = iconName,
                ZoomIndex = existingMarker?.ZoomIndex ?? 4
            };

            if (!string.IsNullOrWhiteSpace(iconName) && WorldMapGump._markerIcons.TryGetValue(iconName, out Texture2D markerIconTexture))
            {
                marker.MarkerIcon = markerIconTexture;
            }

            return marker;
        }

        private static void ValidateMarkerLocation(int x, int y, int map)
        {
            if (Client.Game?.UO?.FileManager?.Maps?.MapsDefaultSize == null)
                throw new InvalidOperationException("Map data is not available");

            int mapCount = Client.Game.UO.FileManager.Maps.MapsDefaultSize.GetLength(0);
            if (map < 0 || map >= mapCount)
                throw new ArgumentException("Invalid map index");

            int maxX = Client.Game.UO.FileManager.Maps.MapsDefaultSize[map, 0];
            int maxY = Client.Game.UO.FileManager.Maps.MapsDefaultSize[map, 1];

            if (x < 0 || x > maxX || y < 0 || y > maxY)
                throw new ArgumentException("Marker location is outside map");
        }

        private static string NormalizeMarkerColor(string requestedColor, string fallbackColor)
        {
            string color = string.IsNullOrWhiteSpace(requestedColor) ? fallbackColor : requestedColor;
            color = CleanMarkerText(color, 10).ToLowerInvariant();

            return MarkerColors.Contains(color, StringComparer.OrdinalIgnoreCase) ? color : "yellow";
        }

        private static string NormalizeMarkerIcon(string requestedIcon, string fallbackIcon)
        {
            string icon = requestedIcon == null ? fallbackIcon : requestedIcon;
            icon = CleanMarkerText(icon, 40).ToLowerInvariant();

            return !string.IsNullOrWhiteSpace(icon) && WorldMapGump._markerIcons.ContainsKey(icon) ? icon : string.Empty;
        }

        private static string CleanMarkerText(string value, int maxLength)
        {
            value = (value ?? string.Empty).Trim().Replace(',', ' ');
            return value.Length > maxLength ? value.Substring(0, maxLength) : value;
        }

        private static void SaveUserMarkers(WorldMapGump.WMapMarkerFile markerFile)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(UserMarkersFilePath));

            using (var writer = new StreamWriter(UserMarkersFilePath, false))
            {
                foreach (WorldMapGump.WMapMarker marker in markerFile.Markers)
                {
                    string name = CleanMarkerText(marker.Name, 25);
                    string iconName = NormalizeMarkerIcon(marker.MarkerIconName, string.Empty);
                    string colorName = NormalizeMarkerColor(marker.ColorName, "yellow");
                    writer.WriteLine($"{marker.X},{marker.Y},{marker.MapId},{name},{iconName},{colorName},{marker.ZoomIndex}");
                }
            }

            markerFile.Markers = WorldMapGump.LoadUserMarkers();
        }

        private static void WriteJson(HttpListenerResponse response, object data, int statusCode = 200)
        {
            byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data));
            response.StatusCode = statusCode;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = buffer.Length;
            response.OutputStream.Write(buffer, 0, buffer.Length);
            response.Close();
        }

        private static string GetIconContentType(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png": return "image/png";
                case ".jpg":
                case ".jpeg": return "image/jpeg";
                case ".ico":
                case ".cur": return "image/x-icon";
                default: return "application/octet-stream";
            }
        }

        private void ServeEventStream(HttpListenerResponse response)
        {
            response.ContentType = "text/event-stream";
            response.Headers.Add("Cache-Control", "no-cache");
            response.Headers.Add("Connection", "keep-alive");

            var clientState = new ClientState
            {
                Response = response,
                LastJournalCount = JournalManager.Entries.Count
            };

            lock (_clientsLock)
            {
                _activeClients.Add(clientState);
            }

            try
            {
                // Keep the connection alive for as long as the server is running.
                //
                // We intentionally do NOT break out of this loop when the player is
                // briefly out of the world (e.g. while recalling/changing facets). The
                // map index setter momentarily sets Map = null, so World.InGame flips to
                // false for a frame. Previously that exited the loop and tore down the
                // SSE connection on every facet change, and the browser did not reliably
                // reconnect - leaving the web map frozen with stale live data. The most
                // visible symptom was that markers from the previous facet would never
                // refresh again ("markers gone forever" after recalling back). Instead we
                // simply skip sending updates until the player is back in the world.
                while (_isRunning)
                {
                    if (World.Instance == null || !World.Instance.InGame)
                    {
                        // Emit an SSE comment heartbeat so a client that disconnected
                        // while we are out of the world surfaces as a broken pipe and
                        // gets cleaned up promptly, instead of lingering until the next
                        // successful data write.
                        SendHeartbeat(response, "waiting-for-world");
                        Thread.Sleep(500);
                        continue;
                    }

                    string message;

                    try
                    {
                        var data = new
                        {
                            mapIndex = World.Instance.MapIndex,
                            player = new
                            {
                                x = World.Instance.Player?.X ?? 0,
                                y = World.Instance.Player?.Y ?? 0,
                                name = World.Instance.Player?.Name ?? ""
                            },
                            party = GetPartyData(),
                            guild = GetGuildData(),
                            markers = GetMarkersData(),
                            mobiles = GetMobilesData(),
                            journal = MainThreadQueue.InvokeOnMainThread(() => GetNewJournalEntries(clientState))
                        };

                        message = $"data: {JsonSerializer.Serialize(data)}\n\n";
                    }
                    catch (Exception ex)
                    {
                        // Gathering data can transiently fail while the world is being
                        // rebuilt during a map change (collections such as the party,
                        // guild and mobile lists get cleared/repopulated on another
                        // thread). Skip this update but keep the connection alive so the
                        // client keeps receiving fresh data once the world has settled.
                        Log.Warn($"Map Web Server: failed to build event update: {ex.Message}");
                        SendHeartbeat(response, "skipped-transient-update");
                        Thread.Sleep(500);
                        continue;
                    }

                    byte[] buffer = Encoding.UTF8.GetBytes(message);

                    // A failure writing to the stream means the client really
                    // disconnected; let it propagate to exit the loop and clean up.
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                    response.OutputStream.Flush();

                    Thread.Sleep(500); // Check for updates twice per second
                }
            }
            catch
            {
                // Client disconnected
            }
            finally
            {
                lock (_clientsLock)
                {
                    _activeClients.Remove(clientState);
                }
                try { response.Close(); } catch { }
            }
        }

        // Writes an SSE comment line (": ...") which carries no data event but keeps the
        // stream active. A failed write throws, which lets the caller's loop tear the
        // connection down and remove the stale client.
        private static void SendHeartbeat(HttpListenerResponse response, string note)
        {
            byte[] heartbeat = Encoding.UTF8.GetBytes($": {note}\n\n");
            response.OutputStream.Write(heartbeat, 0, heartbeat.Length);
            response.OutputStream.Flush();
        }

        private object GetPartyData()
        {
            var partyMembers = new List<object>();

            if (World.Instance == null) return partyMembers;

            for (int i = 0; i < 10; i++)
            {
                PartyMember partyMember = World.Instance.Party.Members[i];

                if (partyMember != null && SerialHelper.IsValid(partyMember.Serial))
                {
                    Mobile mob = World.Instance.Mobiles.Get(partyMember.Serial);

                    if (mob != null && mob.Distance <= World.Instance.ClientViewRange)
                    {
                        WMapEntity wme = World.Instance.WMapManager.GetEntity(mob);

                        if(wme == null) continue;

                        if (string.IsNullOrEmpty(wme.Name) && !string.IsNullOrEmpty(partyMember.Name)) wme.Name = partyMember.Name;

                        partyMembers.Add(new
                        {
                            x = wme.X,
                            y = wme.Y,
                            name = wme.Name,
                            isGuild = wme.IsGuild,
                            map = World.Instance.MapIndex
                        });
                    }
                    else
                    {
                        WMapEntity wme = World.Instance.WMapManager.GetEntity(partyMember.Serial);

                        if (wme != null && !wme.IsGuild)
                        {
                            partyMembers.Add(new
                            {
                                x = wme.X,
                                y = wme.Y,
                                name = wme.Name,
                                isGuild = wme.IsGuild,
                                map = World.Instance.MapIndex
                            });
                        }
                    }
                }
            }

            return partyMembers;
        }

        private List<object> GetGuildData()
        {
            var guildMembers = new List<object>();

            if (World.Instance?.WMapManager != null && World.Instance.WMapManager.Entities != null)
            {
                foreach (WMapEntity wme in World.Instance.WMapManager.Entities.Values)
                {
                    if (wme.IsGuild && !World.Instance.Party.Contains(wme.Serial))
                    {
                        guildMembers.Add(new
                        {
                            x = wme.X,
                            y = wme.Y,
                            name = wme.Name ?? "<out of range>",
                            map = wme.Map
                        });
                    }
                }
            }

            return guildMembers;
        }

        private List<object> GetMarkersData()
        {
            var markers = new List<object>();

            if (WorldMapGump._markerFiles != null)
            {
                for (int fileIndex = 0; fileIndex < WorldMapGump._markerFiles.Count; fileIndex++)
                {
                    WorldMapGump.WMapMarkerFile markerFile = WorldMapGump._markerFiles[fileIndex];

                    if (markerFile.Hidden || markerFile.Markers == null)
                        continue;

                    for (int markerIndex = 0; markerIndex < markerFile.Markers.Count; markerIndex++)
                    {
                        WorldMapGump.WMapMarker marker = markerFile.Markers[markerIndex];

                        if (marker.MapId != World.Instance.MapIndex)
                            continue;

                        markers.Add(new
                        {
                            fileIndex,
                            markerIndex,
                            editable = IsUserMarkerFile(markerFile),
                            x = marker.X,
                            y = marker.Y,
                            map = marker.MapId,
                            name = marker.Name,
                            colorName = marker.ColorName,
                            color = marker.Color == Color.Transparent
                                ? new { r = (byte)255, g = (byte)255, b = (byte)255, a = (byte)255 }
                                : new
                                {
                                    r = marker.Color.R,
                                    g = marker.Color.G,
                                    b = marker.Color.B,
                                    a = marker.Color.A
                                },
                            iconName = marker.MarkerIconName,
                            zoomIndex = marker.ZoomIndex
                        });
                    }
                }
            }

            return markers;
        }

        private object GetMobilesData()
        {
            var enemyMobiles = new List<object>();
            var otherMobiles = new List<object>();
            var allyMobiles = new List<object>();

            return MainThreadQueue.InvokeOnMainThread(() => {

                if (World.Instance?.Mobiles == null)
                {
                    return new { enemies = enemyMobiles, others = otherMobiles, allies = allyMobiles };
                }

                foreach (Mobile mob in World.Instance.Mobiles.Values)
                {
                    // Skip the player
                    if (mob == World.Instance.Player)
                        continue;

                    // Skip hidden mobiles
                    if (mob.IsHidden)
                        continue;

                    // Skip party members (shown separately)
                    if (World.Instance.Party.Contains(mob.Serial))
                        continue;

                    // Skip guild members (shown separately)
                    WMapEntity wme = World.Instance.WMapManager.GetEntity(mob.Serial);
                    if (wme != null && wme.IsGuild)
                        continue;

                    // Classify by notoriety
                    if (mob.NotorietyFlag == NotorietyFlag.Ally)
                    {
                        // Ally mobile (lime green) - only within view range
                        if (mob.Distance <= World.Instance.ClientViewRange)
                        {
                            allyMobiles.Add(new
                            {
                                serial = mob.Serial,
                                x = mob.X,
                                y = mob.Y,
                                name = mob.Name ?? ""
                            });
                        }
                    }
                    else if (mob.NotorietyFlag == NotorietyFlag.Enemy ||
                             mob.NotorietyFlag == NotorietyFlag.Murderer ||
                             mob.NotorietyFlag == NotorietyFlag.Criminal)
                    {
                        // Enemy/hostile mobile (red)
                        enemyMobiles.Add(new
                        {
                            serial = mob.Serial,
                            x = mob.X,
                            y = mob.Y,
                            name = mob.Name ?? "",
                            notoriety = (byte)mob.NotorietyFlag
                        });
                    }
                    else
                    {
                        // Other mobile (gray) - Unknown, Innocent, Gray, Invulnerable
                        otherMobiles.Add(new
                        {
                            serial = mob.Serial,
                            x = mob.X,
                            y = mob.Y,
                            name = mob.Name ?? "",
                            notoriety = (byte)mob.NotorietyFlag
                        });
                    }
                }

                return new { enemies = enemyMobiles, others = otherMobiles, allies = allyMobiles };
            });
        }

        private List<object> GetNewJournalEntries(ClientState clientState)
        {
            var newEntries = new List<object>();

            int currentCount = JournalManager.Entries.Count;

            if (currentCount > clientState.LastJournalCount)
            {
                int startIndex = clientState.LastJournalCount;
                int entriesToSend = currentCount - clientState.LastJournalCount;

                for (int i = 0; i < entriesToSend && i < 100; i++)
                {
                    int index = startIndex + i;
                    if (index < currentCount)
                    {
                        JournalEntry entry = JournalManager.Entries[index];
                        newEntries.Add(new
                        {
                            text = entry.Text,
                            hue = entry.Hue,
                            name = entry.Name ?? "",
                            time = entry.Time.ToString("HH:mm:ss"),
                            textType = entry.TextType.ToString()
                        });
                    }
                }

                clientState.LastJournalCount = currentCount;
            }

            return newEntries;
        }

        private void HandleCommand(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (request.HttpMethod != "POST")
                {
                    response.StatusCode = 405;
                    response.Close();
                    return;
                }

                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    Dictionary<string, string> commandData = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

                    if (commandData != null && commandData.TryGetValue("command", out string command))
                    {
                        if (!string.IsNullOrWhiteSpace(command))
                        {
                            GameActions.Say(command, 0xFFFF, MessageType.Regular, 3);
                        }

                        response.StatusCode = 200;
                        byte[] buffer = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                        response.ContentType = "application/json";
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        response.StatusCode = 400;
                    }
                }

                response.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling command: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        // Matches raw map coordinates, e.g. "1639, 1532", "123 456" or "1331:745".
        // Mirrors the point regex used by the in-game LocationGoWindow.
        private static readonly Regex PointCoordsRegex = new Regex(@"^(?<X>\d+)\s*[,:\s]\s*(?<Y>\d+)$", RegexOptions.Compiled);

        // Sets the player's Go-To location on the in-game World Map from the web map.
        // Mirrors the "Go to location" context menu option, which calls WorldMapGump.GoToMarker.
        // The input text accepts either raw map coordinates ("X, Y") or sextant coordinates
        // (e.g. "100o25'S, 40o04'E"), decoded the same way as the in-game LocationGoWindow.
        private void HandleGoto(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                if (request.HttpMethod != "POST")
                {
                    response.StatusCode = 405;
                    response.Close();
                    return;
                }

                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    Dictionary<string, string> gotoData = JsonSerializer.Deserialize<Dictionary<string, string>>(body);

                    if (gotoData != null && gotoData.TryGetValue("text", out string text) && !string.IsNullOrWhiteSpace(text))
                    {
                        Point? parsedPoint = MainThreadQueue.InvokeOnMainThread<Point?>(() =>
                        {
                            if (World.Instance == null || !World.Instance.InGame)
                                return null;

                            if (!TryParseLocation(World.Instance.Map, text, out Point point))
                                return null;

                            UI.Gumps.WorldMapGump wmap = UIManager.GetGump<UI.Gumps.WorldMapGump>();
                            wmap?.GoToMarker(point.X, point.Y, true);
                            return point;
                        });

                        if (parsedPoint.HasValue)
                        {
                            response.StatusCode = 200;
                            // Return the decoded coordinates so the web map can center itself on
                            // the goto point and switch to free view, mirroring the in-game map.
                            string json = JsonSerializer.Serialize(new Dictionary<string, int>
                            {
                                ["x"] = parsedPoint.Value.X,
                                ["y"] = parsedPoint.Value.Y
                            });
                            byte[] buffer = Encoding.UTF8.GetBytes(json);
                            response.ContentType = "application/json";
                            response.ContentLength64 = buffer.Length;
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                        else
                        {
                            response.StatusCode = 400;
                            byte[] buffer = Encoding.UTF8.GetBytes("{\"status\":\"invalid\"}");
                            response.ContentType = "application/json";
                            response.ContentLength64 = buffer.Length;
                            response.OutputStream.Write(buffer, 0, buffer.Length);
                        }
                    }
                    else
                    {
                        response.StatusCode = 400;
                    }
                }

                response.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"Error handling goto: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        // Decodes goto input into map coordinates. Tries sextant coordinates first, then falls back
        // to raw "X, Y" map coordinates - matching the in-game LocationGoWindow parsing order.
        private static bool TryParseLocation(Map.Map map, string text, out Point point)
        {
            if (map != null && Sextant.Parse(map, text, out point))
                return true;

            point = Sextant.InvalidPoint;

            Match match = PointCoordsRegex.Match(text.Trim());
            if (!match.Success)
                return false;

            point = new Point(int.Parse(match.Groups["X"].Value), int.Parse(match.Groups["Y"].Value));
            return true;
        }

        private void GetJournalSize(HttpListenerResponse response)
        {
            try
            {
                GlobalSettingsSave globalSettings = Configuration.ProfileManager.GlobalSettings;

                int width = globalSettings?.WebMapJournalWidth ?? 400;
                int height = globalSettings?.WebMapJournalHeight ?? 300;

                var data = new
                {
                    width = width,
                    height = height
                };

                string json = JsonSerializer.Serialize(data);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"Error getting journal size: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        private void SetJournalSize(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    Dictionary<string, int> sizeData = JsonSerializer.Deserialize<Dictionary<string, int>>(body);

                    if (sizeData != null && sizeData.TryGetValue("width", out int width) && sizeData.TryGetValue("height", out int height))
                    {
                        GlobalSettingsSave globalSettings = Configuration.ProfileManager.GlobalSettings;
                        if (globalSettings != null)
                        {
                            globalSettings.WebMapJournalWidth = width;
                            globalSettings.WebMapJournalHeight = height;
                        }

                        response.StatusCode = 200;
                        byte[] buffer = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                        response.ContentType = "application/json";
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        response.StatusCode = 400;
                    }
                }

                response.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"Error setting journal size: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        private void GetMinimizeStates(HttpListenerResponse response)
        {
            try
            {
                GlobalSettingsSave globalSettings = Configuration.ProfileManager.GlobalSettings;

                bool journalMinimized = globalSettings?.WebMapJournalMinimized ?? false;
                bool controlsMinimized = globalSettings?.WebMapControlsMinimized ?? false;

                var data = new
                {
                    journalMinimized = journalMinimized,
                    controlsMinimized = controlsMinimized
                };

                string json = JsonSerializer.Serialize(data);
                byte[] buffer = Encoding.UTF8.GetBytes(json);

                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"Error getting minimize states: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        private void SetMinimizeStates(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                using (var reader = new StreamReader(request.InputStream, request.ContentEncoding))
                {
                    string body = reader.ReadToEnd();
                    Dictionary<string, bool> stateData = JsonSerializer.Deserialize<Dictionary<string, bool>>(body);

                    if (stateData != null &&
                        stateData.TryGetValue("journalMinimized", out bool journalMinimized) &&
                        stateData.TryGetValue("controlsMinimized", out bool controlsMinimized))
                    {
                        GlobalSettingsSave globalSettings = Configuration.ProfileManager.GlobalSettings;
                        if (globalSettings != null)
                        {
                            globalSettings.WebMapJournalMinimized = journalMinimized;
                            globalSettings.WebMapControlsMinimized = controlsMinimized;
                        }

                        response.StatusCode = 200;
                        byte[] buffer = Encoding.UTF8.GetBytes("{\"status\":\"ok\"}");
                        response.ContentType = "application/json";
                        response.ContentLength64 = buffer.Length;
                        response.OutputStream.Write(buffer, 0, buffer.Length);
                    }
                    else
                    {
                        response.StatusCode = 400;
                    }
                }

                response.Close();
            }
            catch (Exception ex)
            {
                Log.Error($"Error setting minimize states: {ex.Message}");
                response.StatusCode = 500;
                response.Close();
            }
        }

        private string GetHtmlPage() => @"<!DOCTYPE html>
<html>
<head>
    <title>TazUO World Map</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            font-family: Arial, sans-serif;
            background: #1a1a1a;
            color: #fff;
            overflow: hidden;
        }
#controls {
position: fixed;
top: 10px;
left: 10px;
width: 320px;
max-height: calc(100vh - 20px);
overflow-y: auto;
background: rgba(0,0,0,0.8);
padding: 15px;
border-radius: 8px;
z-index: 1000;
            box-shadow: 0 4px 6px rgba(0,0,0,0.5);
        }
        #controls.minimized {
            padding: 10px 15px;
        }
        #controls.minimized .control-content {
            display: none;
        }
        #controls h2 {
            margin-bottom: 10px;
            font-size: 16px;
            color: #4CAF50;
            display: flex;
            justify-content: space-between;
            align-items: center;
            cursor: pointer;
            user-select: none;
        }
        #controls.minimized h2 {
            margin-bottom: 0;
        }
        #controlsMinimizeBtn {
            background: none;
            border: none;
            color: #4CAF50;
            font-size: 16px;
            cursor: pointer;
            padding: 0 5px;
            line-height: 1;
            margin-left: 10px;
        }
        #controlsMinimizeBtn:hover {
            color: #45a049;
        }
        #controls label {
            display: block;
            margin: 8px 0;
            cursor: pointer;
        }
        #controls input[type=""checkbox""] {
            margin-right: 8px;
        }
        #controls .marker-search {
            display: block;
            width: 100%;
            margin: 4px 0 8px 0;
            padding: 6px 8px;
            background: rgba(0,0,0,0.5);
            border: 1px solid #555;
            border-radius: 4px;
            color: #fff;
            font-size: 12px;
            outline: none;
        }
        #controls .marker-search:focus {
            border-color: #4CAF50;
        }
        #controls .goto-row {
            display: flex;
            align-items: center;
            gap: 5px;
            margin: 5px 0;
        }
        #controls .goto-input {
            flex: 1;
            min-width: 0;
            padding: 6px 8px;
            background: rgba(0,0,0,0.5);
            border: 1px solid #555;
            border-radius: 4px;
            color: #fff;
            font-size: 12px;
            outline: none;
        }
        #controls .goto-input:focus {
            border-color: #4CAF50;
        }
        #controls .goto-row button {
            margin: 0;
        }
#markerManager {
margin-top: 10px;
padding-top: 10px;
border-top: 1px solid #333;
font-size: 12px;
}
.marker-manager-title {
display: flex;
justify-content: space-between;
align-items: center;
margin-bottom: 6px;
color: #4CAF50;
font-weight: bold;
}
#markerManager select,
#markerManager input {
width: 100%;
padding: 6px 8px;
margin: 3px 0;
background: rgba(0,0,0,0.5);
border: 1px solid #555;
border-radius: 4px;
color: #fff;
font-size: 12px;
outline: none;
}
#markerManager select:focus,
#markerManager input:focus {
border-color: #4CAF50;
}
.marker-action-row {
display: grid;
grid-template-columns: 1fr 1fr;
gap: 6px;
margin: 6px 0;
}
#markerList {
max-height: 155px;
overflow-y: auto;
border: 1px solid #333;
background: rgba(0,0,0,0.25);
margin-top: 6px;
}
.marker-row {
display: grid;
grid-template-columns: 1fr auto auto;
gap: 8px;
align-items: center;
padding: 6px 8px;
border-bottom: 1px solid #2a2a2a;
cursor: pointer;
}
.marker-row:last-child {
border-bottom: none;
}
.marker-row:hover,
.marker-row.selected {
background: rgba(76,175,80,0.2);
}
.marker-row-name {
overflow: hidden;
text-overflow: ellipsis;
white-space: nowrap;
}
.marker-row-pos {
color: #c8c8c8;
font-size: 11px;
white-space: nowrap;
}
.marker-row-delete {
background: #8f2d2d;
color: #fff;
border: none;
border-radius: 4px;
padding: 4px 6px;
font-size: 11px;
cursor: pointer;
}
.marker-row-delete:hover {
background: #b33a3a;
}
.marker-row-delete:disabled {
background: #3a3a3a;
color: #888;
cursor: not-allowed;
}
.marker-editor-grid {
display: grid;
grid-template-columns: 1fr 1fr;
gap: 6px;
margin-top: 6px;
}
.marker-editor-wide {
grid-column: 1 / -1;
}
.marker-status {
min-height: 16px;
margin-top: 5px;
color: #c8c8c8;
}
#controls #markerManager button {
width: 100%;
margin: 0;
padding: 6px 8px;
font-size: 12px;
}
#controls #markerManager #markerReloadBtn {
width: auto;
padding: 4px 8px;
}
#controls button {
margin: 5px 5px 5px 0;
padding: 8px 15px;
            background: #4CAF50;
            color: white;
            border: none;
            border-radius: 4px;
            cursor: pointer;
            font-size: 14px;
        }
        #controls button:hover {
            background: #45a049;
        }
        #info {
            position: fixed;
            bottom: 10px;
            right: 10px;
            background: rgba(0,0,0,0.8);
            padding: 10px 15px;
            border-radius: 8px;
            font-size: 12px;
            z-index: 1000;
        }
        #journal {
            position: fixed;
            bottom: 10px;
            left: 10px;
            width: 400px;
            height: 300px;
            min-width: 250px;
            min-height: 150px;
            max-width: 800px;
            max-height: 80vh;
            background: rgba(0,0,0,0.9);
            border-radius: 8px;
            z-index: 1000;
            display: flex;
            flex-direction: column;
            box-shadow: 0 4px 6px rgba(0,0,0,0.5);
        }
        #journal.minimized {
            height: 40px;
        }
        #journal.minimized #journalContent,
        #journal.minimized #journalInputContainer {
            display: none;
        }
        #journalHeader {
            padding: 10px 15px;
            background: rgba(50,50,50,0.9);
            border-radius: 8px 8px 0 0;
            font-size: 14px;
            font-weight: bold;
            color: #4CAF50;
            border-bottom: 1px solid #333;
            display: flex;
            justify-content: space-between;
            align-items: center;
            cursor: pointer;
            user-select: none;
        }
        #journal.minimized #journalHeader {
            border-bottom: none;
            border-radius: 8px;
        }
        #journalMinimizeBtn {
            background: none;
            border: none;
            color: #4CAF50;
            font-size: 16px;
            cursor: pointer;
            padding: 0 5px;
            line-height: 1;
        }
        #journalMinimizeBtn:hover {
            color: #45a049;
        }
        #journalResizeHandle {
            position: absolute;
            top: 0;
            right: 0;
            width: 15px;
            height: 15px;
            cursor: nwse-resize;
            background: linear-gradient(135deg, transparent 0%, transparent 50%, #4CAF50 50%, #4CAF50 100%);
            border-radius: 0 8px 0 0;
            opacity: 0.5;
            z-index: 10;
        }
        #journalResizeHandle:hover {
            opacity: 1;
        }
        #journal.minimized #journalResizeHandle {
            display: none;
        }
        #journalContent {
            flex: 1;
            overflow-y: auto;
            padding: 10px;
            font-size: 12px;
            font-family: 'Courier New', monospace;
        }
        #journalContent::-webkit-scrollbar {
            width: 8px;
        }
        #journalContent::-webkit-scrollbar-track {
            background: rgba(0,0,0,0.3);
        }
        #journalContent::-webkit-scrollbar-thumb {
            background: rgba(255,255,255,0.3);
            border-radius: 4px;
        }
        #journalContent::-webkit-scrollbar-thumb:hover {
            background: rgba(255,255,255,0.5);
        }
        .journal-entry {
            margin-bottom: 4px;
            line-height: 1.4;
        }
        #journalInputContainer {
            padding: 10px;
            background: rgba(30,30,30,0.9);
            border-radius: 0 0 8px 8px;
            border-top: 1px solid #333;
        }
        #journalInput {
            width: 100%;
            padding: 8px;
            background: rgba(0,0,0,0.5);
            border: 1px solid #555;
            border-radius: 4px;
            color: #fff;
            font-size: 12px;
            outline: none;
        }
        #journalInput:focus {
            border-color: #4CAF50;
        }
        #mapCanvas {
            display: block;
            cursor: grab;
            image-rendering: pixelated;
        }
        #mapCanvas:active {
            cursor: grabbing;
        }
        #status {
            position: fixed;
            top: 10px;
            right: 10px;
            background: rgba(0,0,0,0.8);
            padding: 10px 15px;
            border-radius: 8px;
            font-size: 12px;
        }
        .status-indicator {
            display: inline-block;
            width: 10px;
            height: 10px;
            border-radius: 50%;
            margin-right: 5px;
        }
        .status-connected { background: #4CAF50; }
        .status-disconnected { background: #f44336; }
    </style>
</head>
<body>
    <div id=""controls"">
        <h2 id=""mapTitle"">
            <span id=""mapTitleText"">TazUO Web Map</span>
            <button id=""controlsMinimizeBtn"" title=""Minimize/Maximize"">−</button>
        </h2>
        <div class=""control-content"">
            <button onclick=""zoomIn()"">Zoom In (+)</button>
            <button onclick=""zoomOut()"">Zoom Out (-)</button>
            <button onclick=""centerOnPlayer()"">Center</button>
            <br>
            <div class=""goto-row"">
                <input type=""text"" id=""gotoInput"" class=""goto-input"" placeholder=""X, Y or sextant"" title=""e.g. 1639, 1532 or 100o25'S, 40o04'E"" autocomplete=""off"" />
                <button onclick=""sendGoto()"">Go</button>
            </div>
            <label><input type=""checkbox"" id=""followPlayer"" checked> Follow Player</label>
            <label><input type=""checkbox"" id=""rotateMap"" checked> Rotate Map 45°</label>
            <label><input type=""checkbox"" id=""showParty"" checked> Show Party</label>
            <label><input type=""checkbox"" id=""showGuild"" checked> Show Guild</label>
<label><input type=""checkbox"" id=""showMarkers"" checked> Show Markers</label>
<label style=""margin-left: 20px;""><input type=""checkbox"" id=""showMarkerIcons"" checked> Icons</label>
<input type=""text"" id=""markerSearch"" class=""marker-search"" placeholder=""Search markers..."" autocomplete=""off"" />
<div id=""markerManager"">
<div class=""marker-manager-title""><span>Marker Manager</span><button id=""markerReloadBtn"" type=""button"">Reload</button></div>
<select id=""markerFileSelect""></select>
<div class=""marker-action-row"">
<button id=""markerAddPlayerBtn"" type=""button"">Add Player</button>
<button id=""markerAddMouseBtn"" type=""button"">Add Mouse</button>
</div>
<div id=""markerList""></div>
<div class=""marker-editor-grid"">
<input id=""markerNameInput"" class=""marker-editor-wide"" type=""text"" maxlength=""25"" placeholder=""Name"" autocomplete=""off"" />
<input id=""markerXInput"" type=""number"" min=""0"" placeholder=""X"" />
<input id=""markerYInput"" type=""number"" min=""0"" placeholder=""Y"" />
<input id=""markerMapInput"" type=""number"" min=""0"" placeholder=""Map"" />
<select id=""markerColorSelect""></select>
<select id=""markerIconSelect"" class=""marker-editor-wide""></select>
</div>
<div class=""marker-action-row"">
<button id=""markerSaveBtn"" type=""button"">Save</button>
<button id=""markerDeleteBtn"" type=""button"">Delete</button>
</div>
<div class=""marker-action-row"">
<button id=""markerGotoBtn"" type=""button"">Goto</button>
<button id=""markerNewBtn"" type=""button"">New</button>
</div>
<div id=""markerStatus"" class=""marker-status""></div>
</div>
<label><input type=""checkbox"" id=""showMobiles"" checked> Show Mobiles</label>
            <label style=""margin-left: 20px;""><input type=""checkbox"" id=""showEnemies"" checked> Enemies</label>
            <label style=""margin-left: 20px;""><input type=""checkbox"" id=""showOthers"" checked> Other</label>
            <label style=""margin-left: 20px;""><input type=""checkbox"" id=""showAllies"" checked> Allies</label>
            <label><input type=""checkbox"" id=""showNames"" checked> Show Names</label>
            <label><input type=""checkbox"" id=""showGrid"" checked> Show Grid</label>
        </div>
    </div>
    <div id=""status"">
        <span class=""status-indicator status-disconnected"" id=""statusIndicator""></span>
        <span id=""statusText"">Connecting...</span>
    </div>
    <div id=""info"">
        <div>Zoom: <span id=""zoomLevel"">1.0x</span></div>
        <div>Player: <span id=""playerPos"">0, 0</span></div>
        <div>Mouse: <span id=""mousePos"">-</span></div>
    </div>
    <div id=""journal"">
        <div id=""journalResizeHandle"" title=""Drag to resize""></div>
        <div id=""journalHeader"">
            <span>Journal</span>
            <button id=""journalMinimizeBtn"" title=""Minimize/Maximize"">−</button>
        </div>
        <div id=""journalContent""></div>
        <div id=""journalInputContainer"">
            <input type=""text"" id=""journalInput"" placeholder=""Send a message..."" />
        </div>
    </div>
    <canvas id=""mapCanvas""></canvas>

    <script>
        const canvas = document.getElementById('mapCanvas');
        const ctx = canvas.getContext('2d');
        const journalContent = document.getElementById('journalContent');
        const journalInput = document.getElementById('journalInput');
        const journalBox = document.getElementById('journal');
        const journalMinimizeBtn = document.getElementById('journalMinimizeBtn');
const journalResizeHandle = document.getElementById('journalResizeHandle');
const controlsBox = document.getElementById('controls');
const controlsMinimizeBtn = document.getElementById('controlsMinimizeBtn');
const markerFileSelect = document.getElementById('markerFileSelect');
const markerList = document.getElementById('markerList');
const markerNameInput = document.getElementById('markerNameInput');
const markerXInput = document.getElementById('markerXInput');
const markerYInput = document.getElementById('markerYInput');
const markerMapInput = document.getElementById('markerMapInput');
const markerColorSelect = document.getElementById('markerColorSelect');
const markerIconSelect = document.getElementById('markerIconSelect');
const markerSaveBtn = document.getElementById('markerSaveBtn');
const markerDeleteBtn = document.getElementById('markerDeleteBtn');
const markerStatus = document.getElementById('markerStatus');

let mapImage = null;
let mapData = null;
        let zoom = 1.0;
        let targetZoom = 1.0;
        let offsetX = 0;
        let offsetY = 0;
        let targetOffsetX = 0;
        let targetOffsetY = 0;
        let isDragging = false;
        let lastMouseX = 0;
        let lastMouseY = 0;
        let eventSource = null;
        let animationFrameId = null;
        let mouseZoomPoint = null; // Track the world position to keep under cursor during zoom
        let autoScrollJournal = true;
let journalMinimized = false;
let controlsMinimized = false;
let isResizingJournal = false;
let markerSearchText = '';
let markerManagerData = null;
let selectedMarkerRef = null;
let selectedMarkerFileIndex = -1;
let lastMouseWorld = null;
let resizeStartX = 0;
        let resizeStartY = 0;
        let resizeStartWidth = 0;
        let resizeStartHeight = 0;

        const zoomLevels = [0.125, 0.25, 0.5, 0.75, 1, 1.5, 2, 4, 6, 8];
        let zoomIndex = 4;
        const ZOOM_SPEED = 0.15;
        const POSITION_SPEED = 0.2; // Speed for player position tweening

        canvas.width = window.innerWidth;
        canvas.height = window.innerHeight;

        window.addEventListener('resize', () => {
            canvas.width = window.innerWidth;
            canvas.height = window.innerHeight;
            draw();
        });

        function animate() {
            let needsRedraw = false;

            // Smooth zoom interpolation
            if (Math.abs(zoom - targetZoom) > 0.001) {
                const oldZoom = zoom;
                zoom += (targetZoom - zoom) * ZOOM_SPEED;

                // If we're zooming to a mouse point, recalculate offset to keep that point stable
                if (mouseZoomPoint) {
                    offsetX = mouseZoomPoint.canvasX - canvas.width / 2 - mouseZoomPoint.worldX * zoom;
                    offsetY = mouseZoomPoint.canvasY - canvas.height / 2 - mouseZoomPoint.worldY * zoom;
                    targetOffsetX = offsetX;
                    targetOffsetY = offsetY;
                } else {
                    // For button zoom, maintain the center point
                    const zoomRatio = zoom / oldZoom;
                    offsetX *= zoomRatio;
                    offsetY *= zoomRatio;
                    targetOffsetX *= zoomRatio;
                    targetOffsetY *= zoomRatio;
                }

                needsRedraw = true;
            } else if (zoom !== targetZoom) {
                zoom = targetZoom;
                mouseZoomPoint = null; // Clear mouse zoom point when animation completes
                needsRedraw = true;
            }

            // Smooth position interpolation (when following player)
            if (Math.abs(offsetX - targetOffsetX) > 0.1 || Math.abs(offsetY - targetOffsetY) > 0.1) {
                offsetX += (targetOffsetX - offsetX) * POSITION_SPEED;
                offsetY += (targetOffsetY - offsetY) * POSITION_SPEED;
                needsRedraw = true;
            } else if (offsetX !== targetOffsetX || offsetY !== targetOffsetY) {
                offsetX = targetOffsetX;
                offsetY = targetOffsetY;
                needsRedraw = true;
            }

            if (needsRedraw) {
                draw();
            }

            animationFrameId = requestAnimationFrame(animate);
        }

        animate();

        async function loadMapTexture(retryCount = 0, centerAfterLoad = false) {
            try {
                updateStatus(false, 'Loading map...');
                const response = await fetch('/api/maptexture');

                if (!response.ok) {
                    console.log(`Map texture not ready, retrying... (attempt ${retryCount + 1})`);
                    updateStatus(false, `Loading map... (attempt ${retryCount + 1})`);
                    setTimeout(() => loadMapTexture(retryCount + 1, centerAfterLoad), 1000);
                    return;
                }

                const blob = await response.blob();
                const img = new Image();
                img.onload = () => {
                    mapImage = img;
                    updateStatus(true, 'Connected');
                    console.log('Map texture loaded successfully');

                    if (centerAfterLoad) {
                        console.log('Centering on player after map change');
                        document.getElementById('followPlayer').checked = true;
                        centerOnPlayer();
                    } else {
                        draw();
                    }
                };
                img.onerror = (err) => {
                    console.error('Image load error:', err);
                    updateStatus(false, 'Image failed to load');
                };
                img.src = URL.createObjectURL(blob);
            } catch (err) {
                console.error('Failed to load map texture:', err);
                updateStatus(false, 'Failed to load map');
            }
        }

        async function loadMapData() {
            try {
                const response = await fetch('/api/mapdata');
                if (!response.ok) throw new Error('Not in game');

                mapData = await response.json();
                console.log(`[INITIAL LOAD] Map index: ${mapData.mapIndex}, Player: ${mapData.player.name}`);
                updateStatus(true);
                updateTitle();

                if (document.getElementById('followPlayer').checked) {
                    centerOnPlayer();
                }

                draw();
            } catch (err) {
                console.error('Failed to load map data:', err);
                updateStatus(false);
            }
        }

        function updateTitle() {
            if (mapData && mapData.player && mapData.player.name) {
                document.getElementById('mapTitleText').textContent = `TazUO Web Map - ${mapData.player.name}`;
            }
        }

        function hueToRgb(hue) {
            // UO hue to RGB conversion - simplified
            // In reality, UO uses a complex hue system, but this provides basic color coding
            if (hue === 0) return 'rgb(200, 200, 200)'; // Gray for system messages
            if (hue < 100) return 'rgb(255, 255, 100)'; // Yellow
            if (hue < 200) return 'rgb(100, 255, 100)'; // Green
            if (hue < 500) return 'rgb(100, 200, 255)'; // Blue
            if (hue < 1000) return 'rgb(255, 150, 100)'; // Orange
            return 'rgb(255, 100, 255)'; // Magenta
        }

        function addJournalEntries(entries) {
            if (!entries || entries.length === 0) return;

            entries.forEach(entry => {
                const div = document.createElement('div');
                div.className = 'journal-entry';
                const color = hueToRgb(entry.hue);

                let text = '';
                if (entry.name) {
                    text = `[${entry.time}] ${entry.name}: ${entry.text}`;
                } else {
                    text = `[${entry.time}] ${entry.text}`;
                }

                div.style.color = color;
                div.textContent = text;
                journalContent.appendChild(div);
            });

            // Auto-scroll to bottom if enabled
            if (autoScrollJournal) {
                journalContent.scrollTop = journalContent.scrollHeight;
            }

            // Limit journal entries in DOM to prevent memory issues
            while (journalContent.children.length > 500) {
                journalContent.removeChild(journalContent.firstChild);
            }
        }

        async function sendCommand(command) {
            try {
                const response = await fetch('/api/command', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ command: command })
                });

                if (!response.ok) {
                    console.error('Failed to send command');
                }
            } catch (err) {
                console.error('Error sending command:', err);
            }
        }

        async function sendGoto() {
            const input = document.getElementById('gotoInput');
            const text = input.value.trim();

            if (!text) {
                return;
            }

            try {
                const response = await fetch('/api/goto', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ text: text })
                });

                // Server decodes both raw (X, Y) and sextant coordinates. A 400 means the
                // input could not be parsed - flag it so the user knows to fix their input.
                if (response.ok) {
                    input.style.borderColor = '';

                    // Mirror the in-game map: drop out of follow-player (free view) and
                    // center the web map on the decoded goto point returned by the server.
                    const data = await response.json();
                    if (data && typeof data.x === 'number' && typeof data.y === 'number') {
                        document.getElementById('followPlayer').checked = false;
                        centerOnWorldPoint(data.x, data.y);
                    }
                } else {
                    input.style.borderColor = '#f44336';
                    console.error('Failed to set goto location (invalid coordinates?)');
                }
            } catch (err) {
                console.error('Error sending goto:', err);
            }
        }

        async function loadJournalSize() {
            try {
                const response = await fetch('/api/journalsize');
                if (response.ok) {
                    const data = await response.json();
                    journalBox.style.width = data.width + 'px';
                    journalBox.style.height = data.height + 'px';
                    console.log(`Loaded journal size: ${data.width}x${data.height}`);
                }
            } catch (err) {
                console.error('Error loading journal size:', err);
            }
        }

        async function saveJournalSize(width, height) {
            try {
                const response = await fetch('/api/journalsize', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({ width: width, height: height })
                });

                if (response.ok) {
                    console.log(`Saved journal size: ${width}x${height}`);
                }
            } catch (err) {
                console.error('Error saving journal size:', err);
            }
        }

        async function loadMinimizeStates() {
            try {
                const response = await fetch('/api/minimizestates');
                if (response.ok) {
                    const data = await response.json();
                    journalMinimized = data.journalMinimized || false;
                    controlsMinimized = data.controlsMinimized || false;

                    // Apply loaded states
                    if (journalMinimized) {
                        // Save the current height (from loadJournalSize) before minimizing
                        savedJournalHeight = journalBox.offsetHeight;
                        journalBox.classList.add('minimized');
                        journalBox.style.height = '40px';
                        journalBox.style.minHeight = '40px';
                        journalMinimizeBtn.textContent = '+';
                    }
                    if (controlsMinimized) {
                        controlsBox.classList.add('minimized');
                        controlsMinimizeBtn.textContent = '+';
                    }

                    console.log(`Loaded minimize states: journal=${journalMinimized}, controls=${controlsMinimized}`);
                }
            } catch (err) {
                console.error('Error loading minimize states:', err);
            }
        }

        async function saveMinimizeStates() {
            try {
                const response = await fetch('/api/minimizestates', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    },
                    body: JSON.stringify({
                        journalMinimized: journalMinimized,
                        controlsMinimized: controlsMinimized
                    })
                });

                if (response.ok) {
                    console.log(`Saved minimize states: journal=${journalMinimized}, controls=${controlsMinimized}`);
                }
            } catch (err) {
                console.error('Error saving minimize states:', err);
            }
        }

        // Handle journal minimize/maximize
        let savedJournalHeight = null;

        function toggleJournalMinimize() {
            journalMinimized = !journalMinimized;
            if (journalMinimized) {
                // Save current height before minimizing
                savedJournalHeight = journalBox.offsetHeight;
                journalBox.classList.add('minimized');
                journalBox.style.height = '40px';
                journalBox.style.minHeight = '40px';
                journalMinimizeBtn.textContent = '+';
            } else {
                journalBox.classList.remove('minimized');
                // Restore previous height and min-height
                if (savedJournalHeight) {
                    journalBox.style.height = savedJournalHeight + 'px';
                }
                journalBox.style.minHeight = '150px';
                journalMinimizeBtn.textContent = '−';
            }
            saveMinimizeStates();
        }

        function toggleControlsMinimize() {
            controlsMinimized = !controlsMinimized;
            if (controlsMinimized) {
                controlsBox.classList.add('minimized');
                controlsMinimizeBtn.textContent = '+';
            } else {
                controlsBox.classList.remove('minimized');
                controlsMinimizeBtn.textContent = '−';
            }
            saveMinimizeStates();
        }

        journalMinimizeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            toggleJournalMinimize();
        });

        controlsMinimizeBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            toggleControlsMinimize();
        });

        // Handle journal resizing
        journalResizeHandle.addEventListener('mousedown', (e) => {
            e.preventDefault();
            e.stopPropagation();
            isResizingJournal = true;
            resizeStartX = e.clientX;
            resizeStartY = e.clientY;
            resizeStartWidth = journalBox.offsetWidth;
            resizeStartHeight = journalBox.offsetHeight;
        });

        document.addEventListener('mousemove', (e) => {
            if (isResizingJournal) {
                const deltaX = e.clientX - resizeStartX;
                const deltaY = resizeStartY - e.clientY; // Inverted because journal is bottom-aligned

                const newWidth = Math.max(250, Math.min(800, resizeStartWidth + deltaX));
                const newHeight = Math.max(150, Math.min(window.innerHeight * 0.8, resizeStartHeight + deltaY));

                journalBox.style.width = newWidth + 'px';
                journalBox.style.height = newHeight + 'px';
            }
        });

        document.addEventListener('mouseup', () => {
            if (isResizingJournal) {
                isResizingJournal = false;
                // Save the new size to settings
                saveJournalSize(journalBox.offsetWidth, journalBox.offsetHeight);
            }
        });

        // Handle marker search filtering
        const markerSearchInput = document.getElementById('markerSearch');
markerSearchInput.addEventListener('input', () => {
markerSearchText = markerSearchInput.value.trim().toLowerCase();
renderMarkerManager(true);
draw();
});

        // Allow pressing Enter in the goto field to trigger the goto
        document.getElementById('gotoInput').addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                sendGoto();
            }
        });

function setMarkerStatus(message) {
markerStatus.textContent = message || '';
}

function getMarkerFileByIndex(fileIndex) {
if (!markerManagerData || !markerManagerData.files) return null;
return markerManagerData.files.find(file => file.index === fileIndex) || null;
}

function getSelectedMarker() {
if (!selectedMarkerRef) return null;
const file = getMarkerFileByIndex(selectedMarkerRef.fileIndex);
if (!file || !file.markers) return null;
return file.markers.find(marker => marker.markerIndex === selectedMarkerRef.markerIndex) || null;
}

function fillMarkerSelect(select, values, selectedValue, emptyLabel) {
select.innerHTML = '';
(values || []).forEach(value => {
const option = document.createElement('option');
option.value = value;
option.textContent = value || emptyLabel;
select.appendChild(option);
});
select.value = selectedValue || '';
}

async function loadMarkerManager(keepSelection = true) {
try {
const response = await fetch('/api/markermanager');
const data = await response.json();
if (!response.ok) throw new Error(data.error || 'Failed to load markers');
markerManagerData = data;
if (selectedMarkerFileIndex < 0) {
selectedMarkerFileIndex = data.userFileIndex >= 0 ? data.userFileIndex : ((data.files || [])[0]?.index ?? -1);
}
renderMarkerManager(keepSelection);
} catch (err) {
console.error('Failed to load marker manager:', err);
setMarkerStatus('Markers unavailable');
}
}

function renderMarkerManager(keepSelection = true) {
if (!markerManagerData) return;

markerFileSelect.innerHTML = '';
(markerManagerData.files || []).forEach(file => {
const option = document.createElement('option');
option.value = file.index;
option.textContent = file.editable ? `${file.name} *` : file.name;
markerFileSelect.appendChild(option);
});

if (!getMarkerFileByIndex(selectedMarkerFileIndex)) {
selectedMarkerFileIndex = markerManagerData.userFileIndex >= 0 ? markerManagerData.userFileIndex : ((markerManagerData.files || [])[0]?.index ?? -1);
}

markerFileSelect.value = String(selectedMarkerFileIndex);

const selectedMarker = keepSelection ? getSelectedMarker() : null;
fillMarkerSelect(markerColorSelect, markerManagerData.colors || [], selectedMarker?.colorName || 'yellow', 'none');
fillMarkerSelect(markerIconSelect, markerManagerData.icons || [''], selectedMarker?.iconName || '', '(no icon)');

const file = getMarkerFileByIndex(selectedMarkerFileIndex);
markerList.innerHTML = '';

if (!file) {
setMarkerStatus('No marker files');
fillMarkerForm(null);
return;
}

const markers = (file.markers || []).filter(marker => {
if (!markerSearchText) return true;
return marker.name && marker.name.toLowerCase().includes(markerSearchText);
});

markers.forEach(marker => {
const row = document.createElement('div');
row.className = 'marker-row';
if (selectedMarkerRef && selectedMarkerRef.fileIndex === marker.fileIndex && selectedMarkerRef.markerIndex === marker.markerIndex) {
row.classList.add('selected');
}

const name = document.createElement('div');
name.className = 'marker-row-name';
name.textContent = marker.name || '(unnamed)';

const pos = document.createElement('div');
pos.className = 'marker-row-pos';
pos.textContent = `${marker.x}, ${marker.y} m${marker.map}`;

const deleteButton = document.createElement('button');
deleteButton.type = 'button';
deleteButton.className = 'marker-row-delete';
deleteButton.textContent = 'Delete';
deleteButton.disabled = !marker.editable;
deleteButton.title = marker.editable ? 'Delete marker' : 'Read-only marker file';
deleteButton.addEventListener('click', (e) => {
e.preventDefault();
e.stopPropagation();
selectMarker(marker);
deleteMarker(marker);
});

row.appendChild(name);
row.appendChild(pos);
row.appendChild(deleteButton);
row.addEventListener('click', () => selectMarker(marker));
markerList.appendChild(row);
});

if (markers.length === 0) {
const emptyRow = document.createElement('div');
emptyRow.className = 'marker-row';
emptyRow.textContent = 'No markers';
markerList.appendChild(emptyRow);
}

if (!selectedMarker) {
fillMarkerForm(null);
} else {
fillMarkerForm(selectedMarker);
}
}

function fillMarkerForm(marker) {
const currentMap = markerManagerData?.currentMap ?? mapData?.mapIndex ?? 0;
const position = marker || lastMouseWorld || { x: mapData?.player?.x ?? 0, y: mapData?.player?.y ?? 0, map: currentMap };

markerNameInput.value = marker?.name || '';
markerXInput.value = position.x ?? 0;
markerYInput.value = position.y ?? 0;
markerMapInput.value = position.map ?? currentMap;
markerColorSelect.value = marker?.colorName || markerColorSelect.value || 'yellow';
markerIconSelect.value = marker?.iconName || '';

const selected = marker ? getMarkerFileByIndex(marker.fileIndex) : getMarkerFileByIndex(markerManagerData?.userFileIndex ?? -1);
const canEdit = !marker || (selected && selected.editable);
markerDeleteBtn.disabled = !marker || !canEdit;
markerSaveBtn.disabled = !canEdit;
}

function selectMarker(marker) {
selectedMarkerRef = { fileIndex: marker.fileIndex, markerIndex: marker.markerIndex };
selectedMarkerFileIndex = marker.fileIndex;
renderMarkerManager(true);
}

function prepareNewMarker(position) {
selectedMarkerRef = null;
selectedMarkerFileIndex = markerManagerData?.userFileIndex ?? selectedMarkerFileIndex;
renderMarkerManager(false);
const currentMap = markerManagerData?.currentMap ?? mapData?.mapIndex ?? 0;
const target = position || lastMouseWorld || { x: mapData?.player?.x ?? 0, y: mapData?.player?.y ?? 0, map: currentMap };
markerNameInput.value = '';
markerXInput.value = target.x ?? 0;
markerYInput.value = target.y ?? 0;
markerMapInput.value = target.map ?? currentMap;
markerColorSelect.value = 'yellow';
markerIconSelect.value = '';
markerNameInput.focus();
setMarkerStatus('New marker');
}

function markerPayload(action) {
const x = Number.parseInt(markerXInput.value, 10);
const y = Number.parseInt(markerYInput.value, 10);
const map = Number.parseInt(markerMapInput.value, 10);
if (Number.isNaN(x) || Number.isNaN(y) || Number.isNaN(map)) {
throw new Error('Marker coordinates are required');
}

return {
Action: action,
FileIndex: selectedMarkerRef?.fileIndex ?? markerManagerData?.userFileIndex ?? -1,
MarkerIndex: selectedMarkerRef?.markerIndex ?? -1,
Name: markerNameInput.value.trim(),
X: x,
Y: y,
Map: map,
Color: markerColorSelect.value,
Icon: markerIconSelect.value
};
}

async function saveMarker() {
try {
const selectedMarker = getSelectedMarker();
if (selectedMarker && !selectedMarker.editable) {
setMarkerStatus('Read-only marker file');
return;
}

const action = selectedMarker ? 'update' : 'add';
const payload = markerPayload(action);
const response = await fetch('/api/markermanager', {
method: 'POST',
headers: { 'Content-Type': 'application/json' },
body: JSON.stringify(payload)
});
const data = await response.json();
if (!response.ok) throw new Error(data.error || 'Failed to save marker');
markerManagerData = data;

if (action === 'add') {
const userFile = getMarkerFileByIndex(markerManagerData.userFileIndex);
selectedMarkerFileIndex = markerManagerData.userFileIndex;
selectedMarkerRef = userFile && userFile.markers.length > 0
? { fileIndex: userFile.index, markerIndex: userFile.markers[userFile.markers.length - 1].markerIndex }
: null;
}

renderMarkerManager(true);
await loadMapData();
setMarkerStatus('Saved');
} catch (err) {
console.error('Failed to save marker:', err);
setMarkerStatus(err.message || 'Save failed');
}
}

async function deleteMarker(markerToDelete = null) {
try {
const selectedMarker = markerToDelete?.markerIndex !== undefined ? markerToDelete : getSelectedMarker();
if (!selectedMarker) {
setMarkerStatus('Select a marker to delete');
return;
}
if (!selectedMarker.editable) {
setMarkerStatus('Read-only marker file');
return;
}

const response = await fetch('/api/markermanager', {
method: 'POST',
headers: { 'Content-Type': 'application/json' },
body: JSON.stringify({
Action: 'delete',
FileIndex: selectedMarker.fileIndex,
MarkerIndex: selectedMarker.markerIndex
})
});
const data = await response.json();
if (!response.ok) throw new Error(data.error || 'Failed to delete marker');
markerManagerData = data;
selectedMarkerRef = null;
renderMarkerManager(false);
await loadMapData();
setMarkerStatus('Deleted');
} catch (err) {
console.error('Failed to delete marker:', err);
setMarkerStatus(err.message || 'Delete failed');
}
}

function gotoSelectedMarker() {
const selectedMarker = getSelectedMarker();
if (!selectedMarker) return;
document.getElementById('followPlayer').checked = false;
centerOnWorld(selectedMarker.x, selectedMarker.y);
}

function findMarkerNear(world) {
if (!world || !mapData || !mapData.markers) return null;
const threshold = Math.max(5, 12 / zoom);
let bestMarker = null;
let bestDistance = threshold * threshold;

mapData.markers.forEach(marker => {
if (marker.map !== world.map) return;
const dx = marker.x - world.x;
const dy = marker.y - world.y;
const distance = dx * dx + dy * dy;
if (distance <= bestDistance) {
bestDistance = distance;
bestMarker = marker;
}
});

return bestMarker;
}

markerFileSelect.addEventListener('change', () => {
selectedMarkerFileIndex = Number.parseInt(markerFileSelect.value, 10);
selectedMarkerRef = null;
renderMarkerManager(false);
});
document.getElementById('markerReloadBtn').addEventListener('click', () => loadMarkerManager(true));
document.getElementById('markerAddPlayerBtn').addEventListener('click', () => {
prepareNewMarker({ x: mapData?.player?.x ?? 0, y: mapData?.player?.y ?? 0, map: mapData?.mapIndex ?? 0 });
});
document.getElementById('markerAddMouseBtn').addEventListener('click', () => prepareNewMarker(lastMouseWorld));
document.getElementById('markerSaveBtn').addEventListener('click', saveMarker);
document.getElementById('markerDeleteBtn').addEventListener('click', () => deleteMarker());
document.getElementById('markerGotoBtn').addEventListener('click', gotoSelectedMarker);
document.getElementById('markerNewBtn').addEventListener('click', () => prepareNewMarker());

// Handle journal input
journalInput.addEventListener('keypress', (e) => {
            if (e.key === 'Enter') {
                const command = journalInput.value.trim();
                if (command) {
                    sendCommand(command);
                    journalInput.value = '';
                }
            }
        });

        // Detect manual scrolling to disable auto-scroll
        journalContent.addEventListener('scroll', () => {
            const isAtBottom = journalContent.scrollHeight - journalContent.scrollTop <= journalContent.clientHeight + 50;
            autoScrollJournal = isAtBottom;
        });

        function connectEventStream() {
            if (eventSource) {
                eventSource.close();
            }

            eventSource = new EventSource('/api/events');

            eventSource.onmessage = (event) => {
                const data = JSON.parse(event.data);
                if (mapData) {
                    // Check if map changed
                    if (data.mapIndex !== undefined && data.mapIndex !== mapData.mapIndex) {
                        console.log(`[MAP CHANGE DETECTED] Changing from map ${mapData.mapIndex} to map ${data.mapIndex}`);
                        mapData.mapIndex = data.mapIndex;
                        mapData.player = data.player;
                        mapData.party = data.party;
                        mapData.guild = data.guild;
                        mapData.markers = data.markers;
                        mapData.mobiles = data.mobiles;
                        updateTitle();

                        // Clear the map image immediately to show blank screen
                        mapImage = null;
                        draw(); // Redraw to show blank screen

                        loadMapTexture(0, true); // Reload the map texture for the new map and center on player
                        loadMarkerManager(true);
                        return; // loadMapTexture will trigger a redraw when complete
                    }

                    mapData.player = data.player;
                    mapData.party = data.party;
                    mapData.guild = data.guild;
                    mapData.markers = data.markers;
                    mapData.mobiles = data.mobiles;

                    // Handle journal entries
                    if (data.journal && data.journal.length > 0) {
                        addJournalEntries(data.journal);
                    }

                    updateTitle(); // Update title if player name changes

                    if (document.getElementById('followPlayer').checked) {
                        centerOnPlayer();
                    }

                    // Always redraw after applying fresh live data. centerOnPlayer() only
                    // updates the target offset and relies on the animation loop to redraw,
                    // which is skipped when the player is stationary - so without this the
                    // newly received markers/mobiles would not appear until the player moved.
                    draw();
                }
            };

            eventSource.onerror = () => {
                updateStatus(false);
                setTimeout(connectEventStream, 5000);
            };
        }

        function updateStatus(connected, message) {
            const indicator = document.getElementById('statusIndicator');
            const text = document.getElementById('statusText');

            if (connected) {
                indicator.className = 'status-indicator status-connected';
                text.textContent = message || 'Connected';
            } else {
                indicator.className = 'status-indicator status-disconnected';
                text.textContent = message || 'Disconnected';
            }
        }

        // Rotate a point around origin by given angle
        function rotatePoint(x, y, angle) {
            if (angle === 0) return { x: x, y: y };

            const cos = Math.cos(angle);
            const sin = Math.sin(angle);

            return {
                x: cos * x - sin * y,
                y: sin * x + cos * y
            };
        }

        function drawLabel(ctx, text, x, y, color, zoom) {
            // Constant font size on screen regardless of zoom level
            const fontSize = 16 / zoom;
            ctx.font = `${fontSize}px Arial`;
            ctx.textAlign = 'center';
            ctx.textBaseline = 'bottom';

            const metrics = ctx.measureText(text);
            const textWidth = metrics.width;
            const textHeight = fontSize;
            const padding = 2 / zoom;

            const labelX = x;
            const labelY = y - (6 / zoom);

            // Draw background
            ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
            ctx.fillRect(
                labelX - textWidth / 2 - padding,
                labelY - textHeight,
                textWidth + (padding * 2),
                textHeight + padding
            );

            // Draw text
            ctx.fillStyle = color;
            ctx.fillText(text, labelX, labelY);
        }

        // Cache of marker icon images keyed by icon name. Icons are fetched once from the server by
        // their file (via /api/markericon?name=...) and reused; the browser also caches the HTTP
        // response so switching maps/markers doesn't re-download them.
        const markerIconCache = {};

        function getMarkerIcon(name) {
            if (!name) return null;

            const key = name.toLowerCase();
            let entry = markerIconCache[key];

            if (entry === undefined) {
                const img = new Image();
                entry = { img: img, loaded: false, failed: false };
                markerIconCache[key] = entry;

                img.onload = () => { entry.loaded = true; draw(); };
                img.onerror = () => { entry.failed = true; };
                img.src = '/api/markericon?name=' + encodeURIComponent(name);
            }

            return (entry.loaded && !entry.failed) ? entry.img : null;
        }

        function draw() {
            ctx.fillStyle = '#000';
            ctx.fillRect(0, 0, canvas.width, canvas.height);

            if (!mapImage || !mapData) return;

            const centerX = canvas.width / 2;
            const centerY = canvas.height / 2;

            const drawWidth = mapImage.width * zoom;
            const drawHeight = mapImage.height * zoom;

            const isRotated = document.getElementById('rotateMap').checked;
            const rotationAngle = isRotated ? Math.PI / 4 : 0; // 45 degrees in radians

            ctx.save();
            ctx.translate(centerX + offsetX, centerY + offsetY);

            // Apply rotation to the map image only
            if (isRotated) {
                ctx.rotate(rotationAngle);
            }

            ctx.scale(zoom, zoom);
            ctx.translate(-mapImage.width / 2, -mapImage.height / 2);

            ctx.drawImage(mapImage, 0, 0);

            // Draw grid
            if (document.getElementById('showGrid').checked && zoom >= 2) {
                size = 8;
                if (zoom >= 4)
                    size = 4;
                if (zoom >= 6)
                    size = 2;
                if (zoom >= 8)
                    size = 1;
                ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
                ctx.lineWidth = 1 / zoom;
                for (let x = 0; x < mapImage.width; x += size) {
                    ctx.beginPath();
                    ctx.moveTo(x, 0);
                    ctx.lineTo(x, mapImage.height);
                    ctx.stroke();
                }
                for (let y = 0; y < mapImage.height; y += size) {
                    ctx.beginPath();
                    ctx.moveTo(0, y);
                    ctx.lineTo(mapImage.width, y);
                    ctx.stroke();
                }
            }

            // Draw markers
            if (document.getElementById('showMarkers').checked && mapData.markers) {
                mapData.markers.forEach(marker => {
                    // Filter out markers that don't match the search text
                    if (markerSearchText && (!marker.name || !marker.name.toLowerCase().includes(markerSearchText))) {
                        return;
                    }

                    const markerColor = `rgba(${marker.color.r}, ${marker.color.g}, ${marker.color.b}, ${marker.color.a / 255})`;

                    // Save state before drawing marker
                    ctx.save();

                    // Position the marker (this point is already in rotated space)
                    ctx.translate(marker.x, marker.y);

                    // Counter-rotate to keep marker and label upright
                    if (isRotated) {
                        ctx.rotate(-rotationAngle);
                    }

                    // Scale for proper sizing
                    ctx.scale(1 / zoom, 1 / zoom);

                    // Prefer the marker's icon (served from its file on disk) when available;
                    // fall back to a colored circle when there's no icon or it hasn't loaded yet.
                    const markerIcon = document.getElementById('showMarkerIcons').checked
                        ? getMarkerIcon(marker.iconName)
                        : null;

                    if (markerIcon) {
                        ctx.drawImage(markerIcon, -markerIcon.width / 2, -markerIcon.height / 2);
                    } else {
                        // Draw marker circle
                        ctx.fillStyle = markerColor;
                        ctx.strokeStyle = '#ffffff';
                        ctx.lineWidth = 1;
                        ctx.beginPath();
                        ctx.arc(0, 0, 3, 0, Math.PI * 2);
                        ctx.fill();
                        ctx.stroke();
                    }

                    // Draw label
                    if (marker.name) {
                        ctx.font = '16px Arial';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'bottom';
                        const metrics = ctx.measureText(marker.name);
                        const textWidth = metrics.width;
                        const textHeight = 16;
                        const padding = 3;

                        // Draw background
                        ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
                        ctx.fillRect(-textWidth / 2 - padding, -textHeight - 6 - padding, textWidth + padding * 2, textHeight + padding * 2);

                        // Draw text
                        ctx.fillStyle = markerColor;
                        ctx.fillText(marker.name, 0, -6);
                    }

                    ctx.restore();
                });
            }

            // Draw enemy mobiles (red)
            if (document.getElementById('showMobiles').checked &&
                document.getElementById('showEnemies').checked &&
                mapData.mobiles &&
                mapData.mobiles.enemies) {
                const showNames = document.getElementById('showNames').checked;
                mapData.mobiles.enemies.forEach(mobile => {
                    ctx.save();
                    ctx.translate(mobile.x, mobile.y);
                    if (isRotated) ctx.rotate(-rotationAngle);
                    ctx.scale(1 / zoom, 1 / zoom);

                    // Draw red circle
                    ctx.fillStyle = '#ff0000';
                    ctx.strokeStyle = '#ffffff';
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.arc(0, 0, 3, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.stroke();

                    // Draw name if enabled
                    if (showNames && mobile.name) {
                        ctx.font = '14px Arial';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'bottom';
                        const metrics = ctx.measureText(mobile.name);
                        const textWidth = metrics.width;
                        const textHeight = 14;
                        const padding = 3;

                        ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
                        ctx.fillRect(-textWidth / 2 - padding, -textHeight - 6 - padding,
                                    textWidth + padding * 2, textHeight + padding * 2);

                        ctx.fillStyle = '#ff0000';
                        ctx.fillText(mobile.name, 0, -6);
                    }

                    ctx.restore();
                });
            }

            // Draw other mobiles (gray)
            if (document.getElementById('showMobiles').checked &&
                document.getElementById('showOthers').checked &&
                mapData.mobiles &&
                mapData.mobiles.others) {
                const showNames = document.getElementById('showNames').checked;
                mapData.mobiles.others.forEach(mobile => {
                    ctx.save();
                    ctx.translate(mobile.x, mobile.y);
                    if (isRotated) ctx.rotate(-rotationAngle);
                    ctx.scale(1 / zoom, 1 / zoom);

                    // Draw gray circle
                    ctx.fillStyle = '#808080';
                    ctx.strokeStyle = '#ffffff';
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.arc(0, 0, 3, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.stroke();

                    // Draw name if enabled
                    if (showNames && mobile.name) {
                        ctx.font = '14px Arial';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'bottom';
                        const metrics = ctx.measureText(mobile.name);
                        const textWidth = metrics.width;
                        const textHeight = 14;
                        const padding = 3;

                        ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
                        ctx.fillRect(-textWidth / 2 - padding, -textHeight - 6 - padding,
                                    textWidth + padding * 2, textHeight + padding * 2);

                        ctx.fillStyle = '#808080';
                        ctx.fillText(mobile.name, 0, -6);
                    }

                    ctx.restore();
                });
            }

            // Draw ally mobiles (lime green)
            if (document.getElementById('showMobiles').checked &&
                document.getElementById('showAllies').checked &&
                mapData.mobiles &&
                mapData.mobiles.allies) {
                const showNames = document.getElementById('showNames').checked;
                mapData.mobiles.allies.forEach(mobile => {
                    ctx.save();
                    ctx.translate(mobile.x, mobile.y);
                    if (isRotated) ctx.rotate(-rotationAngle);
                    ctx.scale(1 / zoom, 1 / zoom);

                    // Draw lime circle
                    ctx.fillStyle = '#00ff00';
                    ctx.strokeStyle = '#ffffff';
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.arc(0, 0, 3, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.stroke();

                    // Draw name if enabled
                    if (showNames && mobile.name) {
                        ctx.font = '14px Arial';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'bottom';
                        const metrics = ctx.measureText(mobile.name);
                        const textWidth = metrics.width;
                        const textHeight = 14;
                        const padding = 3;

                        ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
                        ctx.fillRect(-textWidth / 2 - padding, -textHeight - 6 - padding,
                                    textWidth + padding * 2, textHeight + padding * 2);

                        ctx.fillStyle = '#00ff00';
                        ctx.fillText(mobile.name, 0, -6);
                    }

                    ctx.restore();
                });
            }

            // Draw guild members
            if (document.getElementById('showGuild').checked && mapData.guild) {
                const showNames = document.getElementById('showNames').checked;
                mapData.guild.forEach(member => {
                    ctx.save();
                    ctx.translate(member.x, member.y);
                    if (isRotated) ctx.rotate(-rotationAngle);
                    ctx.scale(1 / zoom, 1 / zoom);

                    ctx.fillStyle = '#00ff00';
                    ctx.strokeStyle = '#ffffff';
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.arc(0, 0, 4, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.stroke();

                    if (showNames && member.name) {
                        ctx.font = '16px Arial';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'bottom';
                        const metrics = ctx.measureText(member.name);
                        const textWidth = metrics.width;
                        const textHeight = 16;
                        const padding = 3;

                        ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
                        ctx.fillRect(-textWidth / 2 - padding, -textHeight - 6 - padding, textWidth + padding * 2, textHeight + padding * 2);

                        ctx.fillStyle = '#00ff00';
                        ctx.fillText(member.name, 0, -6);
                    }

                    ctx.restore();
                });
            }

            // Draw party members
            if (document.getElementById('showParty').checked && mapData.party) {
                const showNames = document.getElementById('showNames').checked;
                mapData.party.forEach(member => {
                    const color = member.isGuild ? '#00ff00' : '#ffff00';

                    ctx.save();
                    ctx.translate(member.x, member.y);
                    if (isRotated) ctx.rotate(-rotationAngle);
                    ctx.scale(1 / zoom, 1 / zoom);

                    ctx.fillStyle = color;
                    ctx.strokeStyle = '#ffffff';
                    ctx.lineWidth = 1;
                    ctx.beginPath();
                    ctx.arc(0, 0, 4, 0, Math.PI * 2);
                    ctx.fill();
                    ctx.stroke();

                    if (showNames && member.name) {
                        ctx.font = '16px Arial';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'bottom';
                        const metrics = ctx.measureText(member.name);
                        const textWidth = metrics.width;
                        const textHeight = 16;
                        const padding = 3;

                        ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
                        ctx.fillRect(-textWidth / 2 - padding, -textHeight - 6 - padding, textWidth + padding * 2, textHeight + padding * 2);

                        ctx.fillStyle = color;
                        ctx.fillText(member.name, 0, -6);
                    }

                    ctx.restore();
                });
            }

            // Draw player
            if (mapData.player) {
                ctx.save();
                ctx.translate(mapData.player.x, mapData.player.y);
                if (isRotated) ctx.rotate(-rotationAngle);
                ctx.scale(1 / zoom, 1 / zoom);

                ctx.fillStyle = '#ff0000';
                ctx.strokeStyle = '#ffffff';
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.arc(0, 0, 5, 0, Math.PI * 2);
                ctx.fill();
                ctx.stroke();

                const showNames = document.getElementById('showNames').checked;
                if (showNames && mapData.player.name) {
                    ctx.font = '16px Arial';
                    ctx.textAlign = 'center';
                    ctx.textBaseline = 'bottom';
                    const metrics = ctx.measureText(mapData.player.name);
                    const textWidth = metrics.width;
                    const textHeight = 16;
                    const padding = 3;

                    ctx.fillStyle = 'rgba(0, 0, 0, 0.7)';
                    ctx.fillRect(-textWidth / 2 - padding, -textHeight - 6 - padding, textWidth + padding * 2, textHeight + padding * 2);

                    ctx.fillStyle = '#ff0000';
                    ctx.fillText(mapData.player.name, 0, -6);
                }

                ctx.restore();

                document.getElementById('playerPos').textContent =
                    `${mapData.player.x}, ${mapData.player.y}`;
            }

            ctx.restore();

            document.getElementById('zoomLevel').textContent = zoom.toFixed(2) + 'x';
        }

function centerOnWorld(worldX, worldY) {
if (!mapImage) return;

            // Calculate target offset to center player position on screen
            // The map coordinate system has (0,0) at top-left
            // We need to offset so player appears at canvas center

            // Calculate the player's position relative to map center, scaled by zoom
let scaledX = (worldX - mapImage.width / 2) * zoom;
let scaledY = (worldY - mapImage.height / 2) * zoom;

            // If rotated, we need to rotate these coordinates
            const isRotated = document.getElementById('rotateMap').checked;
            if (isRotated) {
                const rotated = rotatePoint(scaledX, scaledY, Math.PI / 4);
                scaledX = rotated.x;
                scaledY = rotated.y;
            }

            // Negate to get the offset (we want to move the map in opposite direction)
            targetOffsetX = -scaledX;
            targetOffsetY = -scaledY;
            // Animation loop will smoothly interpolate to these target values
        }

        // Centers the view on an arbitrary map coordinate (used by the goto feature).
        // Mirrors centerOnPlayer() but for a caller-supplied world point instead of the player.
        function centerOnWorldPoint(worldX, worldY) {
            if (!mapImage) return;

            let scaledX = (worldX - mapImage.width / 2) * zoom;
            let scaledY = (worldY - mapImage.height / 2) * zoom;

            const isRotated = document.getElementById('rotateMap').checked;
            if (isRotated) {
                const rotated = rotatePoint(scaledX, scaledY, Math.PI / 4);
                scaledX = rotated.x;
                scaledY = rotated.y;
            }

            targetOffsetX = -scaledX;
            targetOffsetY = -scaledY;
        }

function centerOnPlayer() {
if (!mapData || !mapData.player || !mapImage) return;
centerOnWorld(mapData.player.x, mapData.player.y);
}

function zoomIn() {
            if (zoomIndex < zoomLevels.length - 1) {
                zoomIndex++;
                targetZoom = zoomLevels[zoomIndex];
                mouseZoomPoint = null; // Button zoom uses center, not mouse position
            }
        }

        function zoomOut() {
            if (zoomIndex > 0) {
                zoomIndex--;
                targetZoom = zoomLevels[zoomIndex];
                mouseZoomPoint = null; // Button zoom uses center, not mouse position
            }
        }

        function zoomToMouse(mouseX, mouseY, zoomDelta) {
            // Apply zoom change
            const newZoomIndex = Math.max(0, Math.min(zoomLevels.length - 1, zoomIndex + zoomDelta));
            if (newZoomIndex === zoomIndex) return;

            zoomIndex = newZoomIndex;
            const newZoom = zoomLevels[zoomIndex];

            // Calculate world position under mouse at current zoom level
            const worldX = (mouseX - canvas.width / 2 - offsetX) / zoom;
            const worldY = (mouseY - canvas.height / 2 - offsetY) / zoom;

            // Store the point to keep stable during zoom animation
            mouseZoomPoint = {
                canvasX: mouseX,
                canvasY: mouseY,
                worldX: worldX,
                worldY: worldY
            };

            targetZoom = newZoom;
            // Let the animate() loop smoothly interpolate to targetZoom
        }

function canvasClientToWorld(clientX, clientY) {
if (!mapImage || !mapData) return null;

const rect = canvas.getBoundingClientRect();
const mouseCanvasX = clientX - rect.left;
const mouseCanvasY = clientY - rect.top;

const centerX = canvas.width / 2;
const centerY = canvas.height / 2;

let screenX = (mouseCanvasX - centerX - offsetX) / zoom;
let screenY = (mouseCanvasY - centerY - offsetY) / zoom;

const isRotated = document.getElementById('rotateMap').checked;
if (isRotated) {
const rotated = rotatePoint(screenX, screenY, -Math.PI / 4);
screenX = rotated.x;
screenY = rotated.y;
}

return {
x: Math.floor(screenX + mapImage.width / 2),
y: Math.floor(screenY + mapImage.height / 2),
map: mapData.mapIndex
};
}

canvas.addEventListener('mousedown', (e) => {
            // Only allow dragging with left mouse button, and not if resizing journal
            if (e.button === 0 && !isResizingJournal) {
                isDragging = true;
                lastMouseX = e.clientX;
                lastMouseY = e.clientY;
            }
        });

        canvas.addEventListener('mousemove', (e) => {
            if (isDragging) {
                const dx = e.clientX - lastMouseX;
                const dy = e.clientY - lastMouseY;

                // Only disable follow player if user has dragged more than 5 pixels
                // This prevents accidental clicks from disabling it
                if (Math.abs(dx) > 5 || Math.abs(dy) > 5) {
                    document.getElementById('followPlayer').checked = false;
                }

                offsetX += dx;
                offsetY += dy;
                targetOffsetX = offsetX;
                targetOffsetY = offsetY;
                lastMouseX = e.clientX;
                lastMouseY = e.clientY;
                draw();
            }

// Update mouse world coordinates
const world = canvasClientToWorld(e.clientX, e.clientY);
if (world) {
lastMouseWorld = world;
document.getElementById('mousePos').textContent = `${world.x}, ${world.y}`;
}
        });

canvas.addEventListener('mouseup', () => {
isDragging = false;
});

canvas.addEventListener('dblclick', (e) => {
const world = canvasClientToWorld(e.clientX, e.clientY);
const marker = findMarkerNear(world);
if (marker) {
selectedMarkerFileIndex = marker.fileIndex;
selectedMarkerRef = { fileIndex: marker.fileIndex, markerIndex: marker.markerIndex };
renderMarkerManager(true);
setMarkerStatus('Selected');
} else if (world) {
prepareNewMarker(world);
}
});

canvas.addEventListener('wheel', (e) => {
e.preventDefault();

            const zoomDelta = e.deltaY < 0 ? 1 : -1;

            // When follow player is enabled, zoom towards center
            // When disabled, zoom towards mouse position
            if (document.getElementById('followPlayer').checked) {
                // Zoom towards center (like button zoom)
                const newZoomIndex = Math.max(0, Math.min(zoomLevels.length - 1, zoomIndex + zoomDelta));
                if (newZoomIndex !== zoomIndex) {
                    zoomIndex = newZoomIndex;
                    targetZoom = zoomLevels[zoomIndex];
                    mouseZoomPoint = null; // Center zoom
                }
            } else {
                // Zoom towards mouse position
                const rect = canvas.getBoundingClientRect();
                const mouseX = e.clientX - rect.left;
                const mouseY = e.clientY - rect.top;
                zoomToMouse(mouseX, mouseY, zoomDelta);
            }
        });

 // Keyboard shortcuts
 window.addEventListener('keydown', (e) => {
 const activeTag = document.activeElement?.tagName?.toLowerCase();
 const activeIsEditable = activeTag === 'input' || activeTag === 'select' || activeTag === 'textarea' || document.activeElement?.isContentEditable;
 if ((e.key === 'Delete' || e.key === 'Backspace') && !activeIsEditable && getSelectedMarker()) {
 e.preventDefault();
 deleteMarker();
 return;
 }

 switch(e.key) {
 case '+':
 case '=':
                    zoomIn();
                    break;
                case '-':
                case '_':
                    zoomOut();
                    break;
                case 'c':
                case 'C':
                    centerOnPlayer();
                    break;
                case 'f':
                case 'F':
                    const followCheckbox = document.getElementById('followPlayer');
                    followCheckbox.checked = !followCheckbox.checked;
                    if (followCheckbox.checked) centerOnPlayer();
                    break;
            }
        });

        // Add event listeners for checkboxes to trigger redraw
        document.getElementById('rotateMap').addEventListener('change', draw);
        document.getElementById('showParty').addEventListener('change', draw);
        document.getElementById('showGuild').addEventListener('change', draw);
        document.getElementById('showMarkers').addEventListener('change', draw);
        document.getElementById('showMobiles').addEventListener('change', draw);
        document.getElementById('showEnemies').addEventListener('change', draw);
        document.getElementById('showOthers').addEventListener('change', draw);
        document.getElementById('showAllies').addEventListener('change', draw);
        document.getElementById('showNames').addEventListener('change', draw);
        document.getElementById('showGrid').addEventListener('change', draw);

        // Initialize
        loadJournalSize();
        loadMinimizeStates();
loadMapTexture();
loadMapData();
loadMarkerManager();
connectEventStream();

// Reload map texture every 30 seconds in case it changes
setInterval(loadMapTexture, 30000);
</script>
</body>
</html>";

        public void Dispose() => Stop();
    }
}
