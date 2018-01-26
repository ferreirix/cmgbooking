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
        private readonly TraceWriter log;
        private CookieContainer cookiesContainer;
        const string loginUri = "https://prod-resamania.cmgsportsclub.com/account/login";

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
            cookiesContainer.Add(new Uri("https://prod-resamania.cmgsportsclub.com"),
                                   new Cookie("clubs", "6", "/", "prod-resamania.cmgsportsclub.com"));
            httpClient = new HttpClient(httpHandler);
            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders
                      .Accept
                      .Add(new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded"));
            httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            httpClient.DefaultRequestHeaders.Add("Origin", "https://prod-resamania.cmgsportsclub.com");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "fr");
            httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://prod-resamania.cmgsportsclub.com/account/login");
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        }

        public async Task Book()
        {
            Console.WriteLine("HELLO");
            log.Info("Attempting to login");

            var email = await AzureKeyVaultProvider.GetSecret("gmail", log);
            var secret = await AzureKeyVaultProvider.GetSecret("cmgsecret", log);

            var data = new FormUrlEncodedContent(new[]{
                new KeyValuePair<string, string>("_username", email),
                new KeyValuePair<string, string>("_password", secret)
            });

            var response = await httpClient.PostAsync(loginUri, data);
            log.Info(response.StatusCode.ToString());

            httpClient = new HttpClient(httpHandler);
            httpClient.DefaultRequestHeaders.Add("Pragma", "no-cache");
            httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
            httpClient.DefaultRequestHeaders.Add("Accept-Language", "fr");
            httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/63.0.3239.132 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,image/apng,*/*;q=0.8");
            httpClient.DefaultRequestHeaders.Add("Referer", "https://prod-resamania.cmgsportsclub.com/members/planning?activity=.*&minhour=7&maxhour=23"); ;
            httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
            httpClient.DefaultRequestHeaders.Add("Cache-Control", "no-cache");

            log.Info("Requesting home page");
            UriBuilder builder = new UriBuilder("https://prod-resamania.cmgsportsclub.com/members/planning");
            builder.Query = "activity=.*&minhour=7&maxhour=23&day=7";
            var homeResult = await httpClient.GetAsync(builder.Uri);
            log.Info(homeResult.StatusCode.ToString());

            log.Info("Cookies: ");
            var responseCookies = cookiesContainer.GetCookies(new Uri(loginUri)).Cast<Cookie>();
            foreach (Cookie cookie in responseCookies)
                log.Info(cookie.Name + ": " + cookie.Value);

            log.Info(builder.Uri.ToString());

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
                    htmlLink.LoadHtml((string)sportsClass.actions);
                    link = htmlLink.DocumentNode.FirstChild.GetAttributeValue("data-subscribelink", "");
                    break;
                }
            }
            return link;
        }
    }
}