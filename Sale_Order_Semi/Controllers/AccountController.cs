using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using Sale_Order_Semi.Models;
using System.IO;
using Sale_Order_Semi.Utils;

namespace Sale_Order_Semi.Controllers
{
    
    public class AccountController : BaseController
    {
        SomeUtils utl = new SomeUtils();

        [AllowAnonymous]
        public ActionResult Login(string step,string applyId)
        {
            return View();
        }
        

        public ActionResult LogOut()
        {   
            var cookie = Request.Cookies["order_semi_cookie"];
            if (cookie != null) {
                utl.writeEventLog("登录模块", "登出系统", "", Request);
                cookie.Expires = DateTime.Now.AddSeconds(-1);
                Response.AppendCookie(cookie);
                Session.Clear();
            }
            return RedirectToAction("Login");
        }

        //从电子CRM跳转过来的url
        public ActionResult DirectFromEle(string userName, string code, string url)
        {
            if (!code.Equals(utl.getMD5(userName))) {
                ViewBag.tip = "redirect error";
                return View("TIP");
            }

            var user = db.User.Where(u => u.job == userName || u.username == userName).FirstOrDefault();
            if (user == null) {
                ViewBag.tip = "你在半导体CRM没有用户，请先联系市场管理部注册用户";
                return View("TIP");
            }

            //构造cookie
            Session.Clear();
            var cookie = new HttpCookie("order_semi_cookie");
            cookie.Expires = DateTime.Now.AddHours(12);
            cookie.Values.Add("userid", user.id.ToString());
            cookie.Values.Add("code", utl.getMD5(user.id.ToString()));
            cookie.Values.Add("username", utl.EncodeToUTF8(user.username));//用于记录日志
            cookie.Values.Add("cop", "semi");
            Response.AppendCookie(cookie);

            return Redirect(url);
        }
        
    }
}
