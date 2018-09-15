using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;

namespace a_slack_bot.Functions
{
    public static class Timer
    {
        [FunctionName(nameof(KeepAlive))]
        public static void KeepAlive(
            [TimerTrigger("0 * 9-18 * * 1-5")]TimerInfo myTimer,
            ILogger logger)
        {
            logger.LogInformation("IT'S {0} AND ALL IS WELL.", DateTimeOffset.Now);
        }
    }
}
