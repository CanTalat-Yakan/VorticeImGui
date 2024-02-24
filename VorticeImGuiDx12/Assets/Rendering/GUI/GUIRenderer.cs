using System.Numerics;
using System;

using ImGuiNET;
using Vortice.Direct3D12;
using Vortice.DXGI;

using Engine.Graphics;

using ImDrawIdx = System.UInt16;

namespace Engine.GUI
{
    unsafe public class GUIRenderer
    {
        public Core Context;

        public InputLayoutDescription InputLayoutDescription;

        public Texture2D FontTexture;
        public Mesh ImGuiMesh;

        public PipelineStateObjectDescription PipelineStateObjectDescription = new()
        {
            InputLayout = "ImGui",
            CullMode = CullMode.None,
            RenderTargetFormat = Format.R8G8B8A8_UNorm,
            RenderTargetCount = 1,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            BlendState = "Alpha",
        };

        public void Initialization()
        {
            Context.ImGuiContext = ImGui.CreateContext();
            ImGui.SetCurrentContext(Context.ImGuiContext);

            var io = ImGui.GetIO();
            io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;

            FontTexture = new Texture2D();
            Context.RenderTargets["ImGui Font"] = FontTexture;

            //ImFontPtr font = io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\SIMHEI.ttf", 14, null, io.Fonts.GetGlyphRangesChineseFull());

            io.Fonts.GetTexDataAsRGBA32(out byte* pixels, out int width, out int height, out int bytesPerPixel);
            io.Fonts.TexID = Context.GetStringID("ImGui Font");

            FontTexture.Width = width;
            FontTexture.Height = height;
            FontTexture.MipLevels = 1;
            FontTexture.Format = Format.R8G8B8A8_UNorm;
            ImGuiMesh = Context.GetMesh("ImGui Mesh");

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
            var graphicsContext = Context.GraphicsContext;

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
            graphicsContext.SetPipelineState(Context.PipelineStateObjects["ImGui"], PipelineStateObjectDescription);
            Context.UploadBuffer.SetCBV(graphicsContext, index1, 0);
            graphicsContext.CommandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);

            Vector2 clipOffset = data.DisplayPos;
            for (int i = 0; i < data.CmdListsCount; i++)
            {
                var cmdList = data.CmdListsRange[i];

                var vertBytes = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
                var indexBytes = cmdList.IdxBuffer.Size * sizeof(ImDrawIdx);

                Context.UploadBuffer.UploadMeshIndex(graphicsContext, ImGuiMesh, new Span<byte>(cmdList.IdxBuffer.Data.ToPointer(), indexBytes), Format.R16_UInt);
                Context.UploadBuffer.UploadVertexBuffer(graphicsContext, ref ImGuiMesh.Vertex, new Span<byte>(cmdList.VtxBuffer.Data.ToPointer(), vertBytes));

                ImGuiMesh.Vertices["POSITION"] = new VertexBuffer() { offset = 0, resource = ImGuiMesh.Vertex, sizeInByte = vertBytes, stride = sizeof(ImDrawVert) };
                ImGuiMesh.Vertices["TEXCOORD"] = new VertexBuffer() { offset = 8, resource = ImGuiMesh.Vertex, sizeInByte = vertBytes, stride = sizeof(ImDrawVert) };
                ImGuiMesh.Vertices["COLOR"] = new VertexBuffer() { offset = 16, resource = ImGuiMesh.Vertex, sizeInByte = vertBytes, stride = sizeof(ImDrawVert) };

                graphicsContext.SetMesh(ImGuiMesh);

                for (int j = 0; j < cmdList.CmdBuffer.Size; j++)
                {
                    var cmd = cmdList.CmdBuffer[j];

                    if (cmd.UserCallback != IntPtr.Zero)
                        throw new NotImplementedException("user callbacks not implemented");
                    else
                    {
                        graphicsContext.SetShaderResourceView(Context.GetTextureByStringID(cmd.TextureId), 0);

                        var rect = new Vortice.RawRect((int)(cmd.ClipRect.X - clipOffset.X), (int)(cmd.ClipRect.Y - clipOffset.Y), (int)(cmd.ClipRect.Z - clipOffset.X), (int)(cmd.ClipRect.W - clipOffset.Y));
                        graphicsContext.CommandList.RSSetScissorRects(new[] { rect });

                        graphicsContext.DrawIndexedInstanced((int)cmd.ElemCount, 1, (int)(cmd.IdxOffset), (int)(cmd.VtxOffset), 0);
                    }
                }
            }
        }
    }
}
