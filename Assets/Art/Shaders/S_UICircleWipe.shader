// Full-screen UI circle wipe: pixels within _Radius (aspect-corrected, screen-centered) are
// opaque procedural starfield (deep space color + twinkling hash-noise stars, a simplified 2D
// take on S_ProceduralSpaceSkybox's star layers), everything outside is transparent, with a soft
// antialiased edge. Animating _Radius from 0 up covers the screen ("circle in"); animating it
// back down uncovers it ("circle out") - see UI/SceneTransitionController.cs, the only user of
// this shader.
Shader "UI/CircleWipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Deep Space Color", Color) = (0.01, 0.015, 0.04, 1)
        _Radius ("Radius", Range(0, 1.5)) = 0
        _Softness ("Softness", Range(0.001, 0.3)) = 0.03
        _Aspect ("Aspect (w/h)", Float) = 1.7777778

        [HDR] _StarColor ("Star Color", Color) = (1.2, 1.35, 1.6, 1)
        _StarDensity ("Star Density", Range(0.9, 0.999)) = 0.985
        _StarBrightness ("Star Brightness", Range(0, 4)) = 1.6
        _StarTwinkleStrength ("Star Twinkle Strength", Range(0, 0.5)) = 0.25
        _StarTwinkleSpeed ("Star Twinkle Speed", Range(0, 1)) = 0.2

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        // Matches UI/Default's own declared default - without this a freshly instantiated
        // Material leaves _ClipRect at (0,0,0,0), which UnityGet2DClipping reads as "clip
        // everything" and made the whole wipe render as effectively invisible/erratic.
        _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            fixed4 _Color;
            float _Radius;
            float _Softness;
            float _Aspect;
            fixed4 _StarColor;
            float _StarDensity;
            float _StarBrightness;
            float _StarTwinkleStrength;
            float _StarTwinkleSpeed;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            float Hash21(float2 value)
            {
                float3 p3 = frac(float3(value.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Simplified 2D take on S_ProceduralSpaceSkybox's StarLayer - a hashed point grid
            // instead of a 3D direction, since the wipe only ever covers a flat screen quad.
            float StarLayer(float2 uv, float scale, float seed)
            {
                float2 pos = uv * scale;
                float2 cell = floor(pos);
                float2 local = frac(pos) - 0.5;

                float existence = Hash21(cell + seed);
                float2 offset = float2(
                    Hash21(cell + seed + 11.0),
                    Hash21(cell + seed + 23.0)) - 0.5;
                offset *= 0.6;

                float distanceToStar = length(local - offset);
                float sizeVariation = Hash21(cell + seed + 51.0);
                float starRadius = lerp(0.025, 0.09, sizeVariation * sizeVariation);
                float star = 1.0 - smoothstep(starRadius, starRadius + 0.03, distanceToStar);
                star *= step(_StarDensity, existence) * lerp(0.5, 1.4, sizeVariation);

                float twinklePhase = Hash21(cell + seed + 71.0);
                float twinkleWave = abs(frac(_Time.y * _StarTwinkleSpeed + twinklePhase) * 2.0 - 1.0);
                float twinkle = lerp(1.0 - _StarTwinkleStrength, 1.0 + _StarTwinkleStrength, twinkleWave);

                return star * twinkle;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord - 0.5;
                uv.x *= _Aspect;
                float dist = length(uv);
                float coverage = smoothstep(_Radius, _Radius - _Softness, dist);

                float stars = StarLayer(uv, 26.0, 7.0) + StarLayer(uv, 46.0, 73.0) * 0.7;

                fixed4 color = IN.color;
                color.rgb += stars * _StarColor.rgb * _StarBrightness;
                color.a *= coverage;
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                return color;
            }
            ENDCG
        }
    }
}
