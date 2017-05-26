using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sale_Order_Semi.Models
{
    public class SimpleProductModel
    {
        public string fmodel { get; set; }
        public string fname { get; set; }
    }

    public class BomProductModel
    {        
        public string levels { get; set; } //层级
        public string fnumber { get; set; }
        public string fmodel { get; set; }
        public string fname { get; set; }
        public decimal fqty { get; set; } //单位用量
        public string unitname { get; set; } //单位
        public string perName { get; set; } //外购或自制
        public string code_s { get; set; } //主料或替料    
        public decimal total_qty { get; set; }
        public string source
        {
            get { return "BOM"; }
            set { value = "BOM"; }
        }
    }

}