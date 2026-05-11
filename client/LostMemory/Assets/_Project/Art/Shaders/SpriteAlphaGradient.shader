// CL-204 후속: Fusion 미소녀 sprite 의 상반신만 보이게 하는 수직 알파 그라데이션.
// UV.y 기반 smoothstep 으로 알파 감쇠 — 위(UV.y=1) = 불투명, 아래(UV.y=0) = 점진 투명.
// Sprites/Default 와 동일 구조 (PerRendererData _MainTex, vertex color, alpha blend) — URP 2D 호환.
// 메모리 규칙 (URP variant 회피) 에 부합.

Shader "LostMemory/Sprites/Alpha Gradient"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0

        _GradientStart ("Gradient Start Y (alpha 0 below)", Range(0,1)) = 0.0
        _GradientEnd ("Gradient End Y (alpha 1 above)", Range(0,1)) = 0.5

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

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment GradientFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            #include "UnitySprites.cginc"

            float _GradientStart;
            float _GradientEnd;

            fixed4 GradientFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                // UV.y 가 _GradientStart 보다 아래면 alpha 0, _GradientEnd 보다 위면 alpha 1, 그 사이는 smoothstep
                float gradient = smoothstep(_GradientStart, _GradientEnd, IN.texcoord.y);
                c.a *= gradient;
                c.rgb *= c.a;  // premultiplied alpha (Sprites/Default Blend One OneMinusSrcAlpha 와 일치)
                return c;
            }
            ENDCG
        }
    }
}
