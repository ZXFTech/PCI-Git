using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCI
{
    enum EmADFormat:long
    {
        ADFMT_STBIN = 0L, //直接二进制输出
        ADFMT_2SBIN = 1L   //二进制补码输出
    }
}
