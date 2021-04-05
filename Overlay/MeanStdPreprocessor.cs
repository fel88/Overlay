using OpenCvSharp;

namespace Overlay
{
    public class MeanStdPreprocessor : IInputPreprocessor
    {
        public double[] Mean = new double[3];
        public double[] Std = new double[3];

        public object Process(object inp)
        {
            var input = inp as Mat;
            Cv2.Subtract(input, new Scalar(Mean[0], Mean[1], Mean[2]), input);
            var res = Cv2.Split(input);
            for (int i = 0; i < 3; i++)
            {
                res[i] /= Std[i];
            }
            Cv2.Merge(res, input);
            return input;
        }
    }
}
