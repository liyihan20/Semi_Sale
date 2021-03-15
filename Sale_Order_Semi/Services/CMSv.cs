using CrystalDecisions.CrystalReports.Engine;
using Newtonsoft.Json;
using org.in2bits.MyXls;
using Sale_Order_Semi.Models;
using Sale_Order_Semi.Models.CMDTTableAdapters;
using Sale_Order_Semi.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sale_Order_Semi.Interfaces;

namespace Sale_Order_Semi.Services
{
    public class CMSv : BillSv,IFinishEmail
    {
        private ModelContract bill;

        public CMSv() { }
        public CMSv(string sysNo)
        {
            bill = db.ModelContract.Where(m => m.sys_no == sysNo).FirstOrDefault();
        }

        public override string BillType
        {
            get { return "CM"; }
        }

        public override string BillTypeName
        {
            get { return "开模改模单"; }
        }

        public override string CreateViewName
        {
            get { return "CreateNCM"; }
        }

        public override string CheckViewName
        {
            get { return "CheckNCM"; }
        }

        public override string CheckListViewName
        {
            get { return "CheckNBillList"; }
        }

        public override object GetNewBill(UserInfo currentUser)
        {
            bill = new ModelContract();
            bill.step_version = 0;
            bill.User = db.User.Where(u => u.id == currentUser.userId).First();
            bill.sys_no = GetNextSysNo(BillType);
            bill.bill_date = DateTime.Now;

            var dep = new K3ItemSv().GetK3Items("agency").Where(k => k.fname == currentUser.departmentName).FirstOrDefault();
            if (dep != null) {
                bill.agency_name = dep.fname;
                bill.agency_no = dep.fid;
            }

            return new CMModel() { mc = bill };
        }

        public override object GetBill(int stepVersion, int userId)
        {
            bill.step_version = stepVersion;
            return new CMModel() { mc = bill, extra = bill.ModelContractExtra.FirstOrDefault() };
        }

        public override string SaveBill(System.Web.Mvc.FormCollection fc, UserInfo user)
        {             
            ModelContract mc = JsonConvert.DeserializeObject<ModelContract>(fc.Get("mc"));
            ModelContractExtra extra = JsonConvert.DeserializeObject<ModelContractExtra>(fc.Get("ex"));

            //如已提交，则不能再保存
            if (mc.step_version == 0) {
                if (new ApplySv().ApplyHasBegan(mc.sys_no)) {
                    throw new Exception("已提交的单据不能再次保存！");
                }
            }

            //验证客户编码与客户名称是否匹配
            if (!new K3ItemSv().IsCustomerNameAndNoMath(mc.customer_name, mc.customer_no)) {
                throw new Exception("购货单位请输入后按回车键搜索后在列表中选择");
            }
            if (new K3ItemSv().GetK3Customer(mc.plan_firm_name).Count()<1) {
                throw new Exception("方案公司请输入后按回车键搜索后在列表中选择");
            }
            if (!new K3ItemSv().IsCustomerNameAndNoMath(mc.oversea_customer_name, mc.oversea_customer_no)) {
                throw new Exception("海外客户请输入后按回车键搜索后在列表中选择");
            }
            if (new K3ItemSv().GetK3Customer(mc.zz_customer_name).Count()<1) {
                throw new Exception("最终客户请输入后按回车键搜索后在列表中选择");
            }

            if (string.IsNullOrEmpty(mc.clerk_no)) {
                throw new Exception("业务员请输入后按回车键搜索后在列表中选择");
            }

            if (mc.step_version == 0) {
                mc.user_id = user.userId;
            }

            if (!string.IsNullOrEmpty(extra.quality_demand) || !string.IsNullOrEmpty(extra.LCD_type) || !string.IsNullOrEmpty(extra.product_level)) {
                extra.ModelContract = mc;
                db.ModelContractExtra.InsertOnSubmit(extra);
            }

            ModelContract existedBill = db.ModelContract.Where(s => s.sys_no == mc.sys_no).FirstOrDefault();
            if (existedBill != null) {
                mc.user_id = existedBill.user_id;

                //备份
                BackupData bd = new BackupData();
                bd.sys_no = mc.sys_no;
                bd.main_data = new SomeUtils().ModelToString(existedBill);
                bd.op_date = DateTime.Now;
                bd.user_id = user.userId;
                db.BackupData.InsertOnSubmit(bd);

                //删除旧数据
                db.ModelContract.DeleteOnSubmit(existedBill);
                db.ModelContractExtra.DeleteAllOnSubmit(existedBill.ModelContractExtra);
            }

            var apply = db.Apply.Where(a => a.sys_no == mc.sys_no).FirstOrDefault();
            if (apply != null) {
                apply.p_model = mc.product_model;
            }

            db.ModelContract.InsertOnSubmit(mc);
            try {
                db.SubmitChanges();
            }
            catch (Exception ex) {
                throw new Exception("保存失败：" + ex.Message);
            }

            return "";
        }

