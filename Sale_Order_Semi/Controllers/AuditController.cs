using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Sale_Order_Semi.Models;
using Sale_Order_Semi.Utils;
using Sale_Order_Semi.Filter;
using System.Text;
using System.Web;
using Newtonsoft.Json;

namespace Sale_Order_Semi.Controllers
{
    public class AuditController : BaseController
    {
        SomeUtils utl = new SomeUtils();

        //审批人查看自己审核的单据
        [SessionTimeOutFilter()]
        public ActionResult CheckAuditList()
        {
            //查询参数保存在Cookie，方便下次继续查询
            var queryData = Request.Cookies["semi_qd"];
            if (queryData != null && queryData.Values.Get("au_so") != null)
            {
                ViewData["sys_no"] = utl.DecodeToUTF8(queryData.Values.Get("au_so"));
                //ViewData["saler"] = utl.DecodeToUTF8(queryData.Values.Get("au_sa"));
                ViewData["audit_result"] = queryData.Values.Get("au_ar");
                ViewData["final_result"] = queryData.Values.Get("au_fr");
                ViewData["from_date"] = queryData.Values.Get("au_fd");
                ViewData["to_date"] = queryData.Values.Get("au_td");
                ViewData["pro_model"] = queryData.Values.Get("au_pm");
            }
            else
            {
                ViewData["audit_result"] = 0;
                ViewData["final_result"] = 0;
            }
            return View();
        }

        //默认搜索输入条件的结果
        public JsonResult GetAuditList()
        {
            string sysNo = "", saler = "", fromDate = "", toDate = "", proModel = "";
            int auditResult = 0, isFinish = 0;
            var queryData = Request.Cookies["semi_qd"];
            if (queryData != null && queryData.Values.Get("au_so") != null)
            {
                sysNo = utl.DecodeToUTF8(queryData.Values.Get("au_so"));
                //saler = utl.DecodeToUTF8(queryData.Values.Get("au_sa"));
                auditResult = Int32.Parse(queryData.Values.Get("au_ar"));
                isFinish = Int32.Parse(queryData.Values.Get("au_fr"));
                fromDate = queryData.Values.Get("au_fd");
                toDate = queryData.Values.Get("au_td");
                proModel = queryData.Values.Get("au_pm");
                
            }
            return SearchAuditBase(sysNo, "",proModel, fromDate, toDate, auditResult, isFinish);
        }

        //审批人搜索单据
        [SessionTimeOutFilter()]
        public JsonResult SearchAuditList(FormCollection fc)
        {
            string sysNo = fc.Get("sys_no");
            //string saler = fc.Get("saler");
            string fromDateString = fc.Get("fromDate");
            string toDateString = fc.Get("toDate");
            string result = fc.Get("auditResult");
            string finalResult = fc.Get("finalResult");
            string proModel = fc.Get("proModel");

            //查询参数保存在Cookie，方便下次继续查询
            var queryData = Request.Cookies["semi_qd"];
            if (queryData == null) queryData = new HttpCookie("semi_qd");
            queryData.Values.Set("au_so", utl.EncodeToUTF8(sysNo));
            //queryData.Values.Set("au_sa", utl.EncodeToUTF8(saler));
            queryData.Values.Set("au_pm", proModel);
            queryData.Values.Set("au_ar", result);
            queryData.Values.Set("au_fr", finalResult);
            queryData.Values.Set("au_fd", fromDateString);
            queryData.Values.Set("au_td", toDateString);
            queryData.Expires = DateTime.Now.AddDays(7);
            Response.AppendCookie(queryData);

            utl.writeEventLog("查询审核单据", fromDateString + "~" + toDateString + ";proModel:" + proModel + ";auditResult:" + result + ";finalResult:" + finalResult, sysNo, Request);
            return SearchAuditBase(sysNo, "",proModel, fromDateString, toDateString, Int32.Parse(result), Int32.Parse(finalResult));
        }

