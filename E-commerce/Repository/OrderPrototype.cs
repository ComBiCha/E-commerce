namespace E_commerce.Repository
{
    public interface IOrderPrototype
    {
        OrderPrototype Clone(); // Phương thức tạo bản sao của đơn hàng
    }

    public class OrderPrototype
    {
        public int Id { get; }
        public string OrderCode { get; }
        public string UserName { get; }
        public DateTime CreatedDate { get; }
        public int Status { get; }

        public OrderPrototype(int id, string orderCode, string userName, DateTime createdDate, int status)
        {
            Id = id;
            OrderCode = orderCode;
            UserName = userName;
            CreatedDate = createdDate;
            Status = status;
        }

        public OrderPrototype Clone()
        {
            return new OrderPrototype(Id, OrderCode, UserName, CreatedDate, Status);
        }
    }

}
