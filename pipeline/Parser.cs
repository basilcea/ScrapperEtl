using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Json;

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

        public async Task ETL(string xPath)
        {
           var startTime = DateTime.Now;
            var genreList = _htmlDoc.DocumentNode.SelectNodes(xPath);
            var categoryBooks = new List<BooksByCategory>();
            Parallel.ForEach(genreList, async  genre => 
            {
                var node = genre.SelectSingleNode("a");
                var genreUrl = GetUri(_settings.Url, node, "href");
                await ExtractTransformByGenre(genreUrl, genre.InnerText.Trim(), categoryBooks);
            });
           
            var books = new Summary
            {
                NumberOfCategories = genreList.Count,
                NumberOfBooks = categoryBooks.Sum(x => x.NumberOfBooks),
                CategoryDetails = categoryBooks,
                DateCreated = DateTime.Now
            };
            var endTime = DateTime.Now;
            Console.WriteLine(endTime - startTime);
            var bookList = File.Create(@"result.json");
            await JsonSerializer.SerializeAsync(bookList, books);
        }
      

        private async Task<List<BooksByCategory>> ExtractTransformByGenre(string url, string genre, List<BooksByCategory> categoryBooks)
        {
            var books = new List<Book>();
            var nodeCollectionList = new List<HtmlNodeCollection>();
            var htmlDoc = GetDocument(url);
            var paging = htmlDoc.DocumentNode.SelectSingleNode("//ul[@class=\"pager\"]");
            // _logger.LogInformation("Transforming data");
            if (paging is not null)
            {
                var pages = int.Parse((paging.SelectSingleNode("//li[@class=\"current\"]").InnerText.Trim().Split(" "))[3]);
                Parallel.For (0, pages, async page => 
               {
                   var htmlDocs = GetDocument(url.Replace("index.html", $"page-{page + 1}.html"))
                        .DocumentNode.SelectNodes("//article[@class=\"product_pod\"]");
                        await RunParallel(htmlDocs, books, _settings.Url, genre);       
               });
           
            }
            else
            {
                var bookList = htmlDoc.DocumentNode.SelectNodes("//article[@class=\"product_pod\"]");
                await RunParallel(bookList, books, _settings.Url, genre);
            }
            categoryBooks.Add(new BooksByCategory
            {
                Category = genre,
                NumberOfBooks = books.Count,
                Books = books
            });

            return categoryBooks;
        }
        private Task RunParallel(HtmlNodeCollection bookCollectionNode,
        List<Book> books, string url,
        string genre)
        {
        var node = 0;
         foreach(var book in bookCollectionNode)
            {   
                books.Add(new Book
                {
                    PageLink = GetUri(url,
                    book.SelectNodes("//h3/a")[node], "href"),
                    ImageLink = GetUri(url,
                    book.SelectNodes("//div[@class=\"image_container\"]/a/img")[node],
                    "src"),
                    Genre = genre,
                    BookName = book.SelectNodes("//h3/a")[node].InnerText,
                    Amount = Convert.ToDecimal(
                        (book.SelectNodes("//p[@class=\"price_color\"]")[node]
                        .InnerText.Split("£"))[1]),
                    Rating = GetRating(book.SelectNodes("//p[contains(@class, \"star-rating\")]")[node]),
                    InStock = CheckInstock(book.SelectNodes("//p[@class=\"instock availability\"]")[node]),

                });
                node++;
                
            };
            return Task.CompletedTask;
    
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
            return node.InnerText.Trim() == "In stock";
        }


    }
}