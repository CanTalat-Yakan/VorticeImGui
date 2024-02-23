using ImGuiNET;

namespace Engine;

public sealed class Program
{
    public AppWindow AppWindow { get; private set; }
    public Renderer Renderer { get; private set; }
    public GUIRenderer GUIRenderer { get; private set; }
    public GUIInputHandler GUIInputHandler { get; private set; }

    [STAThread]
    private static void Main() =>
        new Program().Run();

    public void Run()
    {
        AppWindow = new();
        AppWindow.Show();

        Renderer = new(AppWindow.Win32Window);

        ImGui.SetCurrentContext(ImGui.CreateContext());

        GUIRenderer = new();
        GUIInputHandler = new(AppWindow.Win32Window.Handle);

        AppWindow.Looping(Render);
        AppWindow.Dispose(Renderer.Dispose);
    }

    public void Render()
    {
        Renderer.BeginFrame();
        Renderer.Data.SetViewport(Renderer.Size);

        GUIRenderer.Update(ImGui.GetCurrentContext(), Renderer.Size);
        GUIInputHandler.Update();

        ImGui.ShowDemoWindow();

        ImGui.Render();
        GUIRenderer.Render();

        Renderer.Execute();

        Renderer.EndFrame();
        Renderer.Resolve();

        Renderer.Present();
        Renderer.WaitIdle();
    }
}