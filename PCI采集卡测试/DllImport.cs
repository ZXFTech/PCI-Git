using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace PCI
{
    class DllImport
    {
        /// <summary>
        /// 连接设备
        /// </summary>
        /// <param name="devNum"></param>
        /// <returns></returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_Link", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr PCI2168_Link(int devNum);

        /// <summary>
        /// 断开设备
        /// </summary>
        /// <param name="hdl"></param>
        /// <returns></returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_UnLink", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PCI2168_UnLink(IntPtr hdl);

        /// <summary>
        /// 初始化采样始终函数
        /// </summary>
        /// <param name="HdEVIEC"> 设备句柄 </param>
        /// <param name="lSelADClk"> 采样基准时钟选择 0-选择板上时钟，1-选择外时钟</param>
        /// <param name="ClkDeci"> 分频因子，取值范围1,2,3,4,...，10 </param>
        /// <returns></returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_initADCLK", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PCI2168_initADCLK(IntPtr HdEVIEC, long lSelADClk, long ClkDeci);

        /// <summary>
        /// 初始化AD 启动数据采集调用本函数后，必须用PCI2168_StopAD来结束采集
        /// </summary>
        /// <param name="hdl"> 设备句柄 </param>
        /// <param name="para_init">初始化结构</param>
        /// <returns></returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_initAD", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PCI2168_initAD(IntPtr hdl, ref _PCI2168_PARA_INIT para_init);

        /// <summary>
        /// 读取海量FIFO中采样点个数
        /// </summary>
        /// <param name="hdl"> 设备句柄 </param>
        /// <returns> 当前缓冲区采集点个数，该函数只能返回二级缓存的个数 </returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_GetBufCnt", CallingConvention = CallingConvention.Cdecl)]
        public static extern int PCI2168_GetBufCnt(IntPtr hdl);

        /// <summary>
        /// 读取采集数据
        /// </summary>
        /// <param name="hdl"> 设备句柄 </param>
        /// <param name="PUSHORT"> 接收数据指针 </param>
        /// <param name="nCount"> 读取的采集点数 最大是READ_MAX_LEN(4M)</param>
        /// <param name="bBufOver"></param>
        /// <returns></returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_ReadAD", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PCI2168_ReadAD(IntPtr hdl, ushort[] PUSHORT, ulong nCount, ref ulong bBufOver);

        /// <summary>
        /// 停止AD采集
        /// </summary>
        /// <param name="hdl"></param>
        /// <returns></returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_StopAD", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PCI2168_StopAD(IntPtr hdl);

        /// <summary>
        /// 触发采集中，如果选择触发源为软件触发，则执行该函数，则执行一次触发采集
        /// </summary>
        /// <param name="hdl"> 设备句柄 </param>
        /// <returns></returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_ExeSoftTrug", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PCI2168_ExeSoftTrug(IntPtr hdl);

        /// <summary>
        /// 读取设备信息 包括硬件FIFO大小，AD位数。
        /// </summary>
        /// <param name="hdl"> 设备句柄 </param>
        /// <param name="devFifoSize"> 硬件FIFO大小，单位M采样点</param>
        /// <param name="devADbit"> AD位数 </param>
        /// <returns></returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_GetDevInfo", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PCI2168_GetDevInfo(IntPtr hdl,ref long devFifoSize,ref long devADbit);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="hDevice"> 设备句柄 </param>
        /// <param name="bWtRd"> 读写数据选择 </param>
        /// <param name="lSelReg"> 选择通道，0~7对应AD通道CH1~CH8 </param>
        /// <param name="plADoffet"> 零偏矫正数据，取值范围-500~500 </param>
        /// <returns></returns>
        [DllImport("PCI2168.dll", EntryPoint = "PCI2168_ADoffset", CallingConvention = CallingConvention.Cdecl)]
        public static extern bool PCI2168_ADoffset(IntPtr hDevice, bool bWtRd, long lSelReg, long plADoffet);

    }
}
