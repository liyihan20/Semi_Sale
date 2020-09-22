using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Sale_Order_Semi.Models;
using Sale_Order_Semi.Filter;
using System.Configuration;
using System.IO;
using Sale_Order_Semi.Utils;

namespace Sale_Order_Semi.Controllers
{
    [SessionTimeOutFilter()]
    public class HomeController : BaseController
    {
        SomeUtils utl = new SomeUtils();

        public ActionResult Main(string url) {

            var powers = (from a in db.Authority
                      from u in db.Group
                      from ga in a.GroupAndAuth
                      from gu in u.GroupAndUser
                          where ga.group_id == u.id && gu.user_id == currentUser.userId
                      select a.sname).ToArray();
            ViewData["url"] = string.IsNullOrEmpty(url) ? "" : utl.MyUrlDecoder(url);
            ViewData["powers"] = powers;
            ViewData["username"] = currentUser.realName;
            ViewData["depName"] = currentUser.departmentName;
            return View();
        }

        public ActionResult Index()
        {
            ViewBag.Message = "Modify this template to kick-start your ASP.NET MVC application." ;
            
            return View();
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your app description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }


        public ActionResult ChangeLang(string lang)
        {
            /*记录语言设置到cookies*/
            HttpCookie cookie = new HttpCookie("CoolCode_Lang", lang);
            cookie.Expires = DateTime.Now.AddMonths(1);
            Response.AppendCookie(cookie);
            /*重定向到上一个Action*/
            return new RedirectResult(this.Request.ServerVariables["HTTP_REFERER"]);
            // return RedirectToAction("Index");
        }

        //下载Google浏览器
        public FileStreamResult DownloadChrome()
        {
            string fileName = "Chrome.rar";
            string absoluFilePath = ConfigurationManager.AppSettings["AttachmentPath1"] + fileName;
            FileInfo info = new FileInfo(absoluFilePath);
            if (!info.Exists)
            {                
                return null;
            }
            return File(new FileStream(absoluFilePath, FileMode.Open), "application/octet-stream", Server.UrlEncode(fileName));
        }

        //public ActionResult moveFile() {
        //    foreach (var no in (from ap in db.Apply.Where(a => a.success == true) select ap.sys_no)) {
        //        SomeUtils.moveToFormalDir(no);
        //    }            
        //    return View("tip");
        //}

        public ActionResult testEmail() {
            if (MyEmail.SendEmail("This is a test", "liyihan.ic@truly.com.cn"))
                ViewBag.tip = "ok";
            else
                ViewBag.tip = "no...";
            return View("tip");
        }       
                
        //保存错误信息
        public JsonResult WriteDownErrors(string message)
        {
            var err=new SystemErrors();
            err.user_name = currentUser.realName;
            err.exception=message;
            err.op_time = DateTime.Now;
            db.SystemErrors.InsertOnSubmit(err);
            db.SubmitChanges();
            return Json("");
        }        

        public string SetFileFlag(string fr, string to)
        {
            DateTime fromDate = DateTime.Parse(fr);
            DateTime toDate = DateTime.Parse(to);
            var sysNos = (from a in db.Apply
                          where a.start_date >= fromDate
                          && a.finish_date < toDate
                          && a.success == true
                          && (a.order_type == "SO"
                          || a.order_type == "TH")
                          select a.sys_no).ToArray();
            foreach (string no in sysNos)
            {
                var absoluFilePath = Path.Combine(SomeUtils.getOrderPath(no), no + ".rar");
                var info = new FileInfo(absoluFilePath);
                if (info.Exists)
                {
                    if (db.HasAttachment.Where(h => h.sys_no == no).Count() < 1)
                    {
                        var att = new HasAttachment();
                        att.sys_no = no;
                        db.HasAttachment.InsertOnSubmit(att);
                    }
                }
            }
            try
            {
                db.SubmitChanges();
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            return "ok";
        }

        public bool test() {
            return MyEmail.SendBackToSaler(true,"CM20160514003S","liyihan.ic@truly.com.cn","开模改模单","新增",null,"ABCDEFG","ssslll");
        }
                

    }
}
