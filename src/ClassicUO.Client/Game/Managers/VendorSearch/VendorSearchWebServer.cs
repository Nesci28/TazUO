// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClassicUO.Utility.Logging;

namespace ClassicUO.Game.Managers.VendorSearch;

internal sealed class VendorSearchWebServer : IDisposable
{
    private const int MaxRequestBytes = 64 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly VendorSearchWebManager _manager;
    private HttpListener _listener;
    private Thread _listenerThread;
    private volatile bool _isRunning;

    public VendorSearchWebServer(VendorSearchWebManager manager)
    {
        _manager = manager;
    }

    public bool IsRunning => _isRunning;
    public int Port { get; private set; } = VendorSearchWebManager.DefaultPort;

    public bool Start(int port)
    {
        if (_isRunning)
            return true;

        try
        {
            Port = port;
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{Port}/");
            _listener.Start();
            _isRunning = true;
            _listenerThread = new Thread(Listen)
            {
                IsBackground = true,
                Name = "VendorSearchWebServer"
            };
            _listenerThread.Start();
            Log.Info($"Vendor Search web server started on http://localhost:{Port}/");
            return true;
        }
        catch (Exception ex)
        {
            _isRunning = false;
            _listener?.Close();
            _listener = null;
            Log.Error($"Unable to start Vendor Search web server: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        if (!_isRunning && _listener == null)
            return;

        _isRunning = false;

        try
        {
            _listener?.Stop();
            _listener?.Close();
        }
        catch (Exception ex)
        {
            Log.Warn($"Error stopping Vendor Search web server: {ex.Message}");
        }
        finally
        {
            _listener = null;
            _listenerThread = null;
        }
    }

    public void Dispose() => Stop();

    private void Listen()
    {
        while (_isRunning)
        {
            try
            {
                HttpListenerContext context = _listener.GetContext();
                _ = Task.Run(() => Handle(context));
            }
            catch (HttpListenerException) when (!_isRunning) { }
            catch (ObjectDisposedException) when (!_isRunning) { }
            catch (Exception ex)
            {
                if (_isRunning)
                    Log.Error($"Vendor Search web server listener error: {ex.Message}");
            }
        }
    }

    private void Handle(HttpListenerContext context)
    {
        try
        {
            AddSecurityHeaders(context.Response);
            string path = context.Request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;

            if (path.Length == 0)
            {
                if (!RequireMethod(context, "GET"))
                    return;

                Write(
                    context.Response,
                    200,
                    "text/html; charset=utf-8",
                    Encoding.UTF8.GetBytes(VendorSearchWebPage.Html)
                );
                return;
            }

            if (path.Equals("/api/vendor-search", StringComparison.OrdinalIgnoreCase))
            {
                if (!RequireMethod(context, "GET"))
                    return;

                WriteJson(context.Response, 200, _manager.GetState());
                return;
            }

            if (path.Equals("/api/vendor-search/respond", StringComparison.OrdinalIgnoreCase))
            {
                HandleResponse(context);
                return;
            }

            if (path.Equals("/api/vendor-search/art", StringComparison.OrdinalIgnoreCase))
            {
                HandleArt(context);
                return;
            }

            WriteJson(context.Response, 404, new { error = "Not found." });
        }
        catch (Exception ex)
        {
            Log.Error($"Vendor Search web request failed: {ex.Message}");

            try
            {
                WriteJson(context.Response, 500, new { error = "Request failed." });
            }
            catch { }
        }
    }

    private void HandleResponse(HttpListenerContext context)
    {
        if (!RequireMethod(context, "POST"))
            return;

        if (!HasSameOrigin(context.Request))
        {
            WriteJson(context.Response, 403, new { error = "Cross-origin requests are not allowed." });
            return;
        }

        if (context.Request.ContentLength64 > MaxRequestBytes)
        {
            WriteJson(context.Response, 413, new { error = "Request body is too large." });
            return;
        }

        using var reader = new StreamReader(
            context.Request.InputStream,
            context.Request.ContentEncoding ?? Encoding.UTF8,
            true,
            4096,
            false
        );
        string body = reader.ReadToEnd();

        if (Encoding.UTF8.GetByteCount(body) > MaxRequestBytes)
        {
            WriteJson(context.Response, 413, new { error = "Request body is too large." });
            return;
        }

        VendorSearchResponseRequest request;

        try
        {
            request = JsonSerializer.Deserialize<VendorSearchResponseRequest>(body, JsonOptions);
        }
        catch (JsonException)
        {
            WriteJson(context.Response, 400, new { error = "Invalid JSON." });
            return;
        }

        VendorSearchResponseResult result = _manager.Submit(request);
        WriteJson(
            context.Response,
            result.StatusCode,
            result.Accepted
                ? new { status = "ok", message = result.Message, revision = result.Revision }
                : new { status = "error", error = result.Message, revision = result.Revision }
        );
    }

    private void HandleArt(HttpListenerContext context)
    {
        if (!RequireMethod(context, "GET"))
            return;

        if (
            !ushort.TryParse(context.Request.QueryString["graphic"], out ushort graphic)
            || !ushort.TryParse(context.Request.QueryString["hue"], out ushort hue)
            || !_manager.IsArtAllowed(graphic, hue)
        )
        {
            WriteJson(context.Response, 404, new { error = "Item art is not available." });
            return;
        }

        byte[] png = _manager.GetArtPng(graphic, hue);

        if (png == null)
        {
            WriteJson(context.Response, 404, new { error = "Item art is not available." });
            return;
        }

        context.Response.Headers["Cache-Control"] = "private, max-age=3600";
        Write(context.Response, 200, "image/png", png);
    }

    private bool HasSameOrigin(HttpListenerRequest request)
    {
        string origin = request.Headers["Origin"];

        if (string.IsNullOrEmpty(origin))
            return true;

        return origin.Equals($"http://localhost:{Port}", StringComparison.OrdinalIgnoreCase)
            || origin.Equals($"http://127.0.0.1:{Port}", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequireMethod(HttpListenerContext context, string method)
    {
        if (context.Request.HttpMethod.Equals(method, StringComparison.OrdinalIgnoreCase))
            return true;

        context.Response.Headers["Allow"] = method;
        WriteJson(context.Response, 405, new { error = "Method not allowed." });
        return false;
    }

    private static void AddSecurityHeaders(HttpListenerResponse response)
    {
        response.Headers["Cache-Control"] = "no-store";
        response.Headers["Content-Security-Policy"] =
            "default-src 'self'; img-src 'self'; style-src 'unsafe-inline'; script-src 'unsafe-inline'; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
        response.Headers["Referrer-Policy"] = "no-referrer";
        response.Headers["X-Content-Type-Options"] = "nosniff";
        response.Headers["X-Frame-Options"] = "DENY";
    }

    private static void WriteJson(HttpListenerResponse response, int statusCode, object value)
    {
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        Write(response, statusCode, "application/json; charset=utf-8", bytes);
    }

    private static void Write(
        HttpListenerResponse response,
        int statusCode,
        string contentType,
        byte[] bytes
    )
    {
        response.StatusCode = statusCode;
        response.ContentType = contentType;
        response.ContentLength64 = bytes.Length;
        response.OutputStream.Write(bytes, 0, bytes.Length);
        response.Close();
    }
}
