using System.IO;
using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Support.UI;

namespace Crawler.LoadBrowser
{
    class Program
    {
        static void Main(string[] args)
        {
            var driver = new FirefoxDriver();
            // Initialize the Chrome Driver
            //using (var driver = new ChromeDriver())
            //{
            //    // Go to the home page
            //    driver.Navigate().GoToUrl("https://ohmyhotel.com/");

            //    // Get the page elements
            //    var userNameField = driver.FindElementById("usr");
            //    var userPasswordField = driver.FindElementById("pwd");
            //    var loginButton = driver.FindElementByXPath("//input[@value='Login']");

            //    // Type user name and password
            //    userNameField.SendKeys("admin");
            //    userPasswordField.SendKeys("12345");

            //    // and click the login button
            //    loginButton.Click();

            //    // Extract the text and save it into result.txt
            //    var result = driver.FindElementByXPath("//div[@id='case_login']/h3").Text;
            //    File.WriteAllText("result.txt", result);
            //}
        }
    }
}
