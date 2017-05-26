using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sale_Order_Semi.Models
{
    //流程时间监控的Model
    public class ProcTimeModel
    {
        public int applyId { get; set; }
        public string orderType { get; set; }
        public string ProduceDep { get; set; }
        public string sysNo { get; set; }
        public string agency { get; set; }
        public string applier { get; set; }
        public string applyDate { get; set; }
        public string applyTime { get; set; }
        public List<AuditTimeModel> auditList { get; set; }
    }

    public class AuditTimeModel {
        public int step { get; set; }
        public string stepName { get; set; }
        public string auditor { get; set; }
        public string auditTime { get; set; }
        public string timeCost { get; set; }
        public bool? pass { get; set; }
        public bool? blocking { get; set; }
        public bool timeCostTooLong { get; set; }
    }
}