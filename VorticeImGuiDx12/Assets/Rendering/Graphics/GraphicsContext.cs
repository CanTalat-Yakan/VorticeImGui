using System;
using System.Runtime.InteropServices;

using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Mathematics;

namespace Engine.Graphics;

public class GraphicsContext : IDisposable
{

    public void Initialize(GraphicsDevice graphicsDevice)
    {
        ThrowIfFailed(graphicsDevice.Device.CreateCommandList(0, CommandListType.Direct, graphicsDevice.GetCommandAllocator(), null, out CommandList));
        CommandList.Close();
        this.GraphicsDevice = graphicsDevice;
    }

    public void DrawIndexedInstanced(int indexCountPerInstance, int instanceCount, int startIndexLocation, int baseVertexLocation, int startInstanceLocation)
    {
        CommandList.SetPipelineState(PipelineStateObject.GetState(GraphicsDevice, PipelineStateObjectDescription, CurrentRootSignature, UnnamedInputLayout));
        CommandList.DrawIndexedInstanced(indexCountPerInstance, instanceCount, startIndexLocation, baseVertexLocation, startInstanceLocation);
    }

    public void SetDescriptorHeapDefault()
    {
        CommandList.SetDescriptorHeaps(1, new[] { GraphicsDevice.CBVSRVUAVHeap.Heap });
    }

    public void SetRootSignature(RootSignature rootSignature)
    {
        CurrentRootSignature = rootSignature;
        CommandList.SetGraphicsRootSignature(rootSignature.Resource);
    }

    public void SetComputeRootSignature(RootSignature rootSignature)
    {
        CurrentRootSignature = rootSignature;
        CommandList.SetComputeRootSignature(rootSignature.Resource);
    }

    public void SetShaderResourceView(Texture2D texture, int slot)
    {
        int D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING = 5768;

        ShaderResourceViewDescription shaderResourceViewDescription = new ShaderResourceViewDescription();
        shaderResourceViewDescription.ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D;
        shaderResourceViewDescription.Format = texture.Format;
        shaderResourceViewDescription.Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING;
        shaderResourceViewDescription.Texture2D.MipLevels = texture.MipLevels;

        texture.StateChange(CommandList, ResourceStates.GenericRead);
        GraphicsDevice.CBVSRVUAVHeap.GetTempHandle(out CpuDescriptorHandle cpuHandle, out GpuDescriptorHandle gpuHandle);
        GraphicsDevice.Device.CreateShaderResourceView(texture.Resource, shaderResourceViewDescription, cpuHandle);
        CommandList.SetGraphicsRootDescriptorTable(CurrentRootSignature.ShaderResourceView[slot], gpuHandle);
    }

    public void SetDepthStencilViewRenderTextureView(Texture2D dsv, Texture2D[] rtvs, bool clearDSV, bool clearRTV)
    {
        dsv?.StateChange(CommandList, ResourceStates.DepthWrite);
        CpuDescriptorHandle[] rtvHandles = null;
        if (rtvs != null)
        {
            rtvHandles = new CpuDescriptorHandle[rtvs.Length];
            for (int i = 0; i < rtvs.Length; i++)
            {
                Texture2D rtv = rtvs[i];
                rtv.StateChange(CommandList, ResourceStates.RenderTarget);
                rtvHandles[i] = rtv.RenderTargetView.GetCPUDescriptorHandleForHeapStart();
            }
        }
        if (clearDSV && dsv != null)
            CommandList.ClearDepthStencilView(dsv.DepthStencilView.GetCPUDescriptorHandleForHeapStart(), ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);
        if (clearRTV && rtvs != null)
        {
            foreach (var rtv in rtvs)
                CommandList.ClearRenderTargetView(rtv.RenderTargetView.GetCPUDescriptorHandleForHeapStart(), new Color4());
        }

        CommandList.OMSetRenderTargets(rtvHandles, dsv.DepthStencilView.GetCPUDescriptorHandleForHeapStart());
    }

    public void SetRenderTargetScreen()
    {
        CommandList.RSSetViewport(new Viewport(0, 0, GraphicsDevice.Width, GraphicsDevice.Height, 0.0f, 1.0f));
        CommandList.RSSetScissorRect(new RectI(0, 0, GraphicsDevice.Width, GraphicsDevice.Height));
        CommandList.OMSetRenderTargets(GraphicsDevice.GetRenderTargetScreen());
    }

    public void SetMesh(Mesh mesh)
    {
        CommandList.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);

