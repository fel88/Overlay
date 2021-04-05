using System.Collections.Generic;

namespace Overlay
{
    public class InputInfo
    {
        public List<IInputPreprocessor> Preprocessors = new List<IInputPreprocessor>();
        public object Data;
    }
}
