using System.Collections.Generic;

using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Engine.DataTypes;

public class PipelineStateObject : IDisposable
{
    public List<PipelineStateObjectBundle> PipelineStateObjectBundle = new List<PipelineStateObjectBundle>();

    public ReadOnlyMemory<byte> vertexShader;
    public ReadOnlyMemory<byte> geometryShader;
    public ReadOnlyMemory<byte> pixelShader;

    public string Name;

    public PipelineStateObject(byte[] vertexShader, byte[] pixelShader)
    {
        this.vertexShader = vertexShader;
        this.pixelShader = pixelShader;
    }

    public PipelineStateObject(Shader vertexShader, Shader pixelShader)
    {
        this.vertexShader = vertexShader.CompiledCode;
        this.pixelShader = pixelShader.CompiledCode;
    }

    public ID3D12PipelineState GetState(GraphicsDevice device, PipelineStateObjectDescription description, RootSignature rootSignature, UnnamedInputLayout inputLayout)
    {
        foreach (var psoCombind in PipelineStateObjectBundle)
        {
            if (psoCombind.PipelineStateObjectDescription.Equals(description)
             && psoCombind.RootSignature.Equals(rootSignature)
             && psoCombind.UnnamedInputLayout.Equals(inputLayout))
            {
                if (psoCombind.pipelineState is null)
                    throw new Exception("pipeline state error");

                return psoCombind.pipelineState;
            }
        }
        InputLayoutDescription inputLayoutDescription = inputLayout.InputElementDescriptions;

        GraphicsPipelineStateDescription graphicsPipelineStateDescription = new()
        {
            RootSignature = rootSignature.Resource,
            VertexShader = vertexShader,
            GeometryShader = geometryShader,
            PixelShader = pixelShader,
            PrimitiveTopologyType = PrimitiveTopologyType.Triangle,
            InputLayout = inputLayoutDescription,
            DepthStencilFormat = description.DepthStencilFormat,
            RenderTargetFormats = new Format[description.RenderTargetCount],
        };
        Array.Fill(graphicsPipelineStateDescription.RenderTargetFormats, description.RenderTargetFormat);

        if (description.BlendState == "Alpha")
            graphicsPipelineStateDescription.BlendState = blendStateAlpha();
        else if (description.BlendState == "Add")
            graphicsPipelineStateDescription.BlendState = BlendDescription.Additive;
        else
            graphicsPipelineStateDescription.BlendState = BlendDescription.Opaque;

        graphicsPipelineStateDescription.SampleMask = uint.MaxValue;
        var RasterizerState = new RasterizerDescription(CullMode.None, FillMode.Solid);
        RasterizerState.DepthBias = description.DepthBias;
        RasterizerState.SlopeScaledDepthBias = description.SlopeScaledDepthBias;
        graphicsPipelineStateDescription.RasterizerState = RasterizerState;

        var pipelineState = device.Device.CreateGraphicsPipelineState<ID3D12PipelineState>(graphicsPipelineStateDescription);
        if (pipelineState is null)
            throw new Exception("pipeline state error");

        PipelineStateObjectBundle.Add(new()
        {
            PipelineStateObjectDescription = description,
            pipelineState = pipelineState,
            RootSignature = rootSignature,
            UnnamedInputLayout = inputLayout
        });

        return pipelineState;
    }

    BlendDescription blendStateAlpha()
    {
        BlendDescription blendDescription = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha, Blend.One, Blend.InverseSourceAlpha);

        return blendDescription;
    }

    public void Dispose()
    {
        foreach (var combine in PipelineStateObjectBundle)
            combine.pipelineState.Dispose();

        PipelineStateObjectBundle.Clear();
    }
}

public class PipelineStateObjectBundle
{
    public PipelineStateObjectDescription PipelineStateObjectDescription;
    public RootSignature RootSignature;
    public ID3D12PipelineState pipelineState;
    public UnnamedInputLayout UnnamedInputLayout;
}

public struct PipelineStateObjectDescription : IEquatable<PipelineStateObjectDescription>
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

    public override bool Equals(object obj) =>
        obj is PipelineStateObjectDescription description && Equals(description);

    public bool Equals(PipelineStateObjectDescription other)
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

    public static bool operator ==(PipelineStateObjectDescription x, PipelineStateObjectDescription y)
    {
        return x.Equals(y);
    }

    public static bool operator !=(PipelineStateObjectDescription x, PipelineStateObjectDescription y)
    {
        return !(x == y);
    }
}
