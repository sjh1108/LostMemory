//$ Copyright 2015-25, Code Respawn Technologies Pvt Ltd - All Rights Reserved $//
namespace DungeonArchitect
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class MetaAttribute : System.Attribute
    {
        public string displayText;
        public MetaAttribute(string displayText)
        {
            this.displayText = displayText;
        }
    }
}
