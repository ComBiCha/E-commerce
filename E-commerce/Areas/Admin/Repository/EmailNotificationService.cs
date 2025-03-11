using E_commerce.Models;
using System.Threading.Tasks;

namespace E_commerce.Areas.Admin.Repository
{
	public class EmailNotificationService : IOrderObserver
	{
		private readonly IEmailSender _emailSender;

		public EmailNotificationService(IEmailSender emailSender)
		{
			_emailSender = emailSender;
		}

		public async Task Notify(OrderModel order)
		{
			string subject = $"Order {order.OrderCode} Status Updated";

			string statusText = order.Status switch
			{
				1 => "Đơn hàng mới",
				2 => "Đã xác nhận",
				3 => "Đang giao hàng",
				4 => "Đã giao hàng",
				5 => "Hoàn thành",
				6 => "Đã bị hủy",
				_ => "Không xác định"
			};

			string message = $"Your order with code {order.OrderCode} has been updated to status: {statusText}.";

			await _emailSender.SendEmailAsync(order.UserName, subject, message);
		}

	}
}
