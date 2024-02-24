using System.Numerics;
using System;

using ImGuiNET;
using Vortice.Direct3D12;
using Vortice.DXGI;

using Engine.Rendering;

using ImDrawIdx = System.UInt16;

namespace Engine.GUI;

unsafe public class GUIRender
{
    public Core Context;
    public InputLayoutDescription InputLayoutDescription;
    public Texture2D FontTexture;
    public Mesh ImGuiMesh;

    PSODesc psoDesc = new PSODesc
    {
        CullMode = CullMode.None,
        RenderTargetFormat = Format.R8G8B8A8_UNorm,
        RenderTargetCount = 1,
        PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
        InputLayout = "ImGui",
        BlendState = "Alpha",
    };

    public void Init()
    {
        Context.imguiContext = ImGui.CreateContext();
        ImGui.SetCurrentContext(Context.imguiContext);
        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        FontTexture = new Texture2D();
        Context.RenderTargets["imgui_font"] = FontTexture;

        //ImFontPtr font = io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\SIMHEI.ttf", 14, null, io.Fonts.GetGlyphRangesChineseFull());

        io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);
        io.Fonts.TexID = Context.GetStringId("imgui_font");

        FontTexture.width = width;
        FontTexture.height = height;
        FontTexture.mipLevels = 1;
        FontTexture.format = Format.R8G8B8A8_UNorm;
        ImGuiMesh = Context.GetMesh("imgui_mesh");

        GPUUpload gpuUpload = new GPUUpload();
        gpuUpload.texture2D = FontTexture;
        gpuUpload.format = Format.R8G8B8A8_UNorm;
        gpuUpload.textureData = new byte[width * height * bytesPerPixel];
        new Span<byte>(pixels, gpuUpload.textureData.Length).CopyTo(gpuUpload.textureData);

        Context.UploadQueue.Enqueue(gpuUpload);

    }
    public void Render()
    {
        ImGui.NewFrame();
        ImGui.ShowDemoWindow();
        ImGui.Render();
        var data = ImGui.GetDrawData();
        Renderer graphicsContext = Context.GraphicsContext;
        float L = data.DisplayPos.X;
        float R = data.DisplayPos.X + data.DisplaySize.X;
        float T = data.DisplayPos.Y;
        float B = data.DisplayPos.Y + data.DisplaySize.Y;
        float[] mvp =
        {
                2.0f/(R-L),   0.0f,           0.0f,       0.0f,
                0.0f,         2.0f/(T-B),     0.0f,       0.0f,
                0.0f,         0.0f,           0.5f,       0.0f,
                (R+L)/(L-R),  (T+B)/(B-T),    0.5f,       1.0f,
        };
        int index1 = Context.UploadBuffer.Upload<float>(mvp);
        graphicsContext.SetRootSignature(Context.CreateRootSignatureFromString("Cssss"));
        graphicsContext.SetPipelineState(Context.PipelineStateObjects["ImGui"], psoDesc);
        Context.UploadBuffer.SetCBV(graphicsContext, index1, 0);
        graphicsContext.commandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);

        Vector2 clip_off = data.DisplayPos;
        for (int i = 0; i < data.CmdListsCount; i++)
        {
            var cmdList = data.CmdListsRange[i];
            var vertBytes = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
            var indexBytes = cmdList.IdxBuffer.Size * sizeof(ImDrawIdx);

            Context.UploadBuffer.UploadMeshIndex(graphicsContext, ImGuiMesh, new Span<byte>(cmdList.IdxBuffer.Data.ToPointer(), indexBytes), Format.R16_UInt);
            Context.UploadBuffer.UploadVertexBuffer(graphicsContext,ref ImGuiMesh._vertex, new Span<byte>(cmdList.VtxBuffer.Data.ToPointer(), vertBytes));
            ImGuiMesh.vertices["POSITION"] = new _VertexBuffer() { offset = 0, resource = ImGuiMesh._vertex, sizeInByte = vertBytes, stride = sizeof(ImDrawVert) };
            ImGuiMesh.vertices["TEXCOORD"] = new _VertexBuffer() { offset = 8, resource = ImGuiMesh._vertex, sizeInByte = vertBytes, stride = sizeof(ImDrawVert) };
            ImGuiMesh.vertices["COLOR"] = new _VertexBuffer() { offset = 16, resource = ImGuiMesh._vertex, sizeInByte = vertBytes, stride = sizeof(ImDrawVert) };

            graphicsContext.SetMesh(ImGuiMesh);

            for (int j = 0; j < cmdList.CmdBuffer.Size; j++)
            {
                var cmd = cmdList.CmdBuffer[j];
                if (cmd.UserCallback != IntPtr.Zero)
                {
                    throw new NotImplementedException("user callbacks not implemented");
                }
                else
                {
                    graphicsContext.SetSRV(Context.GetTexByStrId(cmd.TextureId), 0);
                    var rect = new Vortice.RawRect((int)(cmd.ClipRect.X - clip_off.X), (int)(cmd.ClipRect.Y - clip_off.Y), (int)(cmd.ClipRect.Z - clip_off.X), (int)(cmd.ClipRect.W - clip_off.Y));
                    graphicsContext.commandList.RSSetScissorRects(new[] { rect });

                    graphicsContext.DrawIndexedInstanced((int)cmd.ElemCount, 1, (int)(cmd.IdxOffset), (int)(cmd.VtxOffset), 0);
                }
            }
        }
    }
}
