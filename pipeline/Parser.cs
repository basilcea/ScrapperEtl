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

            var genreList = _htmlDoc.DocumentNode.SelectNodes(xPath);
            var categoryBooks = new List<BooksByCategory>();
            var categoriesCount = 0;
            var bookCount = 0;

            Parallel.ForEach(genreList, genre =>
            {
                categoriesCount++;
                var node = genre.SelectSingleNode("a");
                var genreUrl = GetUri(_settings.Url, node, "href");
                _logger.LogInformation("url:{@logger}", genreUrl);
                var booksByGenre = ExtractTransformByGenre(genreUrl, genre.InnerText.Trim(), categoryBooks);

            });
            bookCount = categoryBooks.AsEnumerable().Sum(x => bookCount + x.NumberOfBooks);
            var books = new Summary
            {
                NumberOfCategories = categoriesCount,
                NumberOfBooks = bookCount,
                CategoryDetails = categoryBooks,
                DateCreated = DateTime.Now

            };
            var BookList = File.Create(@"result.json");
            await JsonSerializer.SerializeAsync(BookList, books);
        }

        private Task<IAsyncEnumerable<BooksByCategory>> ExtractTransformByGenre(string url, string genre, List<BooksByCategory> categoryBooks)
        {
            var books = new List<Book>();
            var htmlDoc = GetDocument(url);
            var bookCount = 0;
            var paging = htmlDoc.DocumentNode.SelectSingleNode("//ul[@class=\"pager\"]");
            _logger.LogInformation("Transforming data");
            if (paging is not null)
            {
                var htmlNodeCollectionPerPage = new List<HtmlNodeCollection>();
                var pages = int.Parse((paging.SelectSingleNode("//li[@class=\"current\"]").InnerText.Trim().Split(" "))[3]);
                for(int i = 0; i < pages; i++){
                    htmlNodeCollectionPerPage.Add(
                        GetDocument(url.Replace("index.html", $"page-{i+1}.html"))
                        .DocumentNode.SelectNodes("//article[@class=\"product_pod\"]"));
                }
                 Parallel.ForEach(htmlNodeCollectionPerPage, htmlNodePerPage =>
                {
                    var (count, booksPerPage) =  RunParallel(htmlNodePerPage, books, bookCount, _settings.Url, genre);
                     bookCount += count;
                     books = booksPerPage;
                });
            }
            else
            {
                var bookList = htmlDoc.DocumentNode.SelectNodes("//article[@class=\"product_pod\"]");
                var (count , _) = RunParallel(bookList, books, bookCount, _settings.Url, genre);
                bookCount += count;
            }
            categoryBooks.Add(new BooksByCategory
            {
                Category = genre,
                NumberOfBooks = bookCount,
                Books = books
            });

            return Task.FromResult(categoryBooks.ToAsyncEnumerable());
        }
        private (int, List<Book>) RunParallel(HtmlNodeCollection BookCollectionNode,
        List<Book> books, int count, string url,
        string genre)

        {
            var node = 0;
            Parallel.ForEach(BookCollectionNode, book =>
            {
                count++;
                var newBook = new Book
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

                };
                _logger.LogInformation("Book Added : {@book}", newBook);
                books.Add(newBook);
                node++;

            });
            return (count, books);
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