//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
using UnityEditor;

namespace DungeonArchitect.Editors.Flow.Common
{
    public class BaseFlowExecNodeHandlerInspector_OptimizeTilemap : FlowExecNodeHandlerInspectorBase
    {
        public override void HandleInspectorGUI()
        {
            base.HandleInspectorGUI();

            DrawHeader("Optimize");
            {
                EditorGUI.indentLevel++;
                DrawProperties("discardDistanceFromLayout");
                EditorGUI.indentLevel--;
            }
        }
    }
}