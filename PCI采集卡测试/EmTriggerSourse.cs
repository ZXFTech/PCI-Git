using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCI
{
    enum EmTriggerSourse:long
    {
        TRIG_SRC_EXT_RISING = 0L,    //外正沿触发
        TRIG_SRC_EXT_FALLING = 1L,   //外负沿触发 
        TRIG_SRC_SOFT = 2L           //软件触发
    }
}
