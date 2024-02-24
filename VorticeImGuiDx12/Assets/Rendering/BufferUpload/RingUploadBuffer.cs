using System;
using System.Runtime.InteropServices;

using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Engine.Graphics;

public unsafe class RingUploadBuffer : UploadBuffer
{
    public IntPtr CPUResourcePointer;
    public ulong GPUResourcePointer;

    public int AllocateIndex = 0;

    public void Initialize(GraphicsDevice device, int size)
    {
        this.size = size;
        device.CreateUploadBuffer(this, size);

        void* pointer = null;
        resource.Map(0, &pointer);

        CPUResourcePointer = new nint(pointer);
        GPUResourcePointer = resource.GPUVirtualAddress;
    }

    public int Upload<T>(Span<T> data) where T : struct
    {
        int size1 = data.Length * Marshal.SizeOf(typeof(T));
        int afterAllocateIndex = AllocateIndex + ((size1 + 255) & ~255);
        if (afterAllocateIndex > size)
        {
            AllocateIndex = 0;
            afterAllocateIndex = AllocateIndex + ((size1 + 255) & ~255);
        }

        unsafe
        {
            data.CopyTo(new Span<T>((CPUResourcePointer + AllocateIndex).ToPointer(), data.Length));
        }

        int ofs = AllocateIndex;
        AllocateIndex = afterAllocateIndex % size;
        return ofs;
    }

    public void SetCBV(GraphicsContext graphicsContext, int offset, int slot) =>
        graphicsContext.SetCBV(this, offset, slot);

    public void UploadMeshIndex(GraphicsContext context, Mesh mesh, Span<byte> index, Format indexFormat)
    {
        var graphicsDevice = context.GraphicsDevice;
        var commandList = context.CommandList;

        int uploadMark2 = Upload(index);
        if (mesh.IndexFormat != indexFormat
            || mesh.IndexCount != index.Length / (indexFormat == Format.R32_UInt ? 4 : 2)
            || mesh.IndexSizeInByte != index.Length)
        {
            mesh.IndexFormat = indexFormat;
            mesh.IndexCount = index.Length / (indexFormat == Format.R32_UInt ? 4 : 2);
            mesh.IndexSizeInByte = index.Length;
            graphicsDevice.DestroyResource(mesh.Index);

            mesh.Index = graphicsDevice.Device.CreateCommittedResource<ID3D12Resource>(
                HeapProperties.DefaultHeapProperties,
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)index.Length),
                ResourceStates.CopyDest);
        }
        else
            commandList.ResourceBarrierTransition(mesh.Index, ResourceStates.GenericRead, ResourceStates.CopyDest);

        commandList.CopyBufferRegion(mesh.Index, 0, resource, (ulong)uploadMark2, (ulong)index.Length);
        commandList.ResourceBarrierTransition(mesh.Index, ResourceStates.CopyDest, ResourceStates.GenericRead);
    }

    public void UploadVertexBuffer(GraphicsContext context, ref ID3D12Resource resource1, Span<byte> vertex)
    {
        var graphicsDevice = context.GraphicsDevice;
        var commandList = context.CommandList;

        int uploadMark1 = Upload(vertex);
        graphicsDevice.DestroyResource(resource1);
        resource1 = graphicsDevice.Device.CreateCommittedResource<ID3D12Resource>(
            HeapProperties.DefaultHeapProperties,
            HeapFlags.None,
            ResourceDescription.Buffer((ulong)vertex.Length),
            ResourceStates.CopyDest);

        commandList.CopyBufferRegion(resource1, 0, resource, (ulong)uploadMark1, (ulong)vertex.Length);
        commandList.ResourceBarrierTransition(resource1, ResourceStates.CopyDest, ResourceStates.GenericRead);
    }
}
