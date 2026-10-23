// The look of every model in the game: painted vertex colors (with ambient occlusion baked in by Tools/ArtGen),
// glowing parts where the vertex alpha is above zero, and a soft rim light so miniatures read against the board.
// Per-object tint and desaturation (sleeping enemies, fallen heroes, loot rarity) come from a MaterialPropertyBlock.
Shader "Shards/Miniature"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Desaturate ("Desaturate", Range(0, 1)) = 0
        _Glow ("Glow strength", Float) = 1.6
        _GlowTint ("Glow tint", Color) = (1, 1, 1, 1)
        _RimColor ("Rim color", Color) = (0.6, 0.5, 0.9, 0.35)
        _RimPower ("Rim power", Float) = 3
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        struct Input
        {
            float4 color : COLOR;
            float3 viewDir;
            float3 worldNormal;
        };

        fixed4 _Tint;
        half _Desaturate;
        half _Glow;
        fixed4 _GlowTint;
        fixed4 _RimColor;
        half _RimPower;
        half _Smoothness;

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            half3 c = IN.color.rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
            c = GammaToLinearSpace(c);
            #endif
            c *= _Tint.rgb;
            half grey = dot(c, half3(0.3, 0.59, 0.11));
            c = lerp(c, grey.xxx, _Desaturate);
            half g = IN.color.a;
            o.Albedo = c * (1 - g);
            o.Emission = c * _GlowTint.rgb * g * _Glow;
            half rim = pow(1 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal))), _RimPower);
            o.Emission += _RimColor.rgb * _RimColor.a * rim * (1 - g) * (1 - _Desaturate);
            o.Smoothness = _Smoothness;
            o.Metallic = 0;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
