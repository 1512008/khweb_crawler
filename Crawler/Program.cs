using CsvHelper;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Crawler
{
    class Program
    {
        static void Main(string[] args)
        {
            CrawlerData().GetAwaiter().GetResult();
        }

        private static async Task CrawlerData()
        {
            var vnwUrl = "https://www.careerlink.vn/viec-lam/k/.net";

            var httpClient = new HttpClient();
            var html = await httpClient.GetStringAsync(vnwUrl);

            var htmlDocument = new HtmlDocument();
            htmlDocument.LoadHtml(html);
            var elements = htmlDocument.DocumentNode.Descendants("div")
                .Where(element => element.GetAttributeValue("class", "")
                                .Equals("media p-3 p-lg-4 align-items-lg-center")).ToList();
            var values = new List<CrawlerDataModel>();
            string path = Path.Combine(Directory.GetParent(System.IO.Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName, "Crawler_StaticWeb.csv");
            System.IO.File.Delete(path);
            foreach (var element in elements)
            {
                var title = element.Descendants("a").FirstOrDefault().InnerText;
                var companyName = element.Descendants("a").Where(element => element.GetAttributeValue("class", "").Equals("text-dark job-company mb-1 d-inline-block line-clamp-1")).FirstOrDefault().InnerText;
                var location = element.Descendants("a").Where(element => element.GetAttributeValue("class", "").Equals("text-reset")).FirstOrDefault().InnerText;
                var salary = element.Descendants("span").Where(element => element.GetAttributeValue("class", "").Equals("job-salary text-primary d-flex align-items-center")).FirstOrDefault().InnerText;
                values.Add(new CrawlerDataModel()
                {
                    Title = title.Replace("\n","").Replace("\t",""),
                    CompanyName = companyName.Replace("\n", "").Replace("\t", ""),
                    Location = location.Replace("\n", "").Replace("\t", ""),
                    Salary = salary.Replace("\n", "").Replace("\t", ""),
                });
            }

            var cul = new CultureInfo("vi");
            var writer = new StreamWriter(path, false);
            var csvWriter = new CsvWriter(writer, cul);
            csvWriter.WriteField("Number");
            csvWriter.WriteField("Title");
            csvWriter.WriteField("CompanyName");
            csvWriter.WriteField("Location");
            csvWriter.WriteField("Salary");
            csvWriter.NextRecord();
            var i = 1;
            foreach (var value in values)
            {
                csvWriter.WriteField(i);
                csvWriter.WriteField(value.Title);
                csvWriter.WriteField(value.CompanyName);
                csvWriter.WriteField(value.Location);
                csvWriter.WriteField(value.Salary);
                csvWriter.NextRecord();
                i++;
            }
            writer.Close();
            Console.WriteLine("Crawler static web successful!!");
        }

        private class CrawlerDataModel
        {
            public string Title { get; set; }

            public string CompanyName { get; set; }

            public string Location { get; set; }

            public string Salary { get; set; }
        }
    }
}
