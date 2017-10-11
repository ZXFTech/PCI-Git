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
using MahApps.Metro.Controls;
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
            InitializeComponent();

            ThreadCollectWave = new Thread(WaveCollect);

            firsttime = true;

            DataPropertyList.Add(ZeroProperty);
            DataPropertyList.Add(SacnFrequency);
            DataPropertyList.Add(TimeUtilization);
            DataPropertyList.Add(EffectiveAngle);
            DataPropertyList.Add(SpeedUniformity);
            TextBoxList.Add(TBLZero);
            TextBoxList.Add(TBLScanFreq);
            TextBoxList.Add(TBLTimeUtilization);
            TextBoxList.Add(TBLEffectiveAngle);
            TextBoxList.Add(TBLSpeedUniformity);
            InitDevice();
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
        public delegate void UpdateTBLEventHandler(System.Windows.Controls.TextBox TBL,string status);
        //更新指定控件方法
        public void TBLUpdateStatus(System.Windows.Controls.TextBox TBL,string status)
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
            //初始化
            para_init.lSelDataSrc = m_bADcnt;				                                                                //0-  AD数据源  1-计数器数据源			
            para_init.TriggerDelay = m_nTrigDelay;				                                                            //仅延时触发有效
            para_init.TriggerMode = (long)EmTriggerMode.TRIG_MODE_CONTINUE;	                                                //触发模式
            para_init.TriggerSource = (long)EmTriggerSourse.TRIG_SRC_EXT_FALLING;	                                        //触发源
            para_init.TriggerLength = m_nTrigLen;				                                                            //触发长度
            para_init.TriggerLevel = (long)(m_fLevel * 4096.0 / 5.0);                                                       //触发电平，仅触发源是模拟触发有效
            para_init.lChCnt = m_lChcnt;						                                                            //通道数 当前设置为2 	
            para_init.lADFmt = (long)EmADFormat.ADFMT_2SBIN;						                                        //默认直接二进制输出

            //初始化采样钟
            if (!DllImport.PCI2168_initADCLK(hdl, (long)EmADClkSel.ADCLK_INT, 1))
            {
                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "初始化采样钟失败！" });
                return;
            }

            if (!DllImport.PCI2168_initAD(hdl, ref para_init))
            {
                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "初始化设备失败！" });
                return;
            }

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
                ulong sicnt = (ulong)m_nScnt * 10000;                                                                            //每个通道采集的点数

                sicnt *= (ulong)para_init.lChCnt;                                                                           //每个通道采集的样点

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
                    bufcnt = DllImport.PCI2168_GetBufCnt(hdl);
                    //Thread.Sleep(50);
                    while (bufcnt < (int)read_len)
                    {
                        bufcnt = DllImport.PCI2168_GetBufCnt(hdl);
                        Thread.Sleep(20);
                    }
                    Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "缓存钟数据长度：" + bufcnt + "\n 期望读取长度：" + read_len + "\n总长度：" + sicnt + "\n采集次数：" + read_cnt });

                    ulong bBufOver = 0;
                    if (DllImport.PCI2168_ReadAD(hdl, inBuffer, read_len, ref bBufOver))
                    {
                        int SingleChLength = (int)((long)read_len / para_init.lChCnt) - 1;

                        for (int n = 0; n < SingleChLength; n++)
                        {
                            //V1[n + (int)Constants.READ_MAX_LENGTH / 2 * i] = inBuffer[para_init.lChCnt * n]-32768;
                            //V2[n + (int)Constants.READ_MAX_LENGTH / 2 * i] = inBuffer[para_init.lChCnt * n + 1]-32768;
                            //V1[n + (int)Constants.READ_MAX_LENGTH / para_init.lChCnt * i] = inBuffer[para_init.lChCnt * n];
                            for (int m = 0; m < para_init.lChCnt; m++)
                            {
                                WaveList[m].Add(inBuffer[para_init.lChCnt * n + m]);
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

                //求导
                DerivatedWave = waveProcesser.Derivative(TargetWave, out Zero);

                LinearWave = new Waveform();
                string Linear = (string)TBLinear.Dispatcher.Invoke(new GetTBLStatus(getTBLStatus), TBLinear);
                //Status.Dispatcher.Invoke(new UpdateEventHandler(StutusUpdate), new object[] { "线性度为" + TBLinear });
                double linear = double.Parse(Linear);
                LinearArray = waveProcesser.CalculateLinearArea(DerivatedWave, linear);

                //计算时间利用率和速度均匀性
                TimeUtilizationList = waveProcesser.CalculateTimeUtilizationAndSpeedUniformity(LinearArray,DerivatedWave,out List<double> SpeedUnifomitylist);
                SpeedUniformityList = SpeedUnifomitylist;
                TimeUtilization = TimeUtilizationList.Sum() / TimeUtilizationList.Count;
                SpeedUniformity = SpeedUniformityList.Sum() / SpeedUniformityList.Count;

                #region 对导数部分处理
                #region 计算零点和周期
                List<double> periodArray = new List<double>();
                if (Zero.Length != 0)
                {
                    for (int i = 0; i < Zero.Length; i++)
                    {
                        if (Zero[i]._value == 1)
                        {
                            periodArray.Add(i);
                        }
                    }
                }

                double PeriodSum = 0;
                double PeriodTime = 0;
                for (int i = 1; i < periodArray.Count; i++)
                {
                    PeriodSum += (periodArray[i] - periodArray[i - 1]);
                    ListFrequency.Add(1 / ((periodArray[i] - periodArray[i - 1]) * 2 * Zero.TimeSpan));
                    PeriodTime++;
                }
                SacnFrequency = 1 / (PeriodSum / (PeriodTime / 2) * Zero.TimeSpan);

                #endregion
                #endregion

                TBLZero.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLZero, "0" });
                TBLScanFreq.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLScanFreq, SacnFrequency.ToString("N2") + "Hz" });
                TBLTimeUtilization.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLTimeUtilization, TimeUtilization.ToString("N2")});
                TBLEffectiveAngle.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLEffectiveAngle, SacnFrequency.ToString("N2")});
                TBLSpeedUniformity.Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TBLSpeedUniformity, SpeedUniformity.ToString("N2")});

                OutputFile(TargetWave, "TargetWave");
                OutputFile(DerivatedWave, "DerivatedWave");

                if (isAngle)
                {
                    DrawingWave = TargetWave;
                }
                else
                {
                    DrawingWave = DerivatedWave;
                }
                UI.Dispatcher.BeginInvoke(new DrawWaves(MethodDrawWaves), DrawingWave);

                SaveExcel();

                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "第" + (times + 1) + "次采集完成！\n" + "频率为" + SacnFrequency.ToString() });
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
        Polyline wave1 = new Polyline();
        //点集
        PointCollection Points1 = new PointCollection();
        //波形绘制
        public delegate void DrawWaves(Waveform DrawingWave);
        //绘制波形 新方法
        public void MethodDrawWaves(Waveform Vwave)
        {
            if (Vwave != null && Vwave.Length != 0)
            {
                LinearWave = new Waveform();
                string Linear = (string)TBLinear.Dispatcher.Invoke(new GetTBLStatus(getTBLStatus), TBLinear);
                //Status.Dispatcher.Invoke(new UpdateEventHandler(StutusUpdate), new object[] { "线性度为" + TBLinear });
                double linear = double.Parse(Linear);
                LinearArray = waveProcesser.CalculateLinearArea(DerivatedWave, linear);

                double TopRange = 0;

                //清空之前的数据
                Points1.Clear();

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

                List<double> TargetYList = new List<double>();
                double m = 0;
                for (int i = 0; i < width; i++)
                {
                    double TargetY = height / 2 - Vwave[(int)Math.Floor(m)]._value * heightPercentage;

                    if (m < DerivatedWave.Length)
                    {
                        LinearWave.Add(LinearArray[(int)Math.Floor(m)]);
                    }

                    m += widthPencentage;

                    Points1.Add(new Point(i, TargetY));
                }

                OutputFile(LinearWave, "LinearWave");
                OutputFile(LinearArray, "LinearArray");
                OutputFile(Points1, "PointYArray");


                //wave1.Points = Points1;
                //UI.Children.Clear();
                //UI.Children.Add(wave1);
                if (UI.visualChildrenCount > 0)
                    UI.RemoveVisual(UI.getVisualChild(0));
                UI.AddVisual(DrawPolyline(Points1, new SolidColorBrush(Color.FromRgb(89, 255, 91)), 1));

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
            Pen LinearPen = new Pen(new SolidColorBrush(Colors.Red), thinkness);
            pen.Freeze();

            for (int i = 0; i < points.Count-1; i++)
            {
                if (i < LinearWave.Length-1)
                {
                    if (LinearWave[i]._value == 0 && LinearWave[i + 1]._value == 0)
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
                MethodDrawWaves(TargetWave);
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
        public Waveform ProcessWave(Waveform OriginWaveCH1,Waveform OriginWaveCH2)
        {
            Waveform TargetWave = new Waveform();
            //整体降采样
            Waveform CH11 = waveProcesser.DownSampling(OriginWaveCH1, DownsampleWeight);
            Waveform CH21 = waveProcesser.DownSampling(OriginWaveCH2, DownsampleWeight);
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
            Waveform TargetWave = new Waveform();
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

        //Excel表格保存方法
        public void SaveExcel()
        {
            List<double> StandardZero = new List<double> { 1, 3, 5, 7, 9, 2, 4, 6, 8, 10, 666 };
            List<double> CalculatedZero = new List<double> { 1.1, 2.99, 5.2, 6.8, 9.1, 2.2, 3.9, 6.1, 7.89, 10, 666.5 };
            List<double> differZero = new List<double>();

            List<double> CalculatedFreq = ListFrequency;
            List<double> differFreq = new List<double>();

            HSSFWorkbook workBook = new HSSFWorkbook();
            ISheet DataworkSheet = workBook.CreateSheet("数据记录");
            ISheet WaveworkSheet = workBook.CreateSheet("原始波形");
            DataworkSheet.DefaultColumnWidth = 20;
            WaveworkSheet.DefaultColumnWidth = 20;
            //IRow row = workSheet.CreateRow(0);

            ICellStyle style = workBook.CreateCellStyle();

            style.Alignment = NPOI.SS.UserModel.HorizontalAlignment.Center;
            IFont font = workBook.CreateFont();
            font.Boldweight = short.MaxValue;
            style.SetFont(font);

            NowDay = DateTime.Now.ToString("yyyyMMdd");
            NowTime = DateTime.Now.ToString("hhmmss");

            List<string[]> contentList = new List<string[]>
            {
                new string[] { "产品编号", "零位", "扫描频率", "线性段时间利用率" ,"有效摆角","速度均匀性"},
                new string[] { (string)TBProducerNum.Dispatcher.Invoke(new GetTBLStatus(getTBLStatus),TBProducerNum)},
                new string[] { "测量时间" },
                new string[] { NowDay + NowTime }
            };
            string[] SubTitle = new string[] { "标准-从零点起始", "测量值-按零点起始", "△t", "标准-初始为25HZ", "测量", "偏差", "线性段1时间","时间利用率","线性段2时间","时间利用率","有效摆角1","有效摆角","速度1","速度2" };


            #region 数据记录表
            #region 填写表头
            //第一行
            IRow thisrow = DataworkSheet.CreateRow(0);
            //产品编号——1列
            ICell thiscell1 = thisrow.CreateCell(0);
            thiscell1.CellStyle = style;
            thiscell1.SetCellValue(contentList[0][0]);
            //零位——3列
            ICell thiscell2 = thisrow.CreateCell(1);
            thiscell2.CellStyle = style;
            thiscell2.SetCellValue(contentList[0][1]);
            //扫描频率——3列
            ICell thiscell3 = thisrow.CreateCell(4);
            thiscell3.CellStyle = style;
            thiscell3.SetCellValue(contentList[0][2]);
            //线性段时间利用率——4列
            ICell thiscell4 = thisrow.CreateCell(7);
            thiscell4.CellStyle = style;
            thiscell4.SetCellValue(contentList[0][3]);
            //有效摆角——2列
            ICell thiscell5 = thisrow.CreateCell(11);
            thiscell5.CellStyle = style;
            thiscell5.SetCellValue(contentList[0][4]);
            //速度均匀性——2列
            ICell thiscell6 = thisrow.CreateCell(13);
            thiscell6.CellStyle = style;
            thiscell6.SetCellValue(contentList[0][5]);

            SetCellRangeAddress(DataworkSheet, 0, 0, 1, 3);
            SetCellRangeAddress(DataworkSheet, 0, 0, 4, 6);
            SetCellRangeAddress(DataworkSheet, 0, 0, 7, 10);
            SetCellRangeAddress(DataworkSheet, 0, 0, 11, 12);
            SetCellRangeAddress(DataworkSheet, 0, 0, 13, 14);

            //第一列
            for (int i = 1; i < contentList.Count; i++)
            {
                IRow row = DataworkSheet.CreateRow(i);
                for (int j = 0; j < contentList[i].Length; j++)
                {
                    ICell thiscell = row.CreateCell(j);
                    thiscell.SetCellValue(contentList[i][j]);
                    thiscell.CellStyle = style;
                }
            }

            //第二行
            IRow SubTitleRow = DataworkSheet.GetRow(1);
            for (int i = 0; i < SubTitle.Length; i++)
            {
                ICell thiscell = SubTitleRow.CreateCell(i + 1);
                thiscell.SetCellValue(SubTitle[i]);
                thiscell.CellStyle = style;
            }
            #endregion

            #region 计算误差数据
            differZero = waveProcesser.CalculateDiffer(StandardZero, CalculatedZero);
            differFreq = waveProcesser.CalculateDiffer(25, CalculatedFreq);
            #endregion

            //添加零位数据
            for (int i = 0; i < differZero.Count; i++)
            {
                IRow DataRow = DataworkSheet.GetRow(i + 2);
                if (DataRow == null)
                {
                    DataRow = DataworkSheet.CreateRow(i + 2);
                }

                ICell DataStandardZero = DataRow.CreateCell(1);
                DataStandardZero.CellStyle = style;
                DataStandardZero.SetCellValue(StandardZero[i]);
                ICell DataCalculateZero = DataRow.CreateCell(2);
                DataCalculateZero.SetCellValue(CalculatedZero[i]);
                DataCalculateZero.CellStyle = style;
                ICell DataDifferZero = DataRow.CreateCell(3);
                DataDifferZero.SetCellValue(differZero[i]);
                DataDifferZero.CellStyle = style;
            }

            //添加扫描频率数据
            for (int i = 0; i < differFreq.Count; i++)
            {
                IRow DataRow = DataworkSheet.GetRow(i + 2);
                if (DataRow == null)
                {
                    DataRow = DataworkSheet.CreateRow(i + 2);
                }

                ICell DataDifferFreq = DataRow.CreateCell(4);
                DataDifferFreq.SetCellValue(25);
                DataDifferFreq.CellStyle = style;
                ICell DataStandardFreq = DataRow.CreateCell(5);
                DataStandardFreq.SetCellValue(CalculatedFreq[i]);
                DataStandardFreq.CellStyle = style;
                ICell DataCalculateFreq = DataRow.CreateCell(6);
                DataCalculateFreq.SetCellValue(differFreq[i]);
                DataCalculateFreq.CellStyle = style;
            }
            //添加时间利用率数据
            for (int i = 0; i < TimeUtilizationList.Count; i++)
            {
                IRow DataRow = DataworkSheet.GetRow(i + 2);
                if (DataRow == null)
                {
                    DataRow = DataworkSheet.CreateRow(i + 2);
                }

                ICell LinearTime1 = DataRow.CreateCell(7);
                LinearTime1.SetCellValue(TimeUtilizationList[i]);
                LinearTime1.CellStyle = style;
                ICell TimeUtilization1 = DataRow.CreateCell(8);
                TimeUtilization1.SetCellValue(TimeUtilizationList[i]);
                TimeUtilization1.CellStyle = style;
                ICell LinearTime2 = DataRow.CreateCell(9);
                LinearTime2.SetCellValue(TimeUtilizationList[i]);
                LinearTime2.CellStyle = style;
                ICell TimeUtilization2 = DataRow.CreateCell(10);
                TimeUtilization2.SetCellValue(TimeUtilizationList[i]);
                TimeUtilization2.CellStyle = style;
            }
            ////添加有效摆角数据——未完成
            //for (int i = 0; i < SpeedUniformityList.Count; i += 2)
            //{
            //    IRow DataRow = DataworkSheet.GetRow(i + 2);
            //    if (DataRow == null)
            //    {
            //        DataRow = DataworkSheet.CreateRow(i + 2);
            //    }

            //    ICell LinearTime1 = DataRow.CreateCell(11);
            //    LinearTime1.SetCellValue(25);
            //    LinearTime1.CellStyle = style;
            //    ICell TimeUtilization1 = DataRow.CreateCell(12);
            //    TimeUtilization1.SetCellValue(TimeUtilizationList[i]);
            //    TimeUtilization1.CellStyle = style;
            //}
            //添加速度均匀性数据
            int m = 2;
            for (int i = 0; i < SpeedUniformityList.Count; i += 2)
            {
                IRow DataRow = DataworkSheet.GetRow(m);
                if (DataRow == null)
                {
                    DataRow = DataworkSheet.CreateRow(m);
                    m++;
                }
                if (i<SpeedUniformityList.Count)
                {
                    ICell SpeedUniformity1 = DataRow.CreateCell(13);
                    SpeedUniformity1.SetCellValue(SpeedUniformityList[i]);
                    SpeedUniformity1.CellStyle = style;
                }
                if ((i+1)<SpeedUniformityList.Count)
                {
                    ICell SpeedUniformity2 = DataRow.CreateCell(14);
                    SpeedUniformity2.SetCellValue(SpeedUniformityList[i + 1]);
                    SpeedUniformity2.CellStyle = style;
                }
            }

            #endregion

            #region 原始波形表
            //第一行
            IRow WaveName = WaveworkSheet.CreateRow(0);
            ICell Time = WaveName.CreateCell(0);
            Time.SetCellValue("时间");
            Time.CellStyle = style;
            ICell Voltage = WaveName.CreateCell(1);
            Voltage.SetCellValue("电压");
            Voltage.CellStyle = style;

            //原始波形数据
            for (int i = 0; i < TargetWave.Length; i++)
            {
                IRow DataRow = WaveworkSheet.CreateRow(i + 1);
                ICell TimeX = DataRow.CreateCell(0);
                TimeX.SetCellValue(TargetWave.StartTime + TargetWave.TimeSpan * i);
                TimeX.CellStyle = style;
                ICell VoltageY = DataRow.CreateCell(1);
                VoltageY.SetCellValue(TargetWave[i]._value);
                VoltageY.CellStyle = style;
            }
            #endregion

            NowDay = DateTime.Now.ToString("yyyyMMdd");
            NowTime = DateTime.Now.ToString("HHmmss");
            Status.Dispatcher.Invoke(new UpdateEventHandler(StutusUpdate), FilePath);
            string ExcelPath = System.IO.Path.Combine(FilePath, NowDay);
            if (!Directory.Exists(ExcelPath))
            {
                Directory.CreateDirectory(ExcelPath);
            }
            ExcelPath = System.IO.Path.Combine(ExcelPath, NowTime + ".xls");
            FileStream Fs = File.OpenWrite(ExcelPath);
            workBook.Write(Fs);
            Fs.Close();
        }

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
                    TargetWave = ReadExcelWave(workBook);
                    TargetWave.Type = "Origin";
                    DerivatedWave = waveProcesser.Derivative(TargetWave, out Waveform Zero);

                    LinearWave = new Waveform();

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
                        DataPropertyList[i] = ReadExcelData(workBook, (DataProperty)DataPropertyArray.GetValue(i));
                        TextBoxList[i].Dispatcher.Invoke(new UpdateTBLEventHandler(TBLUpdateStatus), new object[] { TextBoxList[i], DataPropertyList[i].ToString("N4") });
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

        #region 读取原始波形
        /// <summary>
        /// 从Excel中读取原始波形
        /// </summary>
        /// <param name="workBook"></param>
        /// <returns></returns>
        public Waveform ReadExcelWave(IWorkbook workBook)
        {
            Waveform ExcelWave = new Waveform();
            ISheet WaveSheet = workBook.GetSheet("原始波形");
            IRow row;
            for (int i = 1; i < WaveSheet.LastRowNum; i++)
            {
                row = WaveSheet.GetRow(i);
                if (row != null)
                {
                    for (int j = 1; j < row.LastCellNum; j++)
                    {
                        if (double.TryParse(row.GetCell(j).ToString(), out double result))
                        {
                            ExcelWave.Add(result);
                        }
                    }
                }
            }
            double X1, X2;
            if (double.TryParse(WaveSheet.GetRow(1).GetCell(0).ToString(), out double result1))
            {
                X1 = result1;
                if (double.TryParse(WaveSheet.GetRow(2).GetCell(0).ToString(), out double result2))
                {
                    X2 = result2;
                    ExcelWave.TimeSpan = X2 - X1;
                    return ExcelWave;
                }
            }
            return null;
        }
        #endregion

        #region 读取波形属性
        /// <summary>
        /// 读取波形属性
        /// </summary>
        /// <param name="workBook"></param>
        /// <param name="dataProperty"></param>
        /// <returns></returns>
        public double ReadExcelData(IWorkbook workBook, DataProperty dataProperty)
        {
            int DataColumn;
            bool isSpeedUniformity = false;
            switch (dataProperty)
            {
                case DataProperty.ZeroProperty:
                    //读取零位 按零起始 第三列
                    DataColumn = 3;
                    break;
                case DataProperty.SacnFrequency:
                    //读取扫描频率 按零起始 第六列
                    DataColumn = 5;
                    break;
                case DataProperty.LinearTimeAvaliability:
                    //读取时间利用率 按零起始 第八列
                    DataColumn = 8;
                    break;
                case DataProperty.EffectiveAngle:
                    //读取有效摆角 按零起始 第十二列
                    DataColumn = 12;
                    break;
                case DataProperty.SpeedUniformity:
                    //读取速度均匀性 按零起始 第十五列
                    DataColumn = 13;
                    isSpeedUniformity = true;
                    break;
                default:
                    return 0;
            }
            double ReadDataProperty = 0;
            double ReadDataPropertyNum = 0;
            ISheet DataSheet = workBook.GetSheet("数据记录");
            IRow row;
            for (int i = 2; i < DataSheet.LastRowNum; i++)
            {
                row = DataSheet.GetRow(i);
                if (row != null)
                {
                    if (row.GetCell(DataColumn)!=null)
                    {
                        if (double.TryParse(row.GetCell(DataColumn).ToString(), out double result))
                        {
                            ReadDataProperty += result;
                            ReadDataPropertyNum++;
                        }
                    }
                    if (isSpeedUniformity)
                    {
                        if (row.GetCell(DataColumn) != null)
                        {
                            if (double.TryParse(row.GetCell(DataColumn+1).ToString(), out double result))
                            {
                                ReadDataProperty += result;
                                ReadDataPropertyNum++;
                            }
                        }
                    }
                }
            }
            return ReadDataProperty / ReadDataPropertyNum;
        }
        #endregion
        #endregion

        #region 字段
        #region 波形相关参数
        //产品编号
        string ProducerName = "";
        List<TextBox> TextBoxList = new List<TextBox>();
        List<double> DataPropertyList = new List<double>();
        double ZeroProperty = 0;
        double SacnFrequency = 0;
        double TimeUtilization = 0;
        double EffectiveAngle = 0;
        double SpeedUniformity = 0;
        List<double> ZeroPropertyList;
        List<double> TimeUtilizationList;
        List<double> SpeedUniformityList;
        List<double> EffectiveAngleList;
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

        //线性度数组——用于线性度计算
        List<int> LinearArray = new List<int>();
        Waveform LinearWave = new Waveform();

        //目标波形——角度
        Waveform TargetWave = new Waveform(1d / 10000000d);
        //目标波形求导后波形——角速度
        Waveform DerivatedWave;

        /// <summary>
        /// 用于检测是否是第一次采集
        /// </summary>
        bool firsttime = false;

        #endregion

        #region 采集卡相关设置
        string m_stroutput = "";
        int m_nTrigLen = 1024;
        string m_strDI = "";
        string m_strDO = "";
        long m_nhDI = 0;
        long m_nTrigDelay = 0;
        long m_nClkDiv = 0;
        double m_fLevel = 2.0;
        int m_nScnt = 0;
        long m_bADcnt = Constants.FALSE;
        long m_lADoffset0 = 0;
        long m_lADoffset1 = 0;
        long m_lADoffset2 = 0;
        long m_lADoffset3 = 0;
        long m_lADoffset4 = 0;
        long m_lADoffset5 = 0;
        long m_lADoffset6 = 0;
        long m_lADoffset7 = 0;

        int MAXVALUE;   //AD最大点数
        int bUpdate;    //刷新界面
        long display_ch;    //现实通道
        long m_lChcnt = 2;      //使能的通道数

        int bSoftTrig;      //软件触发源

        //读取线程和现实县城公共缓冲区，设立多个快缓冲
        int[] bNewSegmentData = new int[Constants.MAX_SEGMANT];     //用于确定当前段数数据是否为最新数据
        int CurrentIndex;   //数据处理线程当前缓冲区索引号
        int ReadIndex;      //数据采集线程当前缓冲区索引号
        ushort[] dataBuff = new ushort[Constants.MAX_SEGMANT];      //采集信息缓冲，采用Block环形缓冲方式
        uint timer1;

        int bSave;      //保存数据方式

        int dis_cnt;

        int bufcnt;

        ulong samcnt;   //读取的采样点数
        ulong trig_cnt = 1;     //读取触发次数
        int t_n = 1;    //timer时间
        double old_num, new_num;    //测试速度用计数
        long MAX_FIFIO = 0X100000000;   //128M样点
        bool bTrug = false;     //触发模式标志
        long bFifoOver = Constants.FALSE;     //FIFO溢出

        ushort[] buf = null;    //分配内存

        IntPtr hdl = Constants.INVALID_HANDLE_VALUE;

        _PCI2168_PARA_INIT para_init;
        #endregion

        #region 采集相关设置
        //时间间隔-ms
        int CollectTimeSpan = 10;
        //单次时间-ms
        int SingleTime = 10;
        //采集次数
        int SamplingTimes = 1;
        #endregion

        #endregion

        /// <summary>
        /// 初始化设备
        /// </summary>
        public void InitDevice()
        {
            Int32 cardindex = 0;

            hdl = DllImport.PCI2168_Link(cardindex); //如果只有一个卡，是0；如果有两个卡是，0对应第一个卡，1对应第二个卡，以此类推。

            if (hdl == Constants.INVALID_HANDLE_VALUE)
            {
                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "采集卡打开失败！" });
                return;
            }
            else
            {
                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "采集卡打开成功！" });
            }

            //DI DO 根据给的例程软件设置的，暂时不知道有什么作用
            m_strDI = "00";
            m_strDO = "aa";

            for (int i = 0; i < Constants.MAX_SEGMANT; i++)
            {
                dataBuff[i] = 0;
            }

            m_nScnt = 10;//10ms

            //读取设备信息
            long devADbit = 16;
            long devfifo = 1;
            DllImport.PCI2168_GetDevInfo(hdl, ref devfifo, ref devADbit);
            MAXVALUE = (int)(Math.Pow(2.0, devADbit) - 1);
            Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "PCI2168(" + devADbit + "bit AD)采集卡打开成功，FIFO" + devfifo + "M采样点。" });
            MAX_FIFIO = devfifo * 0x100000;

            //设置缓存大小
            buf = new ushort[4096 * 1024];

            para_init.lChCnt = m_lChcnt;   //1-CH1使能 2-CH1和CH2使能
            para_init.TriggerDelay = m_nTrigDelay;      //仅延时触发有效
            para_init.TriggerMode = (long)EmTriggerMode.TRIG_MODE_CONTINUE;   //触发源选择 —— 连续触发
            para_init.TriggerSource = (long)EmTriggerSourse.TRIG_SRC_EXT_FALLING;   //触发模式 —— 下降沿触发
            para_init.TriggerLength = m_nTrigLen;   //触发长度 —— 1024
            //触发电平 仅触发源是模拟触发有效
            para_init.TriggerLevel = (long)(m_nTrigLen * 4096.0 / 5.0);
            para_init.lSelDataSrc = 0;              //0 - AD数据源    1 - 计数器数据源
            para_init.lADFmt = (long)EmADFormat.ADFMT_STBIN;    //默认直接二进制输出

            //初始化采样钟
            if (!DllImport.PCI2168_initADCLK(hdl, 0, 10))
            {
                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "初始化采样钟失败！" });
                return;
            }
            Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "初始化采样钟成功！" });

            if (!DllImport.PCI2168_initAD(hdl, ref para_init))
            {
                Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "初始化设备失败！" });
                return;
            }
            Status.Dispatcher.BeginInvoke(new UpdateEventHandler(StutusUpdate), new object[] { "初始化设备成功！" });
        }

        #region UI事件

        string NowDay;
        string NowTime;
        /// <summary>
        /// 波形采集启动事件
        /// </summary>
        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (ThreadCollectWave.ThreadState== ThreadState.Unstarted||ThreadCollectWave.ThreadState==ThreadState.Stopped||ThreadCollectWave.ThreadState==ThreadState.Aborted)
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
                    m_nScnt = num;
                }
            }
            catch (Exception ex)
            {
                TBSingleTime.Text = m_nScnt.ToString();
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
                Status.Dispatcher.Invoke(new UpdateEventHandler(StutusUpdate), e.ToString());
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
        public void OutputFile(Waveform OutputArray,string Name)
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
