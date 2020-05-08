using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Xml;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using Microsoft.WindowsAzure.Storage;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Paperback.Cache
{


    public static class MangaParkPassivePull
    {
        [FunctionName("MangaParkPassivePull")]
        public static async System.Threading.Tasks.Task RunAsync([TimerTrigger("0 */1 * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            // Connect to the TableStorage
            string connectionString = System.Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            CloudStorageAccount account = AppSettings.createStorageAccountFromConnectionString(connectionString);
            CloudTableClient tableClient = account.CreateCloudTableClient();
            CloudTable table = tableClient.GetTableReference("MangaPark");

            int itemsUpdated = 0; // Track items that have been updated
            int itemsAdded = 0; // Track items added for reporting

            string path = "https://mangapark.net/rss/latest.xml";
            XmlDocument doc = new XmlDocument();
            doc.Load(path);

            XmlNodeList nodes = doc.GetElementsByTagName("item");
            for(int i = 0; i <= nodes.Count - 1; i++) {
                // Spawn a MangaPark element off of the extracted data
                // We collect titles from the URL, not the title itself. Process that here.
                string url = nodes[i].ChildNodes.Item(1).InnerText;
                Regex rx = new Regex("/manga/([\\w*|-]*)/");
                Match match = rx.Match(url);
                if(match.Success) {
                    url = match.Groups[1].Value;
                }
                else {
                    // This is a bad parse, we cannot use this
                    log.LogWarning("Manga: " + nodes[i].ChildNodes.Item(0).InnerText + " cannot be parsed for it's URL properly");
                    continue;
                }
                MangaParkTransitionElement element = new MangaParkTransitionElement(url, nodes[i].ChildNodes.Item(3).InnerText, nodes[i].ChildNodes.Item(0).InnerText);

                // Is this a valid XML entry? If so, process it to the TableStorage
                if(element.isValid) {                    
                    // Check to see if this element already exists in the server
                    try {                       
                        TableOperation retrieveOperation = TableOperation.Retrieve<MangaParkElement>(element.title, "*");
                        TableResult selectResult = await table.ExecuteAsync(retrieveOperation);
                        MangaParkElement selectElement = selectResult.Result as MangaParkElement;

                        if(selectElement != null) {
                            // The element already exists, tear apart the JSON and check if we have this chapter already stored or not
                            ChapterDetails[] arr = Newtonsoft.Json.JsonConvert.DeserializeObject<ChapterDetails[]>(selectElement.json);
                            List<ChapterDetails> list = new List<ChapterDetails>(arr);
                            bool foundChapter = false;
                            foreach(ChapterDetails chapter in arr) {
                                if(chapter.chapterNum == element.chapter) {
                                    // Mark that we found this chapter, and that it should not be added
                                    foundChapter = true;
                                    break;
                                }
                            }

                            // If we didn't find the chapter in our list already, add it
                            if(!foundChapter) {
                                ChapterDetails detail = new ChapterDetails();
                                detail.chapterNum = element.chapter;
                                detail.timestamp = element.pubDate;
                                list.Add(detail);
                            }

                            // If the list is bigger than the array, we've added new objects. Replace the object in the datastore. Otherwise ignore it
                            if(arr.Length < list.Count) {
                                MangaParkElement replaceElement = new MangaParkElement(element.title, list.ToArray());
                                replaceElement.ETag = "*";
                                TableOperation replaceOperation = TableOperation.Replace(replaceElement);
                                TableResult result = await table.ExecuteAsync(replaceOperation);
                                itemsUpdated++;
                            }                            
                        }
                        else {
                            // The element doesn't exist in the database, insert it here!
                            ChapterDetails detail = new ChapterDetails();
                            detail.chapterNum = element.chapter;
                            detail.timestamp = element.pubDate;

                            List<ChapterDetails> list = new List<ChapterDetails>();
                            list.Add(detail);
                            MangaParkElement newElement = new MangaParkElement(element.title, list.ToArray());
                            TableOperation insertOrMergeOperation = TableOperation.InsertOrMerge(newElement);
                            TableResult result = await table.ExecuteAsync(insertOrMergeOperation);
                            itemsAdded++;
                        }
                    } catch(StorageException ex) {
                        log.LogWarning(ex.Message);
                    }
                }
            }

            // Log the information to Azure Dashboards
            log.LogInformation("Updated " + itemsUpdated + " items || Added " + itemsAdded + " items!");
        }
    }
}
