using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sale_Order_Semi.Services;

namespace Sale_Order_Semi.Controllers
{
    public class TestController : Controller
    {
        public bool t1(string name, string no)
        {
            return new K3ItemSv().IsCustomerNameAndNoMath(name, no);
        }

    }
}