        //搜索单据base方法
        [SessionTimeOutFilter()]
        public JsonResult SearchAuditBase(string sysNo, string saler,string proModel, string from_date, string to_date, int auditResult, int isFinish)
        {
            DateTime fromDate = string.IsNullOrWhiteSpace(from_date) ? DateTime.Parse("1980-1-1") : DateTime.Parse(from_date);
            DateTime toDate = string.IsNullOrWhiteSpace(to_date) ? DateTime.Parse("2099-9-9") : DateTime.Parse(to_date);
            SomeUtils utl = new SomeUtils();
            List<AuditListModel> list = new List<AuditListModel>();
            int step;
            string status;
            string finalStatus;
            Apply ap;
            bool? importFlag;
            int maxRecordNum = 200;
            int recordNum = 0;
            var details = from ad in db.ApplyDetails
                          join a in db.Apply on ad.apply_id equals a.id
                          where ad.user_id == currentUser.userId
                          && a.sys_no.Contains(sysNo)
                          //&& ad.Apply.User.real_name.Contains(saler)
                          && a.start_date >= fromDate
                          && a.start_date <= toDate
                          && a.p_model.Contains(proModel)
                          && !a.sys_no.StartsWith("CH") //出货的不能查出来
                          && (
                          (isFinish == 10
                          || a.success == true && isFinish == 1)
                          || (a.success == null && isFinish == 0)
                          || (a.success == false && isFinish == -1)
                          )
                          && (auditResult == 10
                          || ((ad.pass == true || ((ad.countersign == null || ad.countersign == false) && a.ApplyDetails.Where(ads => ads.step == ad.step && ads.pass == true).Count() > 0)) && auditResult == 1)
                          || ((ad.pass == false || ((ad.countersign == null || ad.countersign == false) && a.ApplyDetails.Where(ads => ads.step == ad.step && ads.pass == false).Count() > 0)) && auditResult == -1)
                          || (((ad.countersign == true && ad.pass == null) || ((ad.countersign == null || ad.countersign == false) && a.ApplyDetails.Where(ads => ads.step == ad.step && ads.pass != null).Count() == 0)) && auditResult == 0)
                          )
                          select ad;
            var billTypes = db.Sale_BillTypeName.ToList();
            foreach (var ad in details)
            {
                importFlag = null;
                finalStatus = "----";
                ap = ad.Apply;

                step = (int)ad.step;
                //还没到这一步或者已经在之前结束
                if (step >= 2 && (ap.ApplyDetails.Where(a => a.step == step - 1 && a.pass == true).Count() < 1))
                {
                    continue;
                }

                //如果上一步是会签，而且还未结束，即跳出当前循环
                if (ap.ApplyDetails.Where(a => a.step == step - 1 && a.countersign == true && a.pass == null).Count() > 0)
                {
                    continue;
                }

                //status = (ad.pass == true ? "审核成功" : (ad.pass == false ? "审核失败" : "待审核"));
                if (auditResult == 1 || ad.pass == true) {
                    status = "审核成功";
                }
                else if (auditResult == -1 || ad.pass == false) {
                    status = "审核失败";
                }
                else if (auditResult == 0)
                {                    
                    status = "待审核";
                }
                else {
                    //获取组内其它人审核结果
                    if ((ad.countersign == null || ad.countersign == false) && ad.Apply.ApplyDetails.Where(ads => ads.step == ad.step && ads.pass == true).Count() > 0)
                    {
                        status = "审核成功";
                    }
                    else if ((ad.countersign == null || ad.countersign == false) && ad.Apply.ApplyDetails.Where(ads => ads.step == ad.step && ads.pass == false).Count() > 0)
                    {
                        status = "审核失败";
                    }
                    else {

                        if (ap.success != null)
                        {
                            status = "审核结束";
                        }
                        else
                        {
                            status = "待审核";
                        }
                    }
                }
                if (ad.pass == null && db.BlockOrder.Where(b => b.sys_no == ap.sys_no && b.step == step).Count() > 0)
                {
                    status = "挂起中";
                }

                finalStatus = (ap.success == true ? "PASS" : (ap.success == false ? "NG" : "----"));

                if (ap.success == true)
                {
                    if (db.ImportSysNoLog.Where(im => im.sys_no == ap.sys_no).Count() > 0)
                    {
                        importFlag = true;
                    }
                    else
                    {
                        db.hasImportIntoK3(ap.sys_no, ap.order_type, ref importFlag);
                        if (importFlag == true)
                        {
                            db.ImportSysNoLog.InsertOnSubmit(new ImportSysNoLog() { sys_no = ap.sys_no });
                            db.SubmitChanges();
                        }
                    }
                }                               
                
                list.Add(new AuditListModel()
                            {
                                depName = ap.User.Department1.name,
                                applyId = ap.id,
                                applyDetailId = ad.id,
                                previousStepTime = ((DateTime)ap.start_date).ToString("yyyy-MM-dd HH:mm"),//改成下单时间，之前是到达时间
                                salerName = ap.User.real_name,
                                step = step,
                                stepName = ad.step_name,
                                sysNum = ap.sys_no,
                                status = status,
                                hasImportK3 = (importFlag == true) ? "Y" : ((importFlag == false) ? "N" : ""),
                                finalStatus = finalStatus,
                                encryptNo = utl.myEncript(ap.sys_no),
                                orderType = billTypes.Where(b => b.p_type == ap.order_type).Select(b => b.p_name).FirstOrDefault(),
                                model = ap.p_model
                            });
                recordNum++;
                if (recordNum >= maxRecordNum) break;
            }

            list = list.OrderBy(l => l.step).OrderBy(l => DateTime.Parse(l.previousStepTime)).ToList();
            return Json(list, "text/html");

        }

