using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Unity.Rea.ProjectVania.EditorTools
{
    /// <summary>
    /// Builds the game's URP 2D pipeline in code (U.VAN-T6), so no one wires a pipeline asset by hand. It creates
    /// <see cref="PipelinePath"/> with its 2D renderer (<see cref="RendererPath"/>) if they are missing and assigns it
    /// as the default pipeline and on every quality level, with each level's antiAliasing matched to the pipeline's
    /// MSAA (URP rewrites it on every render otherwise). It only creates what is missing and only assigns what
    /// differs, so rerunning it and every headless import after it leave the committed assets untouched. The Runtime
    /// never references URP: only this Editor assembly knows which pipeline draws.
    /// </summary>
    public static class RenderPipelineBuilder
    {
        public const string Folder = "Assets/Unity.Rea.ProjectVania/Rendering";
        public const string PipelinePath = Folder + "/ProjectVaniaURP.asset";
        public const string RendererPath = Folder + "/ProjectVania2DRenderer.asset";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        [MenuItem("MindAttic/ProjectVania/Build Render Pipeline")]
        public static void BuildPipeline() => Ensure();

        /// <summary>Creates the pipeline and renderer if missing and makes them the active pipeline everywhere.</summary>
        public static UniversalRenderPipelineAsset Ensure()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = CreatePipeline();
            }

            if (GraphicsSettings.defaultRenderPipeline != pipeline)
            {
                GraphicsSettings.defaultRenderPipeline = pipeline;
            }

            // The pipeline's 1 sample is antiAliasing 0.
            var antiAliasing = pipeline.msaaSampleCount > 1 ? pipeline.msaaSampleCount : 0;
            var current = QualitySettings.GetQualityLevel();
            for (var level = 0; level < QualitySettings.count; level++)
            {
                // QualitySettings only exposes the active level for writing.
                QualitySettings.SetQualityLevel(level, false);
                if (QualitySettings.renderPipeline != pipeline)
                {
                    QualitySettings.renderPipeline = pipeline;
                }

                if (QualitySettings.antiAliasing != antiAliasing)
                {
                    QualitySettings.antiAliasing = antiAliasing;
                }
            }

            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            return pipeline;
        }

        static UniversalRenderPipelineAsset CreatePipeline()
        {
            Directory.CreateDirectory(Folder);

            var renderer = AssetDatabase.LoadAssetAtPath<Renderer2DData>(RendererPath);
            if (renderer == null)
            {
                // As URP's own "2D Renderer" create menu does: the default post-process data, then the package's
                // shaders and materials filled into every null resource slot.
                renderer = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                var serialized = new SerializedObject(renderer);
                serialized.FindProperty("m_PostProcessData").objectReferenceValue =
                    AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                ResourceReloader.ReloadAllNullIn(renderer, UniversalRenderPipelineAsset.packagePath);
                EditorUtility.SetDirty(renderer);
            }

            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            AssetDatabase.SaveAssets();
            return pipeline;
        }
    }
}
