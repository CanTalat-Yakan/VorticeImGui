using System;

using Vortice.Direct3D12;

namespace Engine.Rendering;

public class DescriptorHeapX : IDisposable
{
    public GraphicsDevice graphicsDevice;
    public ID3D12DescriptorHeap heap;

    public uint allocatedCount;
    public uint descriptorCount;
    public uint IncrementSize;

    public void Initialize(GraphicsDevice graphicsDevice, DescriptorHeapDescription descriptorHeapDescription)
    {
        this.graphicsDevice = graphicsDevice;
        allocatedCount = 0;
        descriptorCount = (uint)descriptorHeapDescription.DescriptorCount;
        GraphicsDevice.ThrowIfFailed(graphicsDevice.device.CreateDescriptorHeap(descriptorHeapDescription, out heap));
        IncrementSize = (uint)graphicsDevice.device.GetDescriptorHandleIncrementSize(descriptorHeapDescription.Type);
    }


    public void GetTempHandle(out CpuDescriptorHandle cpuHandle, out GpuDescriptorHandle gpuHandle)
    {
        CpuDescriptorHandle cpuHandle1 = heap.GetCPUDescriptorHandleForHeapStart();
        cpuHandle1.Ptr += allocatedCount * IncrementSize;
        GpuDescriptorHandle gpuHandle1 = heap.GetGPUDescriptorHandleForHeapStart();
        gpuHandle1.Ptr += (ulong)(allocatedCount * IncrementSize);

        allocatedCount = (allocatedCount + 1) % descriptorCount;
        cpuHandle = cpuHandle1;
        gpuHandle = gpuHandle1;
    }


    public CpuDescriptorHandle GetTempCpuHandle()
    {
        CpuDescriptorHandle cpuHandle1 = heap.GetCPUDescriptorHandleForHeapStart();
        cpuHandle1.Ptr += allocatedCount * IncrementSize;

        allocatedCount = (allocatedCount + 1) % descriptorCount;
        return cpuHandle1;
    }

    public void Dispose()
    {
        heap?.Dispose();
        heap = null;
    }
}
