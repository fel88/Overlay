using System.Collections.Generic;

namespace Overlay
{
    public class NodeInfo
    {
        public bool IsInput;
        public string Name;
        public int[] Dims;
        public List<string> Tags = new List<string>();
    }
}
