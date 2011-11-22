//@author: Elliot Woods
//@help: Apply normals and lighting effects to DepthMesh
//@tags:
//@credits:

#include "DepthMesh.fxh"

// --------------------------------------------------------------------------------------------------
// PARAMETERS:
// --------------------------------------------------------------------------------------------------

texture TexNorm <string uiname="Normals";>;
sampler SampNorm = sampler_state    //sampler for doing the texture-lookup
{
    Texture   = (TexNorm);          //apply a texture to the sampler
    MipFilter = LINEAR;         //sampler states
    MinFilter = LINEAR;
    MagFilter = LINEAR;
};

// --------------------------------------------------------------------------------------------------
// PIXELSHADERS:
// --------------------------------------------------------------------------------------------------

float4 PSNormals(vs2ps In): COLOR
{
    float4 col = tex2D(SampNorm, In.TexCd);
	col.a = In.existence > drop;
    return col;
}

float3 LightDirection;
float4 PSLight(vs2ps In): COLOR
{
    float4 col = dot(LightDirection, tex2D(SampNorm, In.TexCd));
	col.a = In.existence > drop;
    return col;
}

float3 LightPosition;
float MaxRange = 2.0f;
float MaxDepth = 1.5f;
float Mult  = 1.0f;
float4 PSLightPoint(vs2ps In): COLOR
{
	float3 lv = In.PosW - LightPosition;
    float4 col = dot(lv, tex2D(SampNorm, In.TexCd));
	float l = length(lv);
	col /= pow(l, 2);
	col*= Mult;
	if (l > MaxRange || In.PosW.z > MaxDepth)
		col = 0;
	col.a = In.existence > drop;
    return col;
}


// --------------------------------------------------------------------------------------------------
// TECHNIQUES:
// --------------------------------------------------------------------------------------------------

technique TNormals
{
    pass P0
    {
        //Wrap0 = U;  // useful when mesh is round like a sphere
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PSNormals();
    }
}

technique TLightDirectional
{
    pass P0
    {
        //Wrap0 = U;  // useful when mesh is round like a sphere
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PSLight();
    }
}

technique TLightPoint
{
    pass P0
    {
        //Wrap0 = U;  // useful when mesh is round like a sphere
        VertexShader = compile vs_3_0 VS();
        PixelShader  = compile ps_3_0 PSLightPoint();
    }
}