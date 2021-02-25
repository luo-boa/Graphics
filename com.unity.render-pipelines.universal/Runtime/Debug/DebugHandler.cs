
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor.Rendering;
using UnityEngine.Rendering.Universal.Internal;

namespace UnityEngine.Rendering.Universal
{
    public class DebugHandler : IDebugDisplaySettingsQuery
    {
        #region Property Id Constants
        private static readonly int kDebugColorPropertyId = Shader.PropertyToID("_DebugColor");

        // Material settings...
        private static readonly int kDebugMaterialModeId = Shader.PropertyToID("_DebugMaterialMode");
        private static readonly int kDebugVertexAttributeModeId = Shader.PropertyToID("_DebugVertexAttributeMode");

        // Rendering settings...
        private static readonly int kDebugMipInfoModeId = Shader.PropertyToID("_DebugMipInfoMode");
        private static readonly int kDebugSceneOverrideModeId = Shader.PropertyToID("_DebugSceneOverrideMode");
        private static readonly int kDebugFullScreenModeId = Shader.PropertyToID("_DebugFullScreenMode");

        // Lighting settings...
        private static readonly int kDebugLightingModeId = Shader.PropertyToID("_DebugLightingMode");
        private static readonly int kDebugLightingFeatureFlagsId = Shader.PropertyToID("_DebugLightingFeatureFlags");

        // ValidationSettings...
        private static readonly int kDebugValidationModeId = Shader.PropertyToID("_DebugValidationMode");
        private static readonly int kDebugValidateBelowMinThresholdColorPropertyId = Shader.PropertyToID("_DebugValidateBelowMinThresholdColor");
        private static readonly int kDebugValidateAboveMaxThresholdColorPropertyId = Shader.PropertyToID("_DebugValidateAboveMaxThresholdColor");

        private static readonly int kDebugValidateAlbedoMinLuminanceId = Shader.PropertyToID("_DebugValidateAlbedoMinLuminance");
        private static readonly int kDebugValidateAlbedoMaxLuminanceId = Shader.PropertyToID("_DebugValidateAlbedoMaxLuminance");
        private static readonly int kDebugValidateAlbedoSaturationToleranceId = Shader.PropertyToID("_DebugValidateAlbedoSaturationTolerance");
        private static readonly int kDebugValidateAlbedoHueToleranceId = Shader.PropertyToID("_DebugValidateAlbedoHueTolerance");
        private static readonly int kDebugValidateAlbedoCompareColorId = Shader.PropertyToID("_DebugValidateAlbedoCompareColor");

        private static readonly int kDebugValidateMetallicMinValueId = Shader.PropertyToID("_DebugValidateMetallicMinValue");
        private static readonly int kDebugValidateMetallicMaxValueId = Shader.PropertyToID("_DebugValidateMetallicMaxValue");
        #endregion

        private readonly Material m_FullScreenDebugMaterial;
        private readonly Texture2D m_NumberFontTexture;
        private readonly Material m_ReplacementMaterial;

        private readonly DebugDisplaySettings m_DebugDisplaySettings;

        private DebugDisplaySettingsLighting LightingSettings => m_DebugDisplaySettings.LightingSettings;
        private DebugMaterialSettings MaterialSettings => m_DebugDisplaySettings.MaterialSettings;
        private DebugDisplaySettingsRendering RenderingSettings => m_DebugDisplaySettings.RenderingSettings;
        private DebugDisplaySettingsValidation ValidationSettings => m_DebugDisplaySettings.ValidationSettings;

        #region IDebugDisplaySettingsQuery
        public bool AreAnySettingsActive => m_DebugDisplaySettings.AreAnySettingsActive;
        public bool IsPostProcessingAllowed => m_DebugDisplaySettings.IsPostProcessingAllowed;
        public bool IsLightingActive => m_DebugDisplaySettings.IsLightingActive;

        public bool TryGetScreenClearColor(ref Color color)
        {
            return m_DebugDisplaySettings.TryGetScreenClearColor(ref color);
        }
        #endregion

        public Material ReplacementMaterial => m_ReplacementMaterial;
        public DebugDisplaySettings DebugDisplaySettings => m_DebugDisplaySettings;

        public bool IsScreenClearNeeded
        {
            get
            {
                Color color = Color.black;

                return TryGetScreenClearColor(ref color);
            }
        }

