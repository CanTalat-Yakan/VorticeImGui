﻿using System.Collections.Generic;

using Vortice.Direct3D12;

namespace Engine.DataTypes;

public enum RootSignatureParamP
{
    CBV,
    SRV,
    UAV,
    CBVTable,
    SRVTable,
    UAVTable,
}

public sealed class RootSignature : IDisposable
{
    public Dictionary<int, int> ConstantBufferView = new();
    public Dictionary<int, int> ShaderResourceView = new();
    public Dictionary<int, int> UnorderedAccessView = new();

    public ID3D12RootSignature Resource;
    public string Name;

    public void Dispose()
    {
        Resource?.Dispose();
        Resource = null;
    }
}