using Newtonsoft.Json;
using Sale_Order_Semi.Models;
using Sale_Order_Semi.Services;
using Sale_Order_Semi.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Sale_Order_Semi.Controllers
{
    public class NFileController : BaseController
    {
        private const string TAG = "文件模块";
        private SomeUtils util = new SomeUtils();

        private void Wlog(string log, string sysNo = "", int unusual = 0)
        {
            base.Wlog(TAG, log, sysNo, unusual);
        }

        /// <summary>
        /// 营业单据列表页面，导出Excel
        /// </summary>
        /// <param name="fc">form表单</param>
        public void ExportSalerExcel(FormCollection fc)
        {
            SalerSearchParamModel pm = new SalerSearchParamModel();
            util.SetFieldValueToModel(fc, pm);

            BillSv bill = (BillSv)new BillUtils().GetBillSvInstance(pm.billType);

            Wlog("营业员导出Excel：" + JsonConvert.SerializeObject(pm));

            bill.ExportSalerExcle(pm, currentUser.userId);
        }

        public void ExportAuditorExcel(string billType, FormCollection fc)
        {
            AuditSearchParamModel pm = new AuditSearchParamModel();
            util.SetFieldValueToModel(fc, pm);

            BillSv bill = (BillSv)new BillUtils().GetBillSvInstance(billType);

            if (bill == null) return;

            Wlog("审核人导出Excel：" + JsonConvert.SerializeObject(pm), billType);

            bill.ExportAuditorExcle(pm, currentUser.userId);
        }

        public ActionResult PrintReport(string sysNo)
        {
            try {
                BillSv bill = (BillSv)new BillUtils().GetBillSvInstanceBySysNo(sysNo);

                var stream = bill.PrintReport(Server.MapPath("~/Reports/"));
                return File(stream, "application/pdf");
            }
            catch (Exception ex) {
                ViewBag.tip = ex.Message;
                return View("Tip");
            }
        }

    }
}
