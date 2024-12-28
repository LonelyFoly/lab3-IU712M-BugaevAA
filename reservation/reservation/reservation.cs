using System;

namespace reservation
{
    public class reservation
    {
        public int id { get; set; }
        public Guid reservationUid { get; set; }
        public string username { get; set; }
        public Guid paymentUid { get; set; }
        public Guid hotelUid { get; set; }
        public string status { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }

        public reservation()
        {

        }
    }
    public class reservationToDo
    {
        public Guid reservationUid { get; set; }
        public string username { get; set; }
        public Guid paymentUid { get; set; }
        public Guid hotelUid { get; set; }
        public string status { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate{ get; set; }

        public reservationToDo()
        {

        }
    }
}
