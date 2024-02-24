using System;
using System.Runtime.InteropServices;

using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Mathematics;

namespace Engine.Rendering
{
    unsafe struct D3D12_MEMCPY_DEST
    {
        public void* pData;
        public ulong RowPitch;
        public ulong SlicePitch;
    }

    public class GraphicsContext : IDisposable
    {
        const int D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING = 5768;

        public void Initialize(GraphicsDevice graphicsDevice)
        {
            ThrowIfFailed(graphicsDevice.Device.CreateCommandList(0, CommandListType.Direct, graphicsDevice.GetCommandAllocator(), null, out commandList));
            commandList.Close();
            this.graphicsDevice = graphicsDevice;
        }

        public void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
        {
            commandList.SetPipelineState(pipelineStateObject.GetState(graphicsDevice, psoDesc, currentRootSignature, unnamedInputLayout));
            commandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
        }

        public void SetDescriptorHeapDefault()
        {
            commandList.SetDescriptorHeaps(1, new[] { graphicsDevice.CBVSRVUAVHeap.Heap });
        }

        public void SetRootSignature(RootSignature rootSignature)
        {
            currentRootSignature = rootSignature;
            commandList.SetGraphicsRootSignature(rootSignature.Resource);
        }

        public void SetComputeRootSignature(RootSignature rootSignature)
        {
            currentRootSignature = rootSignature;
            commandList.SetComputeRootSignature(rootSignature.Resource);
        }

        public void SetSRV(Texture2D texture, int slot)
        {
            ShaderResourceViewDescription shaderResourceViewDescription = new ShaderResourceViewDescription();
            shaderResourceViewDescription.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D;
            shaderResourceViewDescription.Format = texture.Format;
            shaderResourceViewDescription.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
            shaderResourceViewDescription.Texture2D.MipLevels = texture.MipLevels;

            texture.StateChange(commandList, ResourceStates.GenericRead);
            graphicsDevice.CBVSRVUAVHeap.GetTempHandle(out CpuDescriptorHandle cpuHandle, out GpuDescriptorHandle gpuHandle);
            graphicsDevice.Device.CreateShaderResourceView(texture.Resource, shaderResourceViewDescription, cpuHandle);
            commandList.SetGraphicsRootDescriptorTable(currentRootSignature.ShaderResourceView[slot], gpuHandle);
        }

        public void SetDSVRTV(Texture2D dsv, Texture2D[] rtvs, bool clearDSV, bool clearRTV)
        {
            dsv?.StateChange(commandList, ResourceStates.DepthWrite);
            CpuDescriptorHandle[] rtvHandles = null;
            if (rtvs != null)
            {
                rtvHandles = new CpuDescriptorHandle[rtvs.Length];
                for (int i = 0; i < rtvs.Length; i++)
                {
                    Texture2D rtv = rtvs[i];
                    rtv.StateChange(commandList, ResourceStates.RenderTarget);
                    rtvHandles[i] = rtv.RenderTargetView.GetCPUDescriptorHandleForHeapStart();
                }
            }
            if (clearDSV && dsv != null)
                commandList.ClearDepthStencilView(dsv.DepthStencilView.GetCPUDescriptorHandleForHeapStart(), ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
            if (clearRTV && rtvs != null)
            {
                foreach (var rtv in rtvs)
                    commandList.ClearRenderTargetView(rtv.RenderTargetView.GetCPUDescriptorHandleForHeapStart(), new Color4());
            }

            commandList.OMSetRenderTargets(rtvHandles, dsv.DepthStencilView.GetCPUDescriptorHandleForHeapStart());
        }

        public void SetRenderTargetScreen()
        {
            commandList.RSSetViewport(new Viewport(0, 0, graphicsDevice.Width, graphicsDevice.Height, 0.0f, 1.0f));
            commandList.RSSetScissorRect(new RectI(0, 0, graphicsDevice.Width, graphicsDevice.Height));
            commandList.OMSetRenderTargets(graphicsDevice.GetRenderTargetScreen());
        }

        public void SetMesh(Mesh mesh)
        {
            commandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

            int c = -1;
            foreach (var desc in mesh.UnnamedInputLayout.InputElementDescriptions)
            {
                if (desc.Slot != c)
                {
                    if (mesh.Vertices != null && mesh.Vertices.TryGetValue(desc.SemanticName, out var vertex))
                    {
                        commandList.IASetVertexBuffers(desc.Slot, new VertexBufferView(vertex.resource.GPUVirtualAddress + (ulong)vertex.offset, vertex.sizeInByte - vertex.offset, vertex.stride));
                    }
                    c = desc.Slot;
                }
            }

            if (mesh.Index != null)
                commandList.IASetIndexBuffer(new IndexBufferView(mesh.Index.GPUVirtualAddress, mesh.IndexSizeInByte, mesh.IndexFormat));
            unnamedInputLayout = mesh.UnnamedInputLayout;
        }

        public void UploadTexture(Texture2D texture, byte[] data)
        {
            ID3D12Resource resourceUpload1 = graphicsDevice.Device.CreateCommittedResource<ID3D12Resource>(
                new HeapProperties(HeapType.Upload),
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)data.Length),
                ResourceStates.GenericRead);
            graphicsDevice.DestroyResource(resourceUpload1);
            graphicsDevice.DestroyResource(texture.Resource);
            texture.Resource = graphicsDevice.Device.CreateCommittedResource<ID3D12Resource>(
                HeapProperties.DefaultHeapProperties,
                HeapFlags.None,
                ResourceDescription.Texture2D(texture.Format, (uint)texture.Width, (uint)texture.Height, 1, 1),
                ResourceStates.CopyDest);

