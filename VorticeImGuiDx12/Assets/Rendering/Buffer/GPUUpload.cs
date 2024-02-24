using Vortice.DXGI;

namespace Engine.Buffer;

public class GPUUpload
{
    public byte[] VertexData;
    public byte[] IndexData;
    public byte[] TextureData;

    public string Name;
    public Format Format;
    public int Stride;

    public Texture2D Texture2D;
}