using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace PCI
{
    public class Waveform:List<double>
    {
        #region 构造函数
        public Waveform()
        {

        }

        public Waveform(double timespan,double startTime = 0,string type="Origin")
        {
            this.timeSpan = timespan;
            this.startTime = startTime;
            this.type = type;
        }

        public Waveform(List<double> waveArray, double timeSpan = 1, double startTime = 0,string type="Origin")
        {
            this.WaveArray = waveArray;
            this.timeSpan = timeSpan;
            this.startTime = startTime;
            this.type = type;
        }
        #endregion

        #region Method
        /// <summary>
        /// 返回波形的最大值
        /// </summary>
        /// <returns></returns>
        public double Max()
        {
            return this.WaveArray.Max();
        }

        /// <summary>
        /// 返回波形的最小值
        /// </summary>
        /// <param name="X"></param>
        /// <returns></returns>
        public double Min()
        {
            return this.WaveArray.Min();
        }

        /// <summary>
        /// 检查double索引是否包含此索引，如果包含则返回对应的index，如果不包含，则返回数字值null
        /// </summary>
        /// <param name="X"></param>
        /// <returns></returns>
        public NullableValue CheckX(double X)
        {
            decimal index;
            if ((index = ((decimal)X - (decimal)StartTime) / (decimal)TimeSpan) >= this.Length || ((((decimal)X - (decimal)StartTime) % (decimal)TimeSpan) != 0))
            {
                return new NullableValue(true);
            }
            return new NullableValue((int)index);
        }

        /// <summary>
        /// 当index值为int类型时，在源波形上创建指定元素范围的新波形
        /// </summary>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public new Waveform GetRange(int index, int count)
        {
            Waveform OutputWaveform = this;
            OutputWaveform.WaveArray = this.WaveArray.GetRange(index, count);
            return OutputWaveform;
        }

        /// <summary>
        /// 当index值为double类型式，认为以波形时间点为起点，在源波形上创建指定元素范围的新波形
        /// </summary>
        /// <param name="index"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        public Waveform GetRange(double index, int count)
        {
            NullableValue output = CheckX(index);   //查找是否存在该时间点，如果存在则返回指定索引，如果不存在则返回数字值null
            if (output._null)
            {
                return null;
            }
            Waveform OutputWaveform = this;
            OutputWaveform.WaveArray = this.WaveArray.GetRange((int)output._value, count);
            return OutputWaveform;
        }

        /// <summary>
        /// 波形Y值求和
        /// </summary>
        /// <returns></returns>
        public double Sum()
        {
            return this.WaveArray.Sum();
        }

        /// <summary>
        /// 波形Y值排序（从小到大）
        /// </summary>
        public void Sort()
        {
            this.WaveArray.Sort();
        }

        /// <summary>
        /// 将对象添加到末尾——添加double值
        /// </summary>
        /// <param name="value"></param>
        public new void Add(double value)
        {
            this.WaveArray.Add(value);
        }

        /// <summary>
        /// 将对象添加到末尾——添加可空值
        /// </summary>
        /// <param name="value"></param>
        public new void Add(NullableValue value)
        {
            if (!value._null)
            {
                this.WaveArray.Add(value._value);
            }
        }

        /// <summary>
        /// 计算波形的平均值
        /// </summary>
        /// <returns></returns>
        public double Average()
        {
            return this.WaveArray.Average();
        }

        ///// <summary>
        ///// 求和
        ///// </summary>
        ///// <returns></returns>
        //public double Sum()
        //{
        //    return this.WaveArray.Sum();
        //}
        #endregion

        #region Property
        /// <summary>
        /// 索引器
        /// </summary>
        /// <param name="X"></param>
        /// <returns></returns>
        public NullableValue this[object X]
        {
            get
            {
                switch (X.GetType().Name)
                {
                    case "Double":
                        NullableValue output = CheckX((double)X);
                        if (output._null)
                        {
                            return new NullableValue(true);
                        }
                        return new NullableValue(WaveArray[(int)output._value]);
                    case "Int32":
                        return new NullableValue(this.WaveArray[(int)X]);
                    default:
                        return new NullableValue(true);
                }

            }
            set
            {
                switch (X.GetType().Name)
                {
                    case "Double":
                        NullableValue output = CheckX((double)X);
                        if (!output._null)
                        {
                            WaveArray[(int)output._value] = value._value;
                        }
                        break;
                    case "Int32":
                        if (!value._null)
                        {
                            WaveArray[(int)X] = value._value;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 以double数组的形式表示的波形数据
        /// </summary>
        List<double> WaveArray = new List<double>();

        /// <summary>
        /// 起始时间
        /// </summary>
        private double startTime;
        public double StartTime { get => startTime; set => startTime = value; }

        /// <summary>
        /// 时间间隔
        /// </summary>
        private double timeSpan;
        public double TimeSpan { get => timeSpan; set => timeSpan = value; }

        /// <summary>
        /// 波形长度
        /// </summary>
        public int Length { get => WaveArray.Count; }

        /// <summary>
        /// 波形类型 默认为空
        /// </summary>
        private string type = "";
        public string Type{ get => type; set => type= value; }
        #endregion

        #region Operator reload
        /// <summary>
        /// 重写加法
        /// </summary>
        /// <param name="WaveA"></param>
        /// <param name="WaveB"></param>
        /// <returns></returns>
        static public Waveform operator +(Waveform WaveA, Waveform WaveB)
        {
            if (WaveA.TimeSpan != WaveB.TimeSpan)
            {
                return null;
            }

            Waveform OutputWave = new Waveform();

            for (int i = 0; i < WaveA.Length; i++)
            {
                OutputWave[i] = WaveA[i] + WaveB[i];
            }

            return OutputWave;
        }

        /// <summary>
        /// 重写减法
        /// </summary>
        /// <param name="WaveA"></param>
        /// <param name="WaveB"></param>
        /// <returns></returns>
        static public Waveform operator -(Waveform WaveA, Waveform WaveB)
        {
            if (WaveA.TimeSpan != WaveB.TimeSpan)
            {
                return null;
            }

            Waveform OutputWave = new Waveform();

            for (int i = 0; i < WaveA.Length; i++)
            {
                OutputWave[i] = WaveA[i] - WaveB[i];
            }

            return OutputWave;
        }

        /// <summary>
        /// 重写乘法
        /// </summary>
        /// <param name="WaveA"></param>
        /// <param name="WaveB"></param>
        /// <returns></returns>
        static public Waveform operator *(Waveform WaveA, Waveform WaveB)
        {
            if (WaveA.TimeSpan != WaveB.TimeSpan)
            {
                return null;
            }

            Waveform OutputWave = new Waveform();

            for (int i = 0; i < WaveA.Length; i++)
            {
                OutputWave[i] = WaveA[i] * WaveB[i];
            }

            return OutputWave;
        }

        /// <summary>
        /// 重写除法
        /// </summary>
        /// <param name="WaveA"></param>
        /// <param name="WaveB"></param>
        /// <returns></returns>
        static public Waveform operator /(Waveform WaveA, Waveform WaveB)
        {
            if (WaveA.TimeSpan != WaveB.TimeSpan)
            {
                return null;
            }

            Waveform OutputWave = new Waveform();

            for (int i = 0; i < WaveA.Length; i++)
            {
                OutputWave[i] = WaveA[i] / WaveB[i];
            }
            

            return OutputWave;
        }
        #endregion

        #region 其他相关属性
        /// <summary>
        /// 自定义类型 数字值
        /// </summary>
        public struct NullableValue
        {
            public NullableValue(bool _null)
            {
                this._null = _null;
                this._value = 0;
            }

            public NullableValue(double _value, bool _null = false)
            {
                this._value = _value;
                this._null = _null;
            }

            public double _value;

            public bool _null;

            #region Overload operate
            static public NullableValue operator +(NullableValue A, NullableValue B)
            {
                if (A._null || B._null)
                {
                    return new NullableValue(0, true);
                }
                return new NullableValue(A._value + B._value);
            }

            static public NullableValue operator -(NullableValue A, NullableValue B)
            {
                if (A._null || B._null)
                {
                    return new NullableValue(0, true);
                }
                return new NullableValue(A._value - B._value);
            }

            static public NullableValue operator *(NullableValue A, NullableValue B)
            {
                if (A._null || B._null)
                {
                    return new NullableValue(0, true);
                }
                return new NullableValue(A._value * B._value);
            }

            static public NullableValue operator /(NullableValue A, NullableValue B)
            {
                if (A._null || B._null)
                {
                    return new NullableValue(0, true);
                }
                return new NullableValue(A._value / B._value);
            }

            static public bool operator >(NullableValue A, NullableValue B)
            {
                if (A._null||B._null)
                {
                    return false;
                }
                return A._value > B._value;
            }

            static public bool operator <(NullableValue A, NullableValue B)
            {
                if (A._null || B._null)
                {
                    return false;
                }
                return A._value < B._value;
            }

            static public bool operator >=(NullableValue A, NullableValue B)
            {
                if (A._null || B._null)
                {
                    return false;
                }
                return A._value >= B._value;
            }

            static public bool operator <=(NullableValue A, NullableValue B)
            {
                if (A._null || B._null)
                {
                    return false;
                }
                return A._value <= B._value;
            }

            static public bool operator <(NullableValue A, int B)
            {
                if (A._null)
                {
                    return false;
                }
                return A._value < B;
            }

            static public bool operator >(NullableValue A, int B)
            {
                if (A._null)
                {
                    return false;
                }
                return A._value > B;
            }

            static public bool operator >=(NullableValue A, int B)
            {
                if (A._null)
                {
                    return false;
                }
                return A._value >= B;
            }

            static public bool operator <=(NullableValue A, int B)
            {
                if (A._null)
                {
                    return false;
                }
                return A._value <= B;
            }
            #endregion
        }

        /// <summary>
        /// 枚举方式
        /// </summary>
        public enum EnumerateType
        {
            by_X,
            by_i
        }
        #endregion
    }
}
