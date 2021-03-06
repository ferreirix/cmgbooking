using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Linq;

namespace CMGBooker
{
    public static class CMGClassBooker
    {
        [FunctionName("CMGClassBooker")]
        public async static Task Run([TimerTrigger("0 2 7 * * *")]TimerInfo myTimer,
            TraceWriter log,
            ExecutionContext context)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

            var booker = new Booking(log, context);

            var classesToBook = booker.GetClassesToBook();

            if (classesToBook?.Count() > 0)
            {
                await booker.PerformLogin();

                await booker.PerformHomeRequest();

                await booker.Book(classesToBook);
            }
        }
    }
}