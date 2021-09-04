using Crawler.WebBrowser.Model;
using HtmlAgilityPack;
using Newtonsoft.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
            // start watch
            var watch = Stopwatch.StartNew();

            // get room info form lux stay
            HandleMultipleThreadLuxStay().GetAwaiter().GetResult();

            // get room info form agoda
            HandleMultipleThreadAgoda().GetAwaiter().GetResult();

            // end watch
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;

            // stop program
            Console.Clear();
            Console.WriteLine("\n\n\n");
            Console.WriteLine("***********************************************");
            Console.WriteLine($"Crawling data successful.Spend time: {elapsedMs}");
            Console.WriteLine("Press any key to stop...");
            Console.WriteLine("***********************************************");
            WriteFile(new List<RoomInfo>(), "time_run.txt", elapsedMs.ToString());
            Console.ReadKey();
        }

        private static async Task GetDataFromLuxStay(string destination)
        {
            //Initialize the Chrome Driver
            using (var driver = new ChromeDriver())
            {
                // Go to the home page
                driver.Navigate().GoToUrl("https://www.luxstay.com/vi/");
                Thread.Sleep(3000);
                driver.Manage().Window.Maximize();
                Thread.Sleep(1000);
                var searchDestination = driver.FindElementById("search-input");
                searchDestination.SendKeys($" {destination}");
                Thread.Sleep(2000);
                var locations = driver.FindElementsByClassName("search-suggest__item").ToList();
                Thread.Sleep(2000);
                var destinationBtn = locations.FirstOrDefault();
                destinationBtn.Click();
                var dateStartTime = DateTime.Now.ToString("yyyy-MM-dd");
                var dateEndTime = DateTime.Now.AddDays(2).ToString("yyyy-MM-dd");
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

                var roomsElements = driver.FindElementsByClassName("promo");
                var roomsinfo = new List<RoomInfo>();
                string[] stringSeparators = new string[] { "\r\n" };
                foreach (var item in roomsElements)
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
                            Price = info[3].Substring(0, info[3].LastIndexOf('/'))
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
                            Price = info[2].Substring(0, info[2].LastIndexOf('/'))
                        });
                    }
                }

                _ = GetLuxStayRoomDetailInfoByAPI(roomsinfo, destination);
            }
            await Task.FromResult(true);
        }

        private static async Task GetDataFromAgoda(string destination, string destinationUrl)
        {
            var rooms = new List<RoomInfo>();
            using (var driver = new ChromeDriver())
            {
                driver.Navigate().GoToUrl(destinationUrl);
                driver.Manage().Window.Maximize();
                Thread.Sleep(2000);
                for (int i = 0; i < 5; i++)
                {
                    driver.ExecuteScript("window.scrollBy(0,1050)", "");
                    Thread.Sleep(3000);
                }

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
                    if (!string.IsNullOrEmpty(price))
                        rooms.Add(new RoomInfo()
                        {
                            HotelName = hotelName,
                            Price = price,
                            Rated = rated,
                            Address = address
                        });
                }

                WriteFile(rooms, $"room_data_agoda_{destination.Replace(" ", "_")}.txt");
            }
            await Task.FromResult(true);
        }

        private static async Task GetLuxStayRoomDetailInfoByAPI(ICollection<RoomInfo> roomInfos, string destination)
        {

            using (var httpClient = new HttpClient())
            {
                foreach (var item in roomInfos)
                {
                    var url = $"https://api.luxstay.com/api/rooms/" + item.RoomId.ToString();
                    var infoResponse = await httpClient.GetAsync(url);
                    var roomInfo = await infoResponse.Content.ReadAsStringAsync();
                    var deserialized = JsonConvert.DeserializeObject<dynamic>(roomInfo);
                    item.Address = deserialized?.data?.address?.data?.full_address ?? string.Empty;
                    item.MaxPrice = deserialized?.data?.price?.data?.nightly_price_vnd ?? string.Empty;
                    Thread.Sleep(1500);
                }
            }

            WriteFile(roomInfos, $"room_data_lux_stay_{destination.Replace(", Ho Chi Minh", "").Replace(" ", "_")}.txt");
        }

        private static async Task HandleMultipleThreadLuxStay()
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(5, 10);
            var destinations = new List<string> {
                "quan 1, Ho Chi Minh",
                 "quan 2, Ho Chi Minh",
                 "quan 3, Ho Chi Minh",
                 "quan 4, Ho Chi Minh",
                 "quan 5, Ho Chi Minh",
                 //"quan 6, Ho Chi Minh", home stay do not have data in district 6
                 "quan 7, Ho Chi Minh",
                 "quan 8, Ho Chi Minh",
                 "quan 9, Ho Chi Minh",
                 "quan 10, Ho Chi Minh",
                 //"quan 11, Ho Chi Minh", home stay do not have data in district 11
                 "quan 12, Ho Chi Minh",
                 "quan tan binh, Ho Chi Minh",
                 //"quan binh tan, Ho Chi Minh", home stay do not have data in binh tan district 
                 "quan phu nhuan, Ho Chi Minh",
                 "quan binh thanh, Ho Chi Minh",
                 "quan go vap, Ho Chi Minh",
            };
            var tasks = new List<Task>();

            tasks = destinations.Select(s => Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await GetDataFromLuxStay(s);
                    return;
                }
                finally
                {
                    semaphore.Release();
                }
            })).ToList();

            await Task.WhenAll(tasks);
        }

        private static async Task HandleMultipleThreadAgoda()
        {
            var checkIn = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var checkOut = DateTime.UtcNow.AddDays(2).ToString("yyyy-MM-dd");

            SemaphoreSlim semaphore = new SemaphoreSlim(5, 10);
            var destinations = new Dictionary<string, string> {
                { "quan 1", "https://www.agoda.com/search?guid=1a2babdf-20af-4261-a0f9-085be1c0edf0&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwES9fRGHnPTpjJwT6ErV8M7j0pbngfvHXOkDFiUa5YN%2FHhHh%2BF4RB%2BXJZiC1BIbAv9AqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8WobEiNwKatvoXAhe6N4YuKcnUHb%2BKC2e3zym6tmyvlzCzM%3D&area=93586&tick=637661342313&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=4ddf76c5-408b-4631-b0e5-4378d63ffbae&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6021&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=District%201&travellerType=1&familyMode=off&productType=-1" },
                { "quan 2","https://www.agoda.com/search?guid=1a2babdf-20af-4261-a0f9-085be1c0edf0&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwES9fRGHnPTpjJwT6ErV8M7j0pbngfvHXOkDFiUa5YN%2FHhHh%2BF4RB%2BXJZiC1BIbAv9AqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8WobEiNwKatvoXAhe6N4YuKcnUHb%2BKC2e3zym6tmyvlzCzM%3D&area=93586&tick=637661342313&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=4ddf76c5-408b-4631-b0e5-4378d63ffbae&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6021&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=District%201&travellerType=1&familyMode=off&productType=-1"},
                { "quan 3","https://www.agoda.com/search?guid=5789fc63-d02e-4267-a7b2-74f57ab802c1&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2F0NG0ju93IANelaodUtTxKwTrlDIkbFXxBHn7hoHloRX0pbngfvHXOkDFiUa5YN%2FHk%2FGRcFTARQxU5nyX044VcgqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8WobXoLiuO11Qyukqo%2BVF4c8Jg%3D%3D&area=31585&tick=637661344310&txtuuid=5789fc63-d02e-4267-a7b2-74f57ab802c1&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=c3017010-e9be-4dcf-8817-8cc5e90af87e&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=hk-crweb-2001&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=district%203&travellerType=1&familyMode=off&productType=-1"},
                { "quan 4","https://www.agoda.com/search?guid=916e329b-19c2-4265-ad1a-2c02d946ace3&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwEdwT5fH2X2nqVvxcQsM%2FyfT0pbngfvHXOkDFiUa5YN%2FHhHh%2BF4RB%2BXJZiC1BIbAv9AqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8WobJNSQbdqmw%2FR1h5%2FKp%2Fckn0Hb%2BKC2e3zym6tmyvlzCzM%3D&area=57519&tick=637661344495&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=3e959325-3280-444d-8eab-8e7309ea7669&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=hk-crweb-2007&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=District%204&productType=-1&travellerType=1&familyMode=off"},
                { "quan 5","https://www.agoda.com/search?guid=f9f87409-c594-4267-9189-1dc053784e0f&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwEbjh56sOv4FQ2rK5U16SAnb0pbngfvHXOkDFiUa5YN%2FHhHh%2BF4RB%2BXJZiC1BIbAv9AqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8Wobjerfo9kPaVCC3yzr2UlBEEHb%2BKC2e3zym6tmyvlzCzM%3D&area=31586&tick=637661344851&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=1afd8317-96fc-4b41-b5e3-eabf236ea882&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=hk-crweb-2009&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=District%205&travellerType=1&familyMode=off&productType=-1"},
                { "quan 6","https://www.agoda.com/search?guid=df71cca8-244f-443d-bb4c-11750c35e0d6&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwEZ6OZJkAYR%2BqsX%2Bz18%2F3mW30pbngfvHXOkDFiUa5YN%2FHk%2FGRcFTARQxU5nyX044VcgqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8WobDmwyW9DCZPSlIiqyyohiQg%3D%3D&area=57546&tick=637661345007&txtuuid=df71cca8-244f-443d-bb4c-11750c35e0d6&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=2b623f72-9bdb-49dc-bd7f-099ce3a7917e&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6010&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=District%206&travellerType=1&familyMode=off&productType=-1"},
                { "quan 7","https://www.agoda.com/search?guid=ff6013eb-7f29-49fc-80d6-050f80930c27&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwETMy8xk2mAGb378l79Lpwmr0pbngfvHXOkDFiUa5YN%2FHhHh%2BF4RB%2BXJZiC1BIbAv9AqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8Wob7GSbkcJk9OPh9mhuhE%2FsjUHb%2BKC2e3zym6tmyvlzCzM%3D&area=57520&tick=637661345173&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=78ec0251-3e7b-437b-8b3c-9b4570490088&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6004&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=District%207&productType=-1&travellerType=1&familyMode=off"},
                { "quan 8","https://www.agoda.com/search?guid=d321f45a-580d-4e41-b61a-e5b97f454bfe&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwEeIqtaqzyOZPTLgVgOEquCj0pbngfvHXOkDFiUa5YN%2FHk%2FGRcFTARQxU5nyX044VcgqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8WobC%2B3gsVZYHN4hYdct89mMtQ%3D%3D&area=378409&tick=637661345358&txtuuid=d321f45a-580d-4e41-b61a-e5b97f454bfe&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=e587d0f7-2fa2-4e14-855d-363239a8fc9f&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6005&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=District%208&productType=-1&travellerType=1&familyMode=off"},
                { "quan 9","https://www.agoda.com/search?guid=df7523f8-5ab2-400b-8b6f-6dae4b7351c7&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwEbEs1u7gho6RHae94RejJPL0pbngfvHXOkDFiUa5YN%2FHk%2FGRcFTARQxU5nyX044VcgqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8WobgZYkiAggYQwP02HN5THpQg%3D%3D&area=503629&tick=637661345507&txtuuid=df7523f8-5ab2-400b-8b6f-6dae4b7351c7&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=18f9722e-b4bd-48a2-b08a-29d40790173d&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6018&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=2&children=0&priceCur=VND&los=1&textToSearch=District%209&productType=-1&travellerType=1&familyMode=off"},
                { "quan 10","https://www.agoda.com/search?area=57522&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=e43c3cf9-da76-4a74-9a78-ed53775b8cc4&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6027&trafficGroupId=1&sessionId=5ayu2nfd3zrbrssvqbly4vuz&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn=2021-09-02&checkOut=2021-09-03&rooms=1&adults=1&children=0&priceCur=VND&los=1&textToSearch=District%2010&travellerType=0&familyMode=off&productType=-1"},
                { "quan 11","https://www.agoda.com/search?guid=6a8986b4-895f-4583-98b2-5f767ef44718&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwEYmdjJfN0ZWcT%2FqxI0IO%2FkPyxbcRNb9CIoFpAXEj0fkHhkG%2Bvpkzu4Cc7%2BZuNb4qGHSk%2FM8eVuQYqDHVLhv%2F6oNyO93GULc5006Ft9qOsApPfH5pNrIBg5FU3MCQIjlG01ur%2Ftc%2B54Iv4fnCDM4f8d8%3D&area=57526&tick=637661345924&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=a73c1767-26ba-483d-9f0d-06aca0ed8698&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6006&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=1&children=0&priceCur=VND&los=2&textToSearch=District%2011&travellerType=0&familyMode=off&productType=-1"},
                { "quan 12","https://www.agoda.com/search?guid=28399b8c-471a-4314-b0ff-baadfda218fe&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FQag7inMrcpSc7vYIumAwEeoQDdE%2Ffo%2FHgnF1XX3PUn3yxbcRNb9CIoFpAXEj0fkHhkG%2Bvpkzu4Cc7%2BZuNb4qGHSk%2FM8eVuQYqDHVLhv%2F6oNyO93GULc5006Ft9qOsApPnPWD3WftOeH1QNbf31bVjVur%2Ftc%2B54Iv4fnCDM4f8d8%3D&area=93432&tick=637661346281&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=f260ebe7-17a2-4bb3-922a-e32e5ff12ccb&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6025&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=1&children=0&priceCur=VND&los=2&textToSearch=District%2012&travellerType=0&familyMode=off&productType=-1"},
                { "quan binh thanh","https://www.agoda.com/search?guid=e242abf2-265f-4d1e-b6ca-86b422b3d1fc&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FvqhleTcf3GaxEvbltkHPLsNp%2FXi6VTNEA30yFkMZMKD0pbngfvHXOkDFiUa5YN%2FHhHh%2BF4RB%2BXJZiC1BIbAv9AqQMycviD3CIWSJVwpQ8soBAIjwUkfjrVaLXVAl8Wob0d8PUSXlSN87gie1myYbbEHb%2BKC2e3zym6tmyvlzCzM%3D&area=61314&tick=637661346745&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=9647d0d0-8489-4e5e-b950-68a1946786dc&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6018&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=1&children=0&priceCur=VND&los=2&textToSearch=Binh%20Thanh&travellerType=0&familyMode=off&productType=-1"},
                { "quan phu nhuan","https://www.agoda.com/search?guid=39dd5ccd-de19-4059-9a80-bf2f730b15ce&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FsFLlYmss%2FmtrfGPRTlbvtuCaMHJVzjMgzaf1R2ASECQ%2FDQyX633GUKa9SfyLNSWOBtms%2B8%2BX5FaFP4OoxwMZbm0t3c82nJ%2Fp%2B0GXkwK5hQ%2BaOlfXVWkDDc%2Fx01SEYrqv%2BO%2BHcmM9GO9IDuQoPnhb38j%2BxG%2F0EXyqEpLKdlme8IQ%3D&area=93597&tick=637661346974&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=9c960eb9-bd70-49e7-8c28-cfabc833a033&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6007&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=1&children=0&priceCur=VND&los=2&textToSearch=Phu%20Nhuan&travellerType=0&familyMode=off&productType=-1"},
                { "quan go vap","https://www.agoda.com/search?guid=6bab9e85-be36-4ffb-ae0c-aec1fa9ccec2&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FLLc18%2F9gne18IqyxDH3Q2GesqXRVaa5PqdRAXb9LP3Blp9FQJNmkZik6kF13FVIVr6QDs48C6hOjLzuYUvlEgOm%2B3QacrQMDUE7JkJAfzu2cjdkv0%2BNN7V68ZSPgq2Qf62kdEP1CStROcMMwLePaYQ%3D%3D&area=57521&tick=637661347227&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=fa84d81c-e166-434d-a37e-77747308b468&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6016&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=1&children=0&priceCur=VND&los=2&textToSearch=Go%20Vap&productType=-1&travellerType=0&familyMode=off"},
                { "quan tan binh","https://www.agoda.com/search?guid=b005f66d-a23c-40de-9286-89d2c5ca1ec1&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2F%2BAUqk68yu5j48lWdVrQ3UnnlSY5ofCGtyUBV137kbJNo7N6lXBkLO%2Fa39huibtrcdm2fcC86kYP4qSwb4Gfbc41vXGtuywoUoaiplWNdnsUBTwinyw0evJrQxyDCuHv4te6S5PREE%2FYhrS8rqZD8Hsj%2BxG%2F0EXyqEpLKdlme8IQ%3D&area=481744&tick=637661347424&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=7b4cb067-5f6e-4a90-b65c-361534a1e5c4&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6004&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=1&children=0&priceCur=VND&los=2&textToSearch=Tan%20Binh&productType=-1&travellerType=0&familyMode=off"},
                { "quan binh tan","https://www.agoda.com/search?guid=04bbe790-22ce-4855-98db-6dbf8af37d5d&asq=NQVGXW6jsE3tbdY9S%2BqUCpufa9Vwpz6XltTHq4n%2B9gPt6Sc9VYM%2BOtJvOdzFsuZ%2FvqhleTcf3GaxEvbltkHPLsENUgA%2F%2FNojAXmRJ8vxB91o7N6lXBkLO%2Fa39huibtrcdm2fcC86kYP4qSwb4Gfbc41vXGtuywoUoaiplWNdnsUBTwinyw0evJrQxyDCuHv4LUd3OYJoutSSSz6TxRHkosj%2BxG%2F0EXyqEpLKdlme8IQ%3D&area=481388&tick=637661348456&locale=en-us&ckuid=337fdcb2-b143-444f-91d7-3af4ac0d33c3&prid=0&currency=VND&correlationId=29b7407a-bc9a-480a-bdf9-ded6943d6784&pageTypeId=103&realLanguageId=1&languageId=1&origin=VN&cid=1844104&userId=337fdcb2-b143-444f-91d7-3af4ac0d33c3&whitelabelid=1&loginLvl=0&storefrontId=3&currencyId=78&currencyCode=VND&htmlLanguage=en-us&cultureInfoName=en-us&machineName=sg-crweb-6010&trafficGroupId=1&sessionId=avf52a2jea2xgy0aom5xxf20&trafficSubGroupId=84&aid=130589&useFullPageLogin=true&cttp=4&isRealUser=true&mode=production&checkIn={checkIn}&checkOut={checkOut}&rooms=1&adults=1&children=0&priceCur=VND&los=2&textToSearch=Binh%20Tan&travellerType=0&familyMode=off&productType=-1"},
            };

            var tasks = new List<Task>();

            tasks = destinations.Select(s => Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    await GetDataFromAgoda(s.Key, s.Value);
                    return;
                }
                finally
                {
                    semaphore.Release();
                }
            })).ToList();

            await Task.WhenAll(tasks);
        }

        private static void WriteFile(ICollection<RoomInfo> roomInfos, string fileName,string timeRun = "")
        {
            string path = Path.Combine(Directory.GetParent(Directory.GetCurrentDirectory()).Parent.Parent.Parent.FullName, fileName);
            File.Delete(path);
            var data = !string.IsNullOrEmpty(timeRun) ? timeRun : JsonConvert.SerializeObject(roomInfos);
            File.WriteAllText(path, data);
        }
    }
}
