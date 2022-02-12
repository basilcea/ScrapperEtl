using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Pipeline
{
    public class EntryController : Controller
    {
        public IActionResult Index()
        {
            string url = "https://en.wikipedia.org/wiki/List_of_programmers";
            string accessToken ="";
            var response = CallUrl(url, accessToken);
            return View();
        }

        private static async Task<string> CallUrl(string fullUrl, string accessToken)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue ("Bearer", accessToken);
            var response = client.GetStringAsync(fullUrl);
            return await response;
        }

    }
}