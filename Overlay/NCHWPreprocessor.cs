using OpenCvSharp;
using System;

namespace Overlay
{
    public class NCHWPreprocessor : IInputPreprocessor
    {
        public object Process(object inp)
        {
            var input = inp as Mat;
            var res2 = input.Split();
            res2[0].GetArray<float>(out float[] ret1);
            res2[1].GetArray<float>(out float[] ret2);
            res2[2].GetArray<float>(out float[] ret3);

            var inputData = new float[ret1.Length + ret2.Length + ret3.Length];

            Array.Copy(ret1, 0, inputData, 0, ret1.Length);
            Array.Copy(ret2, 0, inputData, ret1.Length, ret2.Length);
            Array.Copy(ret3, 0, inputData, ret1.Length + ret2.Length, ret3.Length);
            return inputData;
        }
    }
}
