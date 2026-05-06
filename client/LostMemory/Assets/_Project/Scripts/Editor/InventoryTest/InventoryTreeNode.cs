using LostMemory.Relics;

namespace LostMemory.Editor.InventoryTest
{
    /// <summary>TreeView item data. Group 노드는 Relic == null.</summary>
    public class InventoryTreeNode
    {
        public string DisplayLabel;
        public RelicData Relic;   // null = group 노드
    }
}