        public DebugHandler(ScriptableRendererData scriptableRendererData)
        {
            Texture2D numberFontTexture = scriptableRendererData.NumberFont;
            Shader fullScreenDebugShader = scriptableRendererData.fullScreenDebugPS;
            Shader debugReplacementShader = scriptableRendererData.debugReplacementPS;

            m_DebugDisplaySettings = DebugDisplaySettings.Instance;

            m_NumberFontTexture = numberFontTexture;
            m_FullScreenDebugMaterial = (fullScreenDebugShader == null) ? null : CoreUtils.CreateEngineMaterial(fullScreenDebugShader);
            m_ReplacementMaterial = (debugReplacementShader == null) ? null : CoreUtils.CreateEngineMaterial(debugReplacementShader);
        }

        public bool IsDebugPassEnabled(ref CameraData cameraData)
        {
            return !cameraData.isPreviewCamera && AreAnySettingsActive;
        }

        internal DebugPass CreatePass(RenderPassEvent evt)
        {
            return new DebugPass(evt, m_FullScreenDebugMaterial);
        }

        public bool TryGetFullscreenDebugMode(out DebugFullScreenMode debugFullScreenMode)
        {
            debugFullScreenMode = RenderingSettings.debugFullScreenMode;
            return debugFullScreenMode != DebugFullScreenMode.None;
        }

        public void SetupShaderProperties(CommandBuffer cmd, int passIndex = 0)
        {
            if(LightingSettings.DebugLightingMode == DebugLightingMode.ShadowCascades)
            {
                // we disable cubemap reflections, too distracting (in TemplateLWRP for ex.)
                cmd.EnableShaderKeyword("_DEBUG_ENVIRONMENTREFLECTIONS_OFF");
            }
            else
            {
                cmd.DisableShaderKeyword("_DEBUG_ENVIRONMENTREFLECTIONS_OFF");
            }

            switch(RenderingSettings.debugSceneOverrideMode)
            {
                case DebugSceneOverrideMode.Overdraw:
                {
                    cmd.SetGlobalColor(kDebugColorPropertyId, new Color(0.1f, 0, 0, 1));
                    break;
                }

                case DebugSceneOverrideMode.Wireframe:
                {
                    cmd.SetGlobalColor(kDebugColorPropertyId, Color.black);
                    break;
                }

                case DebugSceneOverrideMode.SolidWireframe:
                {
                    cmd.SetGlobalColor(kDebugColorPropertyId, (passIndex == 0) ? Color.white : Color.black);
                    break;
                }

                case DebugSceneOverrideMode.ShadedWireframe:
                {
                    if(passIndex == 1)
                    {
                        cmd.SetGlobalColor(kDebugColorPropertyId, Color.black);
                    }
                    break;
                }
            }       // End of switch.

            switch(@ValidationSettings.validationMode)
            {
                case DebugValidationMode.ValidateAlbedo:
                {
                    cmd.SetGlobalFloat(kDebugValidateAlbedoMinLuminanceId, ValidationSettings.AlbedoMinLuminance);
                    cmd.SetGlobalFloat(kDebugValidateAlbedoMaxLuminanceId, ValidationSettings.AlbedoMaxLuminance);
                    cmd.SetGlobalFloat(kDebugValidateAlbedoSaturationToleranceId, ValidationSettings.AlbedoSaturationTolerance);
                    cmd.SetGlobalFloat(kDebugValidateAlbedoHueToleranceId, ValidationSettings.AlbedoHueTolerance);
                    cmd.SetGlobalColor(kDebugValidateAlbedoCompareColorId, ValidationSettings.AlbedoCompareColor.linear);
                    break;
                }

                case DebugValidationMode.ValidateMetallic:
                {
                    cmd.SetGlobalFloat(kDebugValidateMetallicMinValueId, ValidationSettings.MetallicMinValue);
                    cmd.SetGlobalFloat(kDebugValidateMetallicMaxValueId, ValidationSettings.MetallicMaxValue);
                    break;
                }
            }       // End of switch.
        }