        //审批人审核
        [SessionTimeOutFilter()]
        public ActionResult BeginAudit(int step, int applyId)
        {
            var aps = db.Apply.Where(a => a.id == applyId);
            if (aps==null || aps.Count() < 1) {
                utl.writeEventLog("审核单据", "单据不存在,applylId:" + applyId.ToString(), "", Request, 1000);
                ViewBag.tip = "单据不存在，请确认公司名是否正确。";
                return View("Tip");
            }
            var ap = aps.First();
            var ads = ap.ApplyDetails.Where(a => a.step == step && a.user_id == currentUser.userId);
             
            //验证是否有审核权限
            if (ads.Count() < 1) {
                utl.writeEventLog("审核单据", "没有权限审核,applyId:" + applyId.ToString() + ";step=" + step, ap.sys_no, Request, 1000);
                ViewBag.tip = "对不起，你没有权限审核或者还未轮到你审核";
                return View("Tip");
            }
            var ad = ads.OrderBy(a => a.pass).First();
            int currentStep = step;

            //上一步还未审核OK，不能审核
            if (ad.step >= 2 && (ap.ApplyDetails.Where(a => a.step == currentStep - 1 && a.pass == true).Count() < 1))
            {
                ViewBag.tip = "还没有轮到你审核";
                return View("Tip");
            }
            //如果上一步是会签，而且还未结束，即不能审核
            if (ap.ApplyDetails.Where(a => a.step == currentStep - 1 && a.countersign == true && a.pass == null).Count() > 0)
            {
                ViewBag.tip = "上一步的会签还未完成";
                return View("Tip");
            }
            ViewData["step"] = currentStep;
            ViewData["applyId"] = ap.id;
            ViewData["orderType"] = ap.order_type;
            ViewData["sys_no"] = ap.sys_no;
            ViewData["create_user"] = ap.User.real_name;
            //该审核步骤是否可编辑
            bool? canEdit = ad.can_modify;
            //该审核步骤是否已处理
            bool hasEdit;
            if (ap.success != null)
            {
                hasEdit = true;
            }
            else if (ad.pass != null)
            {
                hasEdit = true;
            }
            else if (ad.countersign == false || ad.countersign == null)
            {
                //不是会签
                hasEdit = ap.ApplyDetails.Where(a => a.step == currentStep && a.pass != null).Count() > 0;
            }
            else { 
                //是会签
                hasEdit = ap.ApplyDetails.Where(a => a.step == currentStep && a.pass == false).Count() > 0;                
            }
            switch (ap.order_type)
            {
                case "SO":
                    ViewData["order_id"] = db.Sale_SO.Single(s => s.sys_no == ap.sys_no).id;
                    ViewData["step_name"] = ad.step_name;
                    break;
                case "CM":
                    ViewData["order_id"] = db.ModelContract.Single(s => s.sys_no == ap.sys_no).id;
                    break;
                case "SB":
                    ViewData["order_id"] = db.SampleBill.Single(s => s.sys_no == ap.sys_no).id;
                    break;
                case "BL":
                    ViewData["order_id"] = db.Sale_BL.Single(s => s.sys_no == ap.sys_no).id;
                    ViewData["step_name"] = ad.step_name;
                    break;
                case "TH":
                    ViewData["order_id"] = db.ReturnBill.Where(r => r.sys_no == ap.sys_no).First().id;
                    break;
                default:
                    return View("Error");
            }
            //可编辑并且未审核，转到对应编辑界面
            if (canEdit == true && !hasEdit)
            {
                //挂起信息
                ViewData["blockInfo"] = db.BlockOrder.Where(b => b.sys_no == ap.sys_no).OrderBy(b => b.step).ToList();
                utl.writeEventLog("审核单据", "进入可编辑界面", ap.sys_no + ":" + currentStep.ToString(), Request);
                switch (ap.order_type)
                {
                    case "SO":
                        return RedirectToAction("AuditorModifySOBill", "Saler", new { apply_id = ap.id, sys_no = ap.sys_no, step = currentStep });
                    case "MB":
                        return View("ContractEdit");
                    case "CM":
                        return RedirectToAction("AuditorModifyModelContract", "Saler", new { apply_id = ap.id, sys_no = ap.sys_no, step = currentStep });
                    case "SB":
                        return RedirectToAction("AuditorModifySampleBill", "Saler", new { apply_id = ap.id, sys_no = ap.sys_no, step = currentStep });
                    case "BL":
                        return RedirectToAction("AuditorModifyBLBill", "Saler", new { apply_id = ap.id, sys_no = ap.sys_no, step = currentStep });
                    case "TH":
                        var bill = db.ReturnBill.Single(r => r.sys_no == ap.sys_no);
                        ViewData["bill"] = bill;
                        ViewData["details"] = bill.ReturnBillDetail.OrderBy(r => r.entry_no).ToList();
                        ViewData["userName"] = bill.User.real_name;
                        ViewData["status"] = "审核中";
                        ViewData["currentAuditor"] = currentUser.realName;
                        ViewData["return_dep"] = db.Department.Where(d => d.dep_type == "退货事业部" && d.dep_no == bill.return_dept).Count() < 1 ? "不存在" : db.Department.Where(d => d.dep_type == "退货事业部" && d.dep_no == bill.return_dept).First().name;
                        if (ad.step_name.Contains("客服")) {
                            return View("EditReturnBillQty");
                        }
                        else if (ad.step_name.Contains("物流")) {
                            return View("LogEditReturnBill");
                        }
                        else {
                            return View("Error");
                        }
                    default:
                        return View("Error");
                }
            }
            else
            {
                utl.writeEventLog("审核单据", "进入只读界面", ap.sys_no + ":" + currentStep.ToString(), Request);
                return View("MarketAudit");
            }
        }

