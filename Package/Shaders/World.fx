//@author: Elliot Woods
//@help: Converts from OpenNI 16bit depth map to World XYZ (presuming Kinect optics)
//@tags: template, basic
//@credits:

// --------------------------------------------------------------------------------------------------
// PARAMETERS:
// --------------------------------------------------------------------------------------------------


//texture
texture Tex <string uiname="Depth Texture";>;
sampler Samp = sampler_state    //sampler for doing the texture-lookup
{
    Texture   = (Tex);          //apply a texture to the sampler
    MipFilter = LINEAR;         //sampler states
    MinFilter = LINEAR;
    MagFilter = LINEAR;
};

//the data structure: vertexshader to pixelshader
//used as output data with the VS function
//and as input data with the PS function
struct vs2ps
{
    float4 Pos : POSITION;
    float4 TexCd : TEXCOORD0;
};

// --------------------------------------------------------------------------------------------------
// VERTEXSHADERS
// --------------------------------------------------------------------------------------------------

vs2ps VS(
    float4 Pos : POSITION,
    float4 TexCd : TEXCOORD0)
{
    //inititalize all fields of output struct with 0
    vs2ps Out = (vs2ps)0;

    //transform position
    Out.Pos = Pos;
	Out.Pos.w =0.5;

    //transform texturecoordinates
    Out.TexCd = TexCd;

    return Out;
}

// --------------------------------------------------------------------------------------------------
// PIXELSHADERS:
// --------------------------------------------------------------------------------------------------

const float fx_d = 1.0f / 5.9421434211923247e+02f;
const float fy_d = 1.0f / 5.9104053696870778e+02f;
const float cx_d = 3.3930780975300314e+02f;
const float cy_d = 2.4273913761751615e+02f;

float3 world(float2 xy, float depthValue)
{
	//adapted from http://graphics.stanford.edu/~mdfisher/Kinect.html

	//presume we're in mm units
	float3 xyz;
	xyz.z = depthValue / 1000.0f;
	xyz.x = (xy.x - cx_d) * fx_d * xyz.z;
	xyz.y = -(xy.y - cy_d) * fy_d * xyz.z;
	
	return xyz;
}

float4 PS(vs2ps In): COLOR
{
    //In.TexCd = In.TexCd / In.TexCd.w; // for perpective texture projections (e.g. shadow maps) ps_2_0

    float4 lookup = tex2D(Samp, In.TexCd);
	float4 col = (float4)1;
	float z = lookup.r;
	z *= pow(2,16);
	col.rgb = world(float2(In.TexCd.x*640.0f, In.TexCd.y*480.0f), z);
    col.a = 1;
    return col;
}


// --------------------------------------------------------------------------------------------------
// TECHNIQUES:
// --------------------------------------------------------------------------------------------------

technique TWorld
{
    pass P0
    {
        //Wrap0 = U;  // useful when mesh is round like a sphere
        VertexShader = compile vs_2_0 VS();
        PixelShader = compile ps_2_0 PS();
    }
}