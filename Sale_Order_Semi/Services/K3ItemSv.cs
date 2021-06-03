using Sale_Order_Semi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sale_Order_Semi.Services
{
    public class K3ItemSv : BaseSv
    {
        //[192.168.100.202].[AIS20201119095524] 测试
        //[192.168.100.200].[AIS20070122151122] 半导体总部
        private string dbName = "[192.168.100.200].[AIS20070122151122]";

        public List<K3Emp> GetK3Emp(string empInfo)
        {
            string sql = "select top 50 FName as emp_name,FshortNumber as emp_card_number,Fnote as emp_dep ";
            sql += string.Format("from {0}.dbo.t_emp where FName like '%{1}%' or FshortNumber like '%{1}%'", dbName, empInfo);
            return db.ExecuteQuery<K3Emp>(sql).ToList();

            //return db.VwEmp.Where(v => v.name.Contains(empInfo) || v.cardId.Contains(empInfo)).Take(50)
            //    .Select(v => new K3Emp() { emp_card_number = v.cardId, emp_dep = v.dep, emp_name = v.name })
            //    .ToList();
        }

        public List<K3Customer> GetK3Customer(string customerInfo)
        {
            string sql = "select top 50 FName as customer_name,FNumber as customer_number ";
            sql += string.Format("from {0}.dbo.t_Organization where FName like '%{1}%' or FNumber like '{1}%'", dbName, customerInfo);

            return db.ExecuteQuery<K3Customer>(sql).ToList();

            //return db.getCostomer(customerInfo, 0).ToList().Take(50)
            //    .Select(g => new K3Customer() { customer_name = g.name, customer_number = g.number })
            //    .ToList();
        }

        //public K3CustomerInfo GetK3CustomerInfo(string customerNumber)
        //{
        //    return db.ExecuteQuery<K3CustomerInfo>("exec dbo.getK3CustomerInfo @customerNumber = {0}", customerNumber).FirstOrDefault();            
        //}

        public List<K3Product> GetK3ProductByInfo(string itemInfo)
        {
            string sql = @"select top 50 t1.FItemID as item_id,t1.Fmodel as item_model,t1.FName as item_name,t1.FNumber as item_no,
                            t2.FName as unit_name,t2.FNumber as unit_number ";
            sql += string.Format("from {0}.dbo.t_ICItem t1 left join {0}.dbo.t_MeasureUnit t2 on t1.FSaleUnitID = t2.fitemid ",dbName);
            sql += string.Format("inner join {0}.dbo.t_item t3 on t1.FItemID = t3.FItemID and t3.FDeleted = 0 ", dbName);
            sql += string.Format("where t1.FDeleted = 0 and (t1.FModel like '%{0}%' or t1.FName like '%{0}%' or t1.FNumber like '%{0}%')", itemInfo);

            return db.ExecuteQuery<K3Product>(sql).ToList();

            //return db.vwProductInfo.Where(v => v.item_model.Contains(itemInfo) || v.item_name.Contains(itemInfo) || v.item_no.StartsWith(itemInfo)).Take(50).ToList()
            //    .Select(v => new K3Product()
            //    {
            //        item_id = v.item_id,
            //        item_model = v.item_model,
            //        item_name = v.item_name,
            //        item_no = v.item_no,
            //        unit_name = v.unit_name,
            //        unit_number = v.unit_number
            //    }).ToList();
        }
        //读取k3基础资料
        public List<K3Items> GetK3Items(string what)
        {
            return db.vwItems.Where(v => v.what == what).ToList()
                .Select(v => new K3Items() { fid = v.fid, fname = v.fname, what = v.what })
                .ToList();
        }
        public decimal GetK3ExchangeRate(string currencyNo, string currencyName)
        {
            return db.ExecuteQuery<decimal>("exec dbo.getK3ExchangeRate @currencyNo={0},@currencyName={1}", currencyNo, currencyName).First();
        }
        public double? GetK3CommissionRate(string proType, double MU)
        {
            double? result = 0;
            db.getCommissionRate(proType, MU, ref result);
            return result;
        }

        //客户名和客户编码是否对应
        public bool IsCustomerNameAndNoMath(string customerName, string customerNumber)
        {
            string sql = string.Format("select count(1) from {0}.dbo.t_Organization where fname = '{1}' and fnumber = '{2}'", dbName, customerName, customerNumber);
            return db.ExecuteQuery<int>(sql).FirstOrDefault() > 0;
            //return (bool)db.isCustomerNameAndNoMath(customerName, customerNumber).First().suc;
        }

        //读取客户信息
        public CH_delivery_info GetK3CustomerInfo(string customerNo)
        {
            return db.ExecuteQuery<CH_delivery_info>("exec " + dbName + ".dbo.CH_GetCustomerInfo @customer_no = {0}", customerNo).FirstOrDefault();
        }

        //读取可以做出货的订单
        public List<CHEntrys> GetK3Order4CH(string billType, string customerNo, DateTime fromDate, DateTime toDate, string orderNo, string itemModel)
        {
            var entrys = db.ExecuteQuery<CHEntrys>("exec " + dbName + ".dbo.CH_GetSOs4Apply @bill_type = {0},@customer_no = {1}, @from_date = {2}, @to_date = {3}, @order_no = {4},@item_model = {5}",
                billType, customerNo, fromDate.ToString(), toDate.ToString(), orderNo, itemModel).ToList();
            entrys.ForEach(e => e.can_apply_qty = e.order_qty - e.relate_qty);

            return entrys;
        }

        //读取订单的关联数量和库存数量等
        public CHK3Qtys GetK3OrderQtys(string billType, int orderId,int orderEntryId)
        {
            var result = db.ExecuteQuery<CHK3Qtys>("exec " + dbName + ".dbo.CH_GetSOQtys @bill_type = {0},@order_id = {1}, @entry_id = {2}",
                billType, orderId, orderEntryId).First();
            result.can_apply_qty = result.order_qty - result.relate_qty;

            return result;
        }

        //后台导入到K3销售出库单
        public SimpleResultModel GenStockbill(string sysNo)
        {
            return db.ExecuteQuery<SimpleResultModel>("exec " + dbName + ".dbo.CH_GenBill @sys_no = {0}", sysNo).FirstOrDefault();
        }

        //更新回签日期到k3出库单
        public void UpdateStockbillSignDate(string stockNo, string day)
        {
            db.ExecuteCommand("exec " + dbName + ".dbo.CH_UpdateSignBackDate @stock_no = {0},@day = {1}", stockNo, day);
        }

        //读取客户对应的K3销售订单
        public List<K3SOModel> GetK3SOList(string billType, string customerNo, DateTime fromDate, DateTime toDate, string orderNo, string itemModel)
        {
            return db.ExecuteQuery<K3SOModel>("exec " + dbName + ".dbo.CH_GetOrderInfo @bill_type = {0},@customer_no = {1}, @from_date = {2}, @to_date = {3}, @order_no = {4},@item_model = {5}",
                billType, customerNo, fromDate.ToString(), toDate.ToString(), orderNo, itemModel).ToList();            
        }

        //读取销售订单行对应的出货记录
        public List<K3SOStockModel> GetK3SOStockDetail(string billType, int orderId, int entryId)
        {
            return db.ExecuteQuery<K3SOStockModel>("exec " + dbName + ".dbo.CH_GetStockInfo @bill_type = {0},@order_id = {1}, @entry_id = {2}", billType, orderId, entryId).ToList();
        }

    }
}