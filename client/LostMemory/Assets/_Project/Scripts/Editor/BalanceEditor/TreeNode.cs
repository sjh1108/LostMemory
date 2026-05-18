using System.Collections.Generic;
using UnityEngine;

namespace LostMemory.Editor.BalanceEditor
{
    public class TreeNode
    {
        public string DisplayName;
        public ScriptableObject So;
        public string CategoryName;

        // Category 노드 전용: 자식 SO 스냅샷.
        // bindItem 에서 동적으로 dirty count 계산해 라벨 갱신 (선택 손실 없이).
        public List<ScriptableObject> CachedSos;
    }
}
