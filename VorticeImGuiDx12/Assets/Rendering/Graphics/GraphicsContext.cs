using System;
using System.Runtime.InteropServices;

using SharpGen.Runtime;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.Mathematics;

namespace Engine.Graphics;

public sealed partial class GraphicsContext : IDisposable
{
    public ID3D12GraphicsCommandList5 CommandList;
    public GraphicsDevice GraphicsDevice;

    public RootSignature CurrentRootSignature;
    public PipelineStateObject PipelineStateObject;
    public PipelineStateObjectDescription PipelineStateObjectDescription;
    public UnnamedInputLayout UnnamedInputLayout;

    public void Initialize(GraphicsDevice graphicsDevice)
    {
        ThrowIfFailed(graphicsDevice.Device.CreateCommandList(0, CommandListType.Direct, graphicsDevice.GetCommandAllocator(), null, out CommandList));
        CommandList.Close();

        GraphicsDevice = graphicsDevice;
    }

    private void ThrowIfFailed(Result result)
    {
        if (result.Failure)
            throw new NotImplementedException();
    }

    public void Dispose()
    {
        CommandList?.Dispose();
        CommandList = null;
    }
}

public sealed partial class GraphicsContext : IDisposable
{
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

        ShaderResourceViewDescription shaderResourceViewDescription = new()
        {
            ViewDimension = Vortice.Direct3D12.ShaderResourceViewDimension.Texture2D,
            Format = texture.Format,
            Shader4ComponentMapping = D3D12_DEFAULT_SHADER_4_COMPONENT_MAPPING,
        };
        shaderResourceViewDescription.Texture2D.MipLevels = texture.MipLevels;

        texture.StateChange(CommandList, ResourceStates.GenericRead);

        GraphicsDevice.CBVSRVUAVHeap.GetTempHandle(out CpuDescriptorHandle CPUHandle, out GpuDescriptorHandle GPUHandle);
        GraphicsDevice.Device.CreateShaderResourceView(texture.Resource, shaderResourceViewDescription, CPUHandle);

        CommandList.SetGraphicsRootDescriptorTable(CurrentRootSignature.ShaderResourceView[slot], GPUHandle);
    }

    public void SetDepthStencilViewRenderTextureView(Texture2D depthStencilView, Texture2D[] renderTextureViews, bool clearDepthStencilView, bool clearRenderTextureView)
    {
        depthStencilView?.StateChange(CommandList, ResourceStates.DepthWrite);

        CpuDescriptorHandle[] renderTextureViewHandles = null;
        if (renderTextureViews is not null)
        {
            renderTextureViewHandles = new CpuDescriptorHandle[renderTextureViews.Length];
            for (int i = 0; i < renderTextureViews.Length; i++)
            {
                Texture2D renderTextureView = renderTextureViews[i];
                renderTextureView.StateChange(CommandList, ResourceStates.RenderTarget);
                renderTextureViewHandles[i] = renderTextureView.RenderTargetView.GetCPUDescriptorHandleForHeapStart();
            }
        }

        if (clearDepthStencilView && depthStencilView is not null)
            CommandList.ClearDepthStencilView(depthStencilView.DepthStencilView.GetCPUDescriptorHandleForHeapStart(), ClearFlags.Depth | ClearFlags.Stencil, 1.0f, 0);

        if (clearRenderTextureView && renderTextureViews is not null)
            foreach (var rtv in renderTextureViews)
                CommandList.ClearRenderTargetView(rtv.RenderTargetView.GetCPUDescriptorHandleForHeapStart(), new Color4());

        CommandList.OMSetRenderTargets(renderTextureViewHandles, depthStencilView.DepthStencilView.GetCPUDescriptorHandleForHeapStart());
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

        SubresourceData subresourcedata = new()
        {
            Data = Marshal.UnsafeAddrOfPinnedArrayElement(data, 0),
            RowPitch = (IntPtr)(width * bitsPerPixel / 8),
            SlicePitch = (IntPtr)(width * height * bitsPerPixel / 8),
        };
        UpdateSubresources(CommandList, texture.Resource, resourceUpload1, 0, 0, 1, [subresourcedata]);

        GCHandle gcHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        gcHandle.Free();

        CommandList.ResourceBarrierTransition(texture.Resource, ResourceStates.CopyDest, ResourceStates.GenericRead);
        texture.ResourceStates = ResourceStates.GenericRead;
    }

    public void SetConstantBufferView(UploadBuffer uploadBuffer, int offset, int slot)
    {
        CommandList.SetGraphicsRootConstantBufferView(CurrentRootSignature.ConstantBufferView[slot], uploadBuffer.resource.GPUVirtualAddress + (ulong)offset);
    }

    public void SetPipelineState(PipelineStateObject pipelineStateObject, PipelineStateObjectDescription pipelineStateObjectDescription)
    {
        PipelineStateObject = pipelineStateObject;
        PipelineStateObjectDescription = pipelineStateObjectDescription;
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
}

