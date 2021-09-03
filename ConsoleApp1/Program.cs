using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ConsoleApp1
{
    class Program
    {
        static void Main(string[] args)
        {
            test().GetAwaiter().GetResult();
        }

        private static async Task test()
        {
            SemaphoreSlim semaphore = new SemaphoreSlim(6);
            var destinations = new List<string>{
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

        private static async Task GetDataFromLuxStay(string destiation)
        {
            Thread.Sleep(2000);
            Console.WriteLine(destiation);
            await Task.FromResult(true);
        }

    }
}
