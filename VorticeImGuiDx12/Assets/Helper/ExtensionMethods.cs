using SharpGen.Runtime;

namespace Engine.Helper;

public static class ExtensionMethods
{
    public static void ThrowIfFailed(this Result result)
    {
        if (result.Failure)
            throw new NotImplementedException(result.ToString());
    }
}
