Shader "ProjectHive/Encounter Diagnostic"
{
    Properties { [Enum(UnityEngine.Rendering.CompareFunction)] _ZTest("Depth Test", Float) = 4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest [_ZTest]
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; };
            Varyings Vert(Attributes input) { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz); o.color=input.color; return o; }
            half4 Frag(Varyings input):SV_Target { return input.color; }
            ENDHLSL
        }
    }
}
