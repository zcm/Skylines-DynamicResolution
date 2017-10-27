using System;
using System.Reflection;
using UnityEngine;
using ColossalFramework.Plugins;
using ColossalFramework.UI;
using ColossalFramework;
using System.Collections;
using System.Collections.Generic;

namespace DynamicResolution
{
    class Redirect
    {
        public MethodInfo from;
        public RedirectCallsState state;

        internal Redirect(MethodInfo from, RedirectCallsState state)
        {
            this.from = from;
            this.state = state;
        }
    }

    public class CameraRenderer : MonoBehaviour
    {
        public RenderTexture fullResRT;
        public RenderTexture halfVerticalResRT;

        public static Camera mainCamera;
        public Camera camera;

        private Rect unitRect;

        private Material downsampleShader;
        private Material downsampleX2Shader;

        private string checkErrorMessage = null;

        private static UndergroundView undergroundView;
        private static Camera undergroundCamera;

        private static FieldInfo undergroundRGBDField;

        private static CameraController cameraController;
        private static FieldInfo cachedFreeCameraField;

        private static string cachedModPath = null;

        private Stack<Redirect> redirectionStack = new Stack<Redirect>();

        private void pushRedirect(MethodInfo from, MethodInfo to)
        {
            redirectionStack.Push(new Redirect(from, RedirectionHelper.RedirectCalls(from, to)));
        }

        private void revertAllRedirects()
        {
            while (redirectionStack.Count > 0)
            {
                Redirect redirect = redirectionStack.Pop();
                RedirectionHelper.RevertRedirect(redirect.from, redirect.state);
            }
        }

        static string modPath
        {
            get
            {
                if (cachedModPath == null)
                {
                    cachedModPath =
                        PluginManager.instance.FindPluginInfo(Assembly.GetAssembly(typeof(CameraRenderer))).modPath;
                }

                return cachedModPath;
            }
        }

        void HandleCheckError(string message)
        {
#if (DEBUG)
            DebugOutputPanel.AddMessage(PluginManager.MessageType.Error, message);
#endif
            if (checkErrorMessage == null)
            {
                checkErrorMessage = message;
            }
            else
            {
                checkErrorMessage += "; " + message;
            }
        }

        void ThrowPendingCheckErrors()
        {
            if (checkErrorMessage != null)
            {
                throw new Exception(checkErrorMessage);
            }
        }

        void CheckAssetBundle(AssetBundle assetBundle, string assetsUri)
        {
            if (assetBundle == null)
            {
                HandleCheckError("AssetBundle with URI '" + assetsUri + "' could not be loaded");
            }
#if (DEBUG)
            else
            {
                DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Mod Assets URI: " + assetsUri);
                foreach (string asset in assetBundle.GetAllAssetNames())
                {
                    DebugOutputPanel.AddMessage(PluginManager.MessageType.Message, "Asset: " + asset);
                }
            }
#endif
        }

        void CheckShader(Shader shader, string source)
        {
            if (shader == null)
            {
                HandleCheckError("Shader " + source + " is missing or invalid");
            }
            else
            {
                if (!shader.isSupported)
                {
                    HandleCheckError("Shader '" + shader.name + "' " + source + " is not supported");
                }
#if (DEBUG)
                else
                {
                    DebugOutputPanel.AddMessage(
                        PluginManager.MessageType.Message,
                        "Shader '" + shader.name + "' " + source + " loaded");
                }
#endif
            }
        }

        void CheckShader(Shader shader, AssetBundle assetBundle, string shaderAssetName)
        {
            CheckShader(shader, "from asset '" + shaderAssetName + "'");
        }

        void CheckMaterial(Material material, string materialAssetName) {
            if (material == null)
            {
                HandleCheckError("Material for shader '" + materialAssetName + "' could not be created");
            }
#if (DEBUG)
            else
            {
                DebugOutputPanel.AddMessage(
                    PluginManager.MessageType.Message,
                    "Material for shader '" + materialAssetName + "' created");
            }
#endif
        }

        void LoadShaders()
        {
            string assetsUri = "file:///" + modPath.Replace("\\", "/") + "/dynamicresolutionshaders";
            WWW www = new WWW(assetsUri);
            AssetBundle assetBundle = www.assetBundle;

            CheckAssetBundle(assetBundle, assetsUri);
            ThrowPendingCheckErrors();

            string downsampleAssetName = "downsampleShader.shader";
            string downsampleX2AssetName = "downsampleX2Shader.shader";
            Shader downsampleShaderContent = assetBundle.LoadAsset(downsampleAssetName) as Shader;
            Shader downsampleX2ShaderContent = assetBundle.LoadAsset(downsampleX2AssetName) as Shader;

            CheckShader(downsampleShaderContent, assetBundle, downsampleAssetName);
            CheckShader(downsampleX2ShaderContent, assetBundle, downsampleX2AssetName);
            ThrowPendingCheckErrors();

            string downsampleShaderMaterialAsset = downsampleAssetName;
            string downsampleX2ShaderMaterialAsset = downsampleX2AssetName;
            downsampleShader = new Material(downsampleShaderContent);
            downsampleX2Shader = new Material(downsampleX2ShaderContent);

            CheckMaterial(downsampleShader, downsampleShaderMaterialAsset);
            CheckMaterial(downsampleX2Shader, downsampleX2ShaderMaterialAsset);
            ThrowPendingCheckErrors();

            assetBundle.Unload(false);
        }

        public void Awake()
        {
            camera = GetComponent<Camera>();

            unitRect = new Rect(0f, 0f, 1f, 1f);

            LoadShaders();

            undergroundView = FindObjectOfType<UndergroundView>();
            undergroundRGBDField = typeof (UndergroundView).GetField("m_undergroundRGBD",
                BindingFlags.Instance | BindingFlags.NonPublic);

            undergroundCamera = Util.GetPrivate<Camera>(undergroundView, "m_undergroundCamera");

            cameraController = FindObjectOfType<CameraController>();
            cachedFreeCameraField = typeof (CameraController)
                .GetField("m_cachedFreeCamera", BindingFlags.Instance | BindingFlags.NonPublic);

            pushRedirect(
                typeof (UndergroundView).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic),
                typeof (CameraRenderer).GetMethod("UndegroundViewLateUpdate", BindingFlags.Instance | BindingFlags.NonPublic));

            pushRedirect(
                typeof (CameraController).GetMethod("UpdateFreeCamera", BindingFlags.Instance | BindingFlags.NonPublic),
                typeof (CameraRenderer).GetMethod("CameraControllerUpdateFreeCamera", BindingFlags.Instance | BindingFlags.NonPublic));
        }

        void OnDestroy()
        {
            revertAllRedirects();
        }

        public void Update()
        {
            camera.fieldOfView = mainCamera.fieldOfView;
            camera.nearClipPlane = mainCamera.nearClipPlane;
            camera.farClipPlane = mainCamera.farClipPlane;
            camera.transform.position = mainCamera.transform.position;
            camera.transform.rotation = mainCamera.transform.rotation;
            camera.rect = mainCamera.rect;
        }

        void UndegroundViewLateUpdate()
        {
            var undergroundRGBD = Util.GetFieldValue<RenderTexture>(undergroundRGBDField, undergroundView);

            if (undergroundRGBD != null)
            {
                RenderTexture.ReleaseTemporary(undergroundRGBD);
                Util.SetFieldValue(undergroundRGBDField, undergroundView, null);
            }

            if (undergroundCamera != null && mainCamera != null)
            {
#if DEBUG
                CameraHook.undergroundCameraRect = undergroundCamera.rect;
#endif
                if (undergroundCamera.cullingMask != 0)
                {
                    int width = CameraHook.instance.width;
                    int height = CameraHook.instance.height;
                    undergroundRGBD = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);

                    undergroundCamera.fieldOfView = mainCamera.fieldOfView;
                    undergroundCamera.nearClipPlane = mainCamera.nearClipPlane;
                    undergroundCamera.farClipPlane = mainCamera.farClipPlane;
                    undergroundCamera.rect = mainCamera.rect;
                    undergroundCamera.targetTexture = undergroundRGBD;
                    undergroundCamera.enabled = true;

                    Util.SetFieldValue(undergroundRGBDField, undergroundView, undergroundRGBD);
                }
                else
                {
                    undergroundCamera.enabled = false;
                }
            }
        }

        void CameraControllerUpdateFreeCamera()
        {
            bool m_cachedFreeCamera = Util.GetFieldValue<bool>(cachedFreeCameraField, cameraController);

            if (cameraController.m_freeCamera != m_cachedFreeCamera)
            {
                m_cachedFreeCamera = cameraController.m_freeCamera;
                Util.SetFieldValue(cachedFreeCameraField, cameraController, m_cachedFreeCamera);

                UIView.Show(UIView.HasModalInput() || !m_cachedFreeCamera);
                Singleton<NotificationManager>.instance.NotificationsVisible = !m_cachedFreeCamera;
                Singleton<GameAreaManager>.instance.BordersVisible = !m_cachedFreeCamera;
                Singleton<DistrictManager>.instance.NamesVisible = !m_cachedFreeCamera;
                Singleton<PropManager>.instance.MarkersVisible = !m_cachedFreeCamera;
                Singleton<GuideManager>.instance.TutorialDisabled = m_cachedFreeCamera;
                Singleton<DisasterManager>.instance.MarkersVisible = !m_cachedFreeCamera;
                Singleton<NetManager>.instance.RoadNamesVisible = !m_cachedFreeCamera;
            }

            Util.GetPrivate<Camera>(cameraController, "m_camera").rect = new Rect(0f, 0f, 1f, 1f);
        }

        public void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (fullResRT == null)
            {
                return;
            }

            var oldRect = mainCamera.rect;
            mainCamera.rect = unitRect;
            mainCamera.targetTexture = fullResRT;
            mainCamera.Render();
            mainCamera.targetTexture = null;
            mainCamera.rect = oldRect;

#if DEBUG
            CameraHook.mainCameraRect = oldRect;
#endif

            float factor = CameraHook.instance.currentSSAAFactor;

            if (factor != 1.0f && halfVerticalResRT != null)
            {
             
                Material shader = downsampleShader;

                if (factor <= 2.0f)
                {
                    shader = downsampleX2Shader;
                }

                downsampleShader.SetVector("_ResampleOffset", new Vector4(fullResRT.texelSize.x, 0.0f, 0.0f, 0.0f));
                Graphics.Blit(fullResRT, halfVerticalResRT, shader);

                downsampleShader.SetVector("_ResampleOffset", new Vector4(0.0f, fullResRT.texelSize.y, 0.0f, 0.0f));
                Graphics.Blit(halfVerticalResRT, dst, shader);
            }
            else
            {
                Graphics.Blit(fullResRT, dst);
            }
        }

    }

}
