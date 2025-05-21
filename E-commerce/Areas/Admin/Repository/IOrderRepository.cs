using E_commerce.Models;
using E_commerce.Models.ViewModel;

namespace E_commerce.Repository
{
    public interface IOrderRepository
    {
        Task<List<OrderModel>> GetAllOrdersAsync(int page = 1, int pageSize = 10, string sortBy = "CreatedDate", bool ascending = false);
        Task<OrderModel> GetOrderByCodeAsync(string orderCode);
        Task UpdateOrderAsync(OrderModel order);
        Task DeleteOrderAsync(string orderCode);
        Task<int> CloneOrderAsync(string orderCode);
        Task<List<OrderDetails>> GetOrderDetailsAsync(string orderCode);
        Task<decimal> CalculateProductTotalAsync(string orderCode);
        Task AddOrderAsync(OrderModel order);
        Task AddOrderDetailsAsync(List<OrderDetails> orderDetails);
        Task<decimal> GetUserDiscountRateAsync(string email);
        Task<OrderModel> GetOrderByIdAsync(int id);
        Task<List<OrderDetails>> GetOrderDetailsByOrderCodeAsync(string orderCode);
        Task UpdateOrderWithDetailsAsync(EditOrderViewModel model);
    }
}