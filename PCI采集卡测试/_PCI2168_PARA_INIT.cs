using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCI
{
    public struct _PCI2168_PARA_INIT
    {
        public long lChCnt;        //通道数1-1通道 2-2通道 4-4通道 8-8通道
        public long TriggerMode;   //触发模式
        public long TriggerSource; //触发源
        public long TriggerDelay;  //触发延时
        public long TriggerLength;  //触发长度
        public long TriggerLevel;  //模拟触发电平
        public long lSelDataSrc;   //数据源选择，0-AD数据源 1-计数器数据源
        public long lADFmt;        //AD数据输出格式，0表示直接二进制数出 1表示补码输出
    }
//      lChCnt － 通道数，1-使能 CH1，2－ 使能 CH1 CH2, 4－使能 CH1 CH2 CH3 CH4，
//      8－使能 CH1 CH2 CH3 CH4 CH5 CH6 CH7 CH8。
//      TriggerMode－触发模式，包括连续采集、后触发。
//      TriggerSource－触发源，包括外正沿触发，外负沿触发，软件触发。
//      TriggerDelay－延时触发延时参数 n，表示在触发事情发生后，经过 n 个采样钟后采集。
//      TriggerLength－触发长度，以 Sp 为单位，1Sp 即 32 个采集点。
//      TriggerLevel－触发电平参数 n，取值 0～4095，和电压对应关系见表 3.1。
//      lSelDataSrc－数据源选择，0-AD 数据源 1-计数器数据源
//      lADFmt－AD 数据输出格式，0 表示直接二进制输出 1 表示补码输出
}
