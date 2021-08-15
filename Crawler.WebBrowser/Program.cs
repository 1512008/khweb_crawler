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
                            RoomBed = info[0].Split('-')[1].Trim(),
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
                            RoomBed = info[0].Split('-')[1].Trim(),
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

        private static Task GetDataFromAgoda()
        {
            var checkIn = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var checkOut = DateTime.UtcNow.AddDays(1).ToString("yyyy-MM-dd");
            var rooms = new List<RoomInfo>();
            var vnwUrl = $"https://www.agoda.com/search?guid=e6c6768f-986a-425d-b4f0-297d23d8b9c5&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2F5kMzEej9SJ9XOFLrMHApsfrXF%2FveKVgBKI%2FRAtbae%2BTcVDyrNRiphdYIhRTeYe0vRvjiPwOQzlyjMO%2FTwXbiC4pK0j93MeO9eTQGF7dDDKl6KraawkN4LAFofMwsDDRU6pcwohZHIJB5tf%2F5dGSvoqiY6qLH8RctyzDbtjPZR3M%3D&city=13170&tick=637638004484&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=12bc6db4-aa2b-453e-b466-1d45460a3b3a&pageTypeId=1&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6014&trafficGroupId=1&sessionId=xmdksygkgjrtdcay2dc2r5jk&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=Ho%20Chi%20Minh%20City&travellerType=1&familyMode=off";
            using (var driver = new ChromeDriver())
            {
                driver.Navigate().GoToUrl(vnwUrl);
                Thread.Sleep(3000);

                var htmlDocument = new HtmlDocument();
                htmlDocument.LoadHtml(driver.PageSource);
                var elements = htmlDocument.DocumentNode.Descendants("li")
                    .Where(element => element.GetAttributeValue("class", "")
                                    .Equals("PropertyCard PropertyCardItem")).ToList();
                foreach (var element in elements)
                {
                    var hotelName = element.Descendants("h3").Where(e => e.GetAttributeValue("class", "").Equals("PropertyCard__HotelName")).FirstOrDefault()?.InnerText;
                    var address = element.Descendants("span").Where(e => e.GetAttributeValue("class", "").Equals("Address__Text")).FirstOrDefault()?.InnerText;
                    var rated = element.Descendants("p").Where(e => e.GetAttributeValue("class", "").Equals("sc-ctaXAZ kLsGUE kite-js-Typography ")).FirstOrDefault()?.InnerText;
                    var price = element.Descendants("span").Where(e => e.GetAttributeValue("class", "").Equals("PropertyCardPrice__Value")).FirstOrDefault()?.InnerText;
                    rooms.Add(new RoomInfo()
                    {
                        HotelName = hotelName,
                        Price = price,
                        Rated = rated,
                        rom
                    });
                }

                return Task.FromResult(true);
            }
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
