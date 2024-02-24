using System;

using Vortice.Direct3D12;

namespace Engine.Graphics;

public class UploadBuffer : IDisposable
{
    public ID3D12Resource resource;
    public int size;

    public void Dispose() =>
        resource?.Dispose();
}