        //审核员处理申请
        public JsonResult HandleAgencyAudit(FormCollection fc)
        {
            int applyId = int.Parse(fc.Get("applyId"));
            Apply ap = db.Apply.Single(a => a.id == applyId);
            int step = int.Parse(fc.Get("step"));
            bool isOK = bool.Parse(fc.Get("okFlag"));
            string backToPrevious = fc.Get("backToPrevious");
            string comment = fc.Get("agency_comment");
            string newProcDept = fc.Get("new_dep");
            string msg = "审核成功";            

            //如果已结束，提示
            if (ap.success != null)
            {
                utl.writeEventLog("审核单据", "此申请已结束，不能审批:", ap.sys_no + ":" + step.ToString(), Request, 100);
                return Json(new { success = false, msg = "此申请已结束" }, "text/html");
            }

            ApplyDetails thisDetail = ap.ApplyDetails.Where(ad => ad.user_id == currentUser.userId && ad.step == step).OrderBy(ad=>ad.pass).First();
            //已审核或者不是会签的别人已审核，则提示
            if (thisDetail.pass != null || ap.ApplyDetails.Where(ad => ad.step == step && ad.pass != null && (ad.countersign == null || ad.countersign == false)).Count() > 0)
            {
                utl.writeEventLog("审核单据", "该订单已被审核:", ap.sys_no + ":" + step.ToString(), Request, 100);
                return Json(new { success = false, msg = "该订单已被审核" }, "text/html");
            }
            
            int maxStep = (int)db.ApplyDetails.Where(ad => ad.apply_id == ap.id).Max(ad => ad.step);

            #region 退换货申请特殊处理
            if (ap.order_type.Equals("TH")) {
                //验证勾稽状态与蓝字发票状态是否一致

                if (isOK  && step == maxStep) {
                    string validateResult = utl.ValidateHasInvoiceFlag(ap.sys_no);
                    if (!string.IsNullOrEmpty(validateResult)) {
                        utl.writeEventLog("审核申请", validateResult, ap.sys_no, Request, 10);
                        return Json(new { success = false, msg = validateResult }, "text/html");
                    }
                }
                //可以退回到上一步 2014-2-25新增
                if (!isOK && backToPrevious.Equals("1") && step > 1) {
                    return BackToPreviousStep(ap, step, comment);
                }

                //退换货，市场部林秋海（步骤2）需要将符合规则的意见（以备注：开头）插入到市场部备注字段，然后将意见删除
                if (step == 2) {
                    if (comment.Trim().StartsWith("备注：") || comment.Trim().StartsWith("备注:")) {
                        var returnBill = db.ReturnBill.Single(r => r.sys_no == ap.sys_no);
                        returnBill.market_comment = comment.Substring(3);
                        comment = "";
                    }
                }

                //2020-9-14 物流需保存运输费用和责任方
                string expressFee = fc.Get("express_fee");
                if (!string.IsNullOrEmpty(expressFee)) {
                    var returnBill = db.ReturnBill.Single(r => r.sys_no == ap.sys_no);
                    returnBill.express_fee = decimal.Parse(expressFee);
                }
            }
            #endregion

            #region 核心审核代码
            try
            {
                //更新这一步骤的状态
                thisDetail.ip = Request.UserHostAddress;
                thisDetail.pass = isOK;
                thisDetail.user_id = currentUser.userId;
                thisDetail.comment = comment;
                thisDetail.finish_date = DateTime.Now;
                
                //如果通过并且未到最后一级，则到达下一审核,否则审核失败
                if (!isOK || step == maxStep)
                {
                    ap.success = isOK;
                    ap.finish_date = DateTime.Now;
                    if (isOK)
                    {
                        //移动附件
                        msg = "最终审核成功，请尽快将数据导入K3.";
                        utl.moveToFormalDir(ap.sys_no);

                        if (ap.order_type.Equals("BL")) {
                            //写入备料单号
                            var bl = db.Sale_BL.Single(b => b.sys_no == ap.sys_no);
                            bl.bill_no = utl.getBLbillNo(bl.market_dep,bl.bus_dep,(int)bl.original_user_id);

                            //写入备料库存
                            db.Sale_BL_stock.InsertOnSubmit(new Sale_BL_stock(){
                                bill_no = bl.bill_no,
                                bl_time = DateTime.Now,
                                clerk_id = bl.User.id,
                                clerk_name = bl.User.real_name,
                                bus_dep = bl.bus_dep,
                                product_model = bl.product_model,
                                product_name = bl.product_name,
                                product_no = bl.product_no,
                                qty = bl.qty,
                                remain_qty = bl.qty
                            });

                            //备料明细entry_no重新排序
                            int entry_index = 1;
                            foreach (var det in bl.Sale_BL_details.OrderBy(d=>d.id)) {
                                det.entry_no = entry_index++;
                            }

                        }
                        else if (ap.order_type.Equals("SB")) {
                            //写入样品单号
                            var sb = db.SampleBill.Single(s => s.sys_no == ap.sys_no);
                            sb.bill_no = utl.getYPBillNo(sb.currency_no, sb.is_free == "免费");
                        }
                    }
                }
                //提交数据
                db.SubmitChanges();
                utl.writeEventLog("审核单据", msg + ",审核结果:" + isOK.ToString() + ";审核意见：" + comment, ap.sys_no + ":" + step.ToString(), Request);
            }
            catch (Exception ex)
            {
                utl.writeEventLog("审核单据", "抛出异常：" + ex.Message.ToString(), ap.sys_no + ":" + step.ToString(), Request, -1);
                return Json(new { success = false, msg = "审核发生错误" }, "text/html");
            }
            #endregion
                                    
            #region 不是会签或者会签已结束，发送通知邮件给下一环节审批人
            if (thisDetail.countersign == null || thisDetail.countersign == false || ap.ApplyDetails.Where(a => a.step == step && a.pass == null).Count() < 1)
            {
                if (utl.emailToNextAuditor(applyId))
                {
                    utl.writeEventLog("发送邮件", "通知下一环节：发送成功", ap.sys_no + ":" + step.ToString(), Request);
                }
                else
                {
                    utl.writeEventLog("发送邮件", "通知下一环节：发送失败", ap.sys_no + ":" + step.ToString(), Request, -1);
                }
            }
            else
            {
                utl.writeEventLog("发送邮件", "会签中，不用发送", ap.sys_no + ":" + step.ToString(), Request);
            }
            #endregion

            return Json(new { success = true, msg = msg }, "text/html");
        }

        //退回上一审批人，目前只有退换货申请有这个功能
        public JsonResult BackToPreviousStep(Apply ap, int step, string reason)
        {
            try
            {
                //将上一步的审核操作清空
                ApplyDetails detail = ap.ApplyDetails.Where(a => a.step == step - 1 && a.pass != null).First();
                detail.pass = null;
                detail.comment = "";
                detail.ip = "";
                detail.finish_date = null;

                db.SubmitChanges();
                utl.writeEventLog("审核单据", "审核结果:退回到上一审核步骤", ap.sys_no + ":" + step.ToString(), Request);
            }
            catch (Exception ex)
            {
                utl.writeEventLog("审核单据", "抛出异常：" + ex.Message.ToString(), ap.sys_no + ":" + step.ToString(), Request, -1);
                return Json(new { success = false, msg = "审核发生错误" }, "text/html");
            }

            //如果有，删除母步骤是parentStep的子步骤            
            if (utl.RemoveChildrenStep(ap.id, step - 1))
            {
                utl.writeEventLog("审核单据", "删除子步骤成功", ap.sys_no + ":" + step.ToString(), Request);
            }
            else
            {
                utl.writeEventLog("审核单据", "删除子步骤失败", ap.sys_no + ":" + step.ToString(), Request);
                return Json(new { success = false, msg = "审核发生错误，删除子步骤失败" }, "text/html");
            }
            
            //发送邮件通知上一环节审批人
            if (utl.emailToPrevious(ap.id,step-1, reason,currentUser.realName))
            {
                utl.writeEventLog("发送邮件", "通知下一环节：发送成功", ap.sys_no + ":" + step.ToString(), Request);
            }
            else
            {
                utl.writeEventLog("发送邮件", "通知下一环节：发送失败", ap.sys_no + ":" + step.ToString(), Request, -1);
            }

            return Json(new { success = true, msg = "成功退回到上一步骤审核人" });
        }

