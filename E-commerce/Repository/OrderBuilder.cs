namespace E_commerce.Models
{
    public class OrderBuilder
    {
        private readonly OrderModel _order = new();

        public OrderBuilder SetOrderCode(string orderCode)
        {
            _order.OrderCode = orderCode;
            return this;
        }

        public OrderBuilder SetShippingCost(decimal cost)
        {
            _order.ShippingCost = cost;
            return this;
        }

        public OrderBuilder SetAddress(string address)
        {
            _order.Address = address;
            return this;
        }

        public OrderBuilder SetUserName(string userName)
        {
            _order.UserName = userName;
            return this;
        }

        public OrderBuilder SetCreatedDate(DateTime createdDate)
        {
            _order.CreatedDate = createdDate;
            return this;
        }

        public OrderBuilder SetStatus(int status)
        {
            _order.Status = status;
            return this;
        }

        public OrderBuilder SetPaymentIntentId(string paymentIntentId)
        {
            _order.PaymentIntentId = paymentIntentId;
            return this;
        }

        public OrderModel Build()
        {
            return _order;
        }
    }
}
