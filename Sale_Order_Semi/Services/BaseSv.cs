using Sale_Order_Semi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sale_Order_Semi.Services
{
    public class BaseSv
    {
        public SaleDBDataContext db;

        public BaseSv()
        {
            db = new SaleDBDataContext();
        }
    }
}