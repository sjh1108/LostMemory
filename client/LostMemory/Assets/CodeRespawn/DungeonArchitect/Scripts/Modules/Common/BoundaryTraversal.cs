//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using UnityEngine;

namespace DungeonArchitect.Utils
{
    public static class BoundaryTraversal
    {
        public static void TraverseBoundary(Vector2Int location, Vector2Int size, bool visitCorners, System.Action<Vector2Int, float> callback)
        {
            if (visitCorners)
            {
                for (int dx = 0; dx < size.x - 1; dx++)
                {
                    callback(new Vector2Int(location.x + dx, location.y), 0f);
                    callback(new Vector2Int(location.x + dx + 1, location.y + size.y - 1), 180f);
                }

                for (int dy = 0; dy < size.y - 1; dy++)
                {
                    callback(new Vector2Int(location.x, location.y + dy + 1), 90f);
                    callback(new Vector2Int(location.x + size.x - 1, location.y + dy), 270f);
                }
            }
            else
            {
                for (int dx = 1; dx < size.x - 1; dx++)
                {
                    callback(new Vector2Int(location.x + dx, location.y), 0f);
                    callback(new Vector2Int(location.x + dx, location.y + size.y - 1), 180f);
                }

                for (int dy = 1; dy < size.y - 1; dy++)
                {
                    callback(new Vector2Int(location.x, location.y + dy), 90f);
                    callback(new Vector2Int(location.x + size.x - 1, location.y + dy), 270f);
                }
            }
        }
        
        public static void TraverseCorners(Vector2Int location, Vector2Int size, System.Action<Vector2Int, float> callback)
        {
            callback(new Vector2Int(location.x, location.y), 90);                         // Bottom left
            callback(new Vector2Int(location.x + size.x - 1, location.y), 0);          // Bottom right
            callback(new Vector2Int(location.x + size.x - 1, location.y + size.y - 1), 270);     // Top right
            callback(new Vector2Int(location.x, location.y + size.y - 1), 180);          // Top left
        }
    }
}