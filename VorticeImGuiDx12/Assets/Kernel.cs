global using System;
global using System.Numerics;

global using ImGuiNET;

global using Engine.Buffer;
global using Engine.Data;
global using Engine.DataTypes;
global using Engine.Framework;
global using Engine.Graphics;
global using Engine.GUI;
global using Engine.Helper;
global using Engine.Utilities;

namespace Engine;

public sealed class Kernel
{
    public static Kernel Instance { get; private set; }

    public event Action OnRender;
    public event Action OnInitialize;
    public event Action OnGUI;
    public event Action OnDispose;

    public CommonContext Context;
    public Config Config;

    public GUIRenderer GUIRenderer;
    public GUIInputHandler GUIInputHandler;
    public IntPtr GUIContext;

    public Kernel(Config config) =>
        Config = config;

    public void Initialize(CommonContext context)
    {
        // Set the singleton instance of the class, if it hasn't been already.
        Instance ??= this;

        Context = context;

        if (Config.GUI)
        {
            GUIRenderer = new();
            GUIRenderer.Context = Context;

            GUIRenderer.LoadDefaultResource();
        }

        Context.GraphicsDevice.Initialize(true);
        Context.UploadBuffer.Initialize(Context.GraphicsDevice, 67108864); // 64 MB.

        if (Config.GUI)
        {
            GUIRenderer.Initialize();
            GUIInputHandler = new();
        }

        Context.GraphicsContext.Initialize(Context.GraphicsDevice);
    }

    public void Frame()
    {
        if (!Context.IsRendering)
            return;

        OnInitialize?.Invoke();
        OnInitialize = null;

        Context.GraphicsDevice.Begin();

        var graphicsContext = Context.GraphicsContext;
        graphicsContext.BeginCommand();

        Context.GPUUploadData(graphicsContext);

        graphicsContext.SetDescriptorHeapDefault();
        graphicsContext.ScreenBeginRender();
        graphicsContext.SetRenderTargetScreen();
        graphicsContext.ClearRenderTargetScreen();

        OnRender?.Invoke();

        if (Config.GUI)
            RenderGUI();

        graphicsContext.ScreenEndRender();
        graphicsContext.EndCommand();
        graphicsContext.Execute();

        Context.GraphicsDevice.Present((int)Config.VSync);
    }

    public void RenderGUI()
    {
        GUIRenderer.Update(GUIContext);
        GUIInputHandler.Update();

        OnGUI?.Invoke();

        GUIRenderer.Render();
    }

    public void Dispose()
    {
        Context?.Dispose();

        OnDispose?.Invoke();
    }
}