
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace payment.DB
{
    public class dbHandler
    {
        DbContextOptions<ApplicationContext> options;
        public dbHandler(DbContextOptions<ApplicationContext> _option) 
        {
            options = _option;
        }
        public dbHandler()
        {
            options = null;
        }


        public ApplicationContext getDb()
        {
             return new ApplicationContext();
        }
        public void addPayment(Guid paymentUid, int price)
        {
            using (ApplicationContext db = getDb())
            {
                int maxId = 0;
                var Payments = db.payment.ToList();
                //Console.WriteLine("Persons list:");

                foreach (payment u in Payments)
                {
                    if (u.id > maxId)
                         maxId = u.id;
                }
                payment _ = new payment();
                _.id = maxId+1;
                _.paymentUid = paymentUid;
                _.price = price;
                _.status = "PAID";

                db.payment.Add(_);
                db.SaveChanges();
            }
        }
        //add check for PAID status
        public payment cancelPayment(Guid paymentUid)
        {
            using (ApplicationContext db = getDb())
            {
                var Payments = db.payment.ToList();

                foreach (payment u in Payments)
                {
                    if (u.paymentUid == paymentUid)
                    {
                        u.status = "CANCELED";
                        db.payment.Update(u);
                        db.SaveChanges();
                        return u;
                    }
                }
                return null;
            }
        }
        public bool deletePayment(string paymentUid)
        {
            using (ApplicationContext db = getDb())
            {
                var Payments = db.payment.ToList();
                payment payment = null;
                foreach (payment u in Payments)
                {
                    if (u.paymentUid.ToString() == paymentUid)
                        payment = u;
                }

                if (payment != null)
                {
                    db.payment.Remove(payment);
                    db.SaveChanges();
                }

                return payment != null;
            }

        }

        public payment getPayment(Guid paymentUid)
        {
            using (ApplicationContext db = getDb())
            {
                var Payments = db.payment.ToList();

                foreach (payment u in Payments)
                {
                    if (u.paymentUid == paymentUid)
                    {
                       
                        return u;
                    }
                }
                return null;
            }
        }





    }
}