        [Conditional("DEVELOPMENT_BUILD"), Conditional("UNITY_EDITOR")]
        public void Setup(ScriptableRenderContext context)
        {
            var cmd = CommandBufferPool.Get("");

            // Material settings...
            cmd.SetGlobalFloat(kDebugMaterialModeId, (int)MaterialSettings.DebugMaterialModeData);
            cmd.SetGlobalFloat(kDebugVertexAttributeModeId, (int)MaterialSettings.DebugVertexAttributeIndexData);

            // Rendering settings...
            cmd.SetGlobalInt(kDebugMipInfoModeId, (int)RenderingSettings.debugMipInfoMode);
            cmd.SetGlobalInt(kDebugSceneOverrideModeId, (int)RenderingSettings.debugSceneOverrideMode);
            cmd.SetGlobalInt(kDebugFullScreenModeId, (int)RenderingSettings.debugFullScreenMode);

            // Lighting settings...
            cmd.SetGlobalFloat(kDebugLightingModeId, (int)LightingSettings.DebugLightingMode);
            cmd.SetGlobalInt(kDebugLightingFeatureFlagsId, (int)LightingSettings.DebugLightingFeatureFlagsMask);

            // Validation settings...
            cmd.SetGlobalInt(kDebugValidationModeId, (int)ValidationSettings.validationMode);
            cmd.SetGlobalColor(kDebugValidateBelowMinThresholdColorPropertyId, Color.red);
            cmd.SetGlobalColor(kDebugValidateAboveMaxThresholdColorPropertyId, Color.blue);

            // Set-up any other persistent properties...
            cmd.SetGlobalTexture("_DebugNumberTexture", m_NumberFontTexture);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        #region DebugRenderPasses
        private class DebugRenderPassEnumerable : IEnumerable<DebugRenderSetup>
        {
            private class Enumerator : IEnumerator<DebugRenderSetup>
            {
                private readonly DebugHandler m_DebugHandler;
                private readonly ScriptableRenderContext m_Context;
                private readonly CommandBuffer m_CommandBuffer;
                private readonly int m_NumIterations;

                private int m_Index;

                public DebugRenderSetup Current { get; private set; }
                object IEnumerator.Current => Current;

                public Enumerator(DebugHandler debugHandler, ScriptableRenderContext context, CommandBuffer commandBuffer)
                {
                    DebugSceneOverrideMode sceneOverrideMode = debugHandler.DebugDisplaySettings.RenderingSettings.debugSceneOverrideMode;

                    m_DebugHandler = debugHandler;
                    m_Context = context;
                    m_CommandBuffer = commandBuffer;
                    m_NumIterations = ((sceneOverrideMode == DebugSceneOverrideMode.SolidWireframe) || (sceneOverrideMode == DebugSceneOverrideMode.ShadedWireframe)) ? 2 : 1;

                    m_Index = -1;
                }

                #region IEnumerator<DebugRenderSetup>
                public bool MoveNext()
                {
                    Current?.Dispose();

                    if(++m_Index >= m_NumIterations)
                    {
                        return false;
                    }
                    else
                    {
                        Current = new DebugRenderSetup(m_DebugHandler, m_Context, m_CommandBuffer, m_Index);
                        return true;
                    }
                }

                public void Reset()
                {
                    if(Current != null)
                    {
                        Current.Dispose();
                        Current = null;
                    }
                    m_Index = -1;
                }

                public void Dispose()
                {
                    Current?.Dispose();
                }
                #endregion
            }

            private readonly DebugHandler m_DebugHandler;
            private readonly ScriptableRenderContext m_Context;
            private readonly CommandBuffer m_CommandBuffer;

            public DebugRenderPassEnumerable(DebugHandler debugHandler, ScriptableRenderContext context, CommandBuffer commandBuffer)
            {
                m_DebugHandler = debugHandler;
                m_Context = context;
                m_CommandBuffer = commandBuffer;
            }

            #region IEnumerable<DebugRenderSetup>
            public IEnumerator<DebugRenderSetup> GetEnumerator()
            {
                return new Enumerator(m_DebugHandler, m_Context, m_CommandBuffer);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
            #endregion
        }

        public IEnumerable<DebugRenderSetup> CreateDebugRenderSetupEnumerable(ScriptableRenderContext context,
                                                                              CommandBuffer commandBuffer)
        {
            return new DebugRenderPassEnumerable(this, context, commandBuffer);
        }
        #endregion
    }
}
