using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCI
{
    enum EmTriggerMode:long
    {
        TRIG_MODE_CONTINUE = 0L, //连续采集
        TRIG_MODE_POST = 1L,     //后触发 
        TRIG_MODE_DELAY = 2L,    //延时触发
        TRIG_MODE_PRE = 3L,      //前触发，不支持 
        TRIG_MODE_MIDDLE = 4L    //中触发，不支持
    }
}
