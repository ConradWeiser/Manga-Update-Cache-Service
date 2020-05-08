using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Collections.Generic;

namespace Paperback.Cache
{
    public static class QueryCacheGeneric
    {
        [FunctionName("QueryCacheGeneric")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            
            string destination = req.Query["destination"];
            string query = req.Query["query"];

            // Verify that a proper destination was given
            if((destination == null || destination == "") || (query == null || query == "")) {
                return new BadRequestObjectResult("bad request");
            }

            if(destination == "MangaPark") {
                return new OkObjectResult(await queryMangaPark(query));
            }


            return new BadRequestObjectResult("Source not cached");
        }

        public static async Task<string> queryMangaPark(string query) {
            HttpClient client = new HttpClient();
            
            var mangaParkHttpSecret = System.Environment.GetEnvironmentVariable("MangaParkHttpSecret");
            var mangaParkUrl = System.Environment.GetEnvironmentVariable("MangaParkUrl");
            var variables = "?code=" + mangaParkHttpSecret + "&manga_title=" + query;
            var something = mangaParkUrl + variables;
            var response = await client.GetStringAsync(mangaParkUrl + variables);
            return response;
        }
    }
}
