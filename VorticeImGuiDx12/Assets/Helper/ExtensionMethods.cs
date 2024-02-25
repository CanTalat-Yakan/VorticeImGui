using SharpGen.Runtime;
using Vortice.Mathematics;

namespace Engine.Helper;

public static class ExtensionMethods
{
    public static SizeI Scale(this SizeI size, double scale) =>
        new SizeI((int)(size.Width * scale), (int)(size.Height * scale));

    public static Vector2 ToVector2(this SizeI size) =>
        new Vector2(size.Width, size.Height);

    public static void ThrowIfFailed(this Result result)
    {
        if (result.Failure)
            throw new NotImplementedException(result.ToString());
    }
}