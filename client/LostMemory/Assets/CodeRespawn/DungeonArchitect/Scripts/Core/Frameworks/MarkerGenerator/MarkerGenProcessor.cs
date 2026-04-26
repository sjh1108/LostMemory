//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using UnityEngine;

namespace DungeonArchitect.MarkerGenerator.Processor
{
    
    public interface IMarkerGenProcessor
    {
        bool Process(MarkerGenPattern pattern, PropSocket[] markers, System.Random random, out PropSocket[] newMarkers);
        void Release();
    }
    
}