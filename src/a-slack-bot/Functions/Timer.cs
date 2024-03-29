﻿using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;

namespace a_slack_bot.Functions
{
    public static class Timer
    {
        [FunctionName(nameof(KeepAlive))]
        public static void KeepAlive(
            [TimerTrigger("0 */3 8-18 * * 1-5")]TimerInfo myTimer,
            ILogger logger)
        {
            logger.LogInformation($"IT'S {DateTimeOffset.UtcNow:o} AND ALL IS WELL FOR {C.VersionStr}.");
        }
    }
}
