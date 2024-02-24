using System;
using System.Collections.Generic;
using System.Threading;

using Vortice.Direct3D12;
using Vortice.Direct3D12.Debug;
using Vortice.DXGI;
using Vortice.Mathematics;

using Engine.Helper;

namespace Engine.Graphics;

public sealed partial class GraphicsDevice : IDisposable
{
    public ID3D12Device2 Device;
    public IDXGIAdapter Adapter;
    public IDXGIFactory7 DXGIFactory;
    public ID3D12CommandQueue CommandQueue;
    public DescriptorHeapX CBVSRVUAVHeap = new();
    public DescriptorHeapX DepthStencilViewHeap = new();
    public DescriptorHeapX RenderTextureViewHeap = new();
    public IDXGISwapChain3 SwapChain;
    public List<ID3D12CommandAllocator> CommandAllocators;
    public EventWaitHandle WaitHandle;
    public ID3D12Fence Fence;

    public struct ResourceDelayDestroy(ID3D12Object resource, ulong destroyFrame)
    {
        public ID3D12Object Resource = resource;
        public ulong DestroyFrame = destroyFrame;
    }

    public Queue<ResourceDelayDestroy> DelayDestroy = new();

    public int ExecuteIndex = 0;
    public ulong ExecuteCount = 3;//greater equal than 'bufferCount'

    public int Width;
    public int Height;
    public IntPtr WindowHandle;

    public Format SwapChainFormat = Format.R8G8B8A8_UNorm;
    public List<ID3D12Resource> ScreenResources;

    public int BufferCount = 3;
    public int CBVSRVUAVDescriptorCount = 65536;

    public void Initialize()
    {
#if DEBUG
        if (D3D12.D3D12GetDebugInterface<ID3D12Debug>(out var pDx12Debug).Success)
            pDx12Debug.EnableDebugLayer();
#endif

        DXGI.CreateDXGIFactory1(out DXGIFactory).ThrowIfFailed();

        int index1 = 0;
        while (true)
        {
            var result = DXGIFactory.EnumAdapterByGpuPreference(index1, GpuPreference.HighPerformance, out Adapter);
            if (result.Success)
                break;

            index1++;
        }
        D3D12.D3D12CreateDevice(this.Adapter, out Device).ThrowIfFailed();

        CommandQueueDescription description = new()
        {
            Flags = CommandQueueFlags.None,
            Type = CommandListType.Direct,
            NodeMask = 0,
            Priority = 0,
        };
        Device.CreateCommandQueue(description, out CommandQueue).ThrowIfFailed();

        DescriptorHeapDescription descriptorHeapDescription = new()
        {
            DescriptorCount = CBVSRVUAVDescriptorCount,
            Type = DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
            Flags = DescriptorHeapFlags.ShaderVisible,
            NodeMask = 0,
        };
        CBVSRVUAVHeap.Initialize(this, descriptorHeapDescription);

        descriptorHeapDescription = new()
        {
            DescriptorCount = 64,
            Type = DescriptorHeapType.DepthStencilView,
            Flags = DescriptorHeapFlags.None,
        };
        DepthStencilViewHeap.Initialize(this, descriptorHeapDescription);

        descriptorHeapDescription = new()
        {
            DescriptorCount = 64,
            Type = DescriptorHeapType.RenderTargetView,
            Flags = DescriptorHeapFlags.None,
        };
        RenderTextureViewHeap.Initialize(this, descriptorHeapDescription);
        WaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset);

        CommandAllocators = new List<ID3D12CommandAllocator>();
        for (int i = 0; i < BufferCount; i++)
        {
            Device.CreateCommandAllocator(CommandListType.Direct, out ID3D12CommandAllocator commandAllocator).ThrowIfFailed();

            CommandAllocators.Add(commandAllocator);
        }

        Device.CreateFence(ExecuteCount, FenceFlags.None, out Fence).ThrowIfFailed();

