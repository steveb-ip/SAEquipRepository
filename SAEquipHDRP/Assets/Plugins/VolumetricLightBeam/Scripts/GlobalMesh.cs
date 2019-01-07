using UnityEngine;
using System.Collections;

namespace VLB
{
    public static class GlobalMesh
    {
        public static Mesh mesh
        {
            get
            {
                if(ms_Mesh == null)
                {
                    ms_Mesh = MeshGenerator.GenerateConeZ_Radius(
                        lengthZ: 1f,
                        radiusStart: 1f,
                        radiusEnd: 1f,
                        numSides: Config.Instance.sharedMeshSides,
                        numSegments: Config.Instance.sharedMeshSegments,
                        cap: true,
                        doubleSided: Config.Instance.forceSinglePass);

                    ms_Mesh.hideFlags = Consts.ProceduralObjectsHideFlags;
                }

                return ms_Mesh;
            }
        }

#if UNITY_EDITOR
        public static void Destroy()
        {
            if (ms_Mesh != null)
            {
                GameObject.DestroyImmediate(ms_Mesh);
                ms_Mesh = null;
            }
        }
#endif

        static Mesh ms_Mesh = null;
    }
}
