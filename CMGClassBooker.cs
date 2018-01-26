using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace CMGBooker
{
    public static class CMGClassBooker
    {
        [FunctionName("CMGClassBooker")]
        public async static Task Run([TimerTrigger("0 0 7 * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            await new Booking(log).Book();
        }
    }
}