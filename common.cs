using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace Paperback.Cache {

    /**
    * Class responsible for pulling settings from AzureSettings.json
    * This is likely where your database connection URLs will sit
    **/
    public class AppSettings {
        public string StorageConnectionString{get;set;}

        public static CloudStorageAccount createStorageAccountFromConnectionString(string connectionString) {
            CloudStorageAccount storageAccount;
            try {
                storageAccount = CloudStorageAccount.Parse(connectionString);
            } catch(FormatException) {
                Console.WriteLine("Invalid storage account provided. Please confirm the connection string.");
                throw;
            } catch (ArgumentException) {
                Console.WriteLine("Invalid storage account. Please confirm the connection string");
                Console.ReadLine();
                throw;
            }

            return storageAccount;
        }
    }
    
    class MangaParkTransitionElement {
        public MangaParkTransitionElement(string mangaUrl, string pubDate, string internalTitle) {
            Regex regex = new Regex("(.+[vol.\\d]*)ch.(\\d)*");
            Match match = regex.Match(internalTitle);
            if(match.Success) {
                this.chapter = match.Groups[2].Value;
                this.isValid = true;
            }

            this.title = mangaUrl;
            this.pubDate = pubDate;
        }
        public bool isValid = false;
        public string title;
        public string chapter;
        public string pubDate;
    }

    class ChapterDetails {
        public string chapterNum{get;set;}
        public string timestamp{get;set;}
    }
    class MangaParkElement : TableEntity {
        public MangaParkElement() {

        }

        public MangaParkElement(string partitionKey, ChapterDetails[] chapter) : base(partitionKey, "*")
        {
            var chapterConvert = JsonConvert.SerializeObject(chapter);
            this.PartitionKey = partitionKey;
            this.RowKey = "*";

            this.json = chapterConvert;
        }

        public void setEtag(string tag) {
            this.ETag = tag;
        }

        public string json{get;set;}

    }    
}