        public override List<object> GetBillList(SalerSearchParamModel pm, int userId)
        {
            pm.toDate = pm.toDate.AddDays(1);
            pm.sysNo = pm.sysNo ?? "";

            var result = (from o in db.ModelContract
                          join a in db.Apply on o.sys_no equals a.sys_no into X
                          from Y in X.DefaultIfEmpty()
                          where o.user_id == userId
                          && (o.sys_no.Contains(pm.sysNo) || o.product_model.Contains(pm.sysNo))
                          && o.bill_date >= pm.fromDate
                          && o.bill_date <= pm.toDate
                          && (pm.auditResult == 10 || (pm.auditResult == 0 && (Y == null || (Y != null && Y.success == null))) || (pm.auditResult == 1 && Y != null && Y.success == true) || pm.auditResult == -1 && Y != null && Y.success == false)
                          select new OrderModel()
                          {
                              bill_id = o.id,
                              apply_status = (Y == null ? "未开始申请" : Y.success == true ? "申请成功" : Y.success == false ? "申请失败" : "审批当中"),
                              buy_unit = o.customer_name,
                              deal_price = o.price ?? o.cost,
                              product_model = o.product_model,
                              product_name = o.product_name,
                              qty = o.qty,
                              sys_no = o.sys_no,
                              apply_date = (Y == null ? "" : Y.start_date.ToString())
                          }).ToList();

            foreach (var re in result.Where(r => !string.IsNullOrEmpty(r.apply_date))) {
                re.apply_date = DateTime.Parse(re.apply_date).ToString("yyyy-MM-dd HH:mm");
            }

            return result.ToList<Object>();
        }

        public override object GetNewBillFromOld()
        {
            bill.step_version = 0;
            bill.sys_no = GetNextSysNo(BillType);
            bill.product_model = null;
            bill.bill_date = DateTime.Now;

            return bill;
        }

        public override string GetProcessNo()
        {
            if (bill.bus_dep.Contains("OLED")) return "CM_OLED";
            return "CM";
        }

        public override Dictionary<string, int?> GetProcessDic()
        {
            Dictionary<string, int?> auditorsDic = new Dictionary<string, int?>();
            auditorsDic.Add("部门NO", db.User.Single(u => u.id == bill.user_id).Department1.dep_no);
            auditorsDic.Add("研发项目组NO", db.Department.Single(d => d.name == bill.project_team && d.dep_type == "研发项目组").dep_no);
            if (bill.quotation_clerk_id != null) {
                auditorsDic.Add("表单报价员值NO", bill.quotation_clerk_id);
            }
            auditorsDic.Add("开模事业部NO", db.Department.Single(d => d.name == bill.bus_dep && d.dep_type == "开模事业部").dep_no);

            return auditorsDic;
        }

        public override string GetProductModel()
        {
            return bill.product_model;
        }

        public override string GetCustomerName()
        {
            return bill.customer_name;
        }

        public override bool HasOrderSaved(string sysNo)
        {
            return db.ModelContract.Where(m => m.sys_no == sysNo).Count() > 0;
        }

        public override string GetSpecificBillTypeName()
        {
            return BillTypeName + "_" + bill.model_type;
        }

