using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sale_Order_Semi.Interfaces
{
    interface IFinishEmail
    {
        string ccToOthers(string sysNo, bool isPass);
        bool needReport(bool isPass);
    }
}
