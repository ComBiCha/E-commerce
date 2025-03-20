namespace E_commerce.Repository
{
    public interface IOrderPrototype
    {
        OrderPrototype Clone(); // Phương thức tạo bản sao của đơn hàng
    }

    public class OrderPrototype
    {
        public int Id { get; set; }
        public string OrderCode { get; set; }
        public decimal ShippingCost { get; set; }
        public string Address { get; set; }
        public string UserName { get; set; }
        public DateTime CreatedDate { get; set; }
        public int Status { get; set; }
        public string PaymentIntentId { get; set; }

        public OrderPrototype(int id, string orderCode, decimal shippingCost, string address, string userName, DateTime createdDate, int status, string paymentIntentId)
        {
            Id = id;
            OrderCode = orderCode;
            ShippingCost = shippingCost;
            Address = address;
            UserName = userName;
            CreatedDate = createdDate;
            Status = status;
            PaymentIntentId = paymentIntentId;
        }

        public OrderPrototype Clone()
        {
            return (OrderPrototype)this.MemberwiseClone();
        }
    }


}
