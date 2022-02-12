using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Pipeline
{
    public class Parser
    {
        private readonly ParserSettings _settings;
        private readonly HtmlDocument _htmlDoc;
        private readonly ILogger<Parser> _logger;
        public Parser(IConfigurationSection settings, ILogger<Parser> logger)
        {
            _settings = settings.Get<ParserSettings>();
            _logger = logger;
            _htmlDoc = GetDocument($"{_settings.Url}/index.html", _settings.Username, _settings.Password);
        }





        public Task<List<Book>> Extract(string xPath)
        {
            var books = new List<Book>();

            var genreList = _htmlDoc.DocumentNode.SelectNodes(xPath);
            Parallel.ForEach(genreList, async genre =>
            {   
                var node = genre.SelectSingleNode("a");
                var genreUrl = GetUri(_settings.Url, node, "href");
                _logger.LogInformation("url:{@logger}", genreUrl);
                var booksByGenre = await ExtractByGenre(genreUrl, genre.InnerText);
                await foreach (Book book in booksByGenre) books.Add(book);
            });
            return Task.FromResult(books);
        }

        private Task<IAsyncEnumerable<Book>> ExtractByGenre(string url, string genre)
        {
            var books = new List<Book>();
            var htmlDoc = GetDocument(url);
            var booksList = htmlDoc.DocumentNode.SelectNodes("//article[@class=\"product_pod\"]");
            foreach(var book in booksList)
            {
                
                var newBook = new Book
                {
                    PageLink = GetUri(_settings.Url,
                    book.SelectSingleNode("//h3/a"), "href"),
                    ImageLink = GetUri(_settings.Url,
                    book.SelectSingleNode("//div[@class=\"image_container\"]/a/img"),
                     "src"),
                    Genre = genre,
                    BookName = book.SelectSingleNode("//h3/a").InnerText,
                    Amount = Convert.ToDecimal(
                        (book.SelectSingleNode("//p[@class=\"price_color\"]")
                        .InnerText.Split("£"))[1]),
                    Rating = GetRating(book.SelectSingleNode("//p[contains(@class, \"star-rating\")]")),
                    InStock = CheckInstock(book.SelectSingleNode("//p[@class=\"instock availability\"]"))

                };
                _logger.LogInformation("Book Added : {@book}", newBook.Genre);
                books.Add(newBook);

            };
            return Task.FromResult(books.ToAsyncEnumerable());
        }
        private static string GetUri(string baseUrl, HtmlNode relativePath, string attribute = "")
        {
            var url = new Uri(new Uri(baseUrl), relativePath.Attributes[attribute].Value).AbsoluteUri;
            return url;
        }
        private static HtmlDocument GetDocument(string url, string username = null, string password = null)
        {
            var doc = new HtmlWeb().LoadFromWebAsync(url, Encoding.UTF8, username, password);
            return doc.Result;
        }
        private static int GetRating(HtmlNode node)
        {
            var dict = new Dictionary<string, int> {
            {"One", 1},{"Two", 2},{"Three",3},{"Four", 4},{"Five", 5}};
            var nodeValue = node.Attributes["class"].Value.Split(" ")[1];
            return dict[nodeValue];
        }

        private static bool CheckInstock(HtmlNode node)
        {
            return node.InnerText == " In stock ";
        }
    }
}