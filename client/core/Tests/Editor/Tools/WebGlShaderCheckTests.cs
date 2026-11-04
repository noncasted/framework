using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Internal.Tests
{
    // The check finds each problem in the WebGL code of a shader made in memory, and nothing in a clean one. The framework
    // ships no shader of its own outside the forked packages, so these are its only shaders under the check.
    [Category("Tools")]
    public class WebGlShaderCheckTests
    {
        private const string Clean = "return half4(i.uv, 0, 1);";

        // A static const table indexed by a value of the pixel: fxc keeps it as an immediate constant buffer.
        private const string Table =
            "static const float table[4] = { 0.1, 0.5, 0.3, 0.9 };\n" +
            "return half4(table[(uint)(i.uv.x * 4) & 3], 0, 0, 1);";

        // A local array of uniforms indexed by a value of the pixel: fxc makes an indexable temporary.
        private const string Ramp =
            "float4 ramp[3] = { _Ramp0, _Ramp1, _Ramp2 };\n" +
            "return half4(ramp[(uint)(i.uv.x * 3) % 3]);";

        [Test]
        public void Check_CleanShader_FindsNothing()
        {
            var report = Check(Make(Clean, false));

            Assert.AreEqual(1, report.Compiled);
            Assert.IsEmpty(report.Problems);
        }

        [Test]
        public void Check_TableIndexedAtRunTime_FindsImmCB()
        {
            AssertFinds(Make(Table, false), "ImmCB_");
        }

        [Test]
        public void Check_LocalArrayIndexedAtRunTime_FindsTempArray()
        {
            AssertFinds(Make(Ramp, false), "TempArray");
        }

        [Test]
        public void Check_Nointerpolation_FindsFlatVarying()
        {
            AssertFinds(Make(Clean, true), "flat varying");
        }

        [Test]
        public void AssertSafe_Problem_FailsWithTheShaderAndTheProblem()
        {
            var shader = Make(Table, false);

            try
            {
                var exception = Assert.Throws<AssertionException>(() => WebGlShaderCheck.AssertSafe(new[] { shader }));

                StringAssert.Contains($"{shader.name} subshader 0 pass 0 []: ImmCB_", exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(shader);
            }
        }

        private static void AssertFinds(Shader shader, string problem)
        {
            var report = Check(shader);

            Assert.AreEqual(1, report.Problems.Count, string.Join("\n", report.Problems));
            StringAssert.Contains(problem, report.Problems.Single());
        }

        private static WebGlShaderReport Check(Shader shader)
        {
            try
            {
                return WebGlShaderCheck.Check(new[] { shader });
            }
            finally
            {
                Object.DestroyImmediate(shader);
            }
        }

        // Self-contained HLSL (no include of a pipeline): one pass, no keywords; the id is a flat varying when asked.
        private static Shader Make(string fragment, bool isFlat)
        {
            var id = isFlat ? "nointerpolation float id : TEXCOORD1;" : "float id : TEXCOORD1;";
            var source = @"Shader ""Hidden/WebGlShaderCheckTests""
{
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            float4x4 _Matrix;
            float4 _Ramp0;
            float4 _Ramp1;
            float4 _Ramp2;

            struct Varyings
            {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                " + id + @"
            };

            Varyings vert(float4 position : POSITION, float2 uv : TEXCOORD0)
            {
                Varyings o;
                o.position = mul(_Matrix, position);
                o.uv = uv;
                o.id = position.z;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                i.uv.y += i.id;
                " + fragment + @"
            }
            ENDHLSL
        }
    }
}";
            var shader = ShaderUtil.CreateShaderAsset(source, false);
            var messages = ShaderUtil.GetShaderMessages(shader).Select(message => message.message);
            Assert.IsFalse(ShaderUtil.ShaderHasError(shader), string.Join("\n", messages));
            return shader;
        }
    }
}