        ExecuteCount++;
    }

    public void SetupSwapChain(IntPtr hwnd) =>
        WindowHandle = hwnd;

    public void Resize(int width, int height)
    {
        WaitForGPU();

        Width = Math.Max(width, 1);
        Height = Math.Max(height, 1);

        if (SwapChain is null)
        {
            SwapChainDescription1 swapChainDescription = new()
            {
                Width = width,
                Height = height,
                Format = SwapChainFormat,
                Stereo = false,
                SampleDescription = new() { Count = 1, Quality = 0 },
                BufferUsage = Usage.RenderTargetOutput,
                BufferCount = BufferCount,
                SwapEffect = SwapEffect.FlipDiscard,
                Flags = SwapChainFlags.AllowTearing,
                Scaling = Scaling.Stretch,
                AlphaMode = AlphaMode.Ignore,
            };
            IDXGISwapChain1 swapChain1 = DXGIFactory.CreateSwapChainForHwnd(CommandQueue, WindowHandle, swapChainDescription);

            SwapChain = swapChain1.QueryInterface<IDXGISwapChain3>();

            swapChain1.Dispose();
        }
        else
        {
            foreach (var screenResource in ScreenResources)
                screenResource.Dispose();

            SwapChain.ResizeBuffers(BufferCount, width, height, SwapChainFormat, SwapChainFlags.AllowTearing).ThrowIfFailed();
        }

        ScreenResources = new List<ID3D12Resource>();
        for (int i = 0; i < BufferCount; i++)
        {
            SwapChain.GetBuffer(i, out ID3D12Resource res).ThrowIfFailed();

            ScreenResources.Add(res);
        }
    }

    public void Dispose()
    {
        WaitForGPU();

        while (DelayDestroy.Count > 0)
        {
            var p = DelayDestroy.Dequeue();
            p.Resource?.Dispose();
        }

        foreach (var commandAllocator in CommandAllocators)
            commandAllocator.Dispose();
        
        if (ScreenResources != null)
            foreach (var screenResource in ScreenResources)
                screenResource.Dispose();

        DXGIFactory?.Dispose();
        CommandQueue?.Dispose();
        CBVSRVUAVHeap?.Dispose();
        DepthStencilViewHeap?.Dispose();
        RenderTextureViewHeap?.Dispose();
        SwapChain?.Dispose();
        Fence?.Dispose();
        Device?.Dispose();
        Adapter?.Dispose();
    }
}

public sealed partial class GraphicsDevice : IDisposable
{
    public void Begin() =>
        GetCommandAllocator().Reset();

    public void Present(bool vsync)
    {
        if (vsync)
            SwapChain.Present(1, PresentFlags.None).ThrowIfFailed();
        else
            SwapChain.Present(0, PresentFlags.AllowTearing).ThrowIfFailed();

        CommandQueue.Signal(Fence, ExecuteCount);

        ExecuteIndex = (ExecuteIndex + 1) % BufferCount;

        if (Fence.CompletedValue < ExecuteCount - (uint)BufferCount + 1)
        {
            Fence.SetEventOnCompletion(ExecuteCount - (uint)BufferCount + 1, WaitHandle);
            WaitHandle.WaitOne();
        }

        DestroyResourceInternal(Fence.CompletedValue);

        ExecuteCount++;
    }

    public void WaitForGPU()
    {
        CommandQueue.Signal(Fence, ExecuteCount);

        Fence.SetEventOnCompletion(ExecuteCount, WaitHandle);
        WaitHandle.WaitOne();

        DestroyResourceInternal(Fence.CompletedValue);

        ExecuteCount++;
    }
}

