
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace loyalty.DB
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
        public loyalty getLoyalty(string username)
        {
            using (ApplicationContext db = getDb())
            {
                // получаем объекты из бд и выводим на консоль
                var Loyalties = db.loyalty.ToList();
                //Console.WriteLine("Persons list:");

                foreach (loyalty u in Loyalties)
                {
                    if (u.username == username)
                    {
                        Console.WriteLine($"=====: {u.username}");
                        return u;
                    }
                }
                return null;
            }
        }
        public loyalty incLoyalty(string username)
        {
            using (ApplicationContext db = getDb())
            {
                // получаем объекты из бд и выводим на консоль
                var Loyalties = db.loyalty.ToList();
                //Console.WriteLine("Persons list:");
                loyalty _;
                foreach (loyalty u in Loyalties)
                {
                    if (u.username == username)
                    {
                        _ = u;
                        _.reservationCount++;

                        if (_.reservationCount >= 20)
                        {
                            _.status = "GOLD";
                            _.discount = 10;
                        }
                        else if (_.reservationCount >= 10)
                        {
                            _.status = "SILVER";
                            _.discount = 7;
                        }

                        db.loyalty.Update(_);
                        db.SaveChanges();
                        return _;
                    }
                }
                
                return null;
            }
        }
        //add check for PAID status
        public loyalty decLoyalty(string username)
        {
            using (ApplicationContext db = getDb())
            {
                // получаем объекты из бд и выводим на консоль
                var Loyalties = db.loyalty.ToList();
                //Console.WriteLine("Persons list:");
                loyalty _;
                Console.WriteLine("UsernameReal: " + username);
                foreach (loyalty u in Loyalties)
                {
                    Console.WriteLine("Loy_username: "+u.username);
                    if (u.username == username)
                    {
                        Console.WriteLine(u.username);
                        
                        _ = u;
                        Console.WriteLine(_.reservationCount);
                        _.reservationCount--;
                        Console.WriteLine(_.reservationCount);
                        if (_.reservationCount >= 20)
                        {
                            _.status = "GOLD";
                            _.discount = 10;
                        }
                        else if (_.reservationCount >= 10)
                        {
                            _.status = "SILVER";
                            _.discount = 7;
                        }
                        else if(_.reservationCount < 10)
                        {
                            _.status = "BRONZE";
                            _.discount = 5;
                        }

                        db.loyalty.Update(_);
                        db.SaveChanges();
                        return _;
                    }
                }

                return null;
            }
        }





    }
}