public sealed partial class GraphicsContext : IDisposable
{
    private unsafe struct MemoryCopyDestination
    {
        public void* Data;
        public ulong RowPitch;
        public ulong SlicePitch;
    }

    private unsafe void MemoryCopySubresource(
        MemoryCopyDestination* destination,
        SubresourceData source,
        int rowSizeInBytes,
        int numberOfRows,
        int numberOfSlices)
    {
        for (uint i = 0; i < numberOfSlices; ++i)
        {
            byte* destinationSlice = (byte*)destination->Data + destination->SlicePitch * i;
            byte* sourceSlice = (byte*)source.Data + source.SlicePitch * i;

            for (int y = 0; y < numberOfRows; ++y)
            {
                var sourceSpan = new Span<byte>(sourceSlice + ((long)source.RowPitch * y), rowSizeInBytes);
                var destinationSpan = new Span<byte>(destinationSlice + (long)destination->RowPitch * y, rowSizeInBytes);

                sourceSpan.CopyTo(destinationSpan);
            }
        }
    }

    private unsafe ulong UpdateSubresources(
        ID3D12GraphicsCommandList commandList,
        ID3D12Resource destinationResource,
        ID3D12Resource intermediate,
        int firstSubresource,
        int numberOfSubresources,
        ulong requiredSize,
        PlacedSubresourceFootPrint[] layouts,
        int[] numberOfRows,
        ulong[] rowSizesInBytes,
        SubresourceData[] sourceData)
    {
        var IntermediateDescription = intermediate.Description;
        var DestinationDescription = destinationResource.Description;

        if (IntermediateDescription.Dimension != ResourceDimension.Buffer
         || IntermediateDescription.Width < requiredSize + layouts[0].Offset
         || (DestinationDescription.Dimension == ResourceDimension.Buffer
         && (firstSubresource != 0 || numberOfSubresources != 1)))
            return 0;

        void* pointer = null;
        intermediate.Map(0, &pointer);
        IntPtr data1 = new IntPtr(pointer);

        byte* pData;
        pData = (byte*)data1;

        for (uint i = 0; i < numberOfSubresources; ++i)
        {
            MemoryCopyDestination destinationData = new()
            {
                Data = pData + layouts[i].Offset,
                RowPitch = (ulong)layouts[i].Footprint.RowPitch,
                SlicePitch = (uint)(layouts[i].Footprint.RowPitch) * (uint)(numberOfRows[i])
            };
            MemoryCopySubresource(&destinationData, sourceData[i], (int)(rowSizesInBytes[i]), numberOfRows[i], layouts[i].Footprint.Depth);
        }
        intermediate.Unmap(0, null);

        if (DestinationDescription.Dimension == ResourceDimension.Buffer)
        {
            commandList.CopyBufferRegion(
                destinationResource, 0, intermediate, layouts[0].Offset, (ulong)layouts[0].Footprint.Width);
        }
        else
        {
            for (int i = 0; i < numberOfSubresources; ++i)
            {
                TextureCopyLocation Dst = new TextureCopyLocation(destinationResource, i + firstSubresource);
                TextureCopyLocation Src = new TextureCopyLocation(intermediate, layouts[i]);
                commandList.CopyTextureRegion(Dst, 0, 0, 0, Src, null);
            }
        }

        return requiredSize;
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
}
