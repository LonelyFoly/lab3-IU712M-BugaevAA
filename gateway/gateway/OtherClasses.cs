namespace gateway
{
    public class loyalty
    {
        public int id { get; set; }
        public string username { get; set; }
        public int reservationCount { get; set; }
        public string status { get; set; }
        public int discount { get; set; }

        public loyalty()
        {

        }
    }
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
        public reservation(
            Guid reservationUid_,
        string username_, Guid paymentUid_, Guid hotelUid_,
        string status_, DateTime startDate_, DateTime endDate_
            )
        {
            reservationUid = reservationUid_;
            this.username = username_;
            paymentUid = paymentUid_;
            hotelUid = hotelUid_;
            this.status = status_;
            startDate = startDate_;
            endDate = endDate_;
        }
    }
    public class payment
    {
        public int id { get; set; }
        public Guid paymentUid { get; set; }
        public string status { get; set; }
        public int price { get; set; }

        public payment()
        {

        }
    }
    public class DateForm
    {
        public Guid hotelUid { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
        public DateForm() { }
    }
    public class PaymentToDo
    {
        public Guid paymentUid { get; set; }
        public int price { get; set; }
        public PaymentToDo() { }
        public PaymentToDo(Guid _paymentUid, int _price) {
            paymentUid = _paymentUid;
            price = _price;
        }
    }
    public class ReservationToDo
    {
        public Guid reservationUid { get; set; }
        public string username { get; set; }
        public Guid paymentUid { get; set; }
        public int hotelUid { get; set; }
        public string status { get; set; }
        public DateTime startDate { get; set; }
        public DateTime endDate { get; set; }
        public ReservationToDo( 
            Guid reservationUid_,
        string username_,Guid paymentUid_, int hotelId_,
        string status_, DateTime startDate_, DateTime endDate_
            )
        {
            reservationUid = reservationUid_;
            this.username = username_;
            paymentUid = paymentUid_;
            hotelUid = hotelId_;
            this.status = status_;
            startDate = startDate_;
            endDate = endDate_;
        }
    }
    
}
