using Crawler.WebBrowser.Model;
using HtmlAgilityPack;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler.WebBrowser
{
    class Program
    {
        private static readonly By elementType = By.TagName("a");

        static void Main(string[] args)
        {
          //  GetDataFromLuxStay().GetAwaiter().GetResult();

            GetDataFromAgoda().GetAwaiter().GetResult();
        }

        private static async Task GetDataFromLuxStay()
        {
            // Initialize the Chrome Driver
            using (var driver = new ChromeDriver())
            {
                // Go to the home page
                driver.Navigate().GoToUrl("https://www.luxstay.com/vi/");
                Thread.Sleep(3000);
                var searchDestination = driver.FindElementById("search-input");
                searchDestination.SendKeys(" Ho Chi Minh");
                Thread.Sleep(2000);
                var locations = driver.FindElementsByClassName("search-suggest__item").ToList();
                Thread.Sleep(2000);
                var destination = locations.FirstOrDefault();
                destination.Click();
                var dateStartTime = DateTime.Now.ToString("yyyy-MM-dd");
                var dateEndTime = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
                var dateStart = driver.FindElementByXPath($"//button[@date='{dateStartTime}']");
                dateStart.Click();
                var dateEnd = driver.FindElementByXPath($"//button[@date='{dateEndTime}']");
                dateEnd.Click();

                Thread.Sleep(5000);
                var actions = driver.FindElementsByClassName("ng-binding").ToList();
                var skip = actions?.FirstOrDefault(s => String.Equals(s.Text, "Bỏ qua"));
                if (skip != null) skip.Click();

                Thread.Sleep(2000);
                var searchGuest = driver.FindElementById("search-guest-button");
                if (!searchGuest.Selected && searchGuest != null) searchGuest.Click();

                Thread.Sleep(2000);
                var addGuest = driver.FindElementByClassName("el-input-number__increase");
                if (addGuest != null) addGuest.Click();

                Thread.Sleep(2000);
                var buttonApply = driver.FindElementByClassName("c-primary");
                if (buttonApply != null) buttonApply.Click();

                Thread.Sleep(5000);

                var rooms = driver.FindElementsByClassName("promo");
                var roomIds = driver.FindElementsByClassName("promo");
                var roomsinfo = new List<RoomInfo>();
                string[] stringSeparators = new string[] { "\r\n" };
                foreach (var item in rooms)
                {
                    var roomId = item.FindElement(elementType)?.GetAttribute("data-value");
                    var info = item.Text.Split(stringSeparators, StringSplitOptions.None);
                    var length = info.Count();

                    if (length == 4)
                    {
                        roomsinfo.Add(new RoomInfo()
                        {
                            RoomId = roomId,
                            RoomType = info[0].Split('-')[0].Trim(),
                            QuantityRoomBedQuantity = info[0].Split('-')[1].Trim(),
                            Rated = info[1],
                            HotelName = info[2],
                            Price = info[3]
                        });
                    }
                    else if (length == 3)
                    {
                        roomsinfo.Add(new RoomInfo()
                        {
                            RoomId = roomId,
                            RoomType = info[0].Split('-')[0].Trim(),
                            QuantityRoomBedQuantity = info[0].Split('-')[1].Trim(),
                            Rated = string.Empty,
                            HotelName = info[1],
                            Price = info[2]
                        });
                    }
                }

                //await GetLuxStayRoomDetailInfoByAPI(roomsinfo);
            }
            await Task.FromResult(true);
        }

        private static async Task GetDataFromAgoda()
        {

            var vnwUrl = "https://ohmyhotel.com/search?opts.destination.categoryId=1&opts.destination.categoryName=City&opts.destination.id=24542&opts.destination.name=Th%C3%A0nh%20ph%E1%BB%91%20H%E1%BB%93%20Ch%C3%AD%20Minh,%20Vi%E1%BB%87t%20Nam&opts.destination.code=24542&opts.period.checkIn=2021-08-11&opts.period.checkOut=2021-08-12&opts.occupancy.rooms=1&opts.occupancy.adults=1&opts.occupancy.children=0&trans.currency=VND&trans.language=vi-VN&track=0.9330870563857758";
            using (var driver = new ChromeDriver())
            {
                driver.Navigate().GoToUrl(vnwUrl);
                Thread.Sleep(3000);

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(driver.PageSource);
                var elements = htmlDocument.DocumentNode.Descendants("div")
                    .Where(element => element.GetAttributeValue("class", "")
                                    .Equals("vue-recycle-scroller__item-view")).ToList();
                foreach (var item in elements)
                {

                }
            }

            //var htmlDocument = new HtmlDocument();
            //htmlDocument.LoadHtml(html);
            //var elements = htmlDocument.DocumentNode.Descendants("div")
            //    .Where(element => element.GetAttributeValue("class", "")
            //                    .Equals("vue-recycle-scroller__item-view")).ToList();
            //foreach (var item in elements)
            //{

            //}
        }

        private static async Task GetLuxStayRoomDetailInfoByAPI(ICollection<RoomInfo> roomInfos)
        {
            var url = $"https://api.luxstay.com/api/rooms/" + roomInfos.FirstOrDefault().RoomId.ToString();
            var httpClient = new HttpClient();
            var infoResponse = await httpClient.GetAsync(url);
            var roomInfo = infoResponse.Content.ReadAsStringAsync();
        }
    }
}