        //退红字单，客服审核，如果数量有变更，需要通知营业员，插入一个审核步骤
        public JsonResult HandleQtyEditTHAudit(FormCollection fc)
        {
            int applyId = int.Parse(fc.Get("applyId"));
            Apply ap = db.Apply.Single(a => a.id == applyId);
            int step = int.Parse(fc.Get("step"));
            bool isOK = bool.Parse(fc.Get("okFlag"));
            string comment = fc.Get("agency_comment");
            string backToPrevious = fc.Get("backToPrevious");
            string[] FRealQty = fc.Get("FRealQty").Split(',');
            string[] FEntryNo = fc.Get("FEntryNo").Split(',');
            string[] FIsOnline = fc.Get("FIsOnline").Split(',');
            string[] FChDepName = fc.Get("FChDepName").Split(',');
            string FQtyComment = fc.Get("FQtyComment");            
            string msg = "审核成功";
            decimal sumRealQty = 0, sumReturnQty = 0;

            //如果审核状态不为空，说明已经被审核了
            if (ap.ApplyDetails.Where(ad => ad.step == step && ad.pass != null).Count() > 0)
            {
                utl.writeEventLog("审核单据", "该订单已被审核:", ap.sys_no + ":" + step.ToString(), Request, 100);
                return Json(new { success = false, msg = "该订单已被审核" }, "text/html");
            }
            
            //退换货申请，可以退回到上一步 2014-2-25新增
            if (!isOK && backToPrevious.Equals("1") && step > 1)
            {
                return BackToPreviousStep(ap, step, comment);
            }

            try
            {
                //更新这一步骤的状态                
                ApplyDetails thisDetail = ap.ApplyDetails.Where(ad => ad.user_id == currentUser.userId && ad.step == step).First();
                thisDetail.ip = Request.UserHostAddress;
                thisDetail.pass = isOK;
                thisDetail.comment = comment;
                thisDetail.finish_date = DateTime.Now;

                var returnBill = db.ReturnBill.Where(r => r.sys_no == ap.sys_no).First();
                returnBill.qty_comment = FQtyComment;

                if (!isOK)
                {
                    ap.success = false;
                    ap.finish_date = DateTime.Now;
                }
                else
                {
                    //将实退数量update回退货单
                    var dets = returnBill.ReturnBillDetail;
                    //int j = 0;
                    sumRealQty = FRealQty.Sum(r => decimal.Parse(r));
                    sumReturnQty = (decimal)dets.Select(d => new { d.entry_no, d.return_qty }).Distinct().Sum(d => d.return_qty);

                    //重新新增一遍
                    for (int i = 0; i < FRealQty.Length; i++)
                    {
                        int entryNo = Int32.Parse(FEntryNo[i]);
                        var oldRdb = dets.Where(d => d.entry_no == entryNo).First();
                        ReturnBillDetail rbd = new ReturnBillDetail();
                        rbd.bill_id = oldRdb.bill_id;
                        rbd.aux_qty = oldRdb.aux_qty;
                        rbd.can_apply_qty = oldRdb.can_apply_qty;
                        rbd.entry_no = oldRdb.entry_no;
                        rbd.product_model = oldRdb.product_model;
                        rbd.product_name = oldRdb.product_name;
                        rbd.product_number = oldRdb.product_number;
                        rbd.return_qty = oldRdb.return_qty;
                        rbd.seorder_no = oldRdb.seorder_no;
                        rbd.stock_entry_id = oldRdb.stock_entry_id;
                        rbd.stock_inter_id = oldRdb.stock_inter_id;
                        rbd.stock_no = oldRdb.stock_no;
                        rbd.real_return_qty = decimal.Parse(FRealQty[i]);
                        rbd.is_online = FIsOnline[i].Equals("已上线") ? true : false;
                        rbd.ch_dep_name = FChDepName[i];
                        db.ReturnBillDetail.InsertOnSubmit(rbd);
                    }

                    //将旧的删除
                    db.ReturnBillDetail.DeleteAllOnSubmit(dets);
                }
                db.SubmitChanges();
            }
            catch (Exception ex)
            {
                utl.writeEventLog("审核单据", "抛出异常：" + ex.Message.ToString(), ap.sys_no + ":" + step.ToString(), Request, -100);
                return Json(new { success = false, msg = "审核发生错误" }, "text/html");
            }

            if (isOK) {
                foreach (var FChDep in FChDepName.Distinct().OrderByDescending(c => c))
                {
                    if (!string.IsNullOrEmpty(FChDep) && !FChDep.Equals("无"))
                    {
                        int chDepId = (int)db.Department.Single(p => p.name == FChDep && p.dep_type == "退货出货组").dep_no;
                        //int?[] chAuditors = db.ReturnDeptStepAuditor.Where(r => r.step_name == "出货组" && r.return_dept == chDepId).Select(r => r.user_id).ToArray();
                        int?[] chAuditors = db.AuditorsRelation.Where(a => a.step_name == "RED_事业部出货组" && a.relate_value == chDepId).Select(a => a.auditor_id).ToArray();
                        //先插入出货组审核，如果有需要，再插入营业审核，这样营业就会排在出货组之前
                        if (utl.InsertStepAfterStep(applyId, step, "出货组审核", chAuditors))
                        {
                            utl.writeEventLog("审核单据", "插入出货组审核环节成功", ap.sys_no + ":" + step.ToString() + ";dep:" + FChDep, Request);
                        }
                        else
                        {
                            utl.writeEventLog("审核单据", "插入出货组审核环节失败", ap.sys_no + ":" + step.ToString() + ";dep:" + FChDep, Request, -100);
                        }
                        
                    }
                }
                if (Math.Abs(sumReturnQty - sumRealQty) > 0.00001m)
                {
                    //首先判断下一步是不是已经营业员了
                    if (ap.ApplyDetails.Where(ad => ad.step == step + 1).First().user_id != ap.user_id)
                    {
                        //插入下一步，由流程发起者确认数量变更
                        if (utl.InsertStepAfterStep(applyId, step, "营业员确认", new int?[] { ap.user_id }))
                        {
                            utl.writeEventLog("审核单据", "插入营业员确认环节成功", ap.sys_no + ":" + step.ToString(), Request);
                        }
                        else
                        {
                            utl.writeEventLog("审核单据", "插入营业员确认环节失败", ap.sys_no + ":" + step.ToString(), Request, -100);
                        }
                    }
                }
            }

            //发送邮件通知下一环节审批人
            if (utl.emailToNextAuditor(applyId))
            {
                utl.writeEventLog("发送邮件", "通知下一环节：发送成功", ap.sys_no + ":" + step.ToString(), Request);
            }
            else
            {
                utl.writeEventLog("发送邮件", "通知下一环节：发送失败", ap.sys_no + ":" + step.ToString(), Request, -1);
            }

            return Json(new { success = true, msg = msg }, "text/html");
        }

