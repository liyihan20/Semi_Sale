using Sale_Order_Semi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Sale_Order_Semi.Services
{
    public class K3ItemSv : BaseSv
    {        

        public List<K3Emp> GetK3Emp(string empInfo)
        {
            return db.VwEmp.Where(v => v.name.Contains(empInfo) || v.cardId.Contains(empInfo))
                .Select(v => new K3Emp() { emp_card_number = v.cardId, emp_dep = v.dep, emp_name = v.name })
                .ToList();
        }

        public List<K3Customer> GetK3Customer(string customerInfo)
        {
            return db.getCostomer(customerInfo, 0).ToList()
                .Select(g => new K3Customer() { customer_name = g.name, customer_number = g.number })
                .ToList();
        }

        public K3CustomerInfo GetK3CustomerInfo(string customerNumber)
        {
            return db.ExecuteQuery<K3CustomerInfo>("exec dbo.getK3CustomerInfo @customerNumber = {0}", customerNumber).FirstOrDefault();            
        }

        public List<K3Product> GetK3ProductByInfo(string itemInfo)
        {
            return db.vwProductInfo.Where(v => v.item_model.Contains(itemInfo) || v.item_name.Contains(itemInfo) || v.item_no.StartsWith(itemInfo)).ToList()
                .Select(v => new K3Product()
                {
                    item_id = v.item_id,
                    item_model = v.item_model,
                    item_name = v.item_name,
                    item_no = v.item_no,
                    unit_name = v.unit_name,
                    unit_number = v.unit_number
                }).ToList();
        }
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

        public bool IsCustomerNameAndNoMath(string customerName, string customerNumber)
        {
            return (bool)db.isCustomerNameAndNoMath(customerName, customerNumber).First().suc;
        }

    }
}