using Engine.Helper;
using System;

using Vortice.Direct3D12;

namespace Engine.Graphics;

public class DescriptorHeapX : IDisposable
{
    public GraphicsDevice GraphicsDevice;
    public ID3D12DescriptorHeap Heap;

    public uint AllocatedCount;
    public uint DescriptorCount;
    public uint IncrementSize;

    public void Initialize(GraphicsDevice graphicsDevice, DescriptorHeapDescription descriptorHeapDescription)
    {
        GraphicsDevice = graphicsDevice;

        AllocatedCount = 0;
        DescriptorCount = (uint)descriptorHeapDescription.DescriptorCount;

        GraphicsDevice.Device.CreateDescriptorHeap(descriptorHeapDescription, out Heap).ThrowIfFailed();

        IncrementSize = (uint)graphicsDevice.Device.GetDescriptorHandleIncrementSize(descriptorHeapDescription.Type);
    }

    public void GetTempHandle(out CpuDescriptorHandle cpuHandle, out GpuDescriptorHandle gpuHandle)
    {
        CpuDescriptorHandle cpuHandle1 = Heap.GetCPUDescriptorHandleForHeapStart();
        cpuHandle1.Ptr += AllocatedCount * IncrementSize;
        GpuDescriptorHandle gpuHandle1 = Heap.GetGPUDescriptorHandleForHeapStart();
        gpuHandle1.Ptr += (ulong)(AllocatedCount * IncrementSize);

        AllocatedCount = (AllocatedCount + 1) % DescriptorCount;
        cpuHandle = cpuHandle1;
        gpuHandle = gpuHandle1;
    }

    public CpuDescriptorHandle GetTempCpuHandle()
    {
        CpuDescriptorHandle cpuHandle1 = Heap.GetCPUDescriptorHandleForHeapStart();
        cpuHandle1.Ptr += AllocatedCount * IncrementSize;

        AllocatedCount = (AllocatedCount + 1) % DescriptorCount;
        return cpuHandle1;
    }

    public void Dispose()
    {
        Heap?.Dispose();
        Heap = null;
    }
}