        //审核员挂起申请
        public JsonResult BlockOrder(FormCollection fc)
        {
            int applyId = int.Parse(fc.Get("applyId"));
            Apply ap = db.Apply.Single(a => a.id == applyId);
            int step = int.Parse(fc.Get("step"));
            string comment = fc.Get("agency_comment");

            //验证是否有重复挂起操作
            var existblocks = db.BlockOrder.Where(b => b.sys_no == ap.sys_no && b.step == step && b.@operator == currentUser.userId);
            if (existblocks.Count() > 0)
            {
                return Json(new { success = false, msg = "不能重复进行挂起操作。" }, "text/html");
            }

            db.BlockOrder.InsertOnSubmit(new BlockOrder()
            {
                @operator = currentUser.userId,
                block_time = DateTime.Now,
                step = step,
                step_name = ap.ApplyDetails.Where(ad => ad.step == step).First().step_name,
                reason = comment,
                sys_no = ap.sys_no
            });
            db.SubmitChanges();

            utl.writeEventLog("审核单据", "将订单暂时挂起", ap.sys_no, Request);

            //发送通知邮件给申请者
            if (utl.emailForBlock(applyId, currentUser.userId, comment))
            {
                utl.writeEventLog("发送邮件", "挂起通知营业员：发送成功", ap.sys_no + ":" + step.ToString(), Request);
            }
            else
            {
                utl.writeEventLog("发送邮件", "挂起通知营业员：发送失败", ap.sys_no + ":" + step.ToString(), Request);
            }

            return Json(new { success = true, msg = "挂起成功" }, "text/html");
        }

        //刷新处理结果
        public JsonResult RefleshAuditResult(int applyId, int step)
        {
            var details = db.ApplyDetails.Where(ad => ad.apply_id == applyId && ad.step == step);
            bool hasAudited = false;
            bool? pass = false;
            string comment = "";

            if (details.Where(d => d.pass == false).Count() > 0) {
                //被NG的
                hasAudited = true;
                pass = false;
                comment = details.Where(d => d.user_id == currentUser.userId).First().comment;
            }
            else if (details.Where(d => d.pass != null).Count() == 0) {
                //全部未被审核的
                hasAudited = false;
            }
            else {
                //部分被审核，且没有NG的，分会签和不会签两种情况
                if (details.First().countersign == false || details.First().countersign == null) {
                    //不是会签
                    hasAudited = true;
                    pass = true;
                    comment = details.Where(d => d.user_id == currentUser.userId).First().comment;
                }
                else {
                    //是会签
                    if (details.Where(d => d.user_id == currentUser.userId && d.pass == null).Count() > 0) {
                        hasAudited = false;
                    }
                    else {
                        hasAudited = true;
                        pass = true;
                        comment = details.Where(d => d.user_id == currentUser.userId).First().comment;
                    }
                }
            }

            return Json(new { success = hasAudited, pass = pass, comment = comment });
            //var hasAudit = details.Where(d => d.pass != null && (d.user_id == userId || d.countersign == null || d.countersign == false));
            //if (hasAudit.Count() < 1)
            //{
            //    //未审核，没有结果返回
            //    return Json(new { success = false });
            //}
            ////取得处理结果
            //return Json(new { success = true, pass = hasAudit.First().pass, comment = hasAudit.First().comment });
        }

