using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace CMGBooker
{
    public static class CMGClassBooker
    {
        [FunctionName("CMGClassBooker")]
        public async static Task Run([TimerTrigger("0 0 */1 * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            var booker = new Booking(log);

            await booker.PerformLogin();

            await booker.PerformHomeRequest();

            await booker.Book();
        }
    }
}