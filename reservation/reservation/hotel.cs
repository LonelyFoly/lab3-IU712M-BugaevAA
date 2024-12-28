using System;

namespace reservation
{
    public class hotel
    {
        public int id { get; set; }
        public Guid hotelUid { get; set; }
        public string name { get; set; }
        public string country { get; set; }
        public string city { get; set; }
        public string address { get; set; }
        public int stars { get; set; }
        public int price { get; set; }
        public hotel()
        {
                 
        }

    }
}