        //营业员通过临时id与单据类型查看申请状态
        public JsonResult CheckApplyStatusSO(string sys_no)
        {
            string nextStepName = "无";
            string nextAuditors = "无";
            string pre = sys_no.Substring(0, 2);
            var apps = db.Apply.Where(a => a.sys_no == sys_no);
            if (apps.Count() == 0)
            {
                return Json(new { success = false }, "text/html");   //未提交申请
            }
            Apply app = apps.First();
            List<AuditStatusModel> list = new List<AuditStatusModel>();
            list.Add(new AuditStatusModel()
            {
                auditor = app.User.real_name,
                department=app.User.Department1.name,
                step = 0,
                stepName = "提交申请",
                date = ((DateTime)app.start_date).ToShortDateString(),
                time = ((DateTime)app.start_date).ToShortTimeString(),
                pass = true
            });
            AuditStatusModel asm;
            //int maxStep = db.Process.Where(p => p.bill_type == pre & p.is_finish==true).First().ProcessDetail.Max(pr => pr.step);
            int maxStep = (int)app.ApplyDetails.Max(ad => ad.step);
            foreach (var appDetail in app.ApplyDetails.Where(ap => ap.pass != null).OrderBy(ap=>ap.finish_date).OrderBy(ap=>ap.step))
            {
                asm = new AuditStatusModel();
                asm.step = (int)appDetail.step;
                asm.stepName = appDetail.step_name;
                asm.department = appDetail.User.Department1.name;
                //asm.stepName = utl.getStepName(asm.step);
                asm.auditor = appDetail.User.real_name;
                asm.date = ((DateTime)appDetail.finish_date).ToShortDateString();
                asm.time = ((DateTime)appDetail.finish_date).ToShortTimeString();
                asm.pass = appDetail.pass;
                asm.comment = appDetail.comment;
                list.Add(asm);
            }
            //审核成功
            if (app.success == true)
            {
                list.Add(new AuditStatusModel()
                {
                    step = maxStep + 1,
                    stepName = "完成申请",
                    department = "信息中心",
                    auditor = "系统",
                    date = ((DateTime)app.finish_date).ToShortDateString(),
                    time = ((DateTime)app.finish_date).ToShortTimeString(),
                    pass = true
                });
            }
            else if (app.success == null)
            {
                int? currentStep = app.ApplyDetails.Where(a => a.pass == true).Max(a => a.step);
                int nextStep;
                //如果此步骤不是会签的，那么下一步骤=此步骤+1；否则如果是会签的，但是已经会签结束了，也是+1；
                if (currentStep == null) {
                    nextStep = 1;
                }
                else if (app.ApplyDetails.Where(a => a.step == currentStep && (a.countersign == null || a.countersign == false)).Count() > 0)
                {
                    nextStep = (int)currentStep + 1;
                }
                else if (app.ApplyDetails.Where(a => a.step == currentStep && a.pass == null).Count() < 1)
                {
                    nextStep = (int)currentStep + 1;
                }
                else {
                    //会签
                    nextStep = (int)currentStep;
                }
                foreach (var det in app.ApplyDetails.Where(a => a.step == nextStep && a.pass == null))
                {
                    if (nextAuditors.Equals("无"))
                    {
                        nextAuditors = det.User.real_name;
                        nextStepName = det.step_name;
                    }
                    else
                    {
                        nextAuditors += "/" + det.User.real_name;
                    }
                }

            }

            utl.writeEventLog("查看状态", "单据审核流转记录", sys_no, Request);
            return Json(new { success = true, result = list, nextAuditors = nextAuditors, nextStepName = nextStepName });
        }
        //public JsonResult CheckApplyStatus(int id,string sty) {
        //    IQueryable<Apply> apps = null;
        //    switch (sty) { 
        //        case "SO":
        //        case "CS":
        //            apps = db.Apply.Where(a => a.order_id == id);//order_tp_id=>order_id
        //            break;
        //        case "MB":
        //            apps = db.Apply.Where(a => a.contract_tp_id == id);
        //            break;
        //        case "CM":
        //        case "FS":
        //            apps = db.Apply.Where(a => a.model_contract_id == id);
        //            break;
        //    }            
        //    if (apps.Count() == 0) {
        //        return Json(new { success = false });   //未提交申请
        //    }
        //    Apply app = apps.First();
        //    List<AuditStatusModel> list = new List<AuditStatusModel>();
        //    list.Add(new AuditStatusModel()
        //    {
        //        auditor = app.User.real_name,
        //        step = 0,
        //        stepName = "提交申请",
        //        date = ((DateTime)app.start_date).ToShortDateString(),
        //        time = ((DateTime)app.start_date).ToShortTimeString(),
        //        pass = true
        //    });
        //    AuditStatusModel asm;            
        //    foreach (var appDetail in app.ApplyDetails) {
        //        asm = new AuditStatusModel();
        //        asm.step = (int)appDetail.step;
        //        asm.stepName = utl.getStepName(asm.step);
        //        if (appDetail.pass != null)
        //        {
        //            asm.auditor = appDetail.User.real_name;
        //            asm.date = ((DateTime)appDetail.finish_date).ToShortDateString();
        //            asm.time = ((DateTime)appDetail.finish_date).ToShortTimeString();
        //            asm.pass = appDetail.pass;
        //            asm.comment = appDetail.comment;
        //        }
        //        list.Add(asm);
        //    }
        //    //审核成功
        //    if (app.success == true) {
        //        list.Add(new AuditStatusModel()
        //        {
        //            step = 5,
        //            stepName = "完成申请",
        //            auditor = "系统",
        //            date = ((DateTime)app.finish_date).ToShortDateString(),
        //            time = ((DateTime)app.finish_date).ToShortTimeString(),
        //            pass = true
        //        });
        //    }

        //    return Json(new { success = true, result = list });
        //}

        //审核人通过apply id查看审核状态
        //public JsonResult CheckAuditStatus(int applyId)
        //{

        //    var apps = db.Apply.Where(a => a.id == applyId);
        //    if (apps.Count() == 0)
        //    {
        //        return Json(new { success = false });   //未提交申请
        //    }
        //    Apply app = apps.First();
        //    List<AuditStatusModel> list = new List<AuditStatusModel>();
        //    list.Add(new AuditStatusModel()
        //    {
        //        auditor = app.User.real_name,
        //        step = 0,
        //        stepName = "提交申请",
        //        date = ((DateTime)app.start_date).ToShortDateString(),
        //        time = ((DateTime)app.start_date).ToShortTimeString(),
        //        pass = true
        //    });
        //    AuditStatusModel asm;
        //    foreach (var appDetail in app.ApplyDetails)
        //    {
        //        asm = new AuditStatusModel();
        //        asm.step = (int)appDetail.step;
        //        asm.stepName = utl.getStepName(asm.step);
        //        if (appDetail.pass != null)
        //        {
        //            asm.auditor = appDetail.User.real_name;
        //            asm.date = ((DateTime)appDetail.finish_date).ToShortDateString();
        //            asm.time = ((DateTime)appDetail.finish_date).ToShortTimeString();
        //            asm.pass = appDetail.pass;
        //            asm.comment = appDetail.comment;
        //        }
        //        list.Add(asm);
        //    }
        //    //审核成功
        //    if (app.success == true)
        //    {
        //        list.Add(new AuditStatusModel()
        //        {
        //            step = 5,
        //            stepName = "完成申请",
        //            auditor = "系统",
        //            date = ((DateTime)app.finish_date).ToShortDateString(),
        //            time = ((DateTime)app.finish_date).ToShortTimeString(),
        //            pass = true
        //        });
        //    }

