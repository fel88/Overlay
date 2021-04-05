using OpenCvSharp;

namespace Overlay
{
    public class ResizePreprocessor : IInputPreprocessor
    {

        public int[] Dims;

        public object Process(object inp)
        {
            var input = inp as Mat;
            return input.Resize(new OpenCvSharp.Size(Dims[3], Dims[2]));
        }
    }
}
