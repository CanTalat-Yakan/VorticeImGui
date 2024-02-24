using System;

using ImGuiNET;
using Vortice.Mathematics;

using Engine.GUI;
using Engine.ResourcesManage;

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
        AppWindow.Dispose(_context.Dispose);
    }
}

public sealed partial class Program
{
    CommonContext _context = new();
    GUIRender _imGuiRender = new();
    DateTime _current;

    public void Initialize()
    {
        _imGuiRender.Context = _context;
        _context.LoadDefaultResource();

        _context.Device.Initialize();
        _context.UploadBuffer.Initialize(_context.Device, 67108864);//64 MB

        _imGuiRender.Initialization();
        _context.ImGuiInputHandler = new GUIInputHandler();
        _context.ImGuiInputHandler.hwnd = AppWindow.Win32Window.Handle;

        _context.GraphicsContext.Initialize(_context.Device);
        _context.Device.SetupSwapChain((IntPtr)AppWindow.Win32Window.Handle);
    }

    public void UpdateAndDraw()
    {
        if (AppWindow.Win32Window.Width != _context.Device.Width || AppWindow.Win32Window.Height != _context.Device.Height)
        {
            _context.Device.Resize(AppWindow.Win32Window.Width, AppWindow.Win32Window.Height);
            ImGui.GetIO().DisplaySize = new System.Numerics.Vector2(_context.Device.Width, _context.Device.Height);
        }

        var graphicsContext = _context.GraphicsContext;
        _context.Device.Begin();
        graphicsContext.BeginCommand();
        _context.GPUUploadData(graphicsContext);
        graphicsContext.SetDescriptorHeapDefault();
        graphicsContext.ScreenBeginRender();
        graphicsContext.SetRenderTargetScreen();
        graphicsContext.ClearRenderTargetScreen(new Color4(0.5f, 0.5f, 1, 1));

        ImGui.SetCurrentContext(_context.ImGuiContext);
        var previous = _current;
        _current = DateTime.Now;
        float delta = (float)(_current - previous).TotalSeconds;
        ImGui.GetIO().DeltaTime = delta;
        _context.ImGuiInputHandler.Update();
        _imGuiRender.Render();
        graphicsContext.ScreenEndRender();
        graphicsContext.EndCommand();
        graphicsContext.Execute();
        _context.Device.Present(true);
    }
}