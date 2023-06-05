using System;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace FuncOnContainer
{
    public class ehExample
    {
        private readonly ILogger _logger;

        public ehExample(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ehExample>();
        }

        [Function("ehExample")]
        public void Run([EventHubTrigger("workitems", Connection = "EventHubConnectionAppSetting")] string[] input)
        {
            _logger.LogInformation($"First Event Hubs triggered message: {input[0]}");
        }
    }
}
