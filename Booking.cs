using HtmlAgilityPack;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// Gets the list of classes to book in the same day next week.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<SportsClass> GetClassesToBook()
        {
            using (StreamReader r = new StreamReader("mybookings.json"))
            {
                string json = r.ReadToEnd();
                var classes = JsonConvert.DeserializeObject<List<SportsClass>>(json)
                    .Where(c => c.Day == DateTime.UtcNow.DayOfWeek);
                return classes;
            }
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

        public async Task Book(IEnumerable<SportsClass> classesToBook)
        {
            var schedule = await GetSchedule();

            var links = GetClassesLink(schedule, classesToBook);

            if (links?.Count() > 0)
            {
                log.Info($"links for booking {string.Join("; ", links.ToArray())}");
                log.Info($"booking...");
                foreach (var link in links)
                {
                    var bookingResult = await httpClient.PostAsync(link, null);
                    log.Info(bookingResult.StatusCode.ToString());
                }
            }
            else
            {
                log.Info("Could not get links to book");
            }
        }

        /// <summary>
        /// Returns all the classes for the same day next week
        /// </summary>
        /// <returns></returns>
        private async Task<dynamic> GetSchedule()
        {
            UriBuilder builder = new UriBuilder(BaseUrl + PlanningSubPath);
            builder.Query = "activity=.*&minhour=7&maxhour=23&day=7";

            log.Info("Requesting classes page");
            var classesPageResult = await httpClient.GetAsync(builder.Uri);
            log.Info(classesPageResult.StatusCode.ToString());
            var classesContent = await classesPageResult.Content.ReadAsStringAsync();

            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(classesContent);

            //get the schedule from json 
            var script = htmlDoc.DocumentNode.SelectSingleNode("//script[contains(text(),'planningTableData')]");
            var data = script.InnerText.Replace("var planningTableData =", "").Trim('\r', '\n', ';', ' ');
            log.Info(data.Substring(0, 300));

            return JsonConvert.DeserializeObject(data);
        }

        /// <summary>
        /// returns the url that allows to make the reservation
        /// </summary>
        /// <param name="schedule"></param>
        /// <param name="classesToBook"></param>
        /// <returns></returns>
        private IEnumerable<string> GetClassesLink(dynamic schedule, IEnumerable<SportsClass> classesToBook)
        {
            var bookingLinks = new List<string>();

            foreach (var classToBook in classesToBook)
            {
                foreach (var sportsClass in schedule)
                {
                    //evaluate if sports class is in the list of bookings for today
                    if (string.Equals((string)sportsClass.activity.name, classToBook.Name,
                        StringComparison.InvariantCultureIgnoreCase)
                        &&
                        string.Equals((string)sportsClass.date.display, classToBook.Time,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        Console.WriteLine(sportsClass.activity.name);
                        var htmlLink = new HtmlDocument();

                        //load html button with link to make a reservation
                        htmlLink.LoadHtml((string)sportsClass.actions);
                        bookingLinks.Add(htmlLink.DocumentNode.FirstChild.GetAttributeValue("data-subscribelink", ""));
                        break;
                    }
                }
            }
            return bookingLinks;
        }
    }
}