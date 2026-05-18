//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//

using DungeonArchitect.Frameworks.Snap;
using UnityEngine;


namespace DungeonArchitect.Builders.SnapGridFlow.NestedDungeons
{
    public class SnapGridFlowModuleNestedDungeonSetupWizard : MonoBehaviour
    {
        public SnapConnection snapConnectionPrefab;

        public bool createNegationVolume = false;
        public bool centerDungeonActor = false;
        
        [SerializeField, HideInInspector]
        public GameObject autoGenRoot;
    }
}