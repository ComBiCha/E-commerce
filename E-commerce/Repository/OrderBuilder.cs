namespace E_commerce.Areas.Admin.Repository
{
    using System;
    using E_commerce.Models;
    using E_commerce.Repository;

    public class OrderBuilder : IOrderBuilder
    {
        private OrderModel _order = new OrderModel();

        public IOrderBuilder SetId(int id)
        {
            _order.Id = id;
            return this;
        }

        public IOrderBuilder SetOrderCode(string orderCode)
        {
            _order.OrderCode = orderCode;
            return this;
        }

        public IOrderBuilder SetShippingCost(decimal shippingCost)
        {
            _order.ShippingCost = shippingCost;
            return this;
        }

        public IOrderBuilder SetAddress(string address)
        {
            _order.Address = address;
            return this;
        }

        public IOrderBuilder SetUserName(string userName)
        {
            _order.UserName = userName;
            return this;
        }

        public IOrderBuilder SetCreatedDate(DateTime createdDate)
        {
            _order.CreatedDate = createdDate;
            return this;
        }

        public IOrderBuilder SetStatus(int status)
        {
            _order.Status = status;
            return this;
        }

        public IOrderBuilder SetPaymentIntentId(string paymentIntentId)
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