            uint bitsPerPixel = GraphicsDevice.BitsPerPixel(texture.Format);
            int width = texture.Width;
            int height = texture.Height;
            GCHandle gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
            SubresourceData subresourcedata = new SubresourceData();
            subresourcedata.Data = Marshal.UnsafeAddrOfPinnedArrayElement(data, 0);
            subresourcedata.RowPitch = (IntPtr)(width * bitsPerPixel / 8);
            subresourcedata.SlicePitch = (IntPtr)(width * height * bitsPerPixel / 8);
            UpdateSubresources(commandList, texture.Resource, resourceUpload1, 0, 0, 1, new SubresourceData[] { subresourcedata });
            gcHandle.Free();
            commandList.ResourceBarrierTransition(texture.Resource, ResourceStates.CopyDest, ResourceStates.GenericRead);
            texture.ResourceStates = ResourceStates.GenericRead;
        }

        public void SetCBV(UploadBuffer uploadBuffer, int offset, int slot)
        {
            commandList.SetGraphicsRootConstantBufferView(currentRootSignature.ConstantBufferView[slot], uploadBuffer.resource.GPUVirtualAddress + (ulong)offset);
        }

        public void SetPipelineState(PipelineStateObject pipelineStateObject, PSODescription psoDesc)
        {
            this.pipelineStateObject = pipelineStateObject;
            this.psoDesc = psoDesc;
        }

        public void ClearRenderTarget(Texture2D texture2D)
        {
            commandList.ClearRenderTargetView(texture2D.RenderTargetView.GetCPUDescriptorHandleForHeapStart(), new Color4(0, 0, 0, 0));
        }

        public void ClearRenderTargetScreen(Color4 color)
        {
            commandList.ClearRenderTargetView(graphicsDevice.GetRenderTargetScreen(), color);
        }

        public void ScreenBeginRender()
        {
            commandList.ResourceBarrierTransition(graphicsDevice.GetScreenResource(), ResourceStates.Present, ResourceStates.RenderTarget);
        }

        public void ScreenEndRender()
        {
            commandList.ResourceBarrierTransition(graphicsDevice.GetScreenResource(), ResourceStates.RenderTarget, ResourceStates.Present);
        }

        public void BeginCommand()
        {
            commandList.Reset(graphicsDevice.GetCommandAllocator());
        }

        public void EndCommand()
        {
            commandList.Close();
        }

        public void Execute()
        {
            graphicsDevice.CommandQueue.ExecuteCommandList(commandList);
        }

