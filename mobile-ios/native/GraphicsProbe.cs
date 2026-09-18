using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TazUO.iOS;

/// <summary>Renderer integration check, deliberately not a substitute game scene.</summary>
internal sealed class GraphicsProbe : Game
{
    private SpriteBatch? _batch;
    private Texture2D? _texture;
    private int _frames;

    public GraphicsProbe()
    {
        _ = new GraphicsDeviceManager(this)
        {
            PreferredBackBufferWidth = 900,
            PreferredBackBufferHeight = 420,
            IsFullScreen = true,
            GraphicsProfile = GraphicsProfile.HiDef
        };
        IsFixedTimeStep = false;
        Window.Title = "TazUO native renderer check";
    }

    protected override void LoadContent()
    {
        _batch = new SpriteBatch(GraphicsDevice);
        _texture = new Texture2D(GraphicsDevice, 1, 1);
        _texture.SetData(new[] { Color.White });
        using var target = new RenderTarget2D(GraphicsDevice, 8, 8);
        GraphicsDevice.SetRenderTarget(target);
        GraphicsDevice.Clear(Color.Black);
        _batch.Begin();
        _batch.Draw(_texture, new Rectangle(0, 0, 8, 8), Color.Lime);
        _batch.End();
        GraphicsDevice.SetRenderTarget(null);
        var pixels = new Color[64];
        target.GetData(pixels);
        if (pixels[32] != Color.Lime)
            throw new InvalidOperationException($"GPU readback mismatch: {pixels[32]}");
        Log($"FNA texture + shader + readback: PASS\nRenderer: {GraphicsDevice.Adapter.Description}\n");
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(12, 20, 32));
        int width = GraphicsDevice.Viewport.Width;
        int height = GraphicsDevice.Viewport.Height;
        _batch!.Begin();
        _batch.Draw(_texture!, new Rectangle(width / 4, height / 3, width / 2, height / 3), Color.LimeGreen);
        _batch.End();
        if (++_frames == 60)
            Log($"FNA graphics: PASS\nRendered frames: {_frames}\nViewport: {width}x{height}\n");
    }

    internal static void Log(string text) => File.AppendAllText(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "native-runtime.log"), text);
}
