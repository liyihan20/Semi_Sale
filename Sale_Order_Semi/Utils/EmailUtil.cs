using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sale_Order_Semi.Utils
{
    public class EmailUtil
    {//半导体的邮箱
        public bool SendEmail(string content, string emailAddress, string ccEmail = null, string subject = "订单申请审批")
        {
            return TrulyEmail.EmailUtil.SemiSend("CRM客户管理平台", subject + "（半导体）", content, emailAddress, ccEmail);
        }
    }
}