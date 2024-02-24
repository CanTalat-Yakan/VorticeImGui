using System;

using ImGuiNET;
using Vortice.Mathematics;

using Engine.GUI;

namespace Engine;

public sealed partial class Program
{
    public AppWindow AppWindow { get; private set; }

    [STAThread]
    private static void Main() =>
        new Program().Run();

    public void Run()
    {
        AppWindow = new();
        AppWindow.Show();

        Initialize();

        AppWindow.Looping(UpdateAndDraw);
        AppWindow.Dispose(Context.Dispose);
    }
}

public sealed partial class Program
{
    public CommonContext Context { get; private set; } = new();
    public GUIRenderer ImGuiRender { get; private set; } = new();

    DateTime _current;

    public void Initialize()
    {
        ImGuiRender.Context = Context;

        Context.LoadDefaultResource();

        Context.Device.Initialize();
        Context.UploadBuffer.Initialize(Context.Device, 67108864);//64 MB

        ImGuiRender.Initialize();
        
        Context.ImGuiInputHandler = new GUIInputHandler();
        Context.ImGuiInputHandler.hwnd = AppWindow.Win32Window.Handle;

        Context.GraphicsContext.Initialize(Context.Device);
        Context.Device.SetupSwapChain(AppWindow.Win32Window.Handle);
    }

    public void UpdateAndDraw()
    {
        if (AppWindow.Win32Window.Width != Context.Device.Width || AppWindow.Win32Window.Height != Context.Device.Height)
        {
            Context.Device.Resize(AppWindow.Win32Window.Width, AppWindow.Win32Window.Height);
            ImGui.GetIO().DisplaySize = new System.Numerics.Vector2(Context.Device.Width, Context.Device.Height);
        }

        Context.Device.Begin();

        var graphicsContext = Context.GraphicsContext;
        graphicsContext.BeginCommand();

        Context.GPUUploadData(graphicsContext);

        graphicsContext.SetDescriptorHeapDefault();
        graphicsContext.ScreenBeginRender();
        graphicsContext.SetRenderTargetScreen();
        graphicsContext.ClearRenderTargetScreen(new Color4(0.15f, 0.15f, 0.15f, 1));

        ImGui.SetCurrentContext(Context.ImGuiContext);

        var previous = _current;
        _current = DateTime.Now;
        float delta = (float)(_current - previous).TotalSeconds;
        ImGui.GetIO().DeltaTime = delta;

        Context.ImGuiInputHandler.Update();

        ImGuiRender.Render();

        graphicsContext.ScreenEndRender();
        graphicsContext.EndCommand();
        graphicsContext.Execute();

        Context.Device.Present(true);
    }
}