        unsafe void MemcpySubresource(
            D3D12_MEMCPY_DEST* pDest,
            SubresourceData pSrc,
            int RowSizeInBytes,
            int NumRows,
            int NumSlices)
        {
            for (uint z = 0; z < NumSlices; ++z)
            {
                byte* pDestSlice = (byte*)(pDest->pData) + pDest->SlicePitch * z;
                byte* pSrcSlice = (byte*)(pSrc.Data) + (long)pSrc.SlicePitch * z;
                for (int y = 0; y < NumRows; ++y)
                {
                    new Span<byte>(pSrcSlice + ((long)pSrc.RowPitch * y), RowSizeInBytes).CopyTo(new Span<byte>(pDestSlice + (long)pDest->RowPitch * y, RowSizeInBytes));
                }
            }
        }
        unsafe ulong UpdateSubresources(
            ID3D12GraphicsCommandList pCmdList,
            ID3D12Resource pDestinationResource,
            ID3D12Resource pIntermediate,
            int FirstSubresource,
            int NumSubresources,
            ulong RequiredSize,
            PlacedSubresourceFootPrint[] pLayouts,
            int[] pNumRows,
            ulong[] pRowSizesInBytes,
            SubresourceData[] pSrcData)
        {
            var IntermediateDesc = pIntermediate.Description;
            var DestinationDesc = pDestinationResource.Description;
            if (IntermediateDesc.Dimension != ResourceDimension.Buffer ||
                IntermediateDesc.Width < RequiredSize + pLayouts[0].Offset ||
                (DestinationDesc.Dimension == ResourceDimension.Buffer &&
                    (FirstSubresource != 0 || NumSubresources != 1)))
            {
                return 0;
            }

            void* pointer = null;
            pIntermediate.Map(0, &pointer);
            IntPtr data1 = new IntPtr(pointer);

            byte* pData;
            pData = (byte*)data1;

            for (uint i = 0; i < NumSubresources; ++i)
            {
                D3D12_MEMCPY_DEST DestData = new D3D12_MEMCPY_DEST { pData = pData + pLayouts[i].Offset, RowPitch = (ulong)pLayouts[i].Footprint.RowPitch, SlicePitch = (uint)(pLayouts[i].Footprint.RowPitch) * (uint)(pNumRows[i]) };
                MemcpySubresource(&DestData, pSrcData[i], (int)(pRowSizesInBytes[i]), pNumRows[i], pLayouts[i].Footprint.Depth);
            }
            pIntermediate.Unmap(0, null);

            if (DestinationDesc.Dimension == ResourceDimension.Buffer)
            {
                pCmdList.CopyBufferRegion(
                    pDestinationResource, 0, pIntermediate, pLayouts[0].Offset, (ulong)pLayouts[0].Footprint.Width);
            }
            else
            {
                for (int i = 0; i < NumSubresources; ++i)
                {
                    TextureCopyLocation Dst = new TextureCopyLocation(pDestinationResource, i + FirstSubresource);
                    TextureCopyLocation Src = new TextureCopyLocation(pIntermediate, pLayouts[i]);
                    pCmdList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);
                }
            }
            return RequiredSize;
        }

        ulong UpdateSubresources(
            ID3D12GraphicsCommandList pCmdList,
            ID3D12Resource pDestinationResource,
            ID3D12Resource pIntermediate,
            ulong IntermediateOffset,
            int FirstSubresource,
            int NumSubresources,
            SubresourceData[] pSrcData)
        {
            PlacedSubresourceFootPrint[] pLayouts = new PlacedSubresourceFootPrint[NumSubresources];
            ulong[] pRowSizesInBytes = new ulong[NumSubresources];
            int[] pNumRows = new int[NumSubresources];

            var Desc = pDestinationResource.Description;
            ID3D12Device pDevice = null;
            pDestinationResource.GetDevice(out pDevice);
            pDevice.GetCopyableFootprints(Desc, (int)FirstSubresource, (int)NumSubresources, IntermediateOffset, pLayouts, pNumRows, pRowSizesInBytes, out ulong RequiredSize);
            pDevice.Release();

            ulong Result = UpdateSubresources(pCmdList, pDestinationResource, pIntermediate, FirstSubresource, NumSubresources, RequiredSize, pLayouts, pNumRows, pRowSizesInBytes, pSrcData);
            return Result;
        }

        public ID3D12GraphicsCommandList5 commandList;
        public GraphicsDevice graphicsDevice;
        private void ThrowIfFailed(SharpGen.Runtime.Result hr)
        {
            if (hr != SharpGen.Runtime.Result.Ok)
            {
                throw new NotImplementedException();
            }
        }
        public RootSignature currentRootSignature;
        public PipelineStateObject pipelineStateObject;
        public PSODescription psoDesc;
        public UnnamedInputLayout unnamedInputLayout;

        public void Dispose()
        {
            commandList?.Dispose();
            commandList = null;
        }
    }
}