        public override void DoWhenBeforeApply()
        {
            if (bill.model_type.Equals("开模")) {
                //开模要判断规格型号是否重复
                if (db.Apply.Where(a => a.order_type == BillType && a.p_model == bill.product_model && (a.success == null || a.success == true)).Count() > 0) {
                    throw new Exception("存在已提交的重复的开模规格型号，提交失败");
                }
            }
        }

        public override void DoWhenBeforeAudit(int step, string stepName, bool isPass, int userId)
        {
            //下单组必须填写编号
            if (stepName.Contains("下单组") && isPass) {
                if (string.IsNullOrWhiteSpace(bill.old_bill_no)) {
                    throw new Exception("下单组审核必须填写订单号");
                }
                else if (db.ModelContract.Where(m => m.sys_no != bill.sys_no && m.old_bill_no == bill.old_bill_no).Count() > 0) {
                    throw new Exception("订单号在下单系统已存在");
                }
            }
        }

        public override void DoWhenFinishAudit(bool isPass)
        {
            if (isPass) {
                MoveToFormalDir(bill.sys_no); //成功结束的申请，将附件移动到正式目录
            }
        }

        /// <summary>
        /// 导出excel数据的模型
        /// </summary>
        private class CMExcelData
        {
            public ModelContract h { get; set; }
            public string auditStatus { get; set; }
        }

        public override void ExportSalerExcle(SalerSearchParamModel pm, int userId)
        {
            pm.toDate = pm.toDate.AddDays(1);
            pm.sysNo = pm.sysNo ?? "";

            var myData = (from o in db.ModelContract
                          join a in db.Apply on o.sys_no equals a.sys_no into X
                          from Y in X.DefaultIfEmpty()
                          where o.user_id == userId
                          && (o.sys_no.Contains(pm.sysNo) || o.product_model.Contains(pm.sysNo))
                          && o.bill_date >= pm.fromDate
                          && o.bill_date <= pm.toDate
                          && (pm.auditResult == 10 || (pm.auditResult == 0 && (Y == null || (Y != null && Y.success == null))) || (pm.auditResult == 1 && Y != null && Y.success == true) || pm.auditResult == -1 && Y != null && Y.success == false)
                          orderby o.bill_date
                          select new CMExcelData()
                          {
                              h = o,
                              auditStatus = (Y == null ? "未开始申请" : Y.success == true ? "申请成功" : Y.success == false ? "申请失败" : "审批当中"),
                          }).ToList();

            ExportExcel(myData);
        }

        public override void ExportAuditorExcle(AuditSearchParamModel pm, int userId)
        {
            DateTime fromDate, toDate;
            if (!DateTime.TryParse(pm.from_date, out fromDate)) {
                fromDate = DateTime.Parse("2010-01-01");
            }
            if (!DateTime.TryParse(pm.to_date, out toDate)) {
                toDate = DateTime.Parse("2099-01-01");
            }
            else {
                toDate = toDate.AddDays(1);
            }

            pm.sysNo = pm.sysNo ?? "";
            pm.proModel = pm.proModel ?? "";

            var myData = (from a in db.Apply
                          from ad in a.ApplyDetails
                          join o in db.ModelContract on a.sys_no equals o.sys_no
                          where ad.user_id == userId
                          && a.order_type == BillType
                          && a.sys_no.Contains(pm.sysNo)
                          && o.product_model.Contains(pm.proModel)
                          && a.start_date >= fromDate
                          && a.start_date <= toDate
                          && (pm.isFinish == 10
                          || (pm.isFinish == 1 && a.success == true)
                          || (pm.isFinish == 0 && a.success == null)
                          || (pm.isFinish == -1 && a.success == false))
                          && (pm.auditResult == 10
                          || (pm.auditResult == 1 && ad.pass == true)
                          || (pm.auditResult == 0 && ad.pass == null
                               && ((ad.countersign == true && a.ApplyDetails.Where(ads => ads.step == ad.step && ads.pass == false).Count() == 0)
                                   || ((ad.countersign == false || ad.countersign == null) && a.ApplyDetails.Where(ads => ads.step == ad.step && ads.pass == true).Count() == 0)
                               )
                             )
                          || (pm.auditResult == -1 && ad.pass == false)
                          )
                          && (ad.step == 1 || a.ApplyDetails.Where(ads => ads.step == ad.step - 1 && ads.pass == true).Count() > 0)
                          orderby a.start_date descending
                          select new CMExcelData()
                          {
                              h = o,
                              auditStatus = (a.success == true ? "申请成功" : a.success == false ? "申请失败" : "审批当中"),
                          }).Distinct().ToList();

            ExportExcel(myData);
        }

