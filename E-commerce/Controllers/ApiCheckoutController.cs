using E_commerce.Areas.Admin.Repository;
using E_commerce.Models;
using E_commerce.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using PayPal.Api;
using System.Text.Json;

namespace E_commerce.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ApiCheckoutController : ControllerBase
    {
        private readonly DataContext _datacontext;
        private readonly IEmailSender _emailSender;

        public ApiCheckoutController(DataContext context, IEmailSender emailSender)
        {
            _datacontext = context;
            _emailSender = emailSender;
        }

        [HttpPost("Checkout")]
        public async Task<IActionResult> Checkout([FromBody] CheckoutApiRequest request)
        {
            try
            {
                // Validate request
                if (request == null || !request.CartItems.Any())
                {
                    return BadRequest(new { success = false, error = "Invalid request or empty cart" });
                }

                // Validate user email
                if (string.IsNullOrEmpty(request.UserEmail))
                {
                    return BadRequest(new { success = false, error = "User email is required" });
                }

                // 🔹 VALIDATE SHIPPING ADDRESS
                if (request.ShippingAddress == null ||
                    string.IsNullOrEmpty(request.ShippingAddress.City) ||
                    string.IsNullOrEmpty(request.ShippingAddress.District) ||
                    string.IsNullOrEmpty(request.ShippingAddress.Ward) ||
                    string.IsNullOrEmpty(request.ShippingAddress.DetailAddress))
                {
                    return BadRequest(new { success = false, error = "Complete shipping address is required" });
                }

                // 🔹 LẤY THÔNG TIN USER ĐỂ TÍNH DISCOUNT MEMBERSHIP
                var user = await _datacontext.Users.FirstOrDefaultAsync(u => u.Email == request.UserEmail);
                decimal membershipDiscountRate = user?.GetDiscountRate() ?? 0m;

                decimal grandTotal = (decimal)request.CartItems.Sum(x => x.Quantity * x.Price);
                decimal membershipDiscount = grandTotal * membershipDiscountRate;
                decimal couponDiscount = (decimal)request.DiscountAmount;
                decimal totalDiscount = membershipDiscount + couponDiscount;

                Console.WriteLine($"Grand Total: ${grandTotal}");
                Console.WriteLine($"Membership Discount ({membershipDiscountRate * 100}%): ${membershipDiscount}");
                Console.WriteLine($"Coupon Discount: ${couponDiscount}");
                Console.WriteLine($"Total Discount: ${totalDiscount}");

                // 🔹 TẠO ORDER CODE NHƯNG CHƯA LƯU VÀO DATABASE
                var orderCode = Guid.NewGuid().ToString().Substring(0, 10).ToUpper();

                // Convert CartItems từ API request
                var cartItems = request.CartItems.Select(item => new CartItemModel
                {
                    ProductId = item.ProductId,
                    VariationId = item.VariationId,
                    ProductName = item.ProductName,
                    Price = (decimal)item.Price,
                    Quantity = item.Quantity,
                    Image = item.ImageUrl
                }).ToList();

                Console.WriteLine($"Mobile API - CartItems count: {cartItems.Count}");
                Console.WriteLine($"Mobile API - Payment method: {request.PaymentMethod}");
                Console.WriteLine($"Mobile API - User email: {request.UserEmail}");

                // 🔹 LƯU CART ITEMS VÀ DISCOUNT INFO VÀO CACHE TRƯỚC KHI THANH TOÁN
                await SaveOrderCartItems(orderCode, cartItems, (decimal)request.ShippingPrice, membershipDiscount, couponDiscount, request.ShippingAddress, request.CouponCode, request.UserEmail);

                // Xử lý thanh toán theo phương thức
                string redirectUrl;

                if (request.PaymentMethod.ToLower() == "cod")
                {
                    // 🔹 COD - TẠO ORDER NGAY LẬP TỨC (vì đã confirmed)
                    var order = new OrderModel
                    {
                        OrderCode = orderCode,
                        CreatedDate = DateTime.Now,
                        UserName = request.UserEmail,
                        Status = 1, // Chờ xác nhận
                        Address = request.ShippingAddress.GetFullAddress(),
                        ShippingCost = (decimal)request.ShippingPrice,
                        PaymentIntentId = "COD"
                    };

                    _datacontext.Orders.Add(order);
                    await _datacontext.SaveChangesAsync();

                    // Lưu order details với cả 2 loại discount
                    await ProcessOrderDetails(orderCode, cartItems, request.UserEmail, membershipDiscount, couponDiscount, request.CouponCode);

                    // Gửi email xác nhận cho COD
                    await SendOrderConfirmationEmail(orderCode, cartItems, request.UserEmail, (decimal)request.ShippingPrice, membershipDiscount, couponDiscount, request.ShippingAddress);

                    // Xóa cache sau khi xử lý COD
                    await RemoveOrderCartItems(orderCode);

                    redirectUrl = $"/mobile/order-success?orderCode={orderCode}";
                }
                else
                {
                    // 🔹 STRIPE/PAYPAL - CHỈ TẠO PAYMENT URL, KHÔNG TẠO ORDER
                    if (request.PaymentMethod.ToLower() == "stripe")
                    {
                        redirectUrl = await CreateStripePaymentSession(orderCode, cartItems, (decimal)request.ShippingPrice, totalDiscount);
                    }
                    else if (request.PaymentMethod.ToLower() == "paypal")
                    {
                        redirectUrl = await CreatePayPalPayment(orderCode, cartItems, (decimal)request.ShippingPrice, totalDiscount);
                    }
                    else
                    {
                        throw new NotImplementedException($"Payment method {request.PaymentMethod} not implemented for mobile");
                    }
                }

                return Ok(new
                {
                    success = true,
                    redirectUrl = redirectUrl,
                    orderCode = orderCode,
                    paymentMethod = request.PaymentMethod,
                    shippingAddress = request.ShippingAddress.GetFullAddress(),
                    membershipDiscount = membershipDiscount,
                    couponDiscount = couponDiscount,
                    totalDiscount = totalDiscount
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Mobile checkout error: {ex.Message}");
                return BadRequest(new { success = false, error = ex.Message });
            }
        }

        // 🔹 TẠO STRIPE SESSION NHƯNG KHÔNG TẠO ORDER
        private async Task<string> CreateStripePaymentSession(string orderCode, List<CartItemModel> cartItems, decimal shippingPrice, decimal discountAmount)
        {
            var domain = "http://localhost:5139/";

            var options = new Stripe.Checkout.SessionCreateOptions
            {
                SuccessUrl = domain + $"api/ApiCheckout/MobilePaymentSuccess?session_id={{CHECKOUT_SESSION_ID}}&orderCode={orderCode}",
                CancelUrl = domain + $"api/ApiCheckout/MobilePaymentCancel?orderCode={orderCode}",
                LineItems = new List<Stripe.Checkout.SessionLineItemOptions>(),
                Mode = "payment",
                Metadata = new Dictionary<string, string>
                {
                    { "ordercode", orderCode },
                    { "mobile", "true" }
                }
            };

            foreach (var cart in cartItems)
            {
                options.LineItems.Add(new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(cart.Price * 100),
                        Currency = "usd",
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = cart.ProductName
                        }
                    },
                    Quantity = cart.Quantity
                });
            }

            if (shippingPrice > 0)
            {
                options.LineItems.Add(new Stripe.Checkout.SessionLineItemOptions
                {
                    PriceData = new Stripe.Checkout.SessionLineItemPriceDataOptions
                    {
                        UnitAmount = (long)(shippingPrice * 100),
                        Currency = "usd",
                        ProductData = new Stripe.Checkout.SessionLineItemPriceDataProductDataOptions
                        {
                            Name = "Shipping Fee"
                        }
                    },
                    Quantity = 1
                });
            }

            if (discountAmount > 0)
            {
                var couponService = new Stripe.CouponService();
                var coupon = couponService.Create(new Stripe.CouponCreateOptions
                {
                    AmountOff = (long)(discountAmount * 100),
                    Currency = "usd",
                    Duration = "once"
                });

                options.Discounts = new List<Stripe.Checkout.SessionDiscountOptions>
                {
                    new Stripe.Checkout.SessionDiscountOptions { Coupon = coupon.Id }
                };
            }

            var service = new Stripe.Checkout.SessionService();
            var session = service.Create(options);

            return session.Url;
        }

        private async Task<string> CreatePayPalPayment(string orderCode, List<CartItemModel> cartItems, decimal shippingPrice, decimal discountAmount)
        {
            try
            {
                decimal totalAmount = cartItems.Sum(x => x.Quantity * x.Price) + shippingPrice - discountAmount;
                var domain = "http://localhost:5139/";

                Console.WriteLine($"🔹 PayPal HTTP Payment Debug:");
                Console.WriteLine($"   - Order Code: {orderCode}");
                Console.WriteLine($"   - Total Amount: ${totalAmount:F2}");

                if (totalAmount <= 0.01m)
                {
                    throw new Exception($"Amount too small for PayPal: {totalAmount}");
                }

                using var httpClient = new HttpClient();

                // 🔹 BƯỚC 1: LẤY ACCESS TOKEN
                var clientId = "Ad7D6abQz4m4Ja4g-VxgwDZK_BkSgyjpmwQGjK7Yu_IOfsN2dhbRDMQ4qJmWYdxvcGK1IVK1TLg2qFZo";
                var clientSecret = "ELfi3XeppHttGOyn5NSnh4n9L07FYbOknmJR46PeeppLHWfXVN2UE93uDUyFjSuK1ZVKMI-0_ZZ_jf73";

                var tokenRequest = new FormUrlEncodedContent(new[]
                {
            new KeyValuePair<string, string>("grant_type", "client_credentials")
        });

                var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

                Console.WriteLine($"🔹 Getting PayPal access token...");
                var tokenResponse = await httpClient.PostAsync("https://api.sandbox.paypal.com/v1/oauth2/token", tokenRequest);
                var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Failed to get PayPal token: {tokenContent}");
                }

                var tokenData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(tokenContent);
                var accessToken = tokenData.GetProperty("access_token").GetString();
                Console.WriteLine($"🔹 Access token obtained: {accessToken?.Substring(0, 20)}...");

                // 🔹 BƯỚC 2: TẠO PAYMENT VỚI URL ENCODING FIX
                httpClient.DefaultRequestHeaders.Clear();
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                // 🔹 SỬA: ENCODE URL ĐÚNG CÁCH, KHÔNG DÙNG TEMPLATE PLACEHOLDERS
                var returnUrl = $"{domain}api/ApiCheckout/MobilePaymentSuccess?orderCode={orderCode}";
                var cancelUrl = $"{domain}api/ApiCheckout/MobilePaymentCancel?orderCode={orderCode}";

                Console.WriteLine($"🔹 Return URL: {returnUrl}");
                Console.WriteLine($"🔹 Cancel URL: {cancelUrl}");

                var paymentData = new
                {
                    intent = "sale",
                    payer = new
                    {
                        payment_method = "paypal"
                    },
                    transactions = new[]
                    {
                new
                {
                    amount = new
                    {
                        total = totalAmount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                        currency = "USD"
                    },
                    description = $"Mobile Order {orderCode}"
                }
            },
                    redirect_urls = new
                    {
                        return_url = returnUrl,
                        cancel_url = cancelUrl
                    }
                };

                // 🔹 SỬA: SỬ DỤNG JsonSerializerOptions ĐỂ TRÁNH UNICODE ENCODING
                var paymentJson = System.Text.Json.JsonSerializer.Serialize(paymentData, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping // 🔹 QUAN TRỌNG!
                });
                Console.WriteLine($"🔹 Payment JSON Request:");
                Console.WriteLine(paymentJson);

                var paymentRequest = new StringContent(paymentJson, Encoding.UTF8, "application/json");

                Console.WriteLine($"🔹 Sending payment request to PayPal...");
                var paymentResponse = await httpClient.PostAsync("https://api.sandbox.paypal.com/v1/payments/payment", paymentRequest);
                var paymentContent = await paymentResponse.Content.ReadAsStringAsync();

                Console.WriteLine($"🔹 Payment Response Status: {paymentResponse.StatusCode}");
                Console.WriteLine($"🔹 Payment Response:");
                Console.WriteLine(paymentContent);

                if (!paymentResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"PayPal API Error [{paymentResponse.StatusCode}]: {paymentContent}");
                }

                var paymentResult = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(paymentContent);
                var paymentId = paymentResult.GetProperty("id").GetString();
                Console.WriteLine($"🔹 Payment ID: {paymentId}");

                // 🔹 LƯU PAYMENT ID
                await SavePaymentId(orderCode, paymentId);

                // 🔹 LẤY APPROVAL URL
                var links = paymentResult.GetProperty("links");
                string approvalUrl = null;

                foreach (var link in links.EnumerateArray())
                {
                    var rel = link.GetProperty("rel").GetString();
                    var href = link.GetProperty("href").GetString();
                    Console.WriteLine($"🔹 Link: {rel} -> {href}");

                    if (rel == "approval_url")
                    {
                        approvalUrl = href;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(approvalUrl))
                {
                    throw new Exception("Approval URL not found in PayPal response");
                }

                Console.WriteLine($"🔹 ✅ PayPal payment created successfully!");
                Console.WriteLine($"🔹 ✅ Approval URL: {approvalUrl}");

                return approvalUrl;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ PayPal HTTP error: {ex.Message}");
                Console.WriteLine($"❌ Stack trace: {ex.StackTrace}");
                throw new Exception($"PayPal payment failed: {ex.Message}");
            }
        }

        [HttpGet("MobilePaymentSuccess")]
        public async Task<IActionResult> MobilePaymentSuccess(string session_id, string orderCode, string paymentId, string PayerID)
        {
            try
            {
                Console.WriteLine($"🔹 Mobile payment success - OrderCode: {orderCode}, SessionId: {session_id}, PaymentId: {paymentId}, PayerID: {PayerID}");

                // 🔹 LẤY CART ITEMS VÀ DISCOUNT INFO ĐÃ LƯU
                var orderData = await GetOrderCartItems(orderCode);
                if (orderData == null)
                {
                    Console.WriteLine($"❌ Order data not found: {orderCode}");
                    return Content(GenerateErrorHtml("Order data not found"), "text/html");
                }

                // 🔹 XỬ LÝ PAYPAL SUCCESS NẾU CÓ paymentId VÀ PayerID
                if (!string.IsNullOrEmpty(paymentId) && !string.IsNullOrEmpty(PayerID))
                {
                    try
                    {
                        Console.WriteLine($"🔹 Processing PayPal payment execution...");

                        // 🔹 EXECUTE PAYPAL PAYMENT BẰNG HTTP CLIENT
                        using var httpClient = new HttpClient();

                        var clientId = "Ad7D6abQz4m4Ja4g-VxgwDZK_BkSgyjpmwQGjK7Yu_IOfsN2dhbRDMQ4qJmWYdxvcGK1IVK1TLg2qFZo";
                        var clientSecret = "ELfi3XeppHttGOyn5NSnh4n9L07FYbOknmJR46PeeppLHWfXVN2UE93uDUyFjSuK1ZVKMI-0_ZZ_jf73";

                        // Get token
                        var tokenRequest = new FormUrlEncodedContent(new[]
                        {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                        var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authString);

                        var tokenResponse = await httpClient.PostAsync("https://api.sandbox.paypal.com/v1/oauth2/token", tokenRequest);
                        var tokenContent = await tokenResponse.Content.ReadAsStringAsync();

                        if (!tokenResponse.IsSuccessStatusCode)
                        {
                            throw new Exception($"Token error: {tokenContent}");
                        }

                        var tokenData = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(tokenContent);
                        var accessToken = tokenData.GetProperty("access_token").GetString();

                        // Execute payment
                        httpClient.DefaultRequestHeaders.Clear();
                        httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                        var executeData = new
                        {
                            payer_id = PayerID
                        };

                        var executeJson = System.Text.Json.JsonSerializer.Serialize(executeData);
                        var executeRequest = new StringContent(executeJson, Encoding.UTF8, "application/json");

                        var executeResponse = await httpClient.PostAsync($"https://api.sandbox.paypal.com/v1/payments/payment/{paymentId}/execute", executeRequest);
                        var executeContent = await executeResponse.Content.ReadAsStringAsync();

                        Console.WriteLine($"🔹 PayPal execute response: {executeResponse.StatusCode}");
                        Console.WriteLine($"🔹 PayPal execute content: {executeContent}");

                        if (!executeResponse.IsSuccessStatusCode)
                        {
                            throw new Exception($"PayPal execution failed: {executeContent}");
                        }

                        var executeResult = System.Text.Json.JsonSerializer.Deserialize<JsonElement>(executeContent);
                        var state = executeResult.GetProperty("state").GetString();

                        if (state?.ToLower() != "approved")
                        {
                            Console.WriteLine($"❌ PayPal payment not approved: {state}");
                            return Content(GenerateErrorHtml("PayPal payment not approved"), "text/html");
                        }

                        Console.WriteLine($"🔹 ✅ PayPal payment executed successfully: {state}");
                    }
                    catch (Exception paypalEx)
                    {
                        Console.WriteLine($"❌ PayPal execution error: {paypalEx.Message}");
                        return Content(GenerateErrorHtml($"PayPal error: {paypalEx.Message}"), "text/html");
                    }
                }

                // 🔹 BÂY GIỜ MỚI TẠO ORDER SAU KHI THANH TOÁN THÀNH CÔNG
                var order = new OrderModel
                {
                    OrderCode = orderCode,
                    CreatedDate = DateTime.Now,
                    UserName = orderData.UserEmail,
                    Status = 1, // Đã thanh toán
                    Address = orderData.ShippingAddress.GetFullAddress(),
                    ShippingCost = orderData.ShippingPrice,
                    PaymentIntentId = session_id ?? paymentId ?? "PAID"
                };

                _datacontext.Orders.Add(order);
                await _datacontext.SaveChangesAsync();

                Console.WriteLine($"✅ Order created successfully: {orderCode}");

                // Xử lý order details với cả 2 loại discount
                await ProcessOrderDetails(orderCode, orderData.CartItems, orderData.UserEmail, orderData.MembershipDiscount, orderData.CouponDiscount, orderData.CouponCode);
                await SendOrderConfirmationEmail(orderCode, orderData.CartItems, orderData.UserEmail, orderData.ShippingPrice, orderData.MembershipDiscount, orderData.CouponDiscount, orderData.ShippingAddress);

                // Xóa cache sau khi xử lý
                await RemoveOrderCartItems(orderCode);

                // Xóa payment cache nếu có
                var paymentFilePath = $"temp_payment_{orderCode}.txt";
                if (System.IO.File.Exists(paymentFilePath))
                {
                    System.IO.File.Delete(paymentFilePath);
                }

                // Tạo HTML page để redirect về app
                var html = GenerateSuccessHtml(orderCode);
                return Content(html, "text/html");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Mobile payment success error: {ex.Message}");
                var errorHtml = GenerateErrorHtml(ex.Message);
                return Content(errorHtml, "text/html");
            }
        }

        [HttpGet("MobilePaymentCancel")]
        public async Task<IActionResult> MobilePaymentCancel(string orderCode)
        {
            try
            {
                // 🔹 XÓA CACHE KHI CANCEL
                await RemoveOrderCartItems(orderCode);

                var paymentFilePath = $"temp_payment_{orderCode}.txt";
                if (System.IO.File.Exists(paymentFilePath))
                {
                    System.IO.File.Delete(paymentFilePath);
                }

                Console.WriteLine($"✅ Cleaned up cancelled order: {orderCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error cleaning up cancelled order: {ex.Message}");
            }

            var html = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Payment Cancelled</title>
                    <meta name='viewport' content='width=device-width, initial-scale=1'>
                    <style>
                        body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
                        .container {{ max-width: 400px; margin: 50px auto; background: white; padding: 30px; border-radius: 10px; text-align: center; box-shadow: 0 4px 8px rgba(0,0,0,0.1); }}
                        .cancel-icon {{ color: #ff9800; font-size: 60px; margin-bottom: 20px; }}
                        h2 {{ color: #333; margin-bottom: 10px; }}
                        p {{ color: #666; margin: 10px 0; }}
                        button {{ background: #ff9800; color: white; border: none; padding: 12px 24px; border-radius: 5px; cursor: pointer; font-size: 16px; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='cancel-icon'>⚠</div>
                        <h2>Payment Cancelled</h2>
                        <p>Order Code: {orderCode}</p>
                        <p>You can close this window and try again.</p>
                        <button onclick='window.close()'>Close Window</button>
                    </div>
                    <script>
                        setTimeout(function() {{
                            window.close();
                        }}, 5000);
                    </script>
                </body>
                </html>";

            return Content(html, "text/html");
        }

        // 🔹 LƯU PAYMENT ID VÀO CACHE
        private async Task SavePaymentId(string orderCode, string paymentId)
        {
            var filePath = $"temp_payment_{orderCode}.txt";
            await System.IO.File.WriteAllTextAsync(filePath, paymentId);
            Console.WriteLine($"💾 Saved payment ID for order: {orderCode}");
        }

        // 🔹 LẤY PAYMENT ID TỪ CACHE
        private async Task<string> GetPaymentId(string orderCode)
        {
            var filePath = $"temp_payment_{orderCode}.txt";
            if (System.IO.File.Exists(filePath))
            {
                return await System.IO.File.ReadAllTextAsync(filePath);
            }
            return null;
        }

        // 🔹 LƯU CART ITEMS VÀO CACHE (sử dụng MemoryCache hoặc Redis)
        private async Task SaveOrderCartItems(string orderCode, List<CartItemModel> cartItems, decimal shippingPrice, decimal membershipDiscount, decimal couponDiscount, ShippingAddressModel shippingAddress, string couponCode, string userEmail)
        {
            var orderData = new OrderCartData
            {
                OrderCode = orderCode,
                CartItems = cartItems,
                ShippingPrice = shippingPrice,
                MembershipDiscount = membershipDiscount,
                CouponDiscount = couponDiscount,
                ShippingAddress = shippingAddress,
                CouponCode = couponCode,
                UserEmail = userEmail, // 🔹 THÊM USER EMAIL
                CreatedAt = DateTime.UtcNow
            };

            // Lưu vào cache với key là orderCode, expire sau 1 giờ
            var json = System.Text.Json.JsonSerializer.Serialize(orderData);
            await System.IO.File.WriteAllTextAsync($"temp_order_{orderCode}.json", json);

            Console.WriteLine($"💾 Saved cart items and discounts for order: {orderCode}");
        }

        // 🔹 LẤY CART ITEMS TỪ CACHE
        private async Task<OrderCartData> GetOrderCartItems(string orderCode)
        {
            try
            {
                var filePath = $"temp_order_{orderCode}.json";
                if (System.IO.File.Exists(filePath))
                {
                    var json = await System.IO.File.ReadAllTextAsync(filePath);
                    var orderData = System.Text.Json.JsonSerializer.Deserialize<OrderCartData>(json);
                    Console.WriteLine($"📥 Retrieved cart items for order: {orderCode}, Items count: {orderData.CartItems.Count}");
                    return orderData;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting order cart items: {ex.Message}");
            }
            return null;
        }

        // 🔹 XÓA CACHE SAU KHI XỬ LÝ
        private async Task RemoveOrderCartItems(string orderCode)
        {
            try
            {
                var filePath = $"temp_order_{orderCode}.json";
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                    Console.WriteLine($"🗑️ Removed cache for order: {orderCode}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error removing order cache: {ex.Message}");
            }
        }

        private string GenerateSuccessHtml(string orderCode)
        {
            return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <title>Payment Success</title>
            <meta name='viewport' content='width=device-width, initial-scale=1'>
            <style>
                body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
                .container {{ max-width: 400px; margin: 50px auto; background: white; padding: 30px; border-radius: 10px; text-align: center; box-shadow: 0 4px 8px rgba(0,0,0,0.1); }}
                .success-icon {{ color: #4CAF50; font-size: 60px; margin-bottom: 20px; }}
                h2 {{ color: #333; margin-bottom: 10px; }}
                p {{ color: #666; margin: 10px 0; }}
                .order-code {{ background: #f0f0f0; padding: 10px; border-radius: 5px; font-weight: bold; margin: 20px 0; }}
                button {{ background: #4CAF50; color: white; border: none; padding: 12px 24px; border-radius: 5px; cursor: pointer; font-size: 16px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='success-icon'>✓</div>
                <h2>Payment Successful!</h2>
                <div class='order-code'>Order Code: {orderCode}</div>
                <p>Your order has been processed successfully.</p>
                <p>You will receive a confirmation email shortly.</p>
                <button onclick='window.close()'>Close Window</button>
            </div>
            <script>
                setTimeout(function() {{
                    window.close();
                }}, 5000);
            </script>
        </body>
        </html>";
        }

        private string GenerateErrorHtml(string error)
        {
            return $@"
        <!DOCTYPE html>
        <html>
        <head>
            <title>Payment Error</title>
            <meta name='viewport' content='width=device-width, initial-scale=1'>
            <style>
                body {{ font-family: Arial, sans-serif; margin: 0; padding: 20px; background: #f5f5f5; }}
                .container {{ max-width: 400px; margin: 50px auto; background: white; padding: 30px; border-radius: 10px; text-align: center; box-shadow: 0 4px 8px rgba(0,0,0,0.1); }}
                .error-icon {{ color: #f44336; font-size: 60px; margin-bottom: 20px; }}
                h2 {{ color: #333; margin-bottom: 10px; }}
                p {{ color: #666; margin: 10px 0; }}
                button {{ background: #f44336; color: white; border: none; padding: 12px 24px; border-radius: 5px; cursor: pointer; font-size: 16px; }}
            </style>
        </head>
        <body>
            <div class='container'>
                <div class='error-icon'>✗</div>
                <h2>Payment Error</h2>
                <p>Error: {error}</p>
                <button onclick='window.close()'>Close Window</button>
            </div>
        </body>
        </html>";
        }

        private async Task ProcessOrderDetails(string orderCode, List<CartItemModel> cartItems, string userEmail, decimal membershipDiscount, decimal couponDiscount, string couponCode)
        {
            decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);

            foreach (var cart in cartItems)
            {
                // 🔹 SỬA: Include ProductQuantities như CheckoutController
                var variation = await _datacontext.Variations
                    .Include(v => v.Material)
                    .Include(v => v.Color)
                    .Include(v => v.Product)
                    .Include(v => v.ProductQuantities) // ✅ Thêm include này
                    .FirstOrDefaultAsync(v => v.Id == cart.VariationId);

                if (variation == null) continue;

                // 🔹 SỬA: Kiểm tra tồn kho như CheckoutController
                if (cart.Quantity > variation.Stock)
                {
                    throw new Exception($"Số lượng yêu cầu cho sản phẩm {variation.Product.Name} vượt quá tồn kho ({variation.Stock}).");
                }

                // Tính discount cho từng item
                decimal itemTotal = cart.Quantity * cart.Price;
                decimal itemMembershipDiscount = (itemTotal / grandTotal) * membershipDiscount;
                decimal itemCouponDiscount = (itemTotal / grandTotal) * couponDiscount;
                decimal totalDiscountForItem = itemMembershipDiscount + itemCouponDiscount;

                var orderDetail = new OrderDetails
                {
                    UserName = userEmail,
                    OrderCode = orderCode,
                    ProductId = variation.ProductId,
                    VariationId = variation.Id,
                    Price = cart.Price,
                    Quantity = cart.Quantity,
                    DiscountAmount = totalDiscountForItem // Tổng discount cho item này
                };
                _datacontext.OrderDetails.Add(orderDetail);

                // Tạo warranty nếu có
                if (variation.Product.WarrantyPeriod > 0)
                {
                    var warranty = new WarrantyModel
                    {
                        WarrantyCode = Guid.NewGuid().ToString().Substring(0, 10).ToUpper(),
                        ProductId = variation.ProductId,
                        VariationId = variation.Id,
                        OrderCode = orderCode,
                        ExpirationDate = DateTime.Now.AddYears(variation.Product.WarrantyPeriod),
                        CreatedDate = DateTime.Now
                    };
                    _datacontext.Warranties.Add(warranty);
                }

                // 🔹 SỬA: Sử dụng logic FIFO như CheckoutController
                int quantityToDeduct = cart.Quantity;
                var batchStocks = variation.ProductQuantities
                    .Where(pq => pq.CurrentQuantityInBatch > 0)
                    .OrderBy(pq => pq.DateCreated) // FIFO - lô cũ trước
                    .ToList();

                foreach (var pq in batchStocks)
                {
                    if (quantityToDeduct <= 0) break;

                    int deduct = Math.Min(quantityToDeduct, pq.CurrentQuantityInBatch);
                    pq.CurrentQuantityInBatch -= deduct;
                    pq.LastUpdated = DateTime.Now; // Cập nhật thời gian
                    quantityToDeduct -= deduct;

                    _datacontext.ProductQuantities.Update(pq);
                }

                // 🔹 SỬA: Chỉ cập nhật Product.Sold, KHÔNG trừ Product.Quantity
                variation.Product.Sold += cart.Quantity;

                // 🔹 SỬA: Chỉ update Product, không update variation
                _datacontext.Update(variation.Product);
            }

            var user = await _datacontext.Users.FirstOrDefaultAsync(u => u.Email == userEmail);

            // Xử lý coupon usage
            if (!string.IsNullOrEmpty(couponCode) && couponDiscount > 0 && user != null)
            {
                var coupon = await _datacontext.Coupons.FirstOrDefaultAsync(c => c.Code == couponCode);

                if (coupon != null)
                {
                    // 🔹 SỬ DỤNG MODEL CouponUsageModel HIỆN TẠI
                    var couponUsage = new CouponUsageModel
                    {
                        UserId = user.Id,
                        CouponCode = coupon.Code,
                        UsedAt = DateTime.Now
                    };

                    _datacontext.CouponUsages.Add(couponUsage);

                    // Cập nhật coupon usage count
                    coupon.UsedCount += 1;
                    _datacontext.Update(coupon);

                    Console.WriteLine($"✅ Saved coupon usage - User: {user.Email}, Coupon: {couponCode}, Discount: ${couponDiscount}");
                }
            }

            // 🔹 THÊM: Cập nhật points cho user như CheckoutController
            if (user != null)
            {
                int pointsToAdd = (int)(grandTotal * 0.01m); // 1% của tổng tiền
                user.Points += pointsToAdd;
                _datacontext.Update(user);
                Console.WriteLine($"✅ Added {pointsToAdd} points to user {userEmail}");
            }

            await _datacontext.SaveChangesAsync();
        }

        private async Task SendOrderConfirmationEmail(string orderCode, List<CartItemModel> cartItems, string userEmail, decimal shippingPrice, decimal membershipDiscount, decimal couponDiscount, ShippingAddressModel shippingAddress)
        {
            try
            {
                var orderDetails = await _datacontext.OrderDetails
                    .Where(od => od.OrderCode == orderCode)
                    .Include(od => od.Product)
                    .Include(od => od.Variation)
                    .ToListAsync();

                decimal grandTotal = cartItems.Sum(x => x.Quantity * x.Price);
                decimal totalDiscount = membershipDiscount + couponDiscount;
                decimal finalTotal = grandTotal + shippingPrice - totalDiscount;

                var emailBody = new StringBuilder();
                emailBody.AppendLine("Dear Customer,");
                emailBody.AppendLine("We have successfully received your order from mobile app. Here are the details:");
                emailBody.AppendLine();
                emailBody.AppendLine($"**Order Code:** {orderCode}");
                emailBody.AppendLine($"**Order Date:** {DateTime.Now:yyyy-MM-dd}");
                emailBody.AppendLine($"**Shipping Address:** {shippingAddress.GetFullAddress()}");
                emailBody.AppendLine($"**Subtotal:** ${grandTotal:F2}");
                emailBody.AppendLine($"**Shipping Cost:** ${shippingPrice:F2}");
                emailBody.AppendLine($"**Membership Discount:** -${membershipDiscount:F2}");
                emailBody.AppendLine($"**Coupon Discount:** -${couponDiscount:F2}");
                emailBody.AppendLine($"**Total Discount:** -${totalDiscount:F2}");
                emailBody.AppendLine($"**Final Total:** ${finalTotal:F2}");
                emailBody.AppendLine($"**Total Items:** {cartItems.Count}");
                emailBody.AppendLine();

                emailBody.AppendLine("**Products in Your Order:**");
                foreach (var cart in cartItems)
                {
                    emailBody.AppendLine($"- **Product:** {cart.ProductName}");
                    emailBody.AppendLine($"  - Quantity: {cart.Quantity}");
                    emailBody.AppendLine($"  - Price per Unit: ${cart.Price:F2}");
                    emailBody.AppendLine($"  - Total: ${cart.Quantity * cart.Price:F2}");
                    emailBody.AppendLine();
                }

                emailBody.AppendLine("Thank you for shopping with us!");
                emailBody.AppendLine("Best regards");

                await _emailSender.SendEmailAsync(userEmail, "Order Successfully - Mobile App", emailBody.ToString());
                Console.WriteLine($"✅ Sent confirmation email to: {userEmail}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending email: {ex.Message}");
            }
        }
    }

    public class CheckoutApiRequest
    {
        public string PaymentMethod { get; set; }
        public string UserEmail { get; set; }
        public List<CartItemApiModel> CartItems { get; set; }
        public double ShippingPrice { get; set; }
        public double DiscountAmount { get; set; } // Coupon discount
        public ShippingAddressModel ShippingAddress { get; set; }
        public string CouponCode { get; set; } // 🔹 THÊM COUPON CODE
    }

    public class CartItemApiModel
    {
        public int ProductId { get; set; }
        public int VariationId { get; set; }
        public string ProductName { get; set; }
        public double Price { get; set; }
        public int Quantity { get; set; }
        public string ImageUrl { get; set; }
    }

    // 🔹 CẬP NHẬT OrderCartData CLASS
    public class OrderCartData
    {
        public string OrderCode { get; set; }
        public List<CartItemModel> CartItems { get; set; }
        public decimal ShippingPrice { get; set; }
        public decimal MembershipDiscount { get; set; } // Discount từ membership
        public decimal CouponDiscount { get; set; } // Discount từ coupon
        public ShippingAddressModel ShippingAddress { get; set; }
        public string CouponCode { get; set; }
        public string UserEmail { get; set; } // 🔹 THÊM USER EMAIL
        public DateTime CreatedAt { get; set; }
    }

    public class ShippingAddressModel
    {
        public string City { get; set; }
        public string District { get; set; }
        public string Ward { get; set; }
        public string DetailAddress { get; set; }

        public string GetFullAddress()
        {
            return $"{DetailAddress}, {Ward}, {District}, {City}";
        }
    }
}