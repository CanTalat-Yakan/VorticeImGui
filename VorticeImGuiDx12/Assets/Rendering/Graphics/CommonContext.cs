using System.Collections.Concurrent;
using System.Collections.Generic;

using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Engine.Graphics;

public sealed partial class CommonContext : IDisposable
{
    public bool IsRendering => GraphicsDevice.RenderTextureViewHeap is not null;

    public Kernel Kernel { get; private set; }

    public GraphicsDevice GraphicsDevice = new();
    public GraphicsContext GraphicsContext = new();

    public Dictionary<string, Shader> VertexShaders = new();
    public Dictionary<string, Shader> PixelShaders = new();
    public Dictionary<string, RootSignature> RootSignatures = new();
    public Dictionary<string, Mesh> Meshes = new();
    public Dictionary<string, Texture2D> RenderTargets = new();
    public Dictionary<string, PipelineStateObject> PipelineStateObjects = new();

    public Dictionary<IntPtr, string> PointerToString = new();
    public Dictionary<string, IntPtr> StringToPointer = new();

    public ConcurrentQueue<GPUUpload> UploadQueue = new();
    public RingUploadBuffer UploadBuffer = new();

    public CommonContext(Kernel kernel) =>
        Kernel = kernel;

    public void Dispose()
    {
        UploadBuffer?.Dispose();
        DisposeDictionaryItems(RootSignatures);
        DisposeDictionaryItems(RenderTargets);
        DisposeDictionaryItems(PipelineStateObjects);
        DisposeDictionaryItems(Meshes);

        GraphicsContext.Dispose();
        GraphicsDevice.Dispose();
    }

    void DisposeDictionaryItems<T1, T2>(Dictionary<T1, T2> dictionary) where T2 : IDisposable
    {
        foreach (var pair in dictionary)
            pair.Value.Dispose();

        dictionary.Clear();
    }
}

public sealed partial class CommonContext : IDisposable
{
    public string GetStringFromID(nint pointer) =>
        PointerToString[pointer];

    private int somePointerValue = 65536;
    public IntPtr GetIDFromString(string name)
    {
        if (StringToPointer.TryGetValue(name, out IntPtr pointer))
            return pointer;

        pointer = new IntPtr(somePointerValue);

        StringToPointer[name] = pointer;
        PointerToString[pointer] = name;

        somePointerValue++;

        return pointer;
    }

    public Texture2D GetTextureByStringID(nint pointer) =>
        RenderTargets[PointerToString[pointer]];

    public Mesh GetMesh(string name)
    {
        if (Meshes.TryGetValue(name, out Mesh mesh))
            return mesh;
        else
        {
            mesh = new Mesh();
            mesh.UnnamedInputLayout = new UnnamedInputLayout()
            {
                InputElementDescriptions =
                [
                    new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0),
                    new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 1),
                    new InputElementDescription("COLOR", 0, Format.R8G8B8A8_UNorm, 2)
                ]
            };
            Meshes[name] = mesh;

            return mesh;
        }
    }
}

public sealed partial class CommonContext : IDisposable
{
    public RootSignature CreateRootSignatureFromString(string s)
    {
        RootSignature rootSignature;
        if (RootSignatures.TryGetValue(s, out rootSignature))
            return rootSignature;

        rootSignature = new RootSignature();
        RootSignatures[s] = rootSignature;
        RootSignatureParamP[] description = new RootSignatureParamP[s.Length];

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            switch (c)
            {
                case 'C':
                    description[i] = RootSignatureParamP.CBV;
                    break;
                case 'c':
                    description[i] = RootSignatureParamP.CBVTable;
                    break;
                case 'S':
                    description[i] = RootSignatureParamP.SRV;
                    break;
                case 's':
                    description[i] = RootSignatureParamP.SRVTable;
                    break;
                case 'U':
                    description[i] = RootSignatureParamP.UAV;
                    break;
                case 'u':
                    description[i] = RootSignatureParamP.UAVTable;
                    break;
                default:
                    throw new NotImplementedException("error root signature desc.");
            }
        }

        GraphicsDevice.CreateRootSignature(rootSignature, description);

        return rootSignature;
    }

    public void GPUUploadData(GraphicsContext graphicsContext)
    {
        while (UploadQueue.TryDequeue(out var upload))
        {
            //if (upload.mesh is not null)
            //{
            //    graphicsContext1.UploadMesh(upload.mesh, upload.vertexData, upload.indexData, upload.stride, upload.format);
            //}
            if (upload.texture2D is not null)
            {
                graphicsContext.UploadTexture(upload.texture2D, upload.textureData);
            }
        }
    }
}