        private void ExportExcel(List<CMExcelData> data)
        {
            //列宽：
            ushort[] colWidth = new ushort[] {12,16,12,16,16,20,18,16,16,16,
                                             14,32,32,32,20,28,14,14,12,
                                             14,14,20,20,20,16,16,16,32};

            //列名：
            string[] colName = new string[] { "状态","流水号","模单类型","订单编号","下单日期","要求样品完成日期","产品类别","办事处","对应事业部","项目组",
                                            "营业工程师","购货单位","终极客户","方案公司","产品名称","型号规格","样品数量","样品单价","币别",
                                            "收费","免费","产品行业分类","结算方式","交货地点","贸易类型","计入事业部","特殊开模单","备注"};

            //設置excel文件名和sheet名
            XlsDocument xls = new XlsDocument();
            xls.FileName = string.Format("开改模单_{0}.xls", DateTime.Now.ToShortDateString());
            Worksheet sheet = xls.Workbook.Worksheets.Add("订单信息列表");

            //设置各种样式

            //标题样式
            XF boldXF = xls.NewXF();
            boldXF.HorizontalAlignment = HorizontalAlignments.Centered;
            boldXF.Font.Height = 12 * 20;
            boldXF.Font.FontName = "宋体";
            boldXF.Font.Bold = true;

            //设置列宽
            ColumnInfo col;
            for (ushort i = 0; i < colWidth.Length; i++) {
                col = new ColumnInfo(xls, sheet);
                col.ColumnIndexStart = i;
                col.ColumnIndexEnd = i;
                col.Width = (ushort)(colWidth[i] * 256);
                sheet.AddColumnInfo(col);
            }

            Cells cells = sheet.Cells;
            int rowIndex = 1;
            int colIndex = 1;

            //设置标题
            foreach (var name in colName) {
                cells.Add(rowIndex, colIndex++, name, boldXF);
            }

            foreach (var c in data) {
                var d = c.h;
                colIndex = 1;

                cells.Add(++rowIndex, colIndex, c.auditStatus);
                cells.Add(rowIndex, ++colIndex, d.sys_no);
                cells.Add(rowIndex, ++colIndex, d.model_type);
                cells.Add(rowIndex, ++colIndex, d.old_bill_no);
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.bill_date).ToShortDateString());
                cells.Add(rowIndex, ++colIndex, ((DateTime)d.fetch_date).ToShortDateString());
                cells.Add(rowIndex, ++colIndex, d.product_type);
                cells.Add(rowIndex, ++colIndex, d.agency_name);
                cells.Add(rowIndex, ++colIndex, d.bus_dep);
                cells.Add(rowIndex, ++colIndex, d.project_team);

                cells.Add(rowIndex, ++colIndex, d.clerk_name);
                cells.Add(rowIndex, ++colIndex, d.customer_name);
                cells.Add(rowIndex, ++colIndex, d.zz_customer_name);
                cells.Add(rowIndex, ++colIndex, d.plan_firm_name);
                cells.Add(rowIndex, ++colIndex, d.product_name);
                cells.Add(rowIndex, ++colIndex, d.product_model);
                cells.Add(rowIndex, ++colIndex, d.qty);
                cells.Add(rowIndex, ++colIndex, d.price);
                cells.Add(rowIndex, ++colIndex, d.currency_name);

