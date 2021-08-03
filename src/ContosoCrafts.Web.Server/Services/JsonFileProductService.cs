using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ContosoCrafts.Web.Shared.Models;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Stripe.Checkout;

namespace ContosoCrafts.Web.Server.Services
{
    public class JsonFileProductService : IProductService
    {

        private readonly ILogger<JsonFileProductService> logger;

        public JsonFileProductService(ILogger<JsonFileProductService> logger)
        {
            this.logger = logger;

            var manifestEmbeddedProvider = new ManifestEmbeddedFileProvider(typeof(Program).Assembly);
            var fileInfo = manifestEmbeddedProvider.GetFileInfo("_data/products.json");
            using var reader = new StreamReader(fileInfo.CreateReadStream());


            var fileContent = reader.ReadToEnd();

            Products = JsonSerializer.Deserialize<List<Shared.Models.Product>>(fileContent,
                   new JsonSerializerOptions
                   {
                       PropertyNameCaseInsensitive = true
                   });
        }

        private List<Shared.Models.Product> Products { get; }

        public Task<IEnumerable<Shared.Models.Product>> GetProducts() => Task.FromResult(Products.AsEnumerable());

        public Task<Shared.Models.Product> GetProduct(string productId)
        {
            var product = Products.FirstOrDefault(x => x.Id == productId);
            return Task.FromResult(product);
        }

        public async Task AddRating(string productId, int rating)
        {
            var products = await GetProducts();

            if (products.First(x => x.Id == productId).Ratings == null)
            {
                products.First(x => x.Id == productId).Ratings = new int[] { rating };
            }
            else
            {
                var ratings = products.First(x => x.Id == productId).Ratings.ToList();
                ratings.Add(rating);
                products.First(x => x.Id == productId).Ratings = ratings.ToArray();
            }
        }

        public async Task<CheckoutResponse> CheckOut(IEnumerable<CartItem> Items, string callbackRoot)
        {
            logger.LogInformation($"Count: {Items.Count()}");

            List<SessionLineItemOptions> lineItems = new List<SessionLineItemOptions>();

            foreach(var item in Items) {
                // Lookup the product so we can use it's Price
                var product = await GetProduct(item.Id);

                lineItems.Add(new SessionLineItemOptions() {
                    PriceData = new()
                    {
                        UnitAmount = product.Price,
                        ProductData = new()
                        {
                            Name = item.Title,
                            Images = new List<string> { product.Image },
                        },
                        Currency = "USD",
                    },
                    Quantity = item.Quantity,
                });
            }

            var sessionOptions = new SessionCreateOptions()
            {
                SuccessUrl = $"{callbackRoot}/api/checkout/session?session_id=" + "{CHECKOUT_SESSION_ID}", /// redirect after checkout
                CancelUrl = $"{callbackRoot}/checkout/failure",  /// checkout cancelled
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = lineItems,
                Mode = "payment"
            };

            var checkoutService = new SessionService();
            var session = await checkoutService.CreateAsync(sessionOptions);

            return new CheckoutResponse(session.Id);
        }
    }
}
