Shader "SessionRpg/Sunbreak Terrain"
{
    Properties
    {
        _BaseMap("Scanned albedo", 2D) = "white" {}
        _BaseColor("Surface tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Scanned normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0,2)) = 1
        _RoughnessMap("Scanned roughness", 2D) = "white" {}
        _Smoothness("Smoothness multiplier", Range(0,1)) = 1
        [Toggle(_PACKED_MASONRY)] _PackedMasonry("Measured masonry mask", Float) = 0
        _PackedMasonryMap("Masonry mask (G AO, A smoothness)", 2D) = "white" {}
        _OcclusionStrength("Measured occlusion strength", Range(0,1)) = 0
        [Toggle(_SECOND_LAYER)] _SecondLayer("Blend ground layers", Float) = 0
        [Toggle(_VERTEX_LAYER)] _VertexLayer("Use authored landscape layer weights", Float) = 0
        _LayerMap("Exposed earth albedo", 2D) = "white" {}
        _LayerColor("Exposed earth tint", Color) = (1,1,1,1)
        _LayerSaturation("Exposed earth saturation", Range(0,1)) = 1
        [Normal] _LayerNormal("Exposed earth normal", 2D) = "bump" {}
        _LayerRoughness("Exposed earth roughness", 2D) = "white" {}
        _BlendMap("World soil / vegetation mask (linear)", 2D) = "black" {}
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _AlphaClip("Alpha clip", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 300
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _LayerMap_ST;
            half4 _BaseColor;
            half4 _LayerColor;
            half _LayerSaturation;
            half _BumpScale;
            half _Smoothness;
            half _PackedMasonry;
            half _OcclusionStrength;
            half _SecondLayer;
            half _VertexLayer;
            half _Surface;
            half _AlphaClip;
            half _Cull;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
        TEXTURE2D(_RoughnessMap); SAMPLER(sampler_RoughnessMap);
        TEXTURE2D(_PackedMasonryMap); SAMPLER(sampler_PackedMasonryMap);
        TEXTURE2D(_LayerMap); SAMPLER(sampler_LayerMap);
        TEXTURE2D(_LayerNormal); SAMPLER(sampler_LayerNormal);
        TEXTURE2D(_LayerRoughness); SAMPLER(sampler_LayerRoughness);
        TEXTURE2D(_BlendMap); SAMPLER(sampler_BlendMap);
        struct Attributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float4 tangentOS : TANGENT;
            float2 uv : TEXCOORD0;
            #ifdef _VERTEX_LAYER
                half4 color : COLOR;
            #endif
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
            float4 shadowCoord : TEXCOORD5;
            #ifdef _VERTEX_LAYER
                half2 layerMask : TEXCOORD7;
            #endif
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLighting : TEXCOORD6;
            #endif
            UNITY_VERTEX_INPUT_INSTANCE_ID
            UNITY_VERTEX_OUTPUT_STEREO
        };
        Varyings TerrainVertex(Attributes input)
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
            output.uv = input.uv;
            output.fog = ComputeFogFactor(position.positionCS.z);
            output.shadowCoord = GetShadowCoord(position);
            #ifdef _VERTEX_LAYER
                output.layerMask = input.color.rg;
            #endif
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                output.vertexLighting = VertexLighting(position.positionWS, normal.normalWS);
            #endif
            return output;
        }
        half3 TerrainNormal(Varyings input, half3 normalTS)
        {
            half3 bitangent = input.tangentWS.w * cross(input.normalWS, input.tangentWS.xyz);
            return NormalizeNormalPerPixel(TransformTangentToWorld(normalTS,
                half3x3(input.tangentWS.xyz, bitangent, input.normalWS)));
        }
        #if defined(_VERTEX_LAYER)
            half3 LandscapeWeights(half3 geometricNormal)
            {
                half3 weights = abs(geometricNormal);
                weights *= weights;
                weights *= weights;
                return weights / max(dot(weights, half3(1, 1, 1)), 0.0001h);
            }
            half3 LandscapeNormal(half3 geometricNormal, half3 normalX, half3 normalY, half3 normalZ,
                half3 signs, half3 weights)
            {
                // Each signed projection uses a right-handed OpenGL tangent
                // basis. Whiteout combines its measured detail with the actual
                // geometric normal before blending planes. Neutral (0,0,1)
                // therefore returns the geometric normal, never an axis-weight
                // approximation that would relight every mountain face.
                half3 tangentX = half3(-geometricNormal.z * signs.x, geometricNormal.y, abs(geometricNormal.x));
                half3 tangentY = half3(geometricNormal.x, -geometricNormal.z * signs.y, abs(geometricNormal.y));
                half3 tangentZ = half3(geometricNormal.x * signs.z, geometricNormal.y, abs(geometricNormal.z));
                half3 blendedX = half3(tangentX.xy + normalX.xy, tangentX.z * normalX.z);
                half3 blendedY = half3(tangentY.xy + normalY.xy, tangentY.z * normalY.z);
                half3 blendedZ = half3(tangentZ.xy + normalZ.xy, tangentZ.z * normalZ.z);
                half3 worldX = half3(blendedX.z * signs.x, blendedX.y, -blendedX.x * signs.x);
                half3 worldY = half3(blendedY.x, blendedY.z * signs.y, -blendedY.y * signs.y);
                half3 worldZ = half3(blendedZ.x * signs.z, blendedZ.y, blendedZ.z * signs.z);
                return NormalizeNormalPerPixel(worldX * weights.x + worldY * weights.y + worldZ * weights.z);
            }
            void SampleLandscapeLayer(TEXTURE2D_PARAM(colorMap, colorSampler),
                TEXTURE2D_PARAM(normalMap, normalSampler), TEXTURE2D_PARAM(roughMap, roughSampler),
                float3 positionWS, half3 geometricNormal, float4 textureST,
                out half3 albedo, out half roughness, out half3 normalWS)
            {
                half3 signs = half3(geometricNormal.x < 0 ? -1 : 1,
                    geometricNormal.y < 0 ? -1 : 1, geometricNormal.z < 0 ? -1 : 1);
                half3 weights = LandscapeWeights(geometricNormal);
                float2 uvX = float2(-positionWS.z * signs.x, positionWS.y) * textureST.xy + textureST.zw;
                float2 uvY = float2(positionWS.x, -positionWS.z * signs.y) * textureST.xy + textureST.zw;
                float2 uvZ = float2(positionWS.x * signs.z, positionWS.y) * textureST.xy + textureST.zw;
                // Nine measured fetches per layer: three planes times albedo,
                // roughness and GL normal. Vista's two layers cost 18, not 6.
                // Near ground never compiles this variant or pays these reads.
                albedo = SAMPLE_TEXTURE2D(colorMap, colorSampler, uvX).rgb * weights.x +
                    SAMPLE_TEXTURE2D(colorMap, colorSampler, uvY).rgb * weights.y +
                    SAMPLE_TEXTURE2D(colorMap, colorSampler, uvZ).rgb * weights.z;
                roughness = SAMPLE_TEXTURE2D(roughMap, roughSampler, uvX).r * weights.x +
                    SAMPLE_TEXTURE2D(roughMap, roughSampler, uvY).r * weights.y +
                    SAMPLE_TEXTURE2D(roughMap, roughSampler, uvZ).r * weights.z;
                half3 normalX = UnpackNormalScale(SAMPLE_TEXTURE2D(normalMap, normalSampler, uvX), _BumpScale);
                half3 normalY = UnpackNormalScale(SAMPLE_TEXTURE2D(normalMap, normalSampler, uvY), _BumpScale);
                half3 normalZ = UnpackNormalScale(SAMPLE_TEXTURE2D(normalMap, normalSampler, uvZ), _BumpScale);
                normalWS = LandscapeNormal(geometricNormal, normalX, normalY, normalZ, signs, weights);
            }
            void ReadLandscape(Varyings input, out SurfaceData surface, out half3 normalWS)
            {
                surface = (SurfaceData)0;
                half3 geometricNormal = NormalizeNormalPerPixel(input.normalWS);
                half3 rock, grass, rockNormal, grassNormal;
                half rockRough, grassRough;
                SampleLandscapeLayer(TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap),
                    TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), TEXTURE2D_ARGS(_RoughnessMap, sampler_RoughnessMap),
                    input.positionWS, geometricNormal, _BaseMap_ST, rock, rockRough, rockNormal);
                SampleLandscapeLayer(TEXTURE2D_ARGS(_LayerMap, sampler_LayerMap),
                    TEXTURE2D_ARGS(_LayerNormal, sampler_LayerNormal), TEXTURE2D_ARGS(_LayerRoughness, sampler_LayerRoughness),
                    input.positionWS, geometricNormal, _LayerMap_ST, grass, grassRough, grassNormal);
                half2 mask = saturate(input.layerMask);
                half blend = smoothstep(0.28h, 0.72h, mask.r + (grassRough - rockRough) * 0.12h);
                rock *= _BaseColor.rgb;
                grass *= _LayerColor.rgb;
                half luminance = dot(grass, half3(0.2126h, 0.7152h, 0.0722h));
                grass = lerp(luminance.xxx, grass, saturate(_LayerSaturation));
                surface.albedo = lerp(rock, grass, blend) * lerp(0.72h, 1.06h, mask.g);
                surface.normalTS = half3(0, 0, 1);
                surface.metallic = 0;
                surface.specular = half3(0.04h, 0.04h, 0.04h);
                surface.smoothness = saturate(1.0h - lerp(rockRough, grassRough, blend)) * _Smoothness;
                surface.occlusion = 1;
                surface.alpha = 1;
                normalWS = NormalizeNormalPerPixel(lerp(rockNormal, grassNormal, blend));
            }
        #endif
        void ReadTerrain(Varyings input, out SurfaceData surface, out half3 normalWS)
        {
            #if defined(_VERTEX_LAYER)
                ReadLandscape(input, surface, normalWS);
            #else
            surface = (SurfaceData)0;
            float2 uv = input.uv * _BaseMap_ST.xy + _BaseMap_ST.zw;
            half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
            half3 normal = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
            half roughness;
            half occlusion = 1;
            #if defined(_PACKED_MASONRY)
                // Exact existing derivative: G is measured ambient occlusion;
                // A is bounded inverse roughness. Never sample its height B or
                // multiply joint darkness into direct-light albedo.
                half4 masonry = SAMPLE_TEXTURE2D(_PackedMasonryMap, sampler_PackedMasonryMap, uv);
                roughness = 1.0h - masonry.a;
                occlusion = lerp(1.0h, masonry.g, _OcclusionStrength);
            #else
                roughness = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, uv).r;
            #endif
            #if defined(_SECOND_LAYER)
                // This phase is world-space, independent of each fine material's
                // physical scale and of streamed chunk mesh origins.
                half2 mask = SAMPLE_TEXTURE2D(_BlendMap, sampler_BlendMap,
                    (input.positionWS.xz + 64.0) / 192.0).rg;
                float2 soilUV = input.uv * _LayerMap_ST.xy + _LayerMap_ST.zw;
                half soilRough = SAMPLE_TEXTURE2D(_LayerRoughness, sampler_LayerRoughness, soilUV).r;
                // Narrower ecotones retain recognisable grass and earth instead
                // of averaging them into one lawn. Existing measured roughness
                // only breaks the blend edge; it is not height or invented AO.
                half blend = smoothstep(0.28h, 0.72h, mask.r + (soilRough - roughness) * 0.12h);
                half3 earth = SAMPLE_TEXTURE2D(_LayerMap, sampler_LayerMap, soilUV).rgb * _LayerColor.rgb;
                // Local art-directed color only: preserve linear luminance and
                // the scan's fine variation, roughness and normal response.
                // This does not grade lighting or derive occlusion/height.
                half earthLuminance = dot(earth, half3(0.2126h, 0.7152h, 0.0722h));
                earth = lerp(earthLuminance.xxx, earth, saturate(_LayerSaturation));
                albedo = lerp(albedo, earth, blend);
                normal = normalize(lerp(normal, UnpackNormalScale(
                    SAMPLE_TEXTURE2D(_LayerNormal, sampler_LayerNormal, soilUV), _BumpScale), blend));
                roughness = lerp(roughness, soilRough, blend);
                albedo *= lerp(0.72h, 1.06h, mask.g);
            #endif
            surface.albedo = albedo;
            surface.normalTS = normal;
            surface.metallic = 0;
            surface.specular = half3(0.04h, 0.04h, 0.04h);
            surface.smoothness = saturate(1.0h - roughness) * _Smoothness;
            surface.occlusion = occlusion;
            surface.alpha = 1;
            normalWS = TerrainNormal(input, surface.normalTS);
            #endif
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TerrainVertex
            #pragma fragment TerrainFragment
            #pragma shader_feature_local _SECOND_LAYER
            #pragma shader_feature_local _VERTEX_LAYER
            #pragma shader_feature_local _PACKED_MASONRY
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            half4 TerrainFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                SurfaceData surface;
                half3 worldNormal;
                ReadTerrain(input, surface, worldNormal);
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = worldNormal;
                data.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    data.shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    // Large streamed ground quads can cross shadow cascades;
                    // select the cascade per pixel, never interpolate atlases.
                    data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    data.shadowCoord = float4(0,0,0,0);
                #endif
                data.fogCoord = input.fog;
                data.bakedGI = SampleSH(data.normalWS);
                #ifdef _ADDITIONAL_LIGHTS_VERTEX
                    data.vertexLighting = input.vertexLighting;
                #endif
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);
                half4 color = UniversalFragmentPBR(data, surface);
                color.rgb = MixFog(color.rgb, data.fogCoord);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TerrainVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing
            half DepthFragment(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormalsOnly" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex TerrainVertex
            #pragma fragment NormalsFragment
            #pragma shader_feature_local _SECOND_LAYER
            #pragma shader_feature_local _VERTEX_LAYER
            #pragma shader_feature_local _PACKED_MASONRY
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing
            half4 NormalsFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                SurfaceData surface;
                half3 normal;
                ReadTerrain(input, surface, normal);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(octNormal * 0.5 + 0.5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            ZWrite On
            ZTest LEqual
            Cull Back
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing
            float3 _LightDirection;
            float3 _LightPosition;
            float4 ShadowVertex(Attributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                    float3 direction = normalize(_LightPosition - positionWS);
                #else
                    float3 direction = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, direction));
                return ApplyShadowClamping(positionCS);
            }
            half4 ShadowFragment() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
