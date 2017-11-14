using Microsoft.Win32;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.SS.Util;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using static PCI.MainWindow;

namespace PCI
{
    public class ExcelReadNWrite
    {
        public System.Windows.Controls.Label Status;

        WaveProcesser waveProcesser = new WaveProcesser();
        //表格保存
        public delegate void SaveInExcel();

        //Excel表格保存方法
        public void SaveExcel(string ProductNum,Waveform TargetWave,string FilePath, List<double> StandardZeroList, List<double> ZeroPropertyList, List<double> ScanFrequencyList, List<double> EffectiveAngleList, List<double> TimeUltilizationList, List<double> TimeList, List<double> SpeedUniformityList)
        {
            //List<double> CalculatedZero = new List<double> { 1.1, 2.99, 5.2, 6.8, 9.1, 2.2, 3.9, 6.1, 7.89, 10, 666.5 };
            List<double> differZero = new List<double>();

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

            string NowDay = DateTime.Now.ToString("yyyyMMdd");
            string NowTime = DateTime.Now.ToString("hhmmss");

            List<string[]> contentList = new List<string[]>
            {
                new string[] { "产品编号", "零位", "扫描频率", "线性段时间利用率" ,"有效摆角","速度均匀性"},
                new string[] { ProductNum },
                new string[] { "测量时间" },
                new string[] { NowDay + NowTime }
            };
            string[] SubTitle = new string[] { "标准-从零点起始", "测量值-按零点起始", "△t", "标准-初始为25HZ", "测量", "偏差", "线性段1时间", "时间利用率", "线性段2时间", "时间利用率", "有效摆角1", "有效摆角", "速度1", "速度2" };


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
            differZero = waveProcesser.CalculateDiffer(StandardZeroList, ZeroPropertyList);
            differFreq = waveProcesser.CalculateDiffer(25, ScanFrequencyList);
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
                DataStandardZero.SetCellValue(StandardZeroList[i]);
                ICell DataCalculateZero = DataRow.CreateCell(2);
                DataCalculateZero.SetCellValue(ZeroPropertyList[i]);
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
                DataStandardFreq.SetCellValue(ScanFrequencyList[i]);
                DataStandardFreq.CellStyle = style;
                ICell DataCalculateFreq = DataRow.CreateCell(6);
                DataCalculateFreq.SetCellValue(differFreq[i]);
                DataCalculateFreq.CellStyle = style;
            }

            //添加时间利用率数据
            int m = 0;
            for (int i = 1; i < TimeUltilizationList.Count; i += 2)
            {
                IRow DataRow = DataworkSheet.GetRow(m + 2);
                if (DataRow == null)
                {
                    DataRow = DataworkSheet.CreateRow(m + 2);
                }

                ICell LinearTime1 = DataRow.CreateCell(7);
                LinearTime1.SetCellValue(TimeList[i - 1]);
                LinearTime1.CellStyle = style;
                ICell TimeUtilization1 = DataRow.CreateCell(8);
                TimeUtilization1.SetCellValue(TimeUltilizationList[i - 1]);
                TimeUtilization1.CellStyle = style;
                ICell LinearTime2 = DataRow.CreateCell(9);
                LinearTime2.SetCellValue(TimeList[i]);
                LinearTime2.CellStyle = style;
                ICell TimeUtilization2 = DataRow.CreateCell(10);
                TimeUtilization2.SetCellValue(TimeUltilizationList[i]);
                TimeUtilization2.CellStyle = style;

                m++;
            }

            //添加有效摆角数据
            m = 0;
            for (int i = 1; i < EffectiveAngleList.Count; i += 2)
            {
                IRow DataRow = DataworkSheet.GetRow(m + 2);
                if (DataRow == null)
                {
                    DataRow = DataworkSheet.CreateRow(m + 2);
                }

                ICell LinearTime1 = DataRow.CreateCell(11);
                LinearTime1.SetCellValue(EffectiveAngleList[i - 1]);
                LinearTime1.CellStyle = style;
                ICell TimeUtilization1 = DataRow.CreateCell(12);
                TimeUtilization1.SetCellValue(EffectiveAngleList[i]);
                TimeUtilization1.CellStyle = style;
                m++;
            }

            //添加速度均匀性数据
            m = 0;
            for (int i = 1; i < SpeedUniformityList.Count; i += 2)
            {
                IRow DataRow = DataworkSheet.GetRow(m + 2);
                if (DataRow == null)
                {
                    DataRow = DataworkSheet.CreateRow(m + 2);
                }
                ICell SpeedUniformity1 = DataRow.CreateCell(13);
                SpeedUniformity1.SetCellValue(SpeedUniformityList[i - 1]);
                SpeedUniformity1.CellStyle = style;
                ICell SpeedUniformity2 = DataRow.CreateCell(14);
                SpeedUniformity2.SetCellValue(SpeedUniformityList[i]);
                SpeedUniformity2.CellStyle = style;
                m++;
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

        //单元格合并方法
        public static void SetCellRangeAddress(ISheet sheet, int rowstart, int rowend, int colstart, int colend)
        {
            CellRangeAddress cellRangeAddress = new CellRangeAddress(rowstart, rowend, colstart, colend);
            sheet.AddMergedRegion(cellRangeAddress);
        }

        //status更新方法
        void StutusUpdate(string status)
        {
            Status.Content = status;
        }

        public void ReadExcel()
        {
            string ExcelFilePath;
            Microsoft.Win32.OpenFileDialog filedialog = new Microsoft.Win32.OpenFileDialog();
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
                    if (row.GetCell(DataColumn) != null)
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
                            if (double.TryParse(row.GetCell(DataColumn + 1).ToString(), out double result))
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
    }
}
