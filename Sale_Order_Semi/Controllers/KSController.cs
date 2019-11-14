using ExcelDataReader;
using Newtonsoft.Json;
using Sale_Order_Semi.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sale_Order_Semi.Utils;
using Sale_Order_Semi.Filter;

namespace Sale_Order_Semi.Controllers
{
    public class KSController : Controller
    {   
        // 香港SO上传与读取保存
        private string orderType = "KS";
        SaleDBDataContext db = new SaleDBDataContext();

        [SessionTimeOutFilter]
        public ActionResult UploadKSData()
        {
            return View();
        }

        private string GetKSExcelPath(string fileName)
        {
            string filepath = ConfigurationManager.AppSettings["AttachmentPath1"];// "D:\\Sale_upload_temp\\";            
            var folder = Path.Combine(filepath, orderType);
            if (!Directory.Exists(folder)) {
                Directory.CreateDirectory(folder);
            }
            return Path.Combine(folder, fileName);
        }

        //上传excel，等待读取
        [AcceptVerbs(HttpVerbs.Post)]
        public JsonResult BeginUploadKS(HttpPostedFileBase file)
        {            
            string fileName = Path.GetFileNameWithoutExtension(file.FileName) + DateTime.Now.ToString("_yyMMddHHmmss") + Path.GetExtension(file.FileName);
            try {
                file.SaveAs(GetKSExcelPath(fileName));
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true, extra = fileName });
        }

        private List<Sale_HK_SO> GetDataFromExcel(string fileName)
        {
            var result = new List<Sale_HK_SO>();
            var dt = new DataSet();
            using (var stream = System.IO.File.Open(GetKSExcelPath(fileName), FileMode.Open, FileAccess.Read)) {                
                using (var reader = ExcelReaderFactory.CreateReader(stream)) {                    
                    dt = reader.AsDataSet();                    
                }
            }
            var tb = dt.Tables[0];
            DataRow r;
            DateTime tempDt;
            decimal tempDc;
            var productTypeList = db.vwItems.Where(v => v.what == "product_type").ToList();
            var currencyList=db.vwItems.Where(v => v.what == "currency").ToList();
            var hkEmps = db.VwEmp.Where(v => v.dep.StartsWith("香港")).ToList();
            for (var i = 1; i < tb.Rows.Count; i++) {
                //销售单号	交货日期&结束交货日期	海外客户	交货地点	产品类别	组别一	营业员一	规格型号	客户PO	币别	成交价	含税单价	数量	客户型号	产品用途
                r = tb.Rows[i];
                Sale_HK_SO so = new Sale_HK_SO();
                so.error_info = "";
                so.warn_info = "";

                //1. 销售单号
                so.bill_no = Convert.ToString(r[0]);
                if (!so.bill_no.Contains("-")) {
                    so.bill_no = so.bill_no.Substring(0, 2) + "-" + so.bill_no.Substring(2);
                }
                //2. 交货日期
                //try {
                //    so.fetch_date = (DateTime)r[1];
                //}
                //catch {
                //    so.warn_info += "交货日期格式不正确;";
                //}
                if (DateTime.TryParse(Convert.ToString(r[1]), out tempDt)) {
                    so.fetch_date = tempDt;
                }
                else {
                    so.warn_info += "交货日期格式不正确;";
                }
                //3. 海外客户
                so.oversea_client = Convert.ToString(r[2]);
                if (!so.oversea_client.Contains("-")) {
                    so.oversea_client = so.oversea_client.Substring(0, 4) + "-" + so.oversea_client.Substring(4);
                }
                //5. 产品类别
                so.product_type_name = Convert.ToString(r[4]);
                if (productTypeList.Where(p => p.fname == so.product_type_name).Count() == 1) {
                    so.product_type_no = productTypeList.Where(p => p.fname == so.product_type_name).First().fid;
                }
                else {
                    so.warn_info += "产品类别不存在;";
                }
                //6. 组别一
                so.group1_name=Convert.ToString(r[5]);
                //7. 营业员一
                so.clerk1_name = Convert.ToString(r[6]);
                if (hkEmps.Where(e => e.name == so.clerk1_name).Count() == 1) {
                    so.clerk1_no = hkEmps.Where(e => e.name == so.clerk1_name).First().cardId;
                }
                else {
                    so.warn_info += "营业员1不存在;";
                }
                //8. 规格型号
                so.item_model = Convert.ToString(r[7]);
                var existsModel = result.Where(t => t.item_model == so.item_model).FirstOrDefault();
                if (existsModel != null) {
                    so.item_no = existsModel.item_no;
                    so.error_info = existsModel.error_info;
                }
                else {
                    var pinfos = db.vwProductInfo.Where(p => p.item_model == so.item_model).ToList();
                    if (pinfos.Count() < 1) {
                        so.error_info += "规格型号不存在";
                    }
                    else if (pinfos.Count() > 1) {
                        so.error_info += "规格型号存在多个";
                    }
                    else {
                        so.item_no = pinfos.First().item_no;
                    }
                }
                //9. 客户PO
                so.customer_po = Convert.ToString(r[8]);
                //10. 币别
                so.currency_no = Convert.ToString(r[9]);
                if (currencyList.Where(c => c.fid == so.currency_no).Count() != 1) {
                    so.currency_no = "";
                    so.warn_info += "币别不存在";
                }
                //11. 成交价
                if (decimal.TryParse(Convert.ToString(r[10]), out tempDc)) {
                    so.deal_price = tempDc;
                }
                else {
                    so.warn_info += "成交价不合法";
                }
                //12. 含税单价
                if (decimal.TryParse(Convert.ToString(r[11]), out tempDc)) {
                    so.taxed_price = tempDc;
                }
                else {
                    so.warn_info += "含税单价不合法";
                }
                //13. 数量                
                if (decimal.TryParse(Convert.ToString(r[12]), out tempDc)) {
                    so.qty = tempDc;
                }
                else {
                    so.warn_info += "数量不合法";
                }
                //14. 客户型号
                so.customer_pn = Convert.ToString(r[13]);
                //15. 产品用途
                so.product_usage = Convert.ToString(r[14]);

                result.Add(so);
            }
            return result;
        }
        
