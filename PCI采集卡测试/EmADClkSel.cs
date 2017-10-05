using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCI
{
    public enum EmADClkSel : long
    {
        ADCLK_INT = 0L,    //内钟
        ADCLK_EXT = 1L     //外钟
    }
}
