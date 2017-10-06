using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using static PCI.Waveform;

namespace PCI
{
    class WaveProcesser
    {
        /// <summary>
        /// 均值滤波
        /// </summary>
        /// <param name="InputWave"> 需要进行均值滤波的波形 </param>
        /// <param name="Weight"> 权重 </param>
        /// <returns></returns>
        public Waveform MeanFilter(Waveform InputWave, int Weight)
        {
            if (InputWave.Length== 0 )
            {
                return null;
            }
            if (Weight==1)
            {
                return InputWave;
            }
            Waveform OutputWave = new Waveform(InputWave.TimeSpan, InputWave.StartTime,InputWave.Type);

            for (int i = 0; i < InputWave.Length - Weight; i++)
            {
                OutputWave.Add(InputWave.GetRange(i, Weight).Sum() / Weight);
            }

            return OutputWave;
        } 

        /// <summary>
        /// 求导(每两点间的变化率)
        /// </summary>
        /// <param name="InputWave"> 需要进行求导的数组 </param>
        /// <param name="Zero"> 计算得到的零点数组 </param>
        /// <returns></returns>
        public Waveform Derivative(Waveform InputWave ,out Waveform Zero)
        {
            Waveform OutputWave = new Waveform(InputWave.TimeSpan, InputWave.StartTime, "Derivated");
            Zero = new Waveform(InputWave.TimeSpan, InputWave.StartTime);

            for (int i = 0; i < InputWave.Length-1; i++)
            {
                OutputWave.Add((InputWave[i + 1] - InputWave[i])/new NullableValue(InputWave.TimeSpan));
                if (i > 0)
                {
                    if (OutputWave[i] * OutputWave[i - 1] <= 0)
                    {
                        Zero.Add(1);
                    }
                    else
                    {
                        Zero.Add(0);
                    }
                }
            }
            return OutputWave;
        }

        /// <summary>
        /// 中值滤波
        /// </summary>
        /// <param name="InputWave"> 需要进行中值滤波的波形 </param>
        /// <param name="Weight"> 权重 </param>
        /// <returns></returns>
        public Waveform MedianFilter(Waveform InputWave, int Weight)
        {
            Waveform OutputWave = new Waveform(InputWave.TimeSpan * Weight, InputWave.StartTime,InputWave.Type);

            Waveform CacheWave = new Waveform(InputWave.TimeSpan * Weight, InputWave.StartTime);
            for (int i = 0; i < InputWave.Length / Weight - 1; i++)
            {
                CacheWave = InputWave.GetRange(i * Weight, Weight);
                CacheWave.Sort();
                OutputWave.Add(CacheWave[Weight / 2]);
            }

            return OutputWave;
        }

        /// <summary>
        /// 降采样
        /// </summary>
        /// <param name="InputWave"> 需要降采样的波形 </param>
        /// <param name="Weight"> 权重 </param>
        /// <returns></returns>
        public Waveform DownSampling(Waveform InputWave,int Weight)
        {
            Waveform OutputWave = new Waveform(InputWave.TimeSpan*100, InputWave.StartTime,InputWave.Type);

            for (int i = 0; i < InputWave.Length / Weight - 1; i++)
            {
                OutputWave.Add( InputWave[i * Weight]);
            }

            return OutputWave;
        }

        /// <summary>
        /// 计算过零特性
        /// </summary>
        /// <param name="StandardZero"></param>
        /// <param name="MeasuredZero"></param>
        /// <returns></returns>
        public List<double> CalculateZeroPhaseDefference(Waveform StandardZero,Waveform MeasuredZero)
        {
            List<double> ZeroPhaseDifference = new List<double>();
            List<double> StandardZeroPoint = new List<double>();
            List<double> MeasuredZeroPoint = new List<double>();
            for (int i = 0; i < StandardZero.Length; i++)
            {
                if (StandardZero[i]._value == 1)
                {
                    StandardZeroPoint.Add(StandardZero[i]._value);
                }
                if (MeasuredZero[i]._value == 1)
                {
                    MeasuredZeroPoint.Add(MeasuredZero[i]._value);
                }
            }

            for (int i = 0; i < StandardZeroPoint.Count; i++)
            {
                ZeroPhaseDifference.Add(StandardZeroPoint[i] - MeasuredZeroPoint[i]);
            }

            return ZeroPhaseDifference;
        }

        /// <summary>
        /// 根据线性度计算线性区
        /// </summary>
        /// <param name="Wave"></param>
        /// <param name="Linear"></param>
        /// <returns></returns>
        public List<int> CalculateLinearArea(Waveform Wave,double Linear)
        {
            if (Wave.Type=="Origin")
            {
                Wave = Derivative(Wave,out Waveform Zero);
            }
            if (Wave.Type=="Derivated")
            {
                List<int> LinearList = new List<int>();

                double Threshold = Wave.Max() * (1 - Linear / 100);

                for (int i = 0; i < Wave.Length; i++)
                {
                    if (Math.Abs(Wave[i]._value) >= Threshold)
                    {
                        LinearList.Add(1);
                    }
                    else
                    {
                        LinearList.Add(0);
                    }

                }
                return LinearList;
            }
            else
            {
                return null;
            }
        }

        public List<int> CalculateLinearArea(List<double> list, double Linear)
        {
            List<int> LinearList = new List<int>();

            double max = list.Max();
            double min = list.Min();
            double Threshold = (max - min) * Linear / 200;

            for (int i = 0; i < list.Count; i++)
            {
                if ((list[i] > (max-Threshold))||(list[i] < (min+Threshold)))
                {
                    LinearList.Add(1);
                }
                else
                {
                    LinearList.Add(0);
                }

            }

            return LinearList;
        }

        /// <summary>
        /// 计算角度变化波形
        /// </summary>
        /// <param name="differenceWave"></param>
        /// <param name="sumWave"></param>
        /// <returns></returns>
        public Waveform Calculate(Waveform differenceWave, Waveform sumWave)
        {
            if (differenceWave.Length != sumWave.Length)
            {
                return null;
            }

            Waveform OutputWave = new Waveform(differenceWave.TimeSpan, differenceWave.StartTime,differenceWave.Type);

            double rate = 3.303 / 5.667 * 17 / 63;

            for (int i = 0; i < differenceWave.Length; i++)
            {
                OutputWave.Add(Math.Atan((differenceWave[i] / sumWave[i])._value * rate));
            }

            //OutputWave = differenceWave / sumWave;

            return OutputWave;
        }

        //通过标准数据和采集的数据得到之间的偏差
        public List<double> CalculateDiffer(List<double> standard, List<double> calculate)
        {
            List<double> differ = new List<double>();

            for (int i = 0; i < standard.Count; i++)
            {
                differ.Add(standard[i] - calculate[i]);
            }

            return differ;
        }

        public List<double> CalculateDiffer(double standard, List<double> calculate)
        {
            List<double> differ = new List<double>();

            for (int i = 0; i < calculate.Count; i++)
            {
                differ.Add(25 - calculate[i]);
            }

            return differ;
        }
    }
}