        public JsonResult ShowExcelData(string fileName)
        {
            try {
                var result = GetDataFromExcel(fileName);
                return Json(new SimpleResultModel() { suc = true, extra = JsonConvert.SerializeObject(result) });
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
        }
        [SessionTimeOutFilter]
        public JsonResult SaveExcelData(string data)
        {
            try {
                int userId = Int32.Parse(Request.Cookies["order_semi_cookie"]["userid"]);
                string userName = db.User.Single(u => u.id == userId).real_name;
                var utl = new SomeUtils();
                var sos = JsonConvert.DeserializeObject<List<Sale_HK_SO>>(data);
                var billNoList = sos.Select(s => s.bill_no).ToList();
                var existedBill = db.Sale_HK_SO.Where(h => billNoList.Contains(h.bill_no)).ToList();
                if (existedBill.Count() > 0) {
                    return Json(new SimpleResultModel() { suc = false, msg = "订单号【" + existedBill.First().bill_no + "】已存在，不能重复保存" });
                }

                foreach (var so in sos) {                    
                    so.sys_no = utl.getSystemNo(orderType);
                    so.import_time = DateTime.Now;
                    so.user_name = userName;
                }
                db.Sale_HK_SO.InsertAllOnSubmit(sos);
                db.SubmitChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel() { suc = false, msg = ex.Message });
            }
            return Json(new SimpleResultModel() { suc = true });
        }

        [SessionTimeOutFilter]
        public ActionResult CheckHKSO()
        {
            return View();
        }

        public JsonResult SearchHKSO(DateTime fromDate, DateTime toDate, string billNo, int page = 1, int rows = 50)
        {
            billNo = billNo.Trim();
            toDate = toDate.AddDays(1);
            var result = from h in db.Sale_HK_SO
                         where h.import_time >= fromDate
                         && h.import_time <= toDate
                         && (billNo == "" || h.bill_no.Contains(billNo))
                         select h;
            int total = result.Count();
            return Json(new { total = total, rows = result.Skip((page - 1) * rows).Take(rows).OrderBy(r => r.id).ToList() });
        }

        public JsonResult DeleteHKSO(string ids)
        {
            try {
                int[] idArr = JsonConvert.DeserializeObject<int[]>(ids);
                var toDeleted = db.Sale_HK_SO.Where(h => idArr.Contains(h.id)).ToList();
                db.Sale_HK_SO.DeleteAllOnSubmit(toDeleted);
                db.SubmitChanges();
            }
            catch (Exception ex) {
                return Json(new SimpleResultModel(ex));
            }
            return Json(new SimpleResultModel() { suc = true });
        }
    }
}
