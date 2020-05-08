using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.Storage.Table;

namespace Paperback.Cache
{
    public static class QueryMangaPark
    {
        [FunctionName("QueryMangaPark")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)] HttpRequest req,
            ILogger log)
        {

            string title = req.Query["manga_title"];

            // Verify that the request was formed properly
            if(title == null || title == "") {
                return new BadRequestObjectResult("Bad request");
            }

            // There is a correct manga title, attempt to retrieve this title from the cache server
            Microsoft.WindowsAzure.Storage.CloudStorageAccount storageAccount = Microsoft.WindowsAzure.Storage.CloudStorageAccount.Parse(System.Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("MangaPark");

            TableOperation retrieveOperation = TableOperation.Retrieve<MangaParkElement>(title, "*");
            TableResult selectResult = await table.ExecuteAsync(retrieveOperation);
            MangaParkElement selectElement = selectResult.Result as MangaParkElement;

            if(selectElement == null) {
                return new BadRequestObjectResult("not in database");
            }

            return new OkObjectResult(selectElement.json);
        }
    }
}
