using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using static PCI.MainWindow;

namespace PCI
{
    public class Oscillocope
    {
        //public Oscillocope(Label Status)
        //{
        //    this.Status = Status;
        //}

        public Label Status;

        #region 采集卡相关设置
        public string m_stroutput = "";
        public int m_nTrigLen = 1024;
        public string m_strDI = "";
        public string m_strDO = "";
        public long m_nhDI = 0;
        public long m_nTrigDelay = 0;
        public long m_nClkDiv = 0;
        public double m_fLevel = 2.0;
        public int m_nScnt = 0;
        public long m_bADcnt = Constants.FALSE;
        public long m_lADoffset0 = 0;
        public long m_lADoffset1 = 0;
        public long m_lADoffset2 = 0;
        public long m_lADoffset3 = 0;
        public long m_lADoffset4 = 0;
        public long m_lADoffset5 = 0;
        public long m_lADoffset6 = 0;
        public long m_lADoffset7 = 0;

        public int MAXVALUE;   //AD最大点数
        public int bUpdate;    //刷新界面
        public long display_ch;    //现实通道
        public long m_lChcnt = 2;      //使能的通道数

        public int bSoftTrig;      //软件触发源

        //读取线程和现实县城公共缓冲区，设立多个快缓冲
        public int[] bNewSegmentData = new int[Constants.MAX_SEGMANT];     //用于确定当前段数数据是否为最新数据
        public int CurrentIndex;   //数据处理线程当前缓冲区索引号
        public int ReadIndex;      //数据采集线程当前缓冲区索引号
        public ushort[] dataBuff = new ushort[Constants.MAX_SEGMANT];      //采集信息缓冲，采用Block环形缓冲方式
        public uint timer1;

        public int bSave;      //保存数据方式

        public int dis_cnt;

        public int bufcnt;

        public ulong samcnt;   //读取的采样点数
        public ulong trig_cnt = 1;     //读取触发次数
        public int t_n = 1;    //timer时间
        public double old_num, new_num;    //测试速度用计数
        public long MAX_FIFIO = 0X100000000;   //128M样点
        public bool bTrug = false;     //触发模式标志
        public long bFifoOver = Constants.FALSE;     //FIFO溢出

        public ushort[] buf = null;    //分配内存

        public IntPtr hdl = Constants.INVALID_HANDLE_VALUE;

        public _PCI2168_PARA_INIT para_init;
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

        public void InitClock()
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
        }

        //status更新方法
        void StutusUpdate(string status)
        {
            Status.Content = status;
        }
    }
}