                cells.Add(rowIndex, ++colIndex, d.charge);
                cells.Add(rowIndex, ++colIndex, d.free);
                cells.Add(rowIndex, ++colIndex, d.classification);
                cells.Add(rowIndex, ++colIndex, d.clear_way);
                cells.Add(rowIndex, ++colIndex, d.fetch_add_name);
                cells.Add(rowIndex, ++colIndex, d.trade_type_name);
                cells.Add(rowIndex, ++colIndex, !string.IsNullOrEmpty(d.count_in_bus_dep) ? "是" : "否");
                cells.Add(rowIndex, ++colIndex, !string.IsNullOrEmpty(d.special_model) ? "是" : "否");
                cells.Add(rowIndex, ++colIndex, d.comment);
            }
            xls.Send();
        }

        public override void BeforeRollBack(int step)
        {
            throw new NotImplementedException();
        }

        public override System.IO.Stream PrintReport(string fileFolder)
        {
            if ((from a in db.Apply
                 from ad in a.ApplyDetails
                 where a.sys_no == bill.sys_no
                 && ad.user_id == bill.user_id
                 select ad).Count() < 1) {
                if (!new SomeUtils().hasGotPower((int)bill.user_id, "chk_pdf_report")) {
                    throw new Exception("流水号不存在或没有权限查看");
                }
            }

            Stream stream = null;
            using (ReportClass rptH = new ReportClass()) {
                using (CMDT cmDt = new CMDT()) {
                    using (Sale_model_contractTableAdapter cmTa = new Sale_model_contractTableAdapter()) {
                        cmTa.Fill(cmDt.Sale_model_contract, bill.sys_no);
                    }
                    //设置办事处1、总裁办3，市场部2审核人名字
                    string agencyAuditor = "", ceoAuditor = "", marketAuditor = "", yfManager = "",
                        quotationAuditor = "", busAuditor = "", marketManager = "";

                    var auditDetails = (from a in db.Apply
                                        join d in db.ApplyDetails on a.id equals d.apply_id
                                        join u in db.User on d.user_id equals u.id
                                        where a.sys_no == bill.sys_no && d.pass == true
                                        select new
                                        {
                                            d.step,
                                            d.step_name,
                                            u.real_name
                                        }).ToList();

                    agencyAuditor = auditDetails.Where(ad => ad.step == 1 && ad.step_name.Contains("办事处")).Select(ad => ad.real_name).FirstOrDefault() ?? "";
                    marketAuditor = auditDetails.Where(ad => ad.step == 1 && ad.step_name.Contains("总经理")).Select(ad => ad.real_name).FirstOrDefault() ?? "";
                    yfManager = auditDetails.Where(ad => ad.step == 1 && ad.step_name.Contains("项目经理")).Select(ad => ad.real_name).FirstOrDefault() ?? "";
                    quotationAuditor = auditDetails.Where(ad => ad.step == 1 && ad.step_name.Contains("报价员")).Select(ad => ad.real_name).FirstOrDefault() ?? "";
                    busAuditor = auditDetails.Where(ad => ad.step == 1 && ad.step_name.Contains("事业部")).Select(ad => ad.real_name).FirstOrDefault() ?? "";
                    marketAuditor = auditDetails.Where(ad => ad.step == 2).Select(ad => ad.real_name).FirstOrDefault() ?? "";
                    ceoAuditor = auditDetails.Where(ad => ad.step == 3).Select(ad => ad.real_name).FirstOrDefault() ?? "";

                    cmDt.model_contract_auditor.Addmodel_contract_auditorRow(agencyAuditor, marketAuditor, ceoAuditor, yfManager, busAuditor, quotationAuditor, marketManager);

                    rptH.FileName = fileFolder + "CMYF_A4_Report.rpt";
                    rptH.Load();
                    rptH.SetDataSource(cmDt);
                }
                stream = rptH.ExportToStream(CrystalDecisions.Shared.ExportFormatType.PortableDocFormat);
            }
            return stream;
        }

        public override string GetOrderNumber()
        {
            return bill.old_bill_no;
        }

        public string ccToOthers(string sysNo, bool isPass)
        {
            var emailList = (from a in db.Apply
                             join d in db.ApplyDetails on a.id equals d.apply_id
                             join u in db.User on d.user_id equals u.id
                             where a.sys_no == sysNo
                             && d.step == 1
                             && d.step_name.Contains("项目") //项目经理、项目管理员与项目组上级
                             && u.email != null
                             && u.email != ""
                             select u.email).ToList();
            return string.Join(",", emailList);
        }

        public bool needReport(bool isPass)
        {
            return true;
        }
    }
}