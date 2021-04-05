using OpenCvSharp;

namespace Overlay
{
    public class NormalizePreprocessor : IInputPreprocessor
    {
        public object Process(object inp)
        {
            var input = inp as Mat;
            input /= 255f;
            return input;
        }
    }
}
