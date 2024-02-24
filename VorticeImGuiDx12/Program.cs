namespace Engine;

public sealed class Program
{
    public AppWindow AppWindow { get; private set; }
    public Kernel Kernel { get; private set; }

    [STAThread]
    private static void Main() =>
        new Program().Run();

    public void Run(bool renderGUI = true, Config config = null)
    {
        config ??= Config.GetDefault();
        config.GUI = renderGUI;

        AppWindow = new(config.WindowData);
        AppWindow.Show();

        Kernel = new(config);
        Kernel.Initialize(new CommonContext(Kernel));

        AppWindow.ResizeEvent += Kernel.Context.GraphicsDevice.Resize;

        AppWindow.Looping(Kernel.Frame);
        AppWindow.Dispose(Kernel.Dispose);
    }
}