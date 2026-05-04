Shader "LostMemory/Sprites/Silhouette Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineSize ("Outline Size", Float) = 0.015
        _AlphaThreshold ("Alpha Threshold", Range(0,1)) = 0.1
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        CGINCLUDE
        #include "UnitySprites.cginc"

        fixed4 _OutlineColor;
        float _OutlineSize;
        float _AlphaThreshold;

        v2f OutlineVertOffset(appdata_t input, float2 direction)
        {
            v2f output;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

            float4 vertex = UnityFlipSprite(input.vertex, _Flip);
            vertex.xy += direction * _OutlineSize;

            output.vertex = UnityObjectToClipPos(vertex);
            output.texcoord = input.texcoord;
            output.color = input.color * _Color * _RendererColor;

            #ifdef PIXELSNAP_ON
            output.vertex = UnityPixelSnap(output.vertex);
            #endif

            return output;
        }

        fixed4 OutlineFrag(v2f input) : SV_Target
        {
            fixed alpha = SampleSpriteTexture(input.texcoord).a;
            clip(alpha - _AlphaThreshold);

            fixed4 color = _OutlineColor;
            color.a *= alpha * input.color.a;
            color.rgb *= color.a;
            return color;
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex OutlineVertLeft
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            v2f OutlineVertLeft(appdata_t input)
            {
                return OutlineVertOffset(input, float2(-1, 0));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex OutlineVertRight
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            v2f OutlineVertRight(appdata_t input)
            {
                return OutlineVertOffset(input, float2(1, 0));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex OutlineVertDown
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            v2f OutlineVertDown(appdata_t input)
            {
                return OutlineVertOffset(input, float2(0, -1));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex OutlineVertUp
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            v2f OutlineVertUp(appdata_t input)
            {
                return OutlineVertOffset(input, float2(0, 1));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex OutlineVertDownLeft
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            v2f OutlineVertDownLeft(appdata_t input)
            {
                return OutlineVertOffset(input, normalize(float2(-1, -1)));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex OutlineVertUpLeft
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            v2f OutlineVertUpLeft(appdata_t input)
            {
                return OutlineVertOffset(input, normalize(float2(-1, 1)));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex OutlineVertDownRight
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            v2f OutlineVertDownRight(appdata_t input)
            {
                return OutlineVertOffset(input, normalize(float2(1, -1)));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex OutlineVertUpRight
            #pragma fragment OutlineFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            v2f OutlineVertUpRight(appdata_t input)
            {
                return OutlineVertOffset(input, normalize(float2(1, 1)));
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SpriteFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            ENDCG
        }
    }
}
