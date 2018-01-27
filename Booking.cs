using HtmlAgilityPack;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace CMGBooker
{
    public class Booking
    {
        private HttpClient httpClient;
        private HttpClientHandler httpHandler;
        private CookieContainer cookiesContainer;
        private readonly TraceWriter log;
        public const string BaseUrl = "https://prod-resamania.cmgsportsclub.com";
        public const string LoginSubPath = "/account/login";
        public const string PlanningSubPath = "/members/planning";

        public Booking(TraceWriter log)
        {
            this.log = log;
            cookiesContainer = new CookieContainer();
            httpHandler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
            };
            httpHandler.CookieContainer = cookiesContainer;
            cookiesContainer.Add(new Uri(BaseUrl), new Cookie("clubs", "6", "/", "prod-resamania.cmgsportsclub.com"));
            httpClient = new HttpClient(httpHandler);
        }

        public async Task PerformLogin()
        {
            log.Info("Attempting to login");
            httpClient.AddLoginHeaders();

            var email = await AzureKeyVaultProvider.GetSecret("gmail", log);
            var secret = await AzureKeyVaultProvider.GetSecret("cmgsecret", log);

            var data = new FormUrlEncodedContent(new[]{
                new KeyValuePair<string, string>("_username", email),
                new KeyValuePair<string, string>("_password", secret)
            });

            var response = await httpClient.PostAsync(BaseUrl + LoginSubPath, data);
            log.Info(response.StatusCode.ToString());
        }

        /// <summary>
        /// Request that will set the missing cookies in order to book a class
        /// </summary>
        /// <returns></returns>
        public async Task PerformHomeRequest()
        {
            log.Info("Requesting home page");
            httpClient.AddDefaultHeaders();

            var homeResult = await httpClient.GetAsync(BaseUrl + PlanningSubPath);
            log.Info(homeResult.StatusCode.ToString());

            log.Info("Cookies: ");
            var responseCookies = cookiesContainer.GetCookies(new Uri(BaseUrl)).Cast<Cookie>();
            foreach (Cookie cookie in responseCookies)
                log.Info(cookie.Name + ": " + cookie.Value);
        }

        public async Task Book()
        {
            UriBuilder builder = new UriBuilder(BaseUrl + "/members/planning");
            builder.Query = "activity=.*&minhour=7&maxhour=23&day=7";

            log.Info("Requesting classes page");
            var classesPageResult = await httpClient.GetAsync(builder.Uri);

            log.Info(classesPageResult.StatusCode.ToString());
            var classesContent = await classesPageResult.Content.ReadAsStringAsync();

            var link = GetClassLink(classesContent);
            log.Info($"link for booking {link}");

            if (!string.IsNullOrWhiteSpace(link))
            {
                log.Info($"booking...");
                var bookingResult = await httpClient.PostAsync(link, null);
                log.Info(bookingResult.StatusCode.ToString());
            }
        }

        private string GetClassLink(string html)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(html);

            //get the json schedules
            var script = htmlDoc.DocumentNode.SelectSingleNode("//script[contains(text(),'planningTableData')]");
            var data = script.InnerText.Replace("var planningTableData =", "").Trim('\r', '\n', ';', ' ');
            log.Info(data);

            dynamic classes = JsonConvert.DeserializeObject(data);
            var link = string.Empty;

            foreach (var sportsClass in classes)
            {
                if ("BODY PUMP".Equals((string)sportsClass.activity.name))
                {
                    Console.WriteLine(sportsClass.activity.name);
                    Console.WriteLine(sportsClass.actions);
                    var htmlLink = new HtmlDocument();
                    //load html button with link to make a reservation
                    htmlLink.LoadHtml((string)sportsClass.actions);
                    link = htmlLink.DocumentNode.FirstChild.GetAttributeValue("data-subscribelink", "");
                    break;
                }
            }
            return link;
        }
    }
}