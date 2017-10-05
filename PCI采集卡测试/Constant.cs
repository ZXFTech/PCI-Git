using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCI
{
    class Constants
    {
        public const long FALSE = 0;
        public const long TRUE = 1;
        public const long NULL = 0;

        public static IntPtr INVALID_HANDLE_VALUE = (IntPtr)(-1);

        public const int MAX_SEGMANT = 3;   //缓冲区数目

        public const long READ_MAX_LENGTH = 4194304;    //最大读取长度 4M
    }
}
