//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
using UnityEngine;
using DungeonArchitect.Utils;
using UnityEngine.Serialization;

namespace DungeonArchitect
{
    /// <summary>
    /// A volume is an abstract representation of space in the world.  
    /// A volume can be scaled and moved around like any other game object and custom
    /// functionality can be added to volumes to influence the dungeon with it's spatial volume
    /// </summary>
    public class Volume : MonoBehaviour
    {
        public Dungeon dungeon;
		public bool mode2D = false;
        public bool disableRendering = false;
        public bool autoBuildOnMove = true;
        
        /// <summary>
        /// Gets the bounds of the volume
        /// </summary>
        /// <returns>The bounds of the dungeon</returns>
        public Bounds GetBounds()
        {
            var bounds = new Bounds();
            var transform = gameObject.transform;
            bounds.center = transform.position;
            var size = transform.rotation * transform.localScale;
            MathUtils.Abs(ref size);
            bounds.size = size;

			if (mode2D) {
				MathUtils.FlipYZ(ref bounds);
			}
            return bounds;
        }


        /// <summary>
        /// Gets the position and scale of the volume in grid space
        /// </summary>
        /// <param name="positionGrid">The grid position (out)</param>
        /// <param name="scaleGrid">The grid scale (out)</param>
        public void GetVolumeGridTransform(out IntVector positionGrid, out IntVector scaleGrid, Vector3 LogicalGridSize)
        {
            if (dungeon == null)
            {
                positionGrid = IntVector.Zero;
                scaleGrid = IntVector.Zero;
                return;
            }

            var transform = gameObject.transform;
            var position = transform.position;
            var scale = transform.rotation * transform.localScale;
            MathUtils.Abs(ref scale);

            var positionGridF = MathUtils.Divide(position - new Vector3(0.5f, 0.5f, 0.5f), LogicalGridSize);
            var scaleGridF = MathUtils.Divide(scale, LogicalGridSize);

            positionGrid = DungeonArchitect.Utils.MathUtils.RoundToVector3Int(positionGridF);
            scaleGrid = DungeonArchitect.Utils.MathUtils.RoundToVector3Int(scaleGridF);

            if (mode2D)
            {
                positionGrid = MathUtils.FlipYZ(positionGrid);
                scaleGrid = MathUtils.FlipYZ(scaleGrid);
            }
        }

        protected Color COLOR_WIRE = Color.yellow;
        protected Color COLOR_SOLID_DESELECTED = new Color(1, 1, 0, 0.0f);
        protected Color COLOR_SOLID = new Color(1, 1, 0, 0.1f);
        void OnDrawGizmosSelected()
        {
            DrawGizmo(true);
        }

        void OnDrawGizmos()
        {
            DrawGizmo(false);
        }

        void DrawGizmo(bool selected)
        {
            if (!disableRendering)
            {
                var transform = gameObject.transform;
                var position = transform.position;
                var scale = transform.localScale;
                scale = transform.rotation * scale;

                // Draw the wireframe
                Gizmos.color = COLOR_WIRE;
                Gizmos.DrawWireCube(position, scale);

                Gizmos.color = selected ? COLOR_SOLID : COLOR_SOLID_DESELECTED;
                Gizmos.DrawCube(position, scale);
            }
        }
    }
}
