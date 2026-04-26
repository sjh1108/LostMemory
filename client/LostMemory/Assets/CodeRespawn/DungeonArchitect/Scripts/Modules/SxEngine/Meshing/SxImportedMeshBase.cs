//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using UnityEngine;

namespace DungeonArchitect.SxEngine.Utils
{
    public class SxCubeBaseMesh : SxMesh
    {   
        public SxCubeBaseMesh()
        {
            var mesh = Resources.Load<Mesh>("MarkerGen/meshes/cube_base");
            SxMeshImporter.Import(mesh, this);
        }
    }
}