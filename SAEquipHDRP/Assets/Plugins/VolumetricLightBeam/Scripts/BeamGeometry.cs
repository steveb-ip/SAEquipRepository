#if DEBUG
//#define DEBUG_SHOW_MESH_NORMALS
#endif
#define FORCE_CURRENT_CAMERA_DEPTH_TEXTURE_MODE

#if UNITY_2018_1_OR_NEWER
#define SRP_SUPPORT // Comment this to disable SRP support

#if SRP_SUPPORT
#if UNITY_2019_1_OR_NEWER
using AliasCurrentPipeline = UnityEngine.Rendering.RenderPipelineManager;
using AliasCameraEvents = UnityEngine.Rendering.RenderPipelineManager;
#else
using AliasCurrentPipeline = UnityEngine.Experimental.Rendering.RenderPipelineManager;
using AliasCameraEvents = UnityEngine.Experimental.Rendering.RenderPipeline;
#endif // UNITY_2019_1_OR_NEWER
#endif // SRP_SUPPORT
#endif

using UnityEngine;

#pragma warning disable 0429, 0162 // Unreachable expression code detected (because of Noise3D.isSupported on mobile)

namespace VLB
{
    [AddComponentMenu("")] // hide it from Component search
    [ExecuteInEditMode]
    [HelpURL(Consts.HelpUrlBeam)]
    public class BeamGeometry : MonoBehaviour
    {
        VolumetricLightBeam m_Master = null;
        Matrix4x4 m_ColorGradientMatrix;
        MeshType m_CurrentMeshType = MeshType.Shared;

        public MeshRenderer meshRenderer { get; private set; }
        public MeshFilter meshFilter { get; private set; }
        public Material material { get; private set; }
        public Mesh coneMesh { get; private set; }

        public bool visible
        {
            get { return meshRenderer.enabled; }
            set { meshRenderer.enabled = value; }
        }

        public int sortingLayerID
        {
            get { return meshRenderer.sortingLayerID; }
            set { meshRenderer.sortingLayerID = value; }
        }

        public int sortingOrder
        {
            get { return meshRenderer.sortingOrder; }
            set { meshRenderer.sortingOrder = value; }
        }

        void Start()
        {
            // Handle copy / paste the LightBeam in Editor
            if (!m_Master)
                DestroyImmediate(gameObject);
        }

        void OnDestroy()
        {
            if (material)
            {
                DestroyImmediate(material);
                material = null;
            }
        }

#if SRP_SUPPORT
        static bool IsUsingCustomRenderPipeline()
        {
            return AliasCurrentPipeline.currentPipeline != null || UnityEngine.Rendering.GraphicsSettings.renderPipelineAsset != null;
        }

        void OnEnable()
        {
            if (IsUsingCustomRenderPipeline())
                AliasCameraEvents.beginCameraRendering += OnBeginCameraRendering;
        }

        void OnDisable()
        {
            if (IsUsingCustomRenderPipeline())
                AliasCameraEvents.beginCameraRendering -= OnBeginCameraRendering;
        }
#endif

        public void Initialize(VolumetricLightBeam master, Shader shader)
        {
            var hideFlags = Consts.ProceduralObjectsHideFlags;
            m_Master = master;

            transform.SetParent(master.transform, false);
            material = new Material(shader);
            material.hideFlags = hideFlags;

            meshRenderer = gameObject.GetOrAddComponent<MeshRenderer>();
            meshRenderer.hideFlags = hideFlags;
            meshRenderer.material = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
#if UNITY_5_4_OR_NEWER
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
#else
            meshRenderer.useLightProbes = false;
#endif
            if(SortingLayer.IsValid(m_Master.sortingLayerID))
                sortingLayerID = m_Master.sortingLayerID;
            else
                Debug.LogError(string.Format("Beam '{0}' has an invalid sortingLayerID ({1}). Please fix it by setting a valid layer.", Utils.GetPath(m_Master.transform), m_Master.sortingLayerID));

            sortingOrder = m_Master.sortingOrder;

            meshFilter = gameObject.GetOrAddComponent<MeshFilter>();
            meshFilter.hideFlags = hideFlags;

            gameObject.hideFlags = hideFlags;
        }

