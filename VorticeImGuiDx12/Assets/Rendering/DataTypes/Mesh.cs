using System.Collections.Generic;

using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Engine.DataTypes;

public class Mesh : IDisposable
{
    public ID3D12Resource Vertex;
    public ID3D12Resource Index;

    public InputLayoutDescription InputLayoutDescription;

    public Dictionary<string, VertexBuffer> Vertices = new();

    public int IndexCount;
    public int IndexSizeInByte;

    public string Name;
    public Format IndexFormat;

    public void Dispose()
    {
        Index?.Dispose();
        Index = null;

        Vertex?.Dispose();
        Vertex = null;

        if (Vertices is not null)
            foreach (var pair in Vertices)
                pair.Value.Dispose();
        Vertices?.Clear();
    }
}

public class VertexBuffer : IDisposable
{
    public ID3D12Resource Resource;

    public int Offset;
    public int SizeInByte;
    public int Stride;

    public void Dispose()
    {
        if (Offset == 0)
            Resource.Dispose();
    }
}
