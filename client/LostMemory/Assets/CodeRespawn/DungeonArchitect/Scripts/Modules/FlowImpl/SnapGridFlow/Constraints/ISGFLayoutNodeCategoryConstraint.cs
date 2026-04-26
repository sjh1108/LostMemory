//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
namespace DungeonArchitect
{
    public interface ISGFLayoutNodeCategoryConstraint
    {
        string[] GetModuleCategoriesAtNode(int currentPathPosition, int pathLength);
    }
}