        //    return Json(new { success = true, result = list });
        //}

        //取得单位所属的组内的所有单位

        public JsonResult GetGroupUnits(int id)
        {
            var gr = from g in db.getGroupBelongToUnit(id)
                     select new
                     {
                         id = g.unit_id,
                         name = g.unit_name
                     };
            
            return Json(gr);

        }

        //获取佣金率
        public JsonResult GetCommissionRate(string proType, double? MU)
        {
            double? result = 0;
            db.getCommissionRate(proType, MU, ref result);
            return Json(result, "text/html");
        }
             
                
        public JsonResult AuditorSaveModelContract(FormCollection fc) {
            int step = -1;
            if (!Int32.TryParse(fc.Get("step"), out step)) {
                return Json(new { suc = false, msg = "步骤不对，保存失败" }, "text/html");
            }

            string saveResult = utl.saveModelContract(fc, step, currentUser.userId);
            if (string.IsNullOrWhiteSpace(saveResult))
            {
                return Json(new { suc = true },"text/html");
            }
            else
            {
                return Json(new { suc = false, msg = saveResult }, "text/html");
            }
        }

        //审核人保存样品单
        public JsonResult AuditorSaveSampleBill(FormCollection fc)
        {
            int step = -1;
            if (!Int32.TryParse(fc.Get("step"), out step))
            {
                return Json(new { suc = false, msg = "步骤不对，保存失败" }, "text/html");
            }

            string saveResult = utl.saveSampleBill(fc, step, currentUser.userId);
            if (string.IsNullOrWhiteSpace(saveResult))
            {
                return Json(new { suc = true }, "text/html");
            }
            else
            {
                return Json(new { suc = false, msg = saveResult }, "text/html");
            }
        }

        //审核人保存备料单
        public JsonResult AuditorSaveBLBill(FormCollection fc)
        {
            int step = -1;
            if (!Int32.TryParse(fc.Get("step"), out step)) {
                return Json(new { suc = false, msg = "步骤不对，保存失败" }, "text/html");
            }
            string sysNo = fc.Get("sys_no");
            string stepName = db.Apply.Single(a => a.sys_no == sysNo).ApplyDetails.Where(ad => ad.step == step).First().step_name;
            Sale_BL bl = db.Sale_BL.Single(s => s.sys_no == sysNo);

            if (stepName.Contains("成控")) {
                decimal dealPrice = decimal.Parse(fc.Get("deal_price"));
                if (dealPrice != bl.deal_price) {
                    utl.writeEventLog("备料单", "成控修改成交价：" + bl.deal_price + "->" + dealPrice, bl.sys_no, Request);
                    bl.deal_price = dealPrice;
                }

            }
            else if (stepName.Contains("计划")) {
                //计划员指定订料员
                string orderIds = fc.Get("order_ids");
                string orderNames = fc.Get("order_names");
                string plannerComment = fc.Get("planner_comment");
                if (string.IsNullOrEmpty(orderIds)) {
                    return Json(new { suc = false, msg = "必须至少选择一个订料员" }, "text/html");
                }

                bl.order_ids = orderIds;
                bl.order_names = orderNames;
                bl.update_user_id = currentUser.userId;
                bl.step_version = step;
                bl.planner_comment = plannerComment;
            }
            else if (stepName.Contains("订料")) {
                //订料员只能修改备料明细
                string blDetails = fc.Get("Sale_BL_details");
                var details = JsonConvert.DeserializeObject<List<Sale_BL_details>>(blDetails);
                if (details.Count() == 0) {
                    return Json(new { suc = true }, "text/html");
                    //return Json(new { suc = false, msg = "必须录入备料清单明细" }, "text/html");
                }
                if (!utl.ModelsToString<Sale_BL_details>(bl.Sale_BL_details.ToList()).Equals(utl.ModelsToString<Sale_BL_details>(details))) {
                    int entryNo = 1;
                    foreach (var detail in details) {
                        if (detail.order_qty == null || detail.order_qty == 0) {
                            return Json(new { suc = false, msg = "第" + entryNo + "行的订料数量必须填写且不能为0" }, "text/html");
                        }
                        detail.entry_no = entryNo++;
                    }

                    //先备份数据
                    BackupData bd = new BackupData();
                    bd.sys_no = sysNo;
                    bd.user_id = bl.update_user_id;
                    bd.op_date = DateTime.Now;                    
                    bd.secondary_data = utl.ModelsToString<Sale_BL_details>(bl.Sale_BL_details.ToList());
                    db.BackupData.InsertOnSubmit(bd);
                    

                    //因为是会签，同时审批时如果将旧数据删除，会造成数据丢失的情况，A、B同时编辑时，A保存后，B再保存，那么A编辑的内容将会消失。
                    //改为只删除和插入自己的那些分录，其它不动。

                    db.Sale_BL_details.DeleteAllOnSubmit(bl.Sale_BL_details.Where(b => b.order_id == currentUser.userId));
                    bl.Sale_BL_details.AddRange(details.Where(d => d.order_id == currentUser.userId));
                    bl.update_user_id = currentUser.userId;
                    bl.step_version = step;
                }
            }

            try {
                db.Sale_BL_details.DeleteAllOnSubmit(db.Sale_BL_details.Where(d => d.bl_id == null));
                db.SubmitChanges();
            }
            catch (Exception ex) {
                return Json(new { suc = false, msg = ex.Message }, "text/html");
            }

            return Json(new { suc = true }, "text/html");
        }    
    }
}
