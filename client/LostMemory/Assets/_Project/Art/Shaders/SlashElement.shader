// CL-230: 슬래시 sprite 의 알파 기반 그라디언트 + glow + UV scroll.
// 속성(Fire/Ice/Lightning 등) 시스템과 결합해 한 sprite 로 다양한 색/효과 표현.
// Sprites/Default 와 동일 구조 (URP 2D 호환, 메모리 규칙: URP variant 회피).
//
// 알파 그라디언트:
//   - sprite 알파 (0~1) 를 SmoothStep 으로 inner/outer 영역 분리
//   - 안쪽 (alpha 높음) = _InnerColor (보통 흰/노랑 hot core)
//   - 가장자리 (alpha 낮음) = _OuterColor (보통 빨강/파랑 등 속성색)
//
// Glow: 안쪽 영역에 _GlowIntensity 배율로 밝기 boost.
// UV Scroll: _ScrollSpeed 로 텍스처 흐름 (화염은 양수, 얼음은 0).
//
// MaterialPropertyBlock 으로 SpriteRenderer 마다 독립 속성 적용 (material 인스턴스 안 만듦).

Shader "LostMemory/Sprites/Slash Element"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0

        _InnerColor ("Inner Color (hot core)", Color) = (1,1,1,1)
        _OuterColor ("Outer Color (element)", Color) = (1,0.5,0,1)
        _GradientThreshold ("Gradient Threshold", Range(0,1)) = 0.5
        _GradientSoftness ("Gradient Softness", Range(0.01,1)) = 0.3
        _GlowIntensity ("Glow Intensity (inner boost)", Range(0,5)) = 0
        _ScrollSpeed ("Scroll Speed (xy = uv per sec)", Vector) = (0,0,0,0)

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
        // CL-230: 표준 알파 블렌딩 (premultiplied 아님). result.rgb *= result.a 제거와 함께 적용.
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment SlashFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON

            #include "UnitySprites.cginc"

            float4 _InnerColor;
            float4 _OuterColor;
            float _GradientThreshold;
            float _GradientSoftness;
            float _GlowIntensity;
            float4 _ScrollSpeed;

            fixed4 SlashFrag(v2f IN) : SV_Target
            {
                fixed4 tex = SampleSpriteTexture(IN.texcoord);

                // 알파 기반 그라디언트
                float t = smoothstep(_GradientThreshold - _GradientSoftness,
                                     _GradientThreshold + _GradientSoftness,
                                     tex.a);
                fixed3 gradient = lerp(_OuterColor.rgb, _InnerColor.rgb, t);
                gradient *= (1.0 + _GlowIntensity * t);

                // CL-230: IN.color 무시. 셰이더가 색 100% 결정. slashTint 영향 X.
                return fixed4(gradient, tex.a);
            }
            ENDCG
        }
    }
}
