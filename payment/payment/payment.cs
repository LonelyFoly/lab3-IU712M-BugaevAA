namespace payment
{
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
    public class PaymentRequestDto
    {
        public Guid paymentUid { get; set; }
        public int price { get; set; }
    }
}
