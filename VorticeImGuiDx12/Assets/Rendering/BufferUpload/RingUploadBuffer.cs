using System;
using System.Runtime.InteropServices;

using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Engine.Rendering;

public unsafe class RingUploadBuffer : UploadBuffer
{
    public IntPtr cpuResPtr;
    public ulong gpuResPtr;
    public int allocateIndex = 0;


    public void Initialize(GraphicsDevice device, int size)
    {
        this.size = size;
        device.CreateUploadBuffer(this, size);

        void* pointer = null;
        resource.Map(0, &pointer);

        cpuResPtr = new nint(pointer);
        gpuResPtr = resource.GPUVirtualAddress;
    }

    public int Upload<T>(Span<T> data) where T : struct
    {
        int size1 = data.Length * Marshal.SizeOf(typeof(T));
        int afterAllocateIndex = allocateIndex + ((size1 + 255) & ~255);
        if (afterAllocateIndex > size)
        {
            allocateIndex = 0;
            afterAllocateIndex = allocateIndex + ((size1 + 255) & ~255);
        }
        unsafe
        {
            data.CopyTo(new Span<T>((cpuResPtr + allocateIndex).ToPointer(), data.Length));
        }

        int ofs = allocateIndex;
        allocateIndex = afterAllocateIndex % size;
        return ofs;
    }

    public void SetCBV(Renderer graphicsContext, int offset, int slot)
    {
        graphicsContext.SetCBV(this, offset, slot);
    }


    public void UploadMeshIndex(Renderer context, Mesh mesh, Span<byte> index, Format indexFormat)
    {
        var graphicsDevice = context.graphicsDevice;
        var commandList = context.commandList;


        int uploadMark2 = Upload(index);
        if (mesh.indexFormat != indexFormat
            || mesh.indexCount != index.Length / (indexFormat == Format.R32_UInt ? 4 : 2)
            || mesh.indexSizeInByte != index.Length)
        {
            mesh.indexFormat = indexFormat;
            mesh.indexCount = index.Length / (indexFormat == Format.R32_UInt ? 4 : 2);
            mesh.indexSizeInByte = index.Length;
            graphicsDevice.DestroyResource(mesh.index);

            mesh.index = graphicsDevice.device.CreateCommittedResource<ID3D12Resource>(
                HeapProperties.DefaultHeapProperties,
                HeapFlags.None,
                ResourceDescription.Buffer((ulong)index.Length),
                ResourceStates.CopyDest);
        }
        else
        {
            commandList.ResourceBarrierTransition(mesh.index, ResourceStates.GenericRead, ResourceStates.CopyDest);
        }

        commandList.CopyBufferRegion(mesh.index, 0, resource, (ulong)uploadMark2, (ulong)index.Length);
        commandList.ResourceBarrierTransition(mesh.index, ResourceStates.CopyDest, ResourceStates.GenericRead);
    }

    public void UploadVertexBuffer(Renderer context, ref ID3D12Resource resource1, Span<byte> vertex)
    {
        var graphicsDevice = context.graphicsDevice;
        var commandList = context.commandList;

        int uploadMark1 = Upload(vertex);
        graphicsDevice.DestroyResource(resource1);
        resource1 = graphicsDevice.device.CreateCommittedResource<ID3D12Resource>(
            HeapProperties.DefaultHeapProperties,
            HeapFlags.None,
            ResourceDescription.Buffer((ulong)vertex.Length),
            ResourceStates.CopyDest);

        commandList.CopyBufferRegion(resource1, 0, resource, (ulong)uploadMark1, (ulong)vertex.Length);
        commandList.ResourceBarrierTransition(resource1, ResourceStates.CopyDest, ResourceStates.GenericRead);
    }
}
