using E_commerce.Models;

namespace E_commerce.Repository
{
    public interface IOrderBuilder
    {
        IOrderBuilder SetId(int id);
        IOrderBuilder SetOrderCode(string orderCode);
        IOrderBuilder SetShippingCost(decimal shippingCost);
        IOrderBuilder SetAddress(string address);
        IOrderBuilder SetUserName(string userName);
        IOrderBuilder SetCreatedDate(DateTime createdDate);
        IOrderBuilder SetStatus(int status);
        IOrderBuilder SetPaymentIntentId(string paymentIntentId);
        OrderModel Build();
    }
}
