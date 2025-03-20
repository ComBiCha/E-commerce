using PayPal.Api;
using System.Collections.Generic;

namespace E_commerce.Services
{
    public class PayPalSdk
    {
        private readonly APIContext _apiContext;

        public PayPalSdk()
        {
            var config = new Dictionary<string, string>
            {
                { "mode", "sandbox" }, // Chuyển sang "live" nếu dùng môi trường thật
                { "clientId", "Ad7D6abQz4m4Ja4g-VxgwDZK_BkSgyjpmwQGjK7Yu_IOfsN2dhbRDMQ4qJmWYdxvcGK1IVK1TLg2qFZo" },
                { "clientSecret", "ELfi3XeppHttGOyn5NSnh4n9L07FYbOknmJR46PeeppLHWfXVN2UE93uDUyFjSuK1ZVKMI-0_ZZ_jf73" }
            };

            var accessToken = new OAuthTokenCredential(config["clientId"], config["clientSecret"]).GetAccessToken();
            _apiContext = new APIContext(accessToken)
            {
                Config = config
            };
        }

        public APIContext GetAPIContext()
        {
            return _apiContext;
        }
    }
}
