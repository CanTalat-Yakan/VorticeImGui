using Vortice.DXGI;

namespace Engine.Buffer;

public class GPUUpload
{
    public byte[] vertexData;
    public byte[] indexData;
    public byte[] textureData;

    public string name;
    public Format format;
    public int stride;

    public Texture2D texture2D;
}