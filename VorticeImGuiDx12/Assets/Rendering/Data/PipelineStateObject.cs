using System;
using System.Collections.Generic;

using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Engine.Rendering;

public class PipelineStateObject : IDisposable
{
    public List<PSOBundle> PSOBundle = new List<PSOBundle>();

    public ReadOnlyMemory<byte> VertexShader;
    public ReadOnlyMemory<byte> GeometryShader;
    public ReadOnlyMemory<byte> PixelShader;

    public string Name;

    public PipelineStateObject(byte[] vertexShader, byte[] pixelShader)
    {
        this.VertexShader = vertexShader;
        this.PixelShader = pixelShader;
    }

    public PipelineStateObject(Shader vertexShader, Shader pixelShader)
    {
        this.VertexShader = vertexShader.CompiledCode;
        this.PixelShader = pixelShader.CompiledCode;
    }

    public ID3D12PipelineState GetState(GraphicsDevice device, PSODescription desc, RootSignature rootSignature, UnnamedInputLayout inputLayout)
    {
        foreach (var psoCombind in PSOBundle)
        {
            if (psoCombind.PSODescription == desc && psoCombind.RootSignature == rootSignature && psoCombind.UnnamedInputLayout == inputLayout)
            {
                if (psoCombind.PipelineState == null)
                    throw new Exception("pipeline state error");
                return psoCombind.PipelineState;
            }
        }
        InputLayoutDescription inputLayoutDescription = inputLayout.InputElementDescriptions;

        GraphicsPipelineStateDescription graphicsPipelineStateDescription = new GraphicsPipelineStateDescription();
        graphicsPipelineStateDescription.RootSignature = rootSignature.Resource;
        graphicsPipelineStateDescription.VertexShader = VertexShader;
        graphicsPipelineStateDescription.GeometryShader = GeometryShader;
        graphicsPipelineStateDescription.PixelShader = PixelShader;
        graphicsPipelineStateDescription.PrimitiveTopologyType = PrimitiveTopologyType.Triangle;
        graphicsPipelineStateDescription.InputLayout = inputLayoutDescription;
        graphicsPipelineStateDescription.DepthStencilFormat = desc.DepthStencilFormat;
        graphicsPipelineStateDescription.RenderTargetFormats = new Format[desc.RenderTargetCount];
        Array.Fill(graphicsPipelineStateDescription.RenderTargetFormats, desc.RenderTargetFormat);

        if (desc.BlendState == "Alpha")
            graphicsPipelineStateDescription.BlendState = BlendStateAlpha();
        else if (desc.BlendState == "Add")
            graphicsPipelineStateDescription.BlendState = BlendDescription.Additive;
        else
            graphicsPipelineStateDescription.BlendState = BlendDescription.Opaque;


        //graphicsPipelineStateDescription.DepthStencilState = new DepthStencilDescription(desc.DepthStencilFormat != Format.Unknown, desc.DepthStencilFormat != Format.Unknown);
        graphicsPipelineStateDescription.SampleMask = uint.MaxValue;
        var RasterizerState = new RasterizerDescription(CullMode.None, FillMode.Solid);
        RasterizerState.DepthBias = desc.DepthBias;
        RasterizerState.SlopeScaledDepthBias = desc.SlopeScaledDepthBias;
        graphicsPipelineStateDescription.RasterizerState = RasterizerState;

        var pipelineState = device.Device.CreateGraphicsPipelineState<ID3D12PipelineState>(graphicsPipelineStateDescription);
        if (pipelineState == null)
            throw new Exception("pipeline state error");
        PSOBundle.Add(new PSOBundle { PSODescription = desc, PipelineState = pipelineState, RootSignature = rootSignature, UnnamedInputLayout = inputLayout });
        return pipelineState;
    }

    BlendDescription BlendStateAlpha()
    {
        BlendDescription blendDescription = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha, Blend.One, Blend.InverseSourceAlpha);
        return blendDescription;
    }

    public void Dispose()
    {
        foreach (var combine in PSOBundle)
        {
            combine.PipelineState.Dispose();
        }
        PSOBundle.Clear();
    }
}

public class PSOBundle
{
    public PSODescription PSODescription;
    public RootSignature RootSignature;
    public ID3D12PipelineState PipelineState;
    public UnnamedInputLayout UnnamedInputLayout;
}

public struct PSODescription : IEquatable<PSODescription>
{
    public int RenderTargetCount;
    public Format RenderTargetFormat;
    public Format DepthStencilFormat;
    public string BlendState;
    public int DepthBias;
    public float SlopeScaledDepthBias;
    public CullMode CullMode;
    public string InputLayout;
    public PrimitiveTopologyType PrimitiveTopologyType;

    public override bool Equals(object obj)
    {
        return obj is PSODescription desc && Equals(desc);
    }

    public bool Equals(PSODescription other)
    {
        return RenderTargetCount == other.RenderTargetCount &&
               RenderTargetFormat == other.RenderTargetFormat &&
               DepthStencilFormat == other.DepthStencilFormat &&
               BlendState == other.BlendState &&
               DepthBias == other.DepthBias &&
               SlopeScaledDepthBias == other.SlopeScaledDepthBias &&
               CullMode == other.CullMode &&
               InputLayout == other.InputLayout &&
               PrimitiveTopologyType == other.PrimitiveTopologyType;
    }

    public override int GetHashCode()
    {
        HashCode hash = new HashCode();
        hash.Add(RenderTargetCount);
        hash.Add(RenderTargetFormat);
        hash.Add(DepthStencilFormat);
        hash.Add(BlendState);
        hash.Add(DepthBias);
        hash.Add(SlopeScaledDepthBias);
        hash.Add(CullMode);
        hash.Add(InputLayout);
        hash.Add(PrimitiveTopologyType);
        return hash.ToHashCode();
    }

    public static bool operator ==(PSODescription x, PSODescription y)
    {
        return x.Equals(y);
    }

    public static bool operator !=(PSODescription x, PSODescription y)
    {
        return !(x == y);
    }
}
