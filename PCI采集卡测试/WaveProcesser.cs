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
        /// 仅计算导数
        /// </summary>
        /// <param name="InputWave"></param>
        /// <returns></returns>
        public Waveform OnlyDerivated(Waveform InputWave)
        {
            Waveform OutputWave = new Waveform(InputWave.TimeSpan, InputWave.StartTime, "Derivated");
            for (int i = 0; i < InputWave.Length-1; i++)
            {
                OutputWave.Add((InputWave[i + 1] - InputWave[i] )/ new NullableValue(InputWave.TimeSpan));
            }
            return OutputWave;
        }

        /// <summary>
        /// 仅计算零点
        /// </summary>
        /// <param name="InputWave"></param>
        /// <returns></returns>
        public Waveform CalculateZero(Waveform InputWave)
        {
            Waveform Zero = new Waveform(InputWave.TimeSpan, InputWave.StartTime);
            for (int i = 0; i < InputWave.Length-1; i++)
            {
                if (InputWave[i]*InputWave[i+1]<=0)
                {
                    Zero.Add(1);
                }
                else
                {
                    Zero.Add(0);
                }
            }
            return Zero;
        }

        /// <summary>
        /// 计算标志位零点数组和采集零点数组的差值
        /// </summary>
        /// <param name="StandardZero"></param>
        /// <param name="CollectedZero"></param>
        /// <returns></returns>
        List<double> CalculateZeroPoint(Waveform StandardZero,Waveform CollectedZero)
        {
            List<double> outputZero = new List<double>();
            List<double> StandardZeroPoint = new List<double>();
            List<double> CollectedZeroPoint = new List<double>();
            int m = 0, n = 0;
            for (int i = 0; i < StandardZero.Length; i++)
            {
                if (m != 0)
                {
                    if (StandardZero[i]._value == 1)
                    {
                        StandardZeroPoint.Add(m * StandardZero.TimeSpan);
                    }
                    m++;
                }
                else if (StandardZero[i]._value==1)
                {
                    StandardZeroPoint.Add(m*StandardZero.TimeSpan);
                    m++;
                }
            }
            for (int i = 0; i < CollectedZero.Length; i++)
            {
                if (n != 0)
                {
                    if (CollectedZero[i]._value == 1)
                    {
                        CollectedZeroPoint.Add(n * CollectedZero.TimeSpan);
                    }
                    n++;
                }
                else if (StandardZero[i]._value == 1)
                {
                    StandardZeroPoint.Add(n * StandardZero.TimeSpan);
                    n++;
                }
            }

            int length = StandardZeroPoint.Count < CollectedZeroPoint.Count ? StandardZeroPoint.Count : CollectedZeroPoint.Count;

            for (int i = 0; i < length; i++)
            {
                outputZero.Add(CollectedZeroPoint[i] - StandardZeroPoint[i]);
            }
            return outputZero;
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
            Waveform OutputWave = new Waveform(InputWave.TimeSpan*Weight, InputWave.StartTime,InputWave.Type);

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

            //double rate = 3.303 / 5.667 * 17 / 63;

            double rate = 1d / 3d * 17 / 63;

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

        /// <summary>
        /// 计算时间利用率和有效摆角
        /// </summary>
        /// <param name="LinearArray"></param>
        /// <returns></returns>
        public List<double> CalculateTimeUtilizationAndSpeedUniformity(List<int> LinearArray,Waveform DerivatedWave,out List<double> SpeedUniformity)
        {
            double m = 0;
            double n = 0;
            double nc = 0;
            double l = 0;
            SpeedUniformity = new List<double>();
            List<double> TimeUtilization = new List<double>();
            double Speed= 0;
            for (int i = 0; i < LinearArray.Count - 1; i++)
            {
                //检测到一个上升沿
                if (LinearArray[i] - LinearArray[i + 1] < 0)
                {
                    m++;                                            //上升沿次数+1
                    if (m == 3)                                     //完成一个周期
                    {
                        TimeUtilization.Add(n / l);                 //按周期存储时间时间利用率
                        m = 1;                                      //重新开始记录上升沿周期
                        l = 0;                                      //周期点数归零
                        n = 0;                                      //周期内线性区点数归零
                    }
                }
                if (m!=0)                                           //表明已经进入一个新的周期中
                {
                    l++;                                            //记录周期点数
                    if (LinearArray[i] == 1)                        //若线性数组i元素等于1，则表示进入一个线性区
                    {
                        nc++;                                       //记录当前线性区点数
                        n++;                                        //记录周期内线性区点数
                        Speed += Math.Abs(DerivatedWave[i]._value); //累加角速度数组当前速度值
                    }
                    if (LinearArray[i] - LinearArray[i + 1] > 0)    //表示一个下降沿，证明离开了当前线性区
                    {
                        SpeedUniformity.Add(Speed / n);             //计算速度均匀性，并添加到速度均匀性数组中
                        Speed = 0;                                  //速度清零
                        nc = 0;                                     //当前线性区点数清零
                    }
                }
            }
            return TimeUtilization;                                 //按周期存储的时间利用率数组
        }

        /// <summary>
        /// 单纯计算速度均匀性
        /// </summary>
        /// <param name="LinearArray"></param>
        /// <param name="DerivatedWave"></param>
        /// <returns></returns>
        public double CalculateSpeedUniformity(List<int> LinearArray,Waveform DerivatedWave)
        {
            double m = 0;
            double n = 0;
            double speed = 0;
            for (int i = 0; i < LinearArray.Count - 1; i++)
            {
                if (LinearArray[i] - LinearArray[i + 1] < 0)
                {
                    m++;
                    if (m == 3)
                    {
                        break;
                    }
                    if (LinearArray[i] == 1)
                    {
                        n++;
                        speed += DerivatedWave[i]._value;
                    }
                }
            }
            return speed / n;
        }

        /// <summary>
        /// 将AD16位数字转换成真实电压值
        /// </summary>
        /// <param name="inputWave"></param>
        /// <param name="ch"></param>
        /// <returns></returns>
        public Waveform TranslateWaveform(Waveform inputWave,string ch)
        {
            Waveform outputWave = new Waveform(inputWave.TimeSpan, inputWave.StartTime, inputWave.Type);

            switch (ch)
            {
                case "CH1":
                    for (int i = 0; i < inputWave.Length; i++)
                    {
                        outputWave.Add(new NullableValue(inputWave[i]._value - 32768));
                    }
                    return outputWave;
                case "CH2":
                    for (int i = 0; i < inputWave.Length; i++)
                    {
                        outputWave.Add(new NullableValue((inputWave[i]._value - 32768) * 3));
                    }
                    return outputWave;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 计算所有属性，以列表的形式呈现
        /// </summary>
        /// <param name="ZeroWave"></param>
        /// <param name="LinearArray"></param>
        /// <param name="TargetWave"></param>
        /// <param name="DerivatedWave"></param>
        /// <returns></returns>
        public List<List<double>> CalculateProperties(Waveform ZeroWave, List<int> LinearArray,Waveform TargetWave,Waveform DerivatedWave)
        {
            List<List<double>> ListProperty= new List<List<double>>();
            List<double> ListFrequency = new List<double>();
            List<double> ListAngle = new List<double>();
            List<double> ListSpeed = new List<double>();
            List<double> ListTimeU= new List<double>();
            List<double> ListTime= new List<double>();

            ListProperty.Add(ListFrequency);
            ListProperty.Add(ListAngle);
            ListProperty.Add(ListSpeed);
            ListProperty.Add(ListTimeU);
            ListProperty.Add(ListTime);

            int STP = 0;

            //double Frequency = 0;
            double Speed = 0;
            double Angle= 0;
            double Time = 0;

            bool isAPeriod = false;
            bool isFirst = true;

            for (int i = 0; i < ZeroWave.Length; i++)
            {
                if (i<LinearArray.Count)
                {
                    if (ZeroWave[i]._value == 1)
                    {
                        if (!isFirst)
                        {
                            int Range = i - STP;
                            if (isAPeriod)
                            {
                                ListFrequency.Add(0.5 / (Range * ZeroWave.TimeSpan));
                            }
                            ListAngle.Add(Angle / Time);
                            ListSpeed.Add(Speed / Time);
                            ListTime.Add(Time * ZeroWave.TimeSpan);
                            ListTimeU.Add(Time / Range);

                            Speed = 0;
                            Angle = 0;
                            Time = 0;
                            isAPeriod = !isAPeriod;
                        }
                        else
                        {
                            isFirst = false;
                        }
                        STP = i;
                    }
                    if (LinearArray[i]==1)
                    {
                        Time++;
                        Angle += Math.Abs(TargetWave[i]._value);
                        Speed += Math.Abs(DerivatedWave[i]._value);
                    }
                }
            }
            return ListProperty;
        }
    }
}
