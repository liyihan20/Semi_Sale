﻿using Sale_Order_Semi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Sale_Order_Semi.Controllers
{
    public class BaseController : Controller
    {
        public SaleDBDataContext db;
        private UserInfo _currentUser;

        public BaseController()
        {
            db = new SaleDBDataContext();
        }

        public UserInfo currentUser
        {
            get
            {
                _currentUser = (UserInfo)Session["currentUser"];
                if (_currentUser == null) {
                    int userId = Int32.Parse(Request.Cookies["order_semi_cookie"]["userid"]);
                    var user = db.User.Where(u => u.id == userId).FirstOrDefault();
                    if (user != null) {
                        _currentUser = new UserInfo()
                        {
                            userId = userId,
                            userName = user.username,
                            realName = user.real_name,
                            departmentName = user.Department1.name,
                            email = user.email
                        };
                        Session["currentUser"] = _currentUser;
                    }
                }
                return _currentUser;
            }
        }

        public string GetIPAddr()
        {
            return Request.UserHostAddress;
        }

        public void Wlog(string tag, string log, string sysNo = "", int unusual = 0)
        {
            db.EventLog.InsertOnSubmit(new EventLog()
            {
                sysNum = sysNo,
                username = currentUser.realName,
                model = tag,
                ip = GetIPAddr(),
                @event = log,
                op_time = DateTime.Now,
                unusual = unusual
            });
            db.SubmitChanges();
        }

    }
}
