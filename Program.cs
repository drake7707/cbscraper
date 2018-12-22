using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using System.Linq;
using System.IO;
using CsvHelper;

namespace CoolBlueScraper
{
    class Program
    {

        private static HtmlWeb web = new HtmlAgilityPack.HtmlWeb();

        public static void Main(string[] args)
        {
            MainAsync(args).GetAwaiter().GetResult();

            Console.WriteLine("--- DONE ---");
            Console.ReadKey();
        }

        public static async Task MainAsync(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Specify the url with product listing and a csv file to write to");
                return;
            }

            string url = args[0];
            string outPath = args[1];

            var objects = await GetEntries(url);

            var uniqueKeys = objects.SelectMany(dic => dic.Keys).Distinct();

            using (TextWriter writer = new StreamWriter(outPath, false, System.Text.Encoding.UTF8))
            {
                var csv = new CsvWriter(writer);
                csv.Configuration.Delimiter = ";";
                foreach (var key in uniqueKeys)
                    csv.WriteField(key);
                csv.NextRecord();

                foreach (var obj in objects)
                {
                    foreach (var key in uniqueKeys)
                    {

                        string value;
                        if (!obj.TryGetValue(key, out value))
                            value = "";

                        csv.WriteField(value);
                    }
                    csv.NextRecord();
                }
                writer.Flush();
            }


        }

        private static async Task<List<Dictionary<string, string>>> GetEntries(string url)
        {
            var objects = new List<Dictionary<string, string>>();
            while (!string.IsNullOrEmpty(url))
            {
                try
                {
                    var doc = await web.LoadFromWebAsync(url);


                    var nextPageLinkNode = doc.DocumentNode.QuerySelector(".pagination__link[rel='next']");

                    var links = doc.DocumentNode.QuerySelectorAll(".product__title");
                    foreach (var link in links)
                    {
                        var detailUrl = link.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(detailUrl))
                        {
                            try
                            {
                                Console.Error.WriteLine("Fetching " + detailUrl);
                                var details = await GetDetails(detailUrl);

                                objects.Add(details);
                           //     Console.WriteLine(string.Join(Environment.NewLine, details.Select(pair => pair.Key + "=" + pair.Value)));

                                Task.Delay(500);
                            }
                            catch (Exception ex)
                            {
                                Console.Error.WriteLine("Error getting details: " + ex.GetType().FullName + " - " + ex.Message);
                            }
                        }
                    }

                    var baseUrl = url.Split("?")[0];
                    if (nextPageLinkNode != null)
                    {
                        var href = nextPageLinkNode.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(href))
                            url = baseUrl + href;
                    }
                    else
                        url = null;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine("Error getting page " + url + ": " + ex.GetType().FullName + " - " + ex.Message);
                    url = null;
                }
            }

            return objects;
        }

        private static async Task<Dictionary<string, string>> GetDetails(string detailUrl)
        {
            var doc = await web.LoadFromWebAsync(detailUrl);

            Dictionary<string, string> props = new Dictionary<string, string>();
            var name = doc.DocumentNode.QuerySelector("meta[property='og:title']")?.GetAttributeValue("content", "");
            props["name"] = name;

            var price = doc.DocumentNode.QuerySelector(".sales-price__current").InnerText.Trim();
            props["price"] = price;

            props["url"] = detailUrl;

            var specsItems = doc.DocumentNode.QuerySelectorAll(".product-specs__list-item");
            foreach (var item in specsItems)
            {
                var key = item.QuerySelector(".product-specs__help-title")?.InnerText?.Trim();
                var value = item.QuerySelector(".product-specs__item-spec")?.InnerText?.Trim();
                if (!string.IsNullOrEmpty(key))
                {
                    key = HtmlEntity.DeEntitize(key);

                    if (!string.IsNullOrEmpty(value))
                        value = HtmlEntity.DeEntitize(value);


                    props[key] = value;
                }
            }
            return props;
        }
    }
}
