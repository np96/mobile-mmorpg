Shader "SessionRpg/Sunbreak Vegetation"
{
    Properties
    {
        _BaseMap("Source color", 2D) = "white" {}
        _BaseColor("Color tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Source GL normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0,2)) = 1
        _RoughnessMap("Source roughness", 2D) = "white" {}
        _Smoothness("Smoothness multiplier", Range(0,1)) = 1
        _AlphaMap("Source coverage", 2D) = "white" {}
        _AlphaFromBase("Use baked color alpha", Float) = 0
        _Cutoff("Coverage cutoff", Range(0,1)) = 0.4
        _Cull("Cull", Float) = 0
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _AlphaClip("Alpha clip", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "RenderPipeline"="UniversalPipeline" "Queue"="AlphaTest" }
        HLSLINCLUDE
        // This shader is always cutout; a material keyword must never disable
        // measured coverage or the installed URP alpha-to-coverage helpers.
        #define _ALPHATEST_ON 1
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _Smoothness;
            half _AlphaFromBase;
            half _Cutoff;
            half _Cull;
            half _Surface;
            half _AlphaClip;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
        TEXTURE2D(_AlphaMap); SAMPLER(sampler_AlphaMap);
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            half3 normalWS : TEXCOORD1;
            half4 tangentWS : TEXCOORD2;
            float2 uv : TEXCOORD3;
            half fog : TEXCOORD4;
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings VegetationVertex(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
            VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
            VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
            output.positionCS = position.positionCS;
            output.positionWS = position.positionWS;
            output.normalWS = normal.normalWS;
            output.tangentWS = half4(normal.tangentWS, input.tangentOS.w * GetOddNegativeScale());
            output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
            output.fog = ComputeFogFactor(position.positionCS.z);
            return output;
        }
        half VegetationCoverage(float2 uv)
        {
            // All passes read the same real coverage, never albedo luminance.
            return _AlphaFromBase > 0.5h
                ? SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a
                : SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, uv).a;
        }
        half ClipVegetation(float2 uv)
        {
            // URP sharpens coverage only when the current draw supports MSAA.
            // Its 1x/non-MSAA path performs the exact hard cutoff instead.
            return AlphaClip(VegetationCoverage(uv), _Cutoff);
        }
        void ClipVegetationShadow(float2 uv)
        {
            // Shadow maps remain hard cutouts regardless of camera MSAA state.
            half coverage = VegetationCoverage(uv);
            clip(coverage - _Cutoff);
        }
        half3 VegetationNormal(Varyings input, half facing)
        {
            half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
            half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
            return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
                half3x3(input.tangentWS.xyz, bitangent, input.normalWS))) * facing;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull [_Cull]
            ZWrite On
            AlphaToMask On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VegetationVertex
            #pragma fragment ForwardFragment
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            half4 ForwardFragment(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half coverage = ClipVegetation(input.uv);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                surface.smoothness = (1.0h - SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, input.uv).r) * _Smoothness;
                surface.normalTS = half3(0,0,1);
                surface.specular = half3(0.04h,0.04h,0.04h);
                surface.occlusion = 1;
                surface.alpha = 1;
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = VegetationNormal(input, IS_FRONT_VFACE(frontFace, 1.0h, -1.0h));
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.fogCoord = input.fog;
                data.bakedGI = SampleSH(data.normalWS);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    data.vertexLighting = VertexLighting(input.positionWS, data.normalWS);
                #endif
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);
                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, data.fogCoord);
                color.a = OutputAlpha(coverage, false);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            Cull [_Cull]
            ZWrite On
            AlphaToMask On
            ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VegetationVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half4 DepthFragment(Varyings input) : SV_Target
            {
                half coverage = ClipVegetation(input.uv);
                return half4(input.positionCS.zzz, OutputAlpha(coverage, false));
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            Cull [_Cull]
            ZWrite On
            AlphaToMask On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex VegetationVertex
            #pragma fragment NormalsFragment
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            half4 NormalsFragment(Varyings input, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half coverage = ClipVegetation(input.uv);
                half3 normal = VegetationNormal(input, IS_FRONT_VFACE(frontFace, 1.0h, -1.0h));
                #if defined(_GBUFFER_NORMALS_OCT)
                    return half4(PackFloat2To888(saturate(PackNormalOctQuadEncode(normal) * 0.5 + 0.5)), OutputAlpha(coverage, false));
                #else
                    return half4(normal, OutputAlpha(coverage, false));
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            float3 _LightDirection;
            float3 _LightPosition;
            Varyings ShadowVertex(Attributes input)
            {
                Varyings output = VegetationVertex(input);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - output.positionWS);
                #else
                    float3 direction = _LightDirection;
                #endif
                output.positionCS = ApplyShadowClamping(TransformWorldToHClip(
                    ApplyShadowBias(output.positionWS, output.normalWS, direction)));
                return output;
            }
            half4 ShadowFragment(Varyings input) : SV_Target { ClipVegetationShadow(input.uv); return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
