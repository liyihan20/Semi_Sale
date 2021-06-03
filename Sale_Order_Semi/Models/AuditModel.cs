using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sale_Order_Semi.Models
{
    //审核人查看自己待审核或审核过的单据列表
    public class AuditListModel
    {
        public int step { get; set; }
        public string stepName { get; set; }
        public int applyId { get; set; }
        public int applyDetailId { get; set; }
        public string sysNum { get; set; }
        public string depName { get; set; }
        public string salerName { get; set; }
        public string previousStepTime { get; set; }
        public string status { get; set; }
        public string orderNo { get; set; }
        public string hasImportK3 { get; set; }
        public string finalStatus { get; set; }
        public string encryptNo { get; set; }
        public string orderType { get; set; }
        public string model { get; set; }
        public bool flag { get; set; } //2021-03-06 临时筛选用
    }

    //审核人查看自己待审核或审核过的变更单据列表
    public class AuditUpdateListModel
    {
        public int step { get; set; }
        public string bill_no { get; set; }
        public string bill_type { get; set; }
        public int update_id { get; set; }
        public string depName { get; set; }
        public string salerName { get; set; }
        public string previousStepTime { get; set; }
        public string status { get; set; }

       
    }

    //查看审核状态
    public class AuditStatusModel {

        public int step { get; set; }
        public string stepName { get; set; }
        public string date { get; set; }
        public string time { get; set; }
        public string auditor { get; set; }
        public string department { get; set; }
        public string comment { get; set; }
        public bool? pass { get; set; }
    }

    //后台查看订单model
    public class backBills { 
        public int applyId { get; set; }
        public string apply_date { get; set; }
        public string orderType { get; set; }
        public string sysNum { get; set; }
        public string depName { get; set; }
        public string salerName { get; set; }
        public string status { get; set; }
        public string encryptNo { get; set; }
        public string model { get; set; }
    }

    public class ResultModel
    {
        public string value { get; set; }
        public string text { get; set; }
    }

    public class SResultModel
    {
        public bool suc { get; set; }
        public string msg { get; set; }
    }
    public class CeoAuditListModel
    {
        public int? step { get; set; }
        public string stepName { get; set; }
        public int applyId { get; set; }
        public int applyDetailId { get; set; }
        public string sysNum { get; set; }
        public string depName { get; set; }
        public string salerName { get; set; }
        public DateTime? applyTime { get; set; }
        public string applyTimeStr { get; set; }
        public string orderType { get; set; }
        public string model { get; set; }
    }

    public class AuditResultModel
    {
        public bool canAudit { get; set; }
        public string auditResult { get; set; }
        public string comment { get; set; }
    }

    public class AuditInfoModel
    {
        public string editType { get; set; }
        public string sysNo { get; set; }
        public string stepName { get; set; }
    }
}