        int c = -1;
        foreach (var description in mesh.UnnamedInputLayout.InputElementDescriptions)
        {
            if (description.Slot != c)
            {
                if (mesh.Vertices != null && mesh.Vertices.TryGetValue(description.SemanticName, out var vertex))
                {
                    CommandList.IASetVertexBuffers(description.Slot, new VertexBufferView(vertex.resource.GPUVirtualAddress + (ulong)vertex.offset, vertex.sizeInByte - vertex.offset, vertex.stride));
                }
                c = description.Slot;
            }
        }

        if (mesh.Index != null)
            CommandList.IASetIndexBuffer(new IndexBufferView(mesh.Index.GPUVirtualAddress, mesh.IndexSizeInByte, mesh.IndexFormat));
        UnnamedInputLayout = mesh.UnnamedInputLayout;
    }

    public void UploadTexture(Texture2D texture, byte[] data)
    {
        ID3D12Resource resourceUpload1 = GraphicsDevice.Device.CreateCommittedResource<ID3D12Resource>(
            new HeapProperties(HeapType.Upload),
            HeapFlags.None,
            ResourceDescription.Buffer((ulong)data.Length),
            ResourceStates.GenericRead);
        GraphicsDevice.DestroyResource(resourceUpload1);
        GraphicsDevice.DestroyResource(texture.Resource);
        texture.Resource = GraphicsDevice.Device.CreateCommittedResource<ID3D12Resource>(
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
        UpdateSubresources(CommandList, texture.Resource, resourceUpload1, 0, 0, 1, new SubresourceData[] { subresourcedata });
        gcHandle.Free();
        CommandList.ResourceBarrierTransition(texture.Resource, ResourceStates.CopyDest, ResourceStates.GenericRead);
        texture.ResourceStates = ResourceStates.GenericRead;
    }

    public void SetConstantBufferView(UploadBuffer uploadBuffer, int offset, int slot)
    {
        CommandList.SetGraphicsRootConstantBufferView(CurrentRootSignature.ConstantBufferView[slot], uploadBuffer.resource.GPUVirtualAddress + (ulong)offset);
    }

    public void SetPipelineState(PipelineStateObject pipelineStateObject, PipelineStateObjectDescription psoDesc)
    {
        this.PipelineStateObject = pipelineStateObject;
        this.PipelineStateObjectDescription = psoDesc;
    }

    public void ClearRenderTarget(Texture2D texture2D)
    {
        CommandList.ClearRenderTargetView(texture2D.RenderTargetView.GetCPUDescriptorHandleForHeapStart(), new Color4(0, 0, 0, 0));
    }

    public void ClearRenderTargetScreen(Color4 color)
    {
        CommandList.ClearRenderTargetView(GraphicsDevice.GetRenderTargetScreen(), color);
    }

    public void ScreenBeginRender()
    {
        CommandList.ResourceBarrierTransition(GraphicsDevice.GetScreenResource(), ResourceStates.Present, ResourceStates.RenderTarget);
    }

    public void ScreenEndRender()
    {
        CommandList.ResourceBarrierTransition(GraphicsDevice.GetScreenResource(), ResourceStates.RenderTarget, ResourceStates.Present);
    }

    public void BeginCommand()
    {
        CommandList.Reset(GraphicsDevice.GetCommandAllocator());
    }

    public void EndCommand()
    {
        CommandList.Close();
    }

    public void Execute()
    {
        GraphicsDevice.CommandQueue.ExecuteCommandList(CommandList);
    }

    private unsafe struct MemoryCopyDestination
    {
        public void* pData;
        public ulong RowPitch;
        public ulong SlicePitch;
    }

    private unsafe void MemoryCopySubresource(
        MemoryCopyDestination* pDest,
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

    private unsafe ulong UpdateSubresources(
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
            MemoryCopyDestination DestData = new MemoryCopyDestination { pData = pData + pLayouts[i].Offset, RowPitch = (ulong)pLayouts[i].Footprint.RowPitch, SlicePitch = (uint)(pLayouts[i].Footprint.RowPitch) * (uint)(pNumRows[i]) };
            MemoryCopySubresource(&DestData, pSrcData[i], (int)(pRowSizesInBytes[i]), pNumRows[i], pLayouts[i].Footprint.Depth);
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

    private ulong UpdateSubresources(
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

    public ID3D12GraphicsCommandList5 CommandList;
    public GraphicsDevice GraphicsDevice;

    private void ThrowIfFailed(Result hr)
    {
        if (hr != Result.Ok)
        {
            throw new NotImplementedException();
        }
    }

    public RootSignature CurrentRootSignature;
    public PipelineStateObject PipelineStateObject;
    public PipelineStateObjectDescription PipelineStateObjectDescription;
    public UnnamedInputLayout UnnamedInputLayout;

    public void Dispose()
    {
        CommandList?.Dispose();
        CommandList = null;
    }
}
