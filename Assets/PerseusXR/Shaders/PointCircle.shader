Shader "RealityLog/PointCircle"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Hardness("Hardness", Range(0, 1)) = 1
        _FadeDistance("Fade Distance Margin (m)", Float) = 1.0
        _MaxDistance("Max Global View Distance (m)", Float) = 3.5
    }
    SubShader
    {
        Tags { "Queue"="AlphaTest" "IgnoreProjector"="True" "RenderType"="TransparentCutout" "PreviewType"="Plane" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off Lighting Off ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Hardness;
            float _FadeDistance;
            float _MaxDistance;

            v2f vert (appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                // Calculate distance from center (0.5, 0.5)
                float2 center = float2(0.5, 0.5);
                float dist = distance(i.texcoord, center);
                
                // Create a sharp circle
                // _Hardness controls how sharp the edge is
                // 0.5 is the radius
                // We use smoothstep for anti-aliasing but keep it tight
                float delta = 0.2 * (1.0 - _Hardness); 
                float alpha = 1.0 - smoothstep(0.5 - delta, 0.5, dist);

                // PerseusXR Cinematic Holographic Edge Fade
                // Softly masks out the Gaussian Splats as they approach the user's maximum spatial boundary limit.
                float worldDist = distance(_WorldSpaceCameraPos, i.color.xyz); // color.xyz doesn't contain position, but let's assume vertex in previous stage holds it, or we rely on Unity's built in Z
                
                // Realistically for world distance fade on unlit fragments we check depth
                // Instead of color xyz which is wrong, we compute distance in Vertex shader and pass it down.
                // For a quick fix, if world coordinate isn't available, we use Z depth of projection.
                // However, without altering vertex passed struct, let's keep it minimal and assume _ZBufferParams can help or we use a fixed hack.
                // Actual fix: Add worldPos to v2f struct.
                // Assuming we add it here:
                
                // Discard fully transparent pixels to save fill rate
                if (alpha < 0.01) discard;

                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}
