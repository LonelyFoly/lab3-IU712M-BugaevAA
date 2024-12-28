
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reservation.DB
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
        public hotel[] getHotels(int page, int size)
        {
            using (ApplicationContext db = getDb())
            {
                var Hotels = db.hotels.ToList();
                //Console.WriteLine(Hotels.Count());
                List<hotel> hotels = new List<hotel>();
                for (int i = (page-1)*size;
                    (i<Hotels.Count() && i<page*size);i++)
                {
                        hotel u = Hotels[i];
                        Console.WriteLine(u.hotelUid);
                        hotels.Add(u);
                }
                return hotels.ToArray();
            }
        }
        
        public reservation[] getReservationsByUsername(string username)
        {
            using (ApplicationContext db = getDb())
            {
                var Reservations = db.reservation.ToList();

                List<reservation> reservations = new List<reservation>();
                foreach (reservation res in Reservations)
                {
                    Console.WriteLine("Res.Username: "+res.username);
                    if (res.username == username)
                        reservations.Add(res);
                }
                return reservations.ToArray();
            }
        }
        public reservation getReservationsByUsernameAndUid(Guid reservationUid, string username)
        {
            using (ApplicationContext db = getDb())
            {
                var Reservations = db.reservation.ToList();

                reservation reservation = new reservation();
                reservation.username = "";
                foreach (reservation res in Reservations)
                {
                    //Console.WriteLine("Res.Username: " + res.username);
                    if (res.username == username && res.reservationUid==reservationUid)
                        reservation = res;
                }
                return reservation;
            }
        }
        public hotel checkHotel(Guid hotelUid)
        {
            using (ApplicationContext db = getDb())
            {
                var Hotels = db.hotels.ToList();

                foreach (hotel h in Hotels)
                {
                    //Console.WriteLine("Res.Username: " + res.username);
                    if (h.hotelUid == hotelUid)
                        return h;
                }
                return null;
            }
        }
        public reservation cancelReservation(Guid reservationUid, string username)
        {
            
            using (ApplicationContext db = getDb())
            {
                var Reservations = db.reservation.ToList();

                foreach (reservation res in Reservations)
                {
                    //Console.WriteLine("Res.Username: " + res.username);
                    if (res.reservationUid == reservationUid 
                        && res.username == username)

                    {
                        res.status = "CANCELED";
                        db.reservation.Update(res);
                        db.SaveChanges();
                        return res;
                    }
                }
                return null;
            }
        }
        public void PostReservation(reservation res_, string username)
        {
            using (ApplicationContext db = getDb())
            {
                var Reservations = db.reservation.ToList();
                int maxId = 0;
                foreach (reservation res in Reservations)
                {
                    //Console.WriteLine("Res.Username: " + res.username);
                    if (maxId< res.id)

                    {
                        maxId = res.id;
                    }
                }
                reservation new_res = new reservation();
                new_res.startDate = res_.startDate.ToUniversalTime();
                new_res.endDate = res_.endDate.ToUniversalTime();
                new_res.status = res_.status;
                new_res.id = maxId + 1;
                new_res.hotelUid = res_.hotelUid;
                new_res.paymentUid = res_.paymentUid;
                new_res.reservationUid = res_.reservationUid;
                new_res.username = username;

                db.reservation.Add(new_res);
                db.SaveChanges();
            }
        }
        //метод для обращеиня к loyalty для получения инфы о пользователе




    }
}

