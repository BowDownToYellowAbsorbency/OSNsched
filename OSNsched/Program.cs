using Dasync.Collections;
using Newtonsoft.Json.Linq;
using ShellProgressBar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OSNsched
{
    internal class Program
    {
        private static async Task Main()
        {
            using HttpClient client = new HttpClient();
            // construct request for search results, get response
            Dictionary<string, string> searchResultsParams = new Dictionary<string, string>
            {
                { "newDate", DateTime.UtcNow.ToString("MM/dd/yyyy") }, { "searchText", "spongebob" }, { "selectedCountry", "SA" }
            };
            FormUrlEncodedContent content = new FormUrlEncodedContent(searchResultsParams);
            HttpResponseMessage response = await client.PostAsync("https://www.osn.com/CMSPages/TVScheduleWebService.asmx/GetSearchResultsForPrograms", content);
            string responseString = await response.Content.ReadAsStringAsync();

            // go through the search results, get EPG info for each search result
            JToken[] responseTokens = JArray.Parse(XElement.Parse(responseString).Value).Children().ToArray();
            using (ProgressBar progressBar = new ProgressBar(responseTokens.Length, string.Empty, ConsoleColor.Green))
            {
                using StreamWriter sw = File.CreateText("out.json");
                await responseTokens.AsParallel().AsOrdered().ParallelForEachAsync(async token =>
                {
                    Dictionary<string, string> programDetailsParams = new Dictionary<string, string>
                    {
                        { "countryCode", "SA" }, { "prgmEPGUNIQID", token["EPGUNIQID"].ToString() }
                    };
                    FormUrlEncodedContent programDetailsContent = new FormUrlEncodedContent(programDetailsParams);
                    HttpResponseMessage programDetailsResponse = await client.PostAsync("https://www.osn.com/CMSPages/TVScheduleWebService.asmx/GetProgramDetails", programDetailsContent);
                    string programDetailsResponseString = await programDetailsResponse.Content.ReadAsStringAsync();

                    // parse and add data on episode to output json
                    sw.WriteLine(JArray.Parse(XElement.Parse(programDetailsResponseString).Value).ElementAt(0).ToString() + ",");

                    progressBar.Tick();
                }, maxDegreeOfParallelism: Environment.ProcessorCount * 5);
            }

            Console.WriteLine("Done! Press any key to exit.");
            Console.ReadKey();
        }
    }
}