public sealed partial class GraphicsDevice : IDisposable
{
    public void CreateRootSignature(RootSignature rootSignature, IList<RootSignatureParamP> types)
    {
        // Static Samplers.
        var samplerDescription = new StaticSamplerDescription[4];

        samplerDescription[0] = new(ShaderVisibility.All, 0, 0)
        {
            AddressU = TextureAddressMode.Clamp,
            AddressV = TextureAddressMode.Clamp,
            AddressW = TextureAddressMode.Clamp,
            BorderColor = StaticBorderColor.OpaqueBlack,
            ComparisonFunction = ComparisonFunction.Never,
            Filter = Filter.MinMagMipLinear,
            MipLODBias = 0,
            MaxAnisotropy = 0,
            MinLOD = 0,
            MaxLOD = float.MaxValue,
            ShaderVisibility = ShaderVisibility.All,
            RegisterSpace = 0,
            ShaderRegister = 0,
        };
        samplerDescription[1] = samplerDescription[0];
        samplerDescription[2] = samplerDescription[0];
        samplerDescription[3] = samplerDescription[0];

        samplerDescription[1].ShaderRegister = 1;
        samplerDescription[2].ShaderRegister = 2;
        samplerDescription[3].ShaderRegister = 3;

        samplerDescription[1].MaxAnisotropy = 16;
        samplerDescription[1].Filter = Filter.Anisotropic;

        samplerDescription[2].ComparisonFunction = ComparisonFunction.Less;
        samplerDescription[2].Filter = Filter.ComparisonMinMagMipLinear;

        samplerDescription[3].Filter = Filter.MinMagMipPoint;

        RootParameter1[] rootParameters = new RootParameter1[types.Count];

        int cbvCount = 0;
        int srvCount = 0;
        int uavCount = 0;
        rootSignature.ConstantBufferView.Clear();
        rootSignature.ShaderResourceView.Clear();
        rootSignature.UnorderedAccessView.Clear();

        for (int i = 0; i < types.Count; i++)
        {
            RootSignatureParamP t = types[i];
            switch (t)
            {
                case RootSignatureParamP.CBV:
                    rootParameters[i] = new RootParameter1(RootParameterType.ConstantBufferView, new RootDescriptor1(cbvCount, 0), ShaderVisibility.All);
                    rootSignature.ConstantBufferView[cbvCount] = i;
                    cbvCount++;
                    break;
                case RootSignatureParamP.SRV:
                    rootParameters[i] = new RootParameter1(RootParameterType.ShaderResourceView, new RootDescriptor1(srvCount, 0), ShaderVisibility.All);
                    rootSignature.ShaderResourceView[srvCount] = i;
                    srvCount++;
                    break;
                case RootSignatureParamP.UAV:
                    rootParameters[i] = new RootParameter1(RootParameterType.UnorderedAccessView, new RootDescriptor1(uavCount, 0), ShaderVisibility.All);
                    rootSignature.UnorderedAccessView[uavCount] = i;
                    uavCount++;
                    break;
                case RootSignatureParamP.CBVTable:
                    rootParameters[i] = new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.ConstantBufferView, 1, cbvCount)), ShaderVisibility.All);
                    rootSignature.ConstantBufferView[cbvCount] = i;
                    cbvCount++;
                    break;
                case RootSignatureParamP.SRVTable:
                    rootParameters[i] = new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.ShaderResourceView, 1, srvCount)), ShaderVisibility.All);
                    rootSignature.ShaderResourceView[srvCount] = i;
                    srvCount++;
                    break;
                case RootSignatureParamP.UAVTable:
                    rootParameters[i] = new RootParameter1(new RootDescriptorTable1(new DescriptorRange1(DescriptorRangeType.UnorderedAccessView, 1, uavCount)), ShaderVisibility.All);
                    rootSignature.UnorderedAccessView[uavCount] = i;
                    uavCount++;
                    break;
            }
        }

        RootSignatureDescription1 rootSignatureDescription = new()
        {
            StaticSamplers = samplerDescription,
            Flags = RootSignatureFlags.AllowInputAssemblerInputLayout,
            Parameters = rootParameters,
        };

        rootSignature.Resource = Device.CreateRootSignature<ID3D12RootSignature>(0, rootSignatureDescription);
    }

    public void CreateRenderTexture(Texture2D texture)
    {
        ResourceDescription resourceDescription = new()
        {
            Width = (ulong)texture.Width,
            Height = texture.Height,
            MipLevels = 1,
            SampleDescription = new SampleDescription(1, 0),
            Dimension = ResourceDimension.Texture2D,
            DepthOrArraySize = 1,
            Format = texture.Format,
        };

        if (texture.DepthStencilViewFormat != 0)
        {
            DestroyResource(texture.Resource);
            resourceDescription.Flags = ResourceFlags.AllowDepthStencil;

            Device.CreateCommittedResource<ID3D12Resource>(HeapProperties.DefaultHeapProperties,
                 HeapFlags.None,
                 resourceDescription,
                 ResourceStates.GenericRead,
                 new ClearValue(texture.DepthStencilViewFormat, new DepthStencilValue(1.0f, 0)), out texture.Resource)
            .ThrowIfFailed();

            if (texture.DepthStencilView is null)
            {
                DescriptorHeapDescription descriptorHeapDescription = new()
                {
                    DescriptorCount = 1,
                    Type = DescriptorHeapType.DepthStencilView,
                    Flags = DescriptorHeapFlags.None,
                    NodeMask = 0,
                };
                Device.CreateDescriptorHeap(descriptorHeapDescription, out texture.DepthStencilView).ThrowIfFailed();
            }

            Device.CreateDepthStencilView(texture.Resource, null, texture.DepthStencilView.GetCPUDescriptorHandleForHeapStart());
        }
        else if (texture.RenderTextureViewFormat != 0)
        {
            DestroyResource(texture.Resource);
            resourceDescription.Flags = ResourceFlags.AllowRenderTarget | ResourceFlags.AllowUnorderedAccess;
            
            Device.CreateCommittedResource<ID3D12Resource>(HeapProperties.DefaultHeapProperties,
                 HeapFlags.None,
                 resourceDescription,
                 ResourceStates.GenericRead,
                 new ClearValue(texture.DepthStencilViewFormat, new Color4(0, 0, 0, 0)), out texture.Resource)
                .ThrowIfFailed();

            if (texture.RenderTargetView is null)
            {
                DescriptorHeapDescription descriptorHeapDescription = new()
                {
                    DescriptorCount = 1,
                    Type = DescriptorHeapType.RenderTargetView,
                    Flags = DescriptorHeapFlags.None,
                    NodeMask = 0,
                };
                Device.CreateDescriptorHeap(descriptorHeapDescription, out texture.RenderTargetView).ThrowIfFailed();
            }

            Device.CreateRenderTargetView(texture.Resource, null, texture.RenderTargetView.GetCPUDescriptorHandleForHeapStart());
        }
        else
            throw new NotImplementedException();

        texture.ResourceStates = ResourceStates.GenericRead;
    }

    public void CreateUploadBuffer(UploadBuffer uploadBuffer, int size)
    {
        DestroyResource(uploadBuffer.Resource);

        uploadBuffer.Resource = Device.CreateCommittedResource<ID3D12Resource>(
            HeapProperties.UploadHeapProperties,
            HeapFlags.None,
            ResourceDescription.Buffer(new ResourceAllocationInfo((ulong)size, 0)),
            ResourceStates.GenericRead);
        uploadBuffer.Size = size;
    }

    public void DestroyResource(ID3D12Object resource)
    {
        if (resource is not null)
            DelayDestroy.Enqueue(new ResourceDelayDestroy(resource, ExecuteCount));
    }

    private void DestroyResourceInternal(ulong completedFrame)
    {
        while (DelayDestroy.Count > 0)
            if (DelayDestroy.Peek().DestroyFrame <= completedFrame)
            {
                var p = DelayDestroy.Dequeue();
                p.Resource?.Dispose();
            }
            else
                break;
    }
}

