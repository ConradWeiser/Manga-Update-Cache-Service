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
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

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
            string fromDate = req.Query["from_date"];

            // Verify that the request was formed properly
            if(title == null || title == "" || fromDate == null || fromDate == "") {
                return new BadRequestObjectResult("Missing request parameters");
            }

            DateTime date;
            if(!DateTime.TryParse(fromDate, out date)) {
                return new BadRequestObjectResult("Date format error");
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

            List<ChapterDetails> details = new List<ChapterDetails>(JsonConvert.DeserializeObject<ChapterDetails[]>(selectElement.json));
            
            // If the list is empty, throw an error, this should not be the case if it was stored in the DB
            if(!details.Any()) {
                return new BadRequestObjectResult("Error parsing database response field");
            }

            DateTime chapterDate;
            List<String> chapterAlertList = new List<String>();
            foreach(ChapterDetails chapter in details) {
                // Parse the chapter date into it's object representation for comparison - Yes, you have to do some sketchy replacements. 
                // MangaRock RSS doesn't return proper formatted values. This is guarinteed to work every time, despite the magic numbers.
                chapter.timestamp = chapter.timestamp.Substring(0, chapter.timestamp.Length - 5) + "GMT";
                if(!DateTime.TryParse(chapter.timestamp, out chapterDate)) {
                    return new BadRequestObjectResult("Timestamp parse from database failed");
                };

                //Check to see whether or not the chapter is newer than our incoming date
                if(chapterDate.CompareTo(date) < 0) {
                    // It is! Add the chapter title to our alert list. Format the response to match what the normal source would return.
                    // Example: One Piece is called one-piece by MangaRock's primary source, for some reason. ToLower, replace spaces with dashes
                    title = title.ToLower().Replace(" ", "-");
                    chapterAlertList.Add(title);
                }
            }

            return new OkObjectResult(chapterAlertList.ToArray());
        }
    }
}
