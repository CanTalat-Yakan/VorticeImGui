using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System;

using Vortice.Direct3D12;
using Vortice.Dxc;
using Vortice.DXGI;

using Engine.GUI;
using Engine.Graphics;

namespace Engine;

public class Core : IDisposable
{
    public GraphicsDevice Device = new();
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

    public IntPtr ImGuiContext;
    public GUIInputHandler ImGuiInputHandler;

    public void LoadDefaultResource()
    {
        string directoryPath = AppContext.BaseDirectory + @"Assets\Resources\Shaders\";

        VertexShaders["ImGui"] = new Shader() { CompiledCode = LoadShader(DxcShaderStage.Vertex, directoryPath + "ImGui.hlsl", "VS"), Name = "ImGui VS" };
        PixelShaders["ImGui"] = new Shader() { CompiledCode = LoadShader(DxcShaderStage.Pixel, directoryPath + "ImGui.hlsl", "PS"), Name = "ImGui PS" };
        PipelineStateObjects["ImGui"] = new PipelineStateObject(VertexShaders["ImGui"], PixelShaders["ImGui"]); ;
    }

    private int somePointerValue = 65536;
    public IntPtr GetStringID(string name)
    {
        if (StringToPointer.TryGetValue(name, out IntPtr pointer))
            return pointer;

        pointer = new IntPtr(somePointerValue);

        StringToPointer[name] = pointer;
        PointerToString[pointer] = name;

        somePointerValue++;

        return pointer;
    }

    public string IDToString(nint pointer) =>
        PointerToString[pointer];

    public Texture2D GetTextureByStringID(nint pointer) =>
        RenderTargets[PointerToString[pointer]];

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

        Device.CreateRootSignature(rootSignature, description);

        return rootSignature;
    }

    ReadOnlyMemory<byte> LoadShader(DxcShaderStage shaderStage, string path, string entryPoint)
    {
        var result = DxcCompiler.Compile(shaderStage, File.ReadAllText(path), entryPoint);
        if (result.GetStatus().Failure)
            throw new Exception(result.GetErrors());

        return result.GetObjectBytecodeMemory();
    }

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

    public void GPUUploadData(GraphicsContext graphicsContext1)
    {
        while (UploadQueue.TryDequeue(out var upload))
        {
            //if (upload.mesh is not null)
            //{
            //    graphicsContext1.UploadMesh(upload.mesh, upload.vertexData, upload.indexData, upload.stride, upload.format);
            //}
            if (upload.texture2D is not null)
            {
                graphicsContext1.UploadTexture(upload.texture2D, upload.textureData);
            }
        }
    }

    public void Dispose()
    {
        UploadBuffer?.Dispose();
        DisposeDictionaryItems(RootSignatures);
        DisposeDictionaryItems(RenderTargets);
        DisposeDictionaryItems(PipelineStateObjects);
        DisposeDictionaryItems(Meshes);

        GraphicsContext.Dispose();
        Device.Dispose();
    }

    void DisposeDictionaryItems<T1, T2>(Dictionary<T1, T2> dictionary) where T2 : IDisposable
    {
        foreach (var pair in dictionary)
            pair.Value.Dispose();

        dictionary.Clear();
    }
}