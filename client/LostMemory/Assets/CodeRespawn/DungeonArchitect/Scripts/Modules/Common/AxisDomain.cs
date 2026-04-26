//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using UnityEngine;

namespace DungeonArchitect.Utils
{
    public interface IAxisDomain {
        int Get(Vector2Int v);
        int Get(IntVector2 v);
        int Get(Vector3Int v);
        int Get(IntVector v);
        float Get(Vector3 v);
        int Get(Rectangle bounds);
        
        void Set(ref Vector2Int v, int value);
        void Set(ref Vector3Int v, int value);
        void Set(ref Vector3 v, float value);
    };

    public class FAxisDomainX : IAxisDomain {
        public int Get(Vector2Int v)  { return v.x; }
        public int Get(IntVector2 v)  { return v.x; }
        public int Get(Vector3Int v)  { return v.x; }
        public int Get(IntVector v)  { return v.x; }
        public float Get(Vector3 v) { return v.x; }
        public int Get(Rectangle bounds) { return bounds.X; }
        public void Set(ref Vector2Int v, int value) { v.x = value; }
        public void Set(ref Vector3Int v, int value) { v.x = value; }
        public void Set(ref Vector3 v, float value) { v.x = value; }
    };
    
    public class FAxisDomainZ : IAxisDomain {
        public int Get(Vector2Int v) { return v.y; }
        public int Get(IntVector2 v) { return v.y; }
        public int Get(Vector3Int v) { return v.z; }
        public int Get(IntVector v) { return v.z; }
        public float Get(Vector3 v) { return v.z; }
        public int Get(Rectangle bounds) { return bounds.Z; }
        public void Set(ref Vector2Int v, int value) { v.y = value; }
        public void Set(ref Vector3Int v, int value) { v.z = value; }
        public void Set(ref Vector3 v, float value) { v.z = value; }
    };
}