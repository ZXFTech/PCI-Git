using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PCI
{
    public class TestClass
    {
        #region 侧使用方法
        /// <summary>
        /// 测试用方法——将波形以txt文件输出默认D盘根目录
        /// </summary>
        /// <param name="OutputArray"></param>
        /// <param name="Name"></param>

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
