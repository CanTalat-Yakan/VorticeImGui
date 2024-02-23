cbuffer ViewConstantsBuffer : register(b0)
{
    float4x4 ViewProjection;
    float3 Camera;
};

cbuffer PerModelConstantBuffer : register(b1)
{
    float4x4 World;
};

struct VSInputUI
{
    float2 vertex : POSITION;
    float2 texcoord : TEXCOORD0;
    float3 color : COLOR0;
};

struct PSInputUI
{
    float4 pos : SV_POSITION;
    float3 col : COLOR0;
    float2 uv : TEXCOORD0;
};