using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Runtime.InteropServices;
using System.Threading;
using System.IO;
using System.ComponentModel;
using NPOI.HSSF.UserModel;
using NPOI.SS.Util;
using NPOI.SS.UserModel;
using static PCI.Waveform;
using Microsoft.Win32;
using System.Text.RegularExpressions;
using NPOI.XSSF.UserModel;
//using Microsoft.Office;

namespace PCI
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            ERNW = new ExcelReadNWrite();
            osc = new Oscillocope();
            InitializeComponent();
            ERNW.Status = Status;
            osc.Status = Status;

            osc.InitDevice();


            ThreadCollectWave = new Thread(WaveCollect);

            firsttime = true;

            DataPropertyList.Add(ZeroProperty);
            DataPropertyList.Add(ScanFrequency);
            DataPropertyList.Add(TimeUtilization);
            DataPropertyList.Add(EffectiveAngle);
            DataPropertyList.Add(SpeedUniformity);
            TextBoxList.Add(TBLZero);
            TextBoxList.Add(TBLScanFreq);
            TextBoxList.Add(TBLTimeUtilization);
            TextBoxList.Add(TBLEffectiveAngle);
            TextBoxList.Add(TBLSpeedUniformity);
            //TData = new TestData();
        }

        #region MainWindow窗体函数
        /// <summary>
        /// 保证窗体大小发生变化时波形显示能随之变化
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            MethodDrawWaves(DrawingWave);
        }
        #endregion

        #region UI控件数据更新和获取
        //status更新
        public delegate void UpdateEventHandler(string status);
        //status更新方法
        public void StutusUpdate(string status)
        {
            Status.Content = status;
        }

        //更新指定控件
        public delegate void UpdateTBLEventHandler(TextBox TBL, string status);
        //更新指定控件方法
        public void TBLUpdateStatus(TextBox TBL, string status)
        {
            TBL.Text = status;
        }

        //从指定控件获取信息
        public delegate string GetTBLStatus(TextBox TBL);
        //从制定控件获取信息方法
        private string getTBLStatus(TextBox TBL)
        {
            return TBL.Text;
        }
        #endregion

        #region 字段
        #region 波形相关参数
        //产品编号
        string ProducerName = "";
        List<TextBox> TextBoxList = new List<TextBox>();
        List<double> DataPropertyList = new List<double>();
        double ZeroProperty = 0;
        double ScanFrequency = 0;
        double TimeUtilization = 0;
        double EffectiveAngle = 0;
        double SpeedUniformity = 0;
        List<List<double>> ListProperty = new List<List<double>>();
        Waveform ZeroPropertyWave;
        List<double> TimeUtilizationList;
        List<double> SpeedUniformityList;
        List<double> EffectiveAngleList;
        List<double> ScanFrequencyList;
        List<double> ListFrequency = new List<double>();
        #endregion

        #region 波形相关属性
        //波形处理类——包含一些处理波形的方法
        WaveProcesser waveProcesser = new WaveProcesser();

        //波形数组——从0-3按序依次代表通道1-4
        Waveform[] WaveList = new Waveform[4];
        ushort[] CH5;//测试备用

        //零点波形——存放零点信息
        Waveform Zero;
        //零点标志位波形计算出的零点波形
        Waveform ZeroWave;

        //线性度数组——用于线性度计算
        List<int> LinearArray = new List<int>();
        List<int> LinearWave = new List<int>();

        //目标波形——角度
        Waveform TargetWave = new Waveform(1d / 10000000d);
        //目标波形求导后波形——角速度
        Waveform DerivatedWave;

        /// <summary>
        /// 用于检测是否是第一次采集
        /// </summary>
        bool firsttime = false;

        #endregion


        #region 采集相关设置
        //时间间隔-ms
        int CollectTimeSpan = 10;
        //单次时间-ms
        int SingleTime = 10;
        //采集次数
        int SamplingTimes = 1;
        #endregion

        Oscillocope osc;
        ExcelReadNWrite ERNW;

        #endregion

        #region 波形采集
        //滤波权重
        public int MeanFilteWeight = 20;
        public int MedianFilteWeight = 20;
        public int DownsampleWeight = 100;
        //波形采集线程
        Thread ThreadCollectWave;

        /// <summary>
        /// 从采集卡获取波形
        /// </summary>
        public void WaveCollect()
        {
            osc.InitClock();

            //根据采集次数设置循环次数
            for (int times = 0; times < SamplingTimes; times++)
            {
                //初始化四个数组
                Waveform CH1 = new Waveform(1d / 10000000d, 0, "Origin");
                Waveform CH2 = new Waveform(1d / 10000000d, 0, "Origin");
                Waveform CH3 = new Waveform(1d / 10000000d, 0, "Origin");
                Waveform CH4 = new Waveform(1d / 10000000d, 0, "Origin");
                TargetWave = new Waveform(1d / 10000000d, 0, "Origin");
                //将四个波形数组代入波形数组List
                WaveList[0] = CH1;
                WaveList[1] = CH2;
                WaveList[2] = CH3;
                WaveList[3] = CH4;

                //计算实际采集长度
                ulong sicnt = (ulong)osc.m_nScnt * 10000;                                                                            //每个通道采集的点数

                sicnt *= (ulong)osc.para_init.lChCnt;                                                                           //每个通道采集的样点

                //由于读取函数有最大读取要求，分多次读取数据
                int read_cnt = 0;
                ulong read_len = 0;
                bool isdivisible = false;
                if ((sicnt % Constants.READ_MAX_LENGTH) == 0)//如果读取的长度，刚好是最大允许读取长度的整数倍
                {
                    read_cnt = (int)(sicnt / Constants.READ_MAX_LENGTH);
                    isdivisible = true;
                }
                else
                {
                    read_cnt = (int)(sicnt / Constants.READ_MAX_LENGTH) + 1;
                }

                for (int i = 0; i < read_cnt; i++)
                {
                    if (i == (read_cnt - 1))
                    {
                        if (isdivisible)
                        {
                            read_len = Constants.READ_MAX_LENGTH;
                        }
                        else
                        {
                            read_len = sicnt % Constants.READ_MAX_LENGTH;
                        }
                    }
                    else
                    {
                        read_len = Constants.READ_MAX_LENGTH;
                    }

                    ushort[] inBuffer = new ushort[read_len];   //分配内存

                    //等待缓存钟数据量达到要求
                    osc.bufcnt = DllImport.PCI2168_GetBufCnt(osc.hdl);
                    //Thread.Sleep(50);
                    while (osc.bufcnt < (int)read_len)
                    {
                        osc.bufcnt = DllImport.PCI2168_GetBufCnt(osc.hdl);
                        Thread.Sleep(20);
                    }
                    Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "缓存钟数据长度：" + osc.bufcnt + "\n 期望读取长度：" + read_len + "\n总长度：" + sicnt + "\n采集次数：" + read_cnt });

                    ulong bBufOver = 0;
                    if (DllImport.PCI2168_ReadAD(osc.hdl, inBuffer, read_len, ref bBufOver))
                    {
                        int SingleChLength = (int)((long)read_len / osc.para_init.lChCnt) - 1;

                        for (int n = 0; n < SingleChLength; n++)
                        {
                            //V1[n + (int)Constants.READ_MAX_LENGTH / 2 * i] = inBuffer[para_init.lChCnt * n]-32768;
                            //V2[n + (int)Constants.READ_MAX_LENGTH / 2 * i] = inBuffer[para_init.lChCnt * n + 1]-32768;
                            //V1[n + (int)Constants.READ_MAX_LENGTH / para_init.lChCnt * i] = inBuffer[para_init.lChCnt * n];
                            for (int m = 0; m < osc.para_init.lChCnt; m++)
                            {
                                WaveList[m].Add(inBuffer[osc.para_init.lChCnt * n + m]);
                            }
                        }
                        CH5 = inBuffer;
                    }
                    else
                    {
                        return;
                    }
                }

                OutputFile(CH1, "CH1Protype");
                OutputFile(CH2, "CH2Protype");

                TargetWave = ProcessWave(CH1, CH2);
                //Waveform TargetStandardZeroWave = ProcessWave(CH3);
                OutputFile(TargetWave, "TargetName");

                //计算零点属性
                ZeroPropertyWave = waveProcesser.CalculateZero(TargetWave);

                ZeroProperty = ZeroPropertyWave.Sum() / ZeroPropertyWave.Length;

                //求导
                DerivatedWave = waveProcesser.OnlyDerivated(TargetWave);
                Zero = waveProcesser.CalculateZero(DerivatedWave);

                //OutputFile(Zero, "Zero");
                //OutputFile(DerivatedWave, "DerivatedWave");

                ////Waveform DerivatedStandardZero = new Waveform(TargetStandardZeroWave.TimeSpan,TargetStandardZeroWave.StartTime);
                ////Waveform StandardZero = new Waveform(TargetStandardZeroWave.TimeSpan,TargetStandardZeroWave.StartTime);
                //DerivatedStandardZero = waveProcesser.Derivative(TargetStandardZeroWave, out StandardZero);

                LinearWave = new List<int>();
                LinearArray = new List<int>();
                string Linear = (string)TBLinear.Dispatcher.Invoke(new GetTBLStatus(getTBLStatus), TBLinear);
                //Status.Dispatcher.Invoke(new UpdateEventHandler(StutusUpdate), new object[] { "线性度为" + TBLinear });
                double linear = double.Parse(Linear);
                LinearArray = waveProcesser.CalculateLinearArea(DerivatedWave, linear);

                ////计算时间利用率和速度均匀性
                //TimeUtilizationList = new List<double>();
                //TimeUtilizationList = waveProcesser.CalculateTimeUtilizationAndSpeedUniformity(LinearArray, DerivatedWave, out List<double> SpeedUnifomitylist);

                //OutputFile(TimeUtilizationList, "Time");

                //SpeedUniformityList = SpeedUnifomitylist;
                //TimeUtilization = TimeUtilizationList.Sum() / TimeUtilizationList.Count;
                //SpeedUniformity = SpeedUniformityList.Sum() / SpeedUniformityList.Count;


                // 频率 角度 速度 时间利用率 时间
                ListProperty = waveProcesser.CalculateProperties(Zero, LinearArray, TargetWave, DerivatedWave);

                OutputFile(ListProperty[0], "Frequency");
                OutputFile(ListProperty[1], "Angle");
                OutputFile(ListProperty[2], "Speed");
                OutputFile(ListProperty[3], "TimeU");
                OutputFile(ListProperty[4], "Time");

                ScanFrequency = ListProperty[0].Sum() / ListProperty[0].Count;
                EffectiveAngle = ListProperty[1].Sum() / ListProperty[1].Count;
                SpeedUniformity = ListProperty[2].Sum() / ListProperty[2].Count;
                TimeUtilization = ListProperty[3].Sum() / ListProperty[3].Count;

                ScanFrequencyList = ListProperty[0];
                EffectiveAngleList = ListProperty[1];
                SpeedUniformityList = ListProperty[2];
                TimeUtilizationList = ListProperty[3];
                List<double> TimeList = ListProperty[4];

                //#region 对导数部分处理
                //#region 计算零点和周期
                //List<double> periodArray = new List<double>();
                //if (Zero.Length != 0)
                //{
                //    for (int i = 0; i < Zero.Length; i++)
                //    {
                //        if (Zero[i]._value == 1)
                //        {
                //            periodArray.Add(i);
                //        }
                //    }
                //}

                //OutputFile(periodArray,"periodArray");

                //ListFrequency=new List<double>();

                //double PeriodSum = 0;
                //double PeriodTime = 0;
                //for (int i = 2; i < periodArray.Count; i+=2)
                //{
                //    PeriodSum += (periodArray[i] - periodArray[i - 2]);
                //    ListFrequency.Add(1 / ((periodArray[i] - periodArray[i - 2]) * Zero.TimeSpan));
                //    PeriodTime++;
                //}
                //SacnFrequency = 1 / (PeriodSum / (PeriodTime) * Zero.TimeSpan);

                //#endregion
                //#endregion

                TBLZero.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLZero, ZeroProperty.ToString() });
                TBLScanFreq.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLScanFreq, ScanFrequency.ToString("N2") + "Hz" });
                TBLTimeUtilization.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLTimeUtilization, TimeUtilization.ToString("N2") });
                TBLEffectiveAngle.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLEffectiveAngle, EffectiveAngle.ToString("N2") });
                TBLSpeedUniformity.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLSpeedUniformity, SpeedUniformity.ToString("N2") });

                //OutputFile(TargetWave, "TargetWave");
                //OutputFile(DerivatedWave, "DerivatedWave");

                if (isAngle)
                {
                    DrawingWave = TargetWave;
                }
                else
                {
                    DrawingWave = DerivatedWave;
                }
                UI.Dispatcher.BeginInvoke(new DrawWaves(MethodDrawWaves), DrawingWave);

                List<double> StandardZeroList = new List<double>();
                List<double> ZeroPropertyList = new List<double>();

                for (int i = 0; i < ZeroPropertyWave.Length; i++)
                {
                    ZeroPropertyList.Add(ZeroPropertyWave[i]._value);
                    StandardZeroList.Add(ZeroPropertyWave[0]._value + i * 0.04);
                }

                string ProductNum = (string)TBProducerNum.Dispatcher.Invoke(new GetTBLStatus(getTBLStatus), TBProducerNum);

                ERNW.SaveExcel(ProductNum,TargetWave, FilePath, StandardZeroList, ZeroPropertyList, ScanFrequencyList, EffectiveAngleList, TimeUtilizationList, TimeList, SpeedUniformityList);

                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "第" + (times + 1) + "次采集完成！\n" + "频率为" + ScanFrequency.ToString() });
                if (SamplingTimes != 1)
                {
                    //时间间隔
                    Thread.Sleep(CollectTimeSpan * 1000 * 60);
                }
            }
        }
        #endregion

        #region 波形绘制
        //当前正在绘制的波形
        Waveform DrawingWave;
        //波形
        Polyline wave = new Polyline();
        //点集
        PointCollection Points = new PointCollection();
        //波形绘制
        public delegate void DrawWaves(Waveform DrawingWave);
        //绘制波形 新方法
        public void MethodDrawWaves(Waveform Vwave)
        {
            if (Vwave != null && Vwave.Length != 0)
            {

                double TopRange = 0;

                //清空之前的数据
                Points.Clear();

                //点容器
                Point point;
                //获得画布的尺寸
                double height = UI.ActualHeight;
                double width = UI.ActualWidth;

                double WaveRange = (Vwave.Max() - Vwave.Min()) / 2 + Vwave.Min();

                NullableValue RangeValue = new NullableValue(WaveRange);
                for (int i = 0; i < Vwave.Length; i++)
                {
                    Vwave[i] = Vwave[i] - RangeValue;
                }

                double digit = Math.Floor(Math.Log10(Vwave.Max()));

                double digitnum = Vwave.Max() / Math.Pow(10, digit);

                double mid = Math.Floor(digitnum);

                if (digitnum > (mid + 0.5))
                {
                    TopRange = (mid + 1) * Math.Pow(10, digit);
                }
                else
                {
                    TopRange = (mid + 1) * Math.Pow(10, digit) - Math.Pow(10, digit) * 0.5;
                }

                double heightPercentage = height / 2 / TopRange;
                double widthPencentage = Vwave.Length / width;

                List<int> TargetLinearList = new List<int>();
                double m = 0;
                for (int i = 0; i < width; i++)
                {
                    double TargetY = height / 2 - Vwave[(int)Math.Floor(m)]._value * heightPercentage;

                    if (m < DerivatedWave.Length)
                    {
                        TargetLinearList.Add(LinearArray[(int)Math.Floor(m)]);
                    }

                    m += widthPencentage;

                    Points.Add(new Point(i, TargetY));
                }

                //OutputFile(LinearWave, "LinearWave");
                //OutputFile(LinearArray, "LinearArray");
                //OutputFile(Points1, "PointYArray");


                //wave1.Points = Points1;
                //UI.Children.Clear();
                //UI.Children.Add(wave1);
                if (UI.visualChildrenCount > 0)
                    UI.RemoveVisual(UI.getVisualChild(0));
                UI.AddVisual(DrawPolyline(Points, TargetLinearList, new SolidColorBrush(Color.FromRgb(89, 255, 91)), 1));

                //int ActualLong=CBIndexToWidthPoints(CBXAxis.SelectedIndex
            }
        }

        /// <summary>
        /// drawvisual波形绘制
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="thinkness"></param>
        /// <returns></returns>
        public Visual DrawPolyline(PointCollection points, Brush color, double thinkness)
        {
            DrawingVisual visual = new DrawingVisual();
            DrawingContext dc = visual.RenderOpen();
            Pen pen = new Pen(color, thinkness);
            pen.Freeze();

            for (int i = 0; i < points.Count - 1; i++)
            {
                dc.DrawLine(pen, points[i], points[i + 1]);
            }
            dc.Close();

            return visual;
        }

        /// <summary>
        /// drawvisual波形绘制(带线性区)
        /// </summary>
        /// <param name="points"></param>
        /// <param name="color"></param>
        /// <param name="thinkness"></param>
        /// <returns></returns>
        public Visual DrawPolyline(PointCollection points, List<int> TargetLinearList, Brush color, double thinkness)
        {
            DrawingVisual visual = new DrawingVisual();
            DrawingContext dc = visual.RenderOpen();
            Pen pen = new Pen(color, thinkness);
            Pen LinearPen = new Pen(new SolidColorBrush(Colors.Red), thinkness);
            pen.Freeze();

            for (int i = 0; i < points.Count - 1; i++)
            {
                if (i < TargetLinearList.Count - 1)
                {
                    if ((TargetLinearList[i] == 0) && (TargetLinearList[i + 1] == 0))
                    {
                        dc.DrawLine(pen, points[i], points[i + 1]);
                    }
                    else
                    {
                        dc.DrawLine(LinearPen, points[i], points[i + 1]);
                    }
                }
                else
                {
                    dc.DrawLine(pen, points[i], points[i + 1]);
                }
            }
            dc.Close();

            return visual;
        }

        #region 是否为角度显示
        bool isAngle = true;

        private void RBtnAngleDisplay_Checked(object sender, RoutedEventArgs e)
        {
            isAngle = true;
            if (firsttime)
            {
                DrawingWave = TargetWave;
                MethodDrawWaves(DrawingWave);
            }
        }

        private void RBtnAlphaDisplay_Checked(object sender, RoutedEventArgs e)
        {
            isAngle = false;
            if (firsttime)
            {
                DrawingWave = DerivatedWave;
                MethodDrawWaves(DrawingWave);
            }
        }

        #region 废弃的方法
        //    private void btnCheckBufferCount_Click(object sender, RoutedEventArgs e)
        //    {
        //        //while (DllImport.PCI2168_GetBufCnt(hdl) < (int)samcnt)
        //        //{
        //        bufcnt = DllImport.PCI2168_GetBufCnt(hdl);
        //        //Thread.Sleep(5);
        //        //}
        //        ulong inBuffer = 0;

        //        Status.Content += "\n" + bufcnt;
        //        if(!DllImport.PCI2168_ReadAD(hdl,buf,(ulong)bufcnt,ref inBuffer))
        //        {
        //            Status.Content += "\n读取数据失败！";
        //            return;
        //        }
        //        Status.Content += "\n读取数据成功！";

        //        string path = @"C:\test.txt";
        //        FileStream fs = new FileStream(path, FileMode.Create);
        //        StreamWriter sw = new StreamWriter(fs);
        //        //开始写入
        //        for (int i = 0; i < buf.Length; i++)
        //        {
        //            sw.Write(buf[i] + "\t");
        //        }
        //        //清空缓冲区
        //        sw.Flush();
        //        //关闭流
        //        sw.Close();
        //        fs.Close();
        //    }

        ///// <summary>
        ///// 根据选择的触发模式来设置samcnt的长度
        ///// </summary>
        //public void SetsamcntByTriggerMode()
        //{
        //    switch (TriggerMode.SelectedIndex)
        //    {
        //        case (int)EmTriggerMode.TRIG_MODE_MIDDLE:
        //            samcnt = (ulong)(m_nTrigLen) * 32 * trig_cnt;
        //            break;
        //        case (int)EmTriggerMode.TRIG_MODE_DELAY:
        //        case (int)EmTriggerMode.TRIG_MODE_POST:
        //        case (int)EmTriggerMode.TRIG_MODE_PRE:
        //            samcnt = (ulong)m_nTrigLen * 32 * trig_cnt;
        //            break;
        //        case (int)EmTriggerMode.TRIG_MODE_CONTINUE:
        //            samcnt = 5 * 1024 * 1024;//5M
        //            break;
        //        default:
        //            samcnt = 1024 * 1024;//1M
        //            break;
        //    }
        //}

        //使用DrawVisual画Polyline
        #endregion

        #endregion
        #endregion

        #region 波形处理部分
        /// <summary>
        /// 通过输入CH1和CH2的波形，处理出原始波形并计算出目标波形
        /// </summary>
        /// <param name="OriginWaveCH1"></param>
        /// <param name="OriginWaveCH2"></param>
        /// <returns></returns>
        public Waveform ProcessWave(Waveform OriginWaveCH1, Waveform OriginWaveCH2)
        {
            Waveform TargetWave = new Waveform();
            //整体降采样
            Waveform CH11 = waveProcesser.DownSampling(OriginWaveCH1, DownsampleWeight);
            Waveform CH21 = waveProcesser.DownSampling(OriginWaveCH2, DownsampleWeight);

            CH11 = waveProcesser.TranslateWaveform(CH11, "CH1");
            CH21 = waveProcesser.TranslateWaveform(CH21, "CH2");

            OutputFile(CH11, "CH1");
            OutputFile(CH21, "CH2");
            #region 对原始波形处理部分

            //计算角度变化波形
            TargetWave = waveProcesser.Calculate(CH21, CH11);


            //中值滤波
            TargetWave = waveProcesser.MedianFilter(TargetWave, MedianFilteWeight);


            //均值滤波
            TargetWave = waveProcesser.MeanFilter(TargetWave, MeanFilteWeight);


            return TargetWave;
            #endregion
        }

        /// <summary>
        /// 通过输入原始波形，计算出目标波形
        /// </summary>
        /// <param name="OriginWaveCH1"></param>
        /// <returns></returns>
        public Waveform ProcessWave(Waveform OriginWaveCH1)
        {
            Waveform TargetWave = new Waveform(OriginWaveCH1.TimeSpan, OriginWaveCH1.StartTime, OriginWaveCH1.Type);
            #region 对原始波形处理部分
            //整体降采样
            TargetWave = waveProcesser.DownSampling(OriginWaveCH1, DownsampleWeight);
            //中值滤波
            TargetWave = waveProcesser.MedianFilter(TargetWave, MedianFilteWeight);
            //均值滤波
            TargetWave = waveProcesser.MeanFilter(TargetWave, MeanFilteWeight);

            return TargetWave;
            #endregion
        }
        #endregion

        #region 表格保存
        /// <summary>
        /// 存储路径设置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.ShowDialog();
            if (dialog.SelectedPath != string.Empty)
            {
                FilePath = dialog.SelectedPath;
            }
        }

        //表格保存
        public delegate void SaveInExcel();

        ////Excel表格保存方法
        //public void SaveExcel(List<double> StandardZeroList, List<double> ZeroPropertyList, List<double> ScanFrequencyList, List<double> EffectiveAngleList, List<double> TimeUltilizationList, List<double> TimeList, List<double> SpeedUniformityList)
        //{
        //    //List<double> CalculatedZero = new List<double> { 1.1, 2.99, 5.2, 6.8, 9.1, 2.2, 3.9, 6.1, 7.89, 10, 666.5 };
        //    List<double> differZero = new List<double>();

        //    List<double> differFreq = new List<double>();

        //    HSSFWorkbook workBook = new HSSFWorkbook();
        //    ISheet DataworkSheet = workBook.CreateSheet("数据记录");
        //    ISheet WaveworkSheet = workBook.CreateSheet("原始波形");
        //    DataworkSheet.DefaultColumnWidth = 20;
        //    WaveworkSheet.DefaultColumnWidth = 20;
        //    //IRow row = workSheet.CreateRow(0);

        //    ICellStyle style = workBook.CreateCellStyle();

        //    style.Alignment = NPOI.SS.UserModel.HorizontalAlignment.Center;
        //    IFont font = workBook.CreateFont();
        //    font.Boldweight = short.MaxValue;
        //    style.SetFont(font);

        //    string NowDay = DateTime.Now.ToString("yyyyMMdd");
        //    string NowTime = DateTime.Now.ToString("hhmmss");

        //    List<string[]> contentList = new List<string[]>
        //    {
        //        new string[] { "产品编号", "零位", "扫描频率", "线性段时间利用率" ,"有效摆角","速度均匀性"},
        //        new string[] { (string)TBProducerNum.Dispatcher.Invoke(new GetTBLStatus(getTBLStatus),TBProducerNum)},
        //        new string[] { "测量时间" },
        //        new string[] { NowDay + NowTime }
        //    };
        //    string[] SubTitle = new string[] { "标准-从零点起始", "测量值-按零点起始", "△t", "标准-初始为25HZ", "测量", "偏差", "线性段1时间", "时间利用率", "线性段2时间", "时间利用率", "有效摆角1", "有效摆角", "速度1", "速度2" };


        //    #region 数据记录表
        //    #region 填写表头
        //    //第一行
        //    IRow thisrow = DataworkSheet.CreateRow(0);
        //    //产品编号——1列
        //    ICell thiscell1 = thisrow.CreateCell(0);
        //    thiscell1.CellStyle = style;
        //    thiscell1.SetCellValue(contentList[0][0]);
        //    //零位——3列
        //    ICell thiscell2 = thisrow.CreateCell(1);
        //    thiscell2.CellStyle = style;
        //    thiscell2.SetCellValue(contentList[0][1]);
        //    //扫描频率——3列
        //    ICell thiscell3 = thisrow.CreateCell(4);
        //    thiscell3.CellStyle = style;
        //    thiscell3.SetCellValue(contentList[0][2]);
        //    //线性段时间利用率——4列
        //    ICell thiscell4 = thisrow.CreateCell(7);
        //    thiscell4.CellStyle = style;
        //    thiscell4.SetCellValue(contentList[0][3]);
        //    //有效摆角——2列
        //    ICell thiscell5 = thisrow.CreateCell(11);
        //    thiscell5.CellStyle = style;
        //    thiscell5.SetCellValue(contentList[0][4]);
        //    //速度均匀性——2列
        //    ICell thiscell6 = thisrow.CreateCell(13);
        //    thiscell6.CellStyle = style;
        //    thiscell6.SetCellValue(contentList[0][5]);

        //    SetCellRangeAddress(DataworkSheet, 0, 0, 1, 3);
        //    SetCellRangeAddress(DataworkSheet, 0, 0, 4, 6);
        //    SetCellRangeAddress(DataworkSheet, 0, 0, 7, 10);
        //    SetCellRangeAddress(DataworkSheet, 0, 0, 11, 12);
        //    SetCellRangeAddress(DataworkSheet, 0, 0, 13, 14);

        //    //第一列
        //    for (int i = 1; i < contentList.Count; i++)
        //    {
        //        IRow row = DataworkSheet.CreateRow(i);
        //        for (int j = 0; j < contentList[i].Length; j++)
        //        {
        //            ICell thiscell = row.CreateCell(j);
        //            thiscell.SetCellValue(contentList[i][j]);
        //            thiscell.CellStyle = style;
        //        }
        //    }

        //    //第二行
        //    IRow SubTitleRow = DataworkSheet.GetRow(1);
        //    for (int i = 0; i < SubTitle.Length; i++)
        //    {
        //        ICell thiscell = SubTitleRow.CreateCell(i + 1);
        //        thiscell.SetCellValue(SubTitle[i]);
        //        thiscell.CellStyle = style;
        //    }
        //    #endregion

        //    #region 计算误差数据
        //    differZero = waveProcesser.CalculateDiffer(StandardZeroList, ZeroPropertyList);
        //    differFreq = waveProcesser.CalculateDiffer(25, ScanFrequencyList);
        //    #endregion

        //    //添加零位数据
        //    for (int i = 0; i < differZero.Count; i++)
        //    {
        //        IRow DataRow = DataworkSheet.GetRow(i + 2);
        //        if (DataRow == null)
        //        {
        //            DataRow = DataworkSheet.CreateRow(i + 2);
        //        }

        //        ICell DataStandardZero = DataRow.CreateCell(1);
        //        DataStandardZero.CellStyle = style;
        //        DataStandardZero.SetCellValue(StandardZeroList[i]);
        //        ICell DataCalculateZero = DataRow.CreateCell(2);
        //        DataCalculateZero.SetCellValue(ZeroPropertyList[i]);
        //        DataCalculateZero.CellStyle = style;
        //        ICell DataDifferZero = DataRow.CreateCell(3);
        //        DataDifferZero.SetCellValue(differZero[i]);
        //        DataDifferZero.CellStyle = style;
        //    }

        //    //添加扫描频率数据
        //    for (int i = 0; i < differFreq.Count; i++)
        //    {
        //        IRow DataRow = DataworkSheet.GetRow(i + 2);
        //        if (DataRow == null)
        //        {
        //            DataRow = DataworkSheet.CreateRow(i + 2);
        //        }

        //        ICell DataDifferFreq = DataRow.CreateCell(4);
        //        DataDifferFreq.SetCellValue(25);
        //        DataDifferFreq.CellStyle = style;
        //        ICell DataStandardFreq = DataRow.CreateCell(5);
        //        DataStandardFreq.SetCellValue(ScanFrequencyList[i]);
        //        DataStandardFreq.CellStyle = style;
        //        ICell DataCalculateFreq = DataRow.CreateCell(6);
        //        DataCalculateFreq.SetCellValue(differFreq[i]);
        //        DataCalculateFreq.CellStyle = style;
        //    }

        //    //添加时间利用率数据
        //    int m = 0;
        //    for (int i = 1; i < TimeUltilizationList.Count; i += 2)
        //    {
        //        IRow DataRow = DataworkSheet.GetRow(m + 2);
        //        if (DataRow == null)
        //        {
        //            DataRow = DataworkSheet.CreateRow(m + 2);
        //        }

        //        ICell LinearTime1 = DataRow.CreateCell(7);
        //        LinearTime1.SetCellValue(TimeList[i - 1]);
        //        LinearTime1.CellStyle = style;
        //        ICell TimeUtilization1 = DataRow.CreateCell(8);
        //        TimeUtilization1.SetCellValue(TimeUltilizationList[i - 1]);
        //        TimeUtilization1.CellStyle = style;
        //        ICell LinearTime2 = DataRow.CreateCell(9);
        //        LinearTime2.SetCellValue(TimeList[i]);
        //        LinearTime2.CellStyle = style;
        //        ICell TimeUtilization2 = DataRow.CreateCell(10);
        //        TimeUtilization2.SetCellValue(TimeUltilizationList[i]);
        //        TimeUtilization2.CellStyle = style;

        //        m++;
        //    }

        //    //添加有效摆角数据
        //    m = 0;
        //    for (int i = 1; i < EffectiveAngleList.Count; i += 2)
        //    {
        //        IRow DataRow = DataworkSheet.GetRow(m + 2);
        //        if (DataRow == null)
        //        {
        //            DataRow = DataworkSheet.CreateRow(m + 2);
        //        }

        //        ICell LinearTime1 = DataRow.CreateCell(11);
        //        LinearTime1.SetCellValue(EffectiveAngleList[i - 1]);
        //        LinearTime1.CellStyle = style;
        //        ICell TimeUtilization1 = DataRow.CreateCell(12);
        //        TimeUtilization1.SetCellValue(EffectiveAngleList[i]);
        //        TimeUtilization1.CellStyle = style;
        //        m++;
        //    }

        //    //添加速度均匀性数据
        //    m = 0;
        //    for (int i = 1; i < SpeedUniformityList.Count; i += 2)
        //    {
        //        IRow DataRow = DataworkSheet.GetRow(m + 2);
        //        if (DataRow == null)
        //        {
        //            DataRow = DataworkSheet.CreateRow(m + 2);
        //        }
        //        ICell SpeedUniformity1 = DataRow.CreateCell(13);
        //        SpeedUniformity1.SetCellValue(SpeedUniformityList[i - 1]);
        //        SpeedUniformity1.CellStyle = style;
        //        ICell SpeedUniformity2 = DataRow.CreateCell(14);
        //        SpeedUniformity2.SetCellValue(SpeedUniformityList[i]);
        //        SpeedUniformity2.CellStyle = style;
        //        m++;
        //    }

        //    #endregion

        //    #region 原始波形表
        //    //第一行
        //    IRow WaveName = WaveworkSheet.CreateRow(0);
        //    ICell Time = WaveName.CreateCell(0);
        //    Time.SetCellValue("时间");
        //    Time.CellStyle = style;
        //    ICell Voltage = WaveName.CreateCell(1);
        //    Voltage.SetCellValue("电压");
        //    Voltage.CellStyle = style;

        //    //原始波形数据
        //    for (int i = 0; i < TargetWave.Length; i++)
        //    {
        //        IRow DataRow = WaveworkSheet.CreateRow(i + 1);
        //        ICell TimeX = DataRow.CreateCell(0);
        //        TimeX.SetCellValue(TargetWave.StartTime + TargetWave.TimeSpan * i);
        //        TimeX.CellStyle = style;
        //        ICell VoltageY = DataRow.CreateCell(1);
        //        VoltageY.SetCellValue(TargetWave[i]._value);
        //        VoltageY.CellStyle = style;
        //    }
        //    #endregion

        //    Status.Dispatcher.Invoke(new UpdateEventHandler(StutusUpdate), FilePath);
        //    string ExcelPath = System.IO.Path.Combine(FilePath, NowDay);
        //    if (!Directory.Exists(ExcelPath))
        //    {
        //        Directory.CreateDirectory(ExcelPath);
        //    }
        //    ExcelPath = System.IO.Path.Combine(ExcelPath, NowTime + ".xls");
        //    FileStream Fs = File.OpenWrite(ExcelPath);
        //    workBook.Write(Fs);
        //    Fs.Close();
        //}

        string FilePath = string.Empty;
        //单元格合并方法
        public static void SetCellRangeAddress(ISheet sheet, int rowstart, int rowend, int colstart, int colend)
        {
            CellRangeAddress cellRangeAddress = new CellRangeAddress(rowstart, rowend, colstart, colend);
            sheet.AddMergedRegion(cellRangeAddress);
        }
        #endregion

        #region 表格读取
        /// <summary>
        /// 表格属性枚举
        /// </summary>
        public enum DataProperty
        {
            ZeroProperty,
            SacnFrequency,
            LinearTimeAvaliability,
            EffectiveAngle,
            SpeedUniformity
        }

        #region 表格读取事件
        private void ReadButton_Click(object sender, RoutedEventArgs e)
        {
            string ExcelFilePath;
            OpenFileDialog filedialog = new OpenFileDialog();
            filedialog.DefaultExt = ".xls";
            filedialog.Filter = "Excel文件(.xls)|*.xls";

            if (filedialog.ShowDialog() == true)
            {
                ExcelFilePath = filedialog.FileName;
                IWorkbook workBook = null;
                try
                {
                    FileStream fs = new FileStream(ExcelFilePath, FileMode.Open, FileAccess.Read);
                    if (ExcelFilePath.IndexOf(".xls") > 0)
                    {
                        workBook = new HSSFWorkbook(fs);
                    }
                    else
                    {
                        workBook = new XSSFWorkbook(fs);
                    }
                    TargetWave = ERNW.ReadExcelWave(workBook);
                    TargetWave.Type = "Origin";
                    DerivatedWave = waveProcesser.Derivative(TargetWave, out Waveform Zero);

                    List<int> LinearWave = new List<int>();

                    string Linear = (string)TBLinear.Dispatcher.Invoke(new GetTBLStatus(getTBLStatus), TBLinear);
                    double linear = double.Parse(Linear);
                    LinearArray = waveProcesser.CalculateLinearArea(DerivatedWave, linear);

                    if (isAngle)
                    {
                        DrawingWave = TargetWave;
                    }
                    else
                    {
                        DrawingWave = DerivatedWave;
                    }
                    UI.Dispatcher.BeginInvoke(new DrawWaves(MethodDrawWaves), DrawingWave);

                    Array DataPropertyArray = Enum.GetValues(typeof(DataProperty));
                    for (int i = 0; i < DataPropertyArray.Length; i++)
                    {
                        DataPropertyList[i] = ERNW.ReadExcelData(workBook, (DataProperty)DataPropertyArray.GetValue(i));
                        TextBoxList[i].Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TextBoxList[i], DataPropertyList[i].ToString("N2") });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }

            }
            else
            {
                Status.Dispatcher.Invoke(new UpdateEventHandler(StutusUpdate), "操作取消！");
            }
        }
        #endregion

        #endregion


        #region UI事件


        /// <summary>
        /// 波形采集启动事件
        /// </summary>
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (ThreadCollectWave.ThreadState == ThreadState.Unstarted || ThreadCollectWave.ThreadState == ThreadState.Stopped || ThreadCollectWave.ThreadState == ThreadState.Aborted)
            {
                if (FilePath == string.Empty)
                {
                    System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
                    dialog.ShowDialog();
                    if (dialog.SelectedPath == string.Empty)
                    {
                        Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "采集失败，路径为空！" });
                        e.Handled = true;
                        return;
                    }
                    else
                    {
                        FilePath = dialog.SelectedPath;
                    }
                }
                ThreadCollectWave = new Thread(WaveCollect);
                ThreadCollectWave.Start();
                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "采集开始！" });
            }
            else
            {
                e.Handled = true;
            }
        }


        /// <summary>
        /// 停止采集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            if (ThreadCollectWave.ThreadState == ThreadState.Running)
            {
                ThreadCollectWave.Abort();
                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "采集已停止。" });
            }
        }

        #endregion

        /// <summary>
        /// 检测是否是数字
        /// </summary>
        /// <param name="_string"></param>
        /// <returns></returns>
        public static bool IsNumberic(string _string)
        {
            return !string.IsNullOrEmpty(_string) && _string.All(char.IsDigit);
        }

        //单次时间上下限
        int SingleTimeUpperLimit = 1000;
        int SingleTimeLowerLimit = 0;
        //时间间隔上下限
        int TimeSpanUpperLimit = 100;
        int TimeSpanLowerLimit = 0;
        //采集次数上下限
        int SamplingTimesUpperLimit = 100;
        int SamplingTimesLowerLimit = 0;
        #region 单次时间
        //单次时间
        private void TBSingleTime_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsNumberic(e.Text))
            {
                e.Handled = true;
            }
        }

        string oldString = string.Empty;
        private void TBSingleTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                int num = Convert.ToInt32(TBSingleTime.Text);
                if (num < SingleTimeLowerLimit)
                {
                    MessageBox.Show("输入超出范围！\n单次最大时间：" + SingleTimeUpperLimit + "\n单次最小时间：" + SingleTimeLowerLimit);
                    TBSingleTime.Text = SingleTimeLowerLimit.ToString();
                }
                else if (num > SingleTimeUpperLimit)
                {
                    MessageBox.Show("输入超出范围！\n单次最大时间：" + SingleTimeUpperLimit + "\n单次最小时间：" + SingleTimeLowerLimit);
                    TBSingleTime.Text = SingleTimeUpperLimit.ToString();
                }
                else
                {
                    //获取采集时长
                    osc.m_nScnt = num;
                    TBSingleTime.Text = osc.m_nScnt.ToString();
                }
            }
            catch (Exception ex)
            {
                TBSingleTime.Text = osc.m_nScnt.ToString();
                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), e.ToString());
            }
        }
        #endregion

        #region 时间间隔
        //时间间隔
        private void TBTimeSpan_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsNumberic(e.Text))
            {
                e.Handled = true;
            }
        }

        private void TBTimeSpan_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                int num = Convert.ToInt32(TBTimeSpan.Text);
                if (num < SingleTimeLowerLimit)
                {
                    MessageBox.Show("输入超出范围！\n单次最大时间：" + TimeSpanUpperLimit + "\n单次最小时间：" + TimeSpanLowerLimit);
                    TBTimeSpan.Text = TimeSpanLowerLimit.ToString();
                }
                else if (num > SingleTimeUpperLimit)
                {
                    MessageBox.Show("输入超出范围！\n单次最大时间：" + TimeSpanUpperLimit + "\n单次最小时间：" + TimeSpanLowerLimit);
                    TBTimeSpan.Text = TimeSpanUpperLimit.ToString();
                }
                else
                {
                    //获取采集间隔
                    CollectTimeSpan = num;
                    TBTimeSpan.SelectionStart = TBTimeSpan.Text.Length;
                }
            }
            catch (Exception)
            {
                TBTimeSpan.Text = CollectTimeSpan.ToString();
                //Status.Dispatcher.Invoke(new UpdateEventHandler(StutusUpdate), e.ToString());
            }
        }
        #endregion

        #region 采集次数
        //采集次数
        private void TBSamplingTimes_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (!IsNumberic(e.Text))
            {
                e.Handled = true;
            }
        }

        private void TBSamplingTimes_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                int num = Convert.ToInt32(TBSamplingTimes.Text);
                if (int.TryParse(TBSamplingTimes.Text, out int result))
                {
                    if (num < SamplingTimesLowerLimit)
                    {
                        MessageBox.Show("输入超出范围！\n单次最大时间：" + SamplingTimesUpperLimit + "\n单次最小时间：" + SamplingTimesLowerLimit);
                        TBSamplingTimes.Text = SamplingTimesLowerLimit.ToString();
                    }
                    else if (num > SingleTimeUpperLimit)
                    {
                        MessageBox.Show("输入超出范围！\n单次最大时间：" + SamplingTimesUpperLimit + "\n单次最小时间：" + SamplingTimesLowerLimit);
                        TBSamplingTimes.Text = SamplingTimesUpperLimit.ToString();
                    }
                    else
                    {
                        SamplingTimes = num;
                    }
                }
            }
            catch (Exception)
            {
                TBTimeSpan.Text = CollectTimeSpan.ToString();
            }
        }
        #endregion

        /// <summary>
        /// 测试用方法——将波形以txt文件输出默认D盘根目录
        /// </summary>
        /// <param name="OutputArray"></param>
        /// <param name="Name"></param>
        #region
        public void OutputFile(Waveform OutputArray, string Name)
        {
            string path = @"D:\" + Name + ".txt";
            FileStream fs = new FileStream(path, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            //开始写入
            for (int i = 0; i < OutputArray.Length; i++)
            {
                sw.Write(OutputArray[i]._value + "\t");
            }
            //清空缓冲区
            sw.Flush();
            //关闭流
            sw.Close();
            fs.Close();
        }

        public void OutputFile(List<int> OutputArray, string Name)
        {
            string path = @"D:\" + Name + ".txt";
            FileStream fs = new FileStream(path, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            //开始写入
            for (int i = 0; i < OutputArray.Count; i++)
            {
                sw.Write(OutputArray[i] + "\t");
            }
            //清空缓冲区
            sw.Flush();
            //关闭流
            sw.Close();
            fs.Close();
        }

        public void OutputFile(List<double> OutputArray, string Name)
        {
            string path = @"D:\" + Name + ".txt";
            FileStream fs = new FileStream(path, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            //开始写入
            for (int i = 0; i < OutputArray.Count; i++)
            {
                sw.Write(OutputArray[i] + "\t");
            }
            //清空缓冲区
            sw.Flush();
            //关闭流
            sw.Close();
            fs.Close();
        }

        public void OutputFile(PointCollection OutputArray, string Name)
        {
            string path = @"D:\" + Name + ".txt";
            FileStream fs = new FileStream(path, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            //开始写入
            for (int i = 0; i < OutputArray.Count; i++)
            {
                sw.Write(OutputArray[i].Y + "\t");
            }
            //清空缓冲区
            sw.Flush();
            //关闭流
            sw.Close();
            fs.Close();
        }
        #endregion
    }
}
