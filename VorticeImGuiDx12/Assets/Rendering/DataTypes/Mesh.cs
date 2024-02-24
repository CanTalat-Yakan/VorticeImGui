using System.Collections.Generic;

using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Engine.DataTypes;

public class Mesh : IDisposable
{
    public ID3D12Resource Vertex;
    public ID3D12Resource Index;

    public UnnamedInputLayout UnnamedInputLayout;

    public Dictionary<string, VertexBuffer> Vertices = new();

    public int IndexCount;
    public int IndexSizeInByte;

    public string Name;
    public Format IndexFormat;

    public void Dispose()
    {
        Vertex?.Dispose();
        Vertex = null;
        if (Vertices is not null)
            foreach (var pair in Vertices)
                pair.Value.Dispose();
        Vertices?.Clear();
        Index?.Dispose();
        Index = null;
    }
}

public class VertexBuffer : IDisposable
{
    public ID3D12Resource resource;

    public int offset;
    public int sizeInByte;
    public int stride;

    public void Dispose()
    {
        if (offset == 0)
            resource.Dispose();
    }
}
