struct PS_INPUT_UI
{
    float4 pos : SV_POSITION;
    float4 col : COLOR0;
    float2 uv : TEXCOORD0;
};

struct VS_INPUT_UI
{
    float2 pos : POSITION;
    float4 col : COLOR0;
    float2 uv : TEXCOORD0;
};

cbuffer VertexBuffer : register(b0)
{
    float4x4 ProjectionMatrix;
};