public sealed partial class GraphicsDevice : IDisposable
{
    public static uint GetBitsPerPixel(Format format)
    {
        switch (format)
        {
            case Format.R32G32B32A32_Typeless:
            case Format.R32G32B32A32_Float:
            case Format.R32G32B32A32_UInt:
            case Format.R32G32B32A32_SInt:
                return 128;

            case Format.R32G32B32_Typeless:
            case Format.R32G32B32_Float:
            case Format.R32G32B32_UInt:
            case Format.R32G32B32_SInt:
                return 96;

            case Format.R16G16B16A16_Typeless:
            case Format.R16G16B16A16_Float:
            case Format.R16G16B16A16_UNorm:
            case Format.R16G16B16A16_UInt:
            case Format.R16G16B16A16_SNorm:
            case Format.R16G16B16A16_SInt:
            case Format.R32G32_Typeless:
            case Format.R32G32_Float:
            case Format.R32G32_UInt:
            case Format.R32G32_SInt:
            case Format.R32G8X24_Typeless:
            case Format.D32_Float_S8X24_UInt:
            case Format.R32_Float_X8X24_Typeless:
            case Format.X32_Typeless_G8X24_UInt:
            case Format.Y416:
            case Format.Y210:
            case Format.Y216:
                return 64;

            case Format.R10G10B10A2_Typeless:
            case Format.R10G10B10A2_UNorm:
            case Format.R10G10B10A2_UInt:
            case Format.R11G11B10_Float:
            case Format.R8G8B8A8_Typeless:
            case Format.R8G8B8A8_UNorm:
            case Format.R8G8B8A8_UNorm_SRgb:
            case Format.R8G8B8A8_UInt:
            case Format.R8G8B8A8_SNorm:
            case Format.R8G8B8A8_SInt:
            case Format.R16G16_Typeless:
            case Format.R16G16_Float:
            case Format.R16G16_UNorm:
            case Format.R16G16_UInt:
            case Format.R16G16_SNorm:
            case Format.R16G16_SInt:
            case Format.R32_Typeless:
            case Format.D32_Float:
            case Format.R32_Float:
            case Format.R32_UInt:
            case Format.R32_SInt:
            case Format.R24G8_Typeless:
            case Format.D24_UNorm_S8_UInt:
            case Format.R24_UNorm_X8_Typeless:
            case Format.X24_Typeless_G8_UInt:
            case Format.R9G9B9E5_SharedExp:
            case Format.R8G8_B8G8_UNorm:
            case Format.G8R8_G8B8_UNorm:
            case Format.B8G8R8A8_UNorm:
            case Format.B8G8R8X8_UNorm:
            case Format.R10G10B10_Xr_Bias_A2_UNorm:
            case Format.B8G8R8A8_Typeless:
            case Format.B8G8R8A8_UNorm_SRgb:
            case Format.B8G8R8X8_Typeless:
            case Format.B8G8R8X8_UNorm_SRgb:
            case Format.AYUV:
            case Format.Y410:
            case Format.YUY2:
                return 32;

            case Format.P010:
            case Format.P016:
                return 24;

            case Format.R8G8_Typeless:
            case Format.R8G8_UNorm:
            case Format.R8G8_UInt:
            case Format.R8G8_SNorm:
            case Format.R8G8_SInt:
            case Format.R16_Typeless:
            case Format.R16_Float:
            case Format.D16_UNorm:
            case Format.R16_UNorm:
            case Format.R16_UInt:
            case Format.R16_SNorm:
            case Format.R16_SInt:
            case Format.B5G6R5_UNorm:
            case Format.B5G5R5A1_UNorm:
            case Format.A8P8:
            case Format.B4G4R4A4_UNorm:
                return 16;

            case Format.NV12:
            //case Format.420_OPAQUE:
            case Format.Opaque420:
            case Format.NV11:
                return 12;

            case Format.R8_Typeless:
            case Format.R8_UNorm:
            case Format.R8_UInt:
            case Format.R8_SNorm:
            case Format.R8_SInt:
            case Format.A8_UNorm:
            case Format.AI44:
            case Format.IA44:
            case Format.P8:
                return 8;

            case Format.R1_UNorm:
                return 1;

            case Format.BC1_Typeless:
            case Format.BC1_UNorm:
            case Format.BC1_UNorm_SRgb:
            case Format.BC4_Typeless:
            case Format.BC4_UNorm:
            case Format.BC4_SNorm:
                return 4;

            case Format.BC2_Typeless:
            case Format.BC2_UNorm:
            case Format.BC2_UNorm_SRgb:
            case Format.BC3_Typeless:
            case Format.BC3_UNorm:
            case Format.BC3_UNorm_SRgb:
            case Format.BC5_Typeless:
            case Format.BC5_UNorm:
            case Format.BC5_SNorm:
            case Format.BC6H_Typeless:
            case Format.BC6H_Uf16:
            case Format.BC6H_Sf16:
            case Format.BC7_Typeless:
            case Format.BC7_UNorm:
            case Format.BC7_UNorm_SRgb:
                return 8;

            default:
                return 0;
        }
    }

    public ID3D12CommandAllocator GetCommandAllocator() =>
        CommandAllocators[ExecuteIndex];

    public CpuDescriptorHandle GetRenderTargetScreen()
    {
        CpuDescriptorHandle handle = RenderTextureViewHeap.GetTempCpuHandle();
        var resource = ScreenResources[SwapChain.CurrentBackBufferIndex];

        Device.CreateRenderTargetView(resource, null, handle);

        return handle;
    }

    public ID3D12Resource GetScreenResource() =>
        ScreenResources[SwapChain.CurrentBackBufferIndex];
}