        /// <summary>
        /// Generate the cone mesh and calls UpdateMaterialAndBounds.
        /// Since this process involves recreating a new mesh, make sure to not call it at every frame during playtime.
        /// </summary>
        public void RegenerateMesh()
        {
            Debug.Assert(m_Master);

            if (Config.Instance.geometryOverrideLayer)
                gameObject.layer = Config.Instance.geometryLayerID;
            else
                gameObject.layer = m_Master.gameObject.layer;

            gameObject.tag = Config.Instance.geometryTag;

            if (coneMesh && m_CurrentMeshType == MeshType.Custom)
            {
                DestroyImmediate(coneMesh);
            }

            m_CurrentMeshType = m_Master.geomMeshType;

            switch (m_Master.geomMeshType)
            {
                case MeshType.Custom:
                    {
                        coneMesh = MeshGenerator.GenerateConeZ_Radius(1f, 1f, 1f, m_Master.geomCustomSides, m_Master.geomCustomSegments, m_Master.geomCap, Config.Instance.forceSinglePass);
                        coneMesh.hideFlags = Consts.ProceduralObjectsHideFlags;
                        meshFilter.mesh = coneMesh;
                        break;
                    }
                case MeshType.Shared:
                    {
                        coneMesh = GlobalMesh.mesh;
                        meshFilter.sharedMesh = coneMesh;
                        break;
                    }
                default:
                    {
                        Debug.LogError("Unsupported MeshType");
                        break;
                    }
            }

            UpdateMaterialAndBounds();
        }

        void ComputeLocalMatrix()
        {
            // In the VS, we compute the vertices so the whole beam fits into a fixed 2x2x1 box.
            // We have to apply some scaling to get the proper beam size.
            // This way we have the proper bounds without having to recompute specific bounds foreach beam.
            var maxRadius = Mathf.Max(m_Master.coneRadiusStart, m_Master.coneRadiusEnd);
            transform.localScale = new Vector3(maxRadius, maxRadius, m_Master.fadeEnd);
        }

        public void UpdateMaterialAndBounds()
        {
            Debug.Assert(m_Master);

            material.renderQueue = Config.Instance.geometryRenderQueue;

            float slopeRad = (m_Master.coneAngle * Mathf.Deg2Rad) / 2; // use coneAngle (instead of spotAngle) which is more correct with the geometry
            material.SetVector("_ConeSlopeCosSin", new Vector2(Mathf.Cos(slopeRad), Mathf.Sin(slopeRad)));

            // kMinRadius and kMinApexOffset prevents artifacts when fresnel computation is done in the vertex shader
            const float kMinRadius = 0.0001f;
            var coneRadius = new Vector2(Mathf.Max(m_Master.coneRadiusStart, kMinRadius), Mathf.Max(m_Master.coneRadiusEnd, kMinRadius));
            material.SetVector("_ConeRadius", coneRadius);

            const float kMinApexOffset = 0.0001f;
            float nonNullApex = Mathf.Sign(m_Master.coneApexOffsetZ) * Mathf.Max(Mathf.Abs(m_Master.coneApexOffsetZ), kMinApexOffset);
            material.SetFloat("_ConeApexOffsetZ", nonNullApex);

            if (m_Master.colorMode == ColorMode.Gradient)
            {
                var precision = Utils.GetFloatPackingPrecision();
                material.EnableKeyword(precision == Utils.FloatPackingPrecision.High ? "VLB_COLOR_GRADIENT_MATRIX_HIGH" : "VLB_COLOR_GRADIENT_MATRIX_LOW");
                m_ColorGradientMatrix = m_Master.colorGradient.SampleInMatrix((int)precision);
                // pass the gradient matrix in OnWillRenderObject()
            }
            else
            {
                material.DisableKeyword("VLB_COLOR_GRADIENT_MATRIX_HIGH");
                material.DisableKeyword("VLB_COLOR_GRADIENT_MATRIX_LOW");
                material.SetColor("_ColorFlat", m_Master.color);
            }

            // Blending Mode
            if (Consts.BlendingMode_AlphaAsBlack[m_Master.blendingModeAsInt])
                material.EnableKeyword("ALPHA_AS_BLACK");
            else
                material.DisableKeyword("ALPHA_AS_BLACK");

            material.SetInt("_BlendSrcFactor", (int)Consts.BlendingMode_SrcFactor[m_Master.blendingModeAsInt]);
            material.SetInt("_BlendDstFactor", (int)Consts.BlendingMode_DstFactor[m_Master.blendingModeAsInt]);

            material.SetFloat("_AlphaInside", m_Master.alphaInside);
            material.SetFloat("_AlphaOutside", m_Master.alphaOutside);
            material.SetFloat("_AttenuationLerpLinearQuad", m_Master.attenuationLerpLinearQuad);
            material.SetFloat("_DistanceFadeStart", m_Master.fadeStart);
            material.SetFloat("_DistanceFadeEnd", m_Master.fadeEnd);
            material.SetFloat("_DistanceCamClipping", m_Master.cameraClippingDistance);
            material.SetFloat("_FresnelPow", Mathf.Max(0.001f, m_Master.fresnelPow)); // no pow 0, otherwise will generate inf fresnel and issues on iOS
            material.SetFloat("_GlareBehind", m_Master.glareBehind);
            material.SetFloat("_GlareFrontal", m_Master.glareFrontal);
            material.SetFloat("_DrawCap", m_Master.geomCap ? 1 : 0);

            if (m_Master.depthBlendDistance > 0f)
            {
                material.EnableKeyword("VLB_DEPTH_BLEND");
                material.SetFloat("_DepthBlendDistance", m_Master.depthBlendDistance);
            }
            else
                material.DisableKeyword("VLB_DEPTH_BLEND");

            if (m_Master.noiseEnabled && m_Master.noiseIntensity > 0f && Noise3D.isSupported) // test Noise3D.isSupported the last
            {
                Noise3D.LoadIfNeeded();
                material.EnableKeyword("VLB_NOISE_3D");
                material.SetVector("_NoiseLocal", new Vector4(m_Master.noiseVelocityLocal.x, m_Master.noiseVelocityLocal.y, m_Master.noiseVelocityLocal.z, m_Master.noiseScaleLocal));
                material.SetVector("_NoiseParam", new Vector3(m_Master.noiseIntensity, m_Master.noiseVelocityUseGlobal ? 1f : 0f, m_Master.noiseScaleUseGlobal ? 1f : 0f));
            }
            else
                material.DisableKeyword("VLB_NOISE_3D");

            ComputeLocalMatrix();

#if DEBUG_SHOW_MESH_NORMALS
            for (int vertexInd = 0; vertexInd < coneMesh.vertexCount; vertexInd++)
            {
                var vertex = coneMesh.vertices[vertexInd];

                // apply modification done inside VS
                vertex.x *= Mathf.Lerp(coneRadius.x, coneRadius.y, vertex.z);
                vertex.y *= Mathf.Lerp(coneRadius.x, coneRadius.y, vertex.z);
                vertex.z *= m_Master.fadeEnd;

                var cosSinFlat = new Vector2(vertex.x, vertex.y).normalized;
                var normal = new Vector3(cosSinFlat.x * Mathf.Cos(slopeRad), cosSinFlat.y * Mathf.Cos(slopeRad), -Mathf.Sin(slopeRad)).normalized;

                vertex = transform.TransformPoint(vertex);
                normal = transform.TransformDirection(normal);
                Debug.DrawRay(vertex, normal * 0.25f);
            }
#endif
        }

        public void SetClippingPlane(Plane planeWS)
        {
            var normal = planeWS.normal;
            material.EnableKeyword("VLB_CLIPPING_PLANE");
            material.SetVector("_ClippingPlaneWS", new Vector4(normal.x, normal.y, normal.z, planeWS.distance));
        }

        public void SetClippingPlaneOff()
        {
            material.DisableKeyword("VLB_CLIPPING_PLANE");
        }

#if SRP_SUPPORT
        void OnBeginCameraRendering(Camera cam)
        {
            UpdateCameraRelatedProperties(cam);
        }
#endif

        void OnWillRenderObject()
        {
#if SRP_SUPPORT
            if (!IsUsingCustomRenderPipeline())
#endif
            {
                var cam = Camera.current;
                if (cam != null)
                    UpdateCameraRelatedProperties(cam);
            }
        }

        void UpdateCameraRelatedProperties(Camera cam)
        {
            if (cam && m_Master)
            {
                if (material)
                {
                    var camPosOS = m_Master.transform.InverseTransformPoint(cam.transform.position);
                    material.SetVector("_CameraPosObjectSpace", camPosOS);

                    var camForwardVectorOSN = transform.InverseTransformDirection(cam.transform.forward).normalized;
                    float camIsInsideBeamFactor = cam.orthographic ? -1f : m_Master.GetInsideBeamFactorFromObjectSpacePos(camPosOS);
                    material.SetVector("_CameraParams", new Vector4(camForwardVectorOSN.x, camForwardVectorOSN.y, camForwardVectorOSN.z, camIsInsideBeamFactor));

                    if (m_Master.colorMode == ColorMode.Gradient)
                    {
                        // Send the gradient matrix every frame since it's not a shader's property
                        material.SetMatrix("_ColorGradientMatrix", m_ColorGradientMatrix);
                    }

                }

#if FORCE_CURRENT_CAMERA_DEPTH_TEXTURE_MODE
                if (m_Master.depthBlendDistance > 0f)
                    cam.depthTextureMode |= DepthTextureMode.Depth;
#endif
            }
        }
    }
}
