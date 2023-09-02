namespace AmazonScraper;

using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Playwright;
using Newtonsoft.Json;
using System.Linq;
using System.Text.RegularExpressions;

public class Program {
    private const string CurrentStateFileName = "current_state.json";
    private static CultureInfo culture;
    private static CurrentState currentState = new() {
        MaxPages = 1,
        AmazonBaseUrl = "https://www.amazon.com.br",
        ProductReviewBaseUrl = "https://www.amazon.com.br/product-reviews",
        ProductsUrl = "https://www.amazon.com.br/s?k=livros",
        Delay = 5,
        ProductList = new List<string>(),
        Reviews = new List<Review>(),
        StoreLanguage = "pt-BR"
    };

    private static async Task SaveCurrentState() {
        Console.WriteLine($"saving current state: {currentState}");
        await File.WriteAllTextAsync(CurrentStateFileName, JsonConvert.SerializeObject(currentState));
    }

    private static async Task LoadCurrentState() {
        if (!File.Exists(CurrentStateFileName)) return;
        Console.WriteLine($"loading current state: {currentState}");
        var tFile = await File.ReadAllTextAsync(CurrentStateFileName);
        currentState = JsonConvert.DeserializeObject<CurrentState>(tFile);
    }

    public static async Task Navigate() {
        var random = new Random();

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(
            /*
                this is important if you did not installed the "embedded"
                browser. here I'am pointing the installed location of 
                chromium
            */
            new BrowserTypeLaunchOptions()
            {
                /*
                    Headless = true if you want to run the browser without 
                    rendring the main window
                */
                Headless = false,
                /*
                    system browser executable path
                */
                ExecutablePath = "/snap/bin/chromium" 
            }
        );

        var page = await browser.NewPageAsync();

        IResponse response = null;

        if (string.IsNullOrEmpty(currentState.NextUrl))
                currentState.NextUrl = currentState.ProductsUrl;
            
        Console.WriteLine($"started to scrape with these settings:\n{currentState}");
            
        do
        {
            try
            {
                currentState.ProductsUrl = currentState.NextUrl;
                response = await page.GotoAsync(currentState.NextUrl);
                if (response?.Ok != true)
                    throw new Exception("could not open page");

                var productsContainer = await page.QuerySelectorAllAsync("div[data-asin]");
                if (productsContainer.Count == 0) throw new Exception("could not get product id list");

                var nextPageContainer =
                    await page.QuerySelectorAsync("div[role='navigation'] > span > a.s-pagination-next");
                if (nextPageContainer == null) {
                    currentState.NextUrl = null;
                } else {
                    currentState.NextUrl = string.Join(
                        "/", 
                        currentState.AmazonBaseUrl.TrimEnd('/'), 
                        await nextPageContainer.GetAttributeAsync("href")
                    );
                }

                currentState.ProductList = new List<string>();
                foreach (var productContainer in productsContainer) {
                    var productId = await productContainer.GetAttributeAsync("data-asin");
                    if (string.IsNullOrEmpty(productId?.Trim())) continue;
                    currentState.ProductList.Add(productId);
                }

                if (currentState.ProductList.Any(x => x.Equals(currentState.CurrentProduct))) {
                    Console.WriteLine($"the product {currentState.CurrentProduct} is in the current product list. trying to recover state...");
                    currentState.ProductList.RemoveRange(0, currentState.ProductList.LastIndexOf(currentState.CurrentProduct));
                }

                var productCount = 0;
                foreach (var product in currentState.ProductList) {
                    try {
                        productCount++;
                        Console.WriteLine($"reading reviews. product={product} (product {productCount} of {currentState.ProductList.Count}) on page {currentState.CurrentPage + 1}");

                        currentState.CurrentProduct = product;
                        response = await page.GotoAsync($"{currentState.ProductReviewBaseUrl.TrimEnd('/')}/{product}/");
                        if (response?.Ok != true) {
                            Console.WriteLine($"could not get review list for product={product}");
                            continue;
                        }

                        var histogramTableContainer = await page.QuerySelectorAllAsync("#histogramTable > tbody > tr[data-reftag]");
                        if (histogramTableContainer.Any()) {
                            // navigate for each group of review one star, two stars, and so on...
                            foreach (var histogramTableItem in histogramTableContainer) {
                                await histogramTableItem.ClickAsync();
                                try {
                                    await FnReadComments(product);
                                } catch (Exception ex) {
                                    Console.WriteLine($"could not read review info. product={currentState.CurrentProduct}: {ex.Message}");
                                }

                                // waiting some time before navigate again
                                if(currentState.Delay > 0) {
                                    var drift = (int)(currentState.Delay * 0.5);
                                    drift = drift / 2 - random.Next(drift + 1);
                                    await Task.Delay(TimeSpan.FromSeconds(currentState.Delay + drift));
                                }
                            }
                        } else {
                            Console.WriteLine($"could not read histogram table. product={currentState.CurrentProduct}");
                            continue;
                        }
                        await SaveCurrentState();
                    } catch (Exception ex) {
                        Console.WriteLine($"could not navigate to product={product}: {ex.Message}");
                        Console.WriteLine(ex.StackTrace);
                    }
                }
                currentState.CurrentPage++;
                await SaveCurrentState();
            } catch (Exception ex) {
                Console.WriteLine($"could not navigate: {ex.Message}");
                break;
            }
        } while (!string.IsNullOrEmpty(currentState.NextUrl) && currentState.CurrentPage < currentState.MaxPages);


        async Task FnReadComments(string productId) {
            var reviews = await page.QuerySelectorAllAsync("div[data-hook=\"review\"]");
            foreach (var review in reviews) {
                try {
                    var id = await review.GetAttributeAsync("id") ?? "-1";

                    var star = "-1";
                    var title = "";
                    var comment = "";

                    var titleContainer = await review.QuerySelectorAsync("div:nth-child(2) > a > span:nth-child(3)");
                    if (titleContainer != null) {
                        title = await titleContainer.InnerTextAsync();
                    }

                    var starsContainer = await review.QuerySelectorAsync("i[data-hook=\"review-star-rating\"] > span");
                    if (starsContainer != null) {
                        star = (await starsContainer.InnerTextAsync())?.Split(" ").FirstOrDefault();
                    }

                    var commentContainer = await review.QuerySelectorAsync("span[data-hook=\"review-body\"] > span");
                    if (commentContainer != null) {
                        comment = await commentContainer.InnerTextAsync();
                    }

                    if(decimal.TryParse(star, NumberStyles.Float, culture, out var d) && d > 0) {
                        currentState.Reviews.Add(new Review() {
                            Id = id,
                            ProductId = productId,
                            Title = title?.Trim(),
                            Rating = d,
                            Comment = comment?.Trim()
                        }
                    );
                    } else {
                        Console.WriteLine($"cannot parse rating for product {productId}");
                        continue;
                    }

                    
                } catch (Exception ex) {
                    Console.WriteLine($"could not read review: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }
    }

    public static async Task Export() {
        var reviews = currentState.Reviews
            .GroupBy(x => x.Id)
            .Select(x => x.First()) //exclude duplicates
            .OrderBy(x => x.Rating)
            .ToList();
        
        reviews.ForEach(r =>
        {
            if (Regex.IsMatch(r.Comment, @"\s{2,}"))
            {
                r.Comment = Regex.Replace(r.Comment, @"\s{2,}", " ", RegexOptions.Multiline);
            }
            if (Regex.IsMatch(r.Comment, @"\xA0"))
            {
                r.Comment = Regex.Replace(r.Comment, @"\xA0", "", RegexOptions.Multiline);
            }
            if (Regex.IsMatch(r.Comment, @"\n"))
            {
                r.Comment = Regex.Replace(r.Comment, @"\n+", "", RegexOptions.Multiline);
            }
            if(r.Rating < 3) r.Rating = 0;
            if(r.Rating == 3) r.Rating = 1;
            if(r.Rating > 3) r.Rating = 2;
        });

        Console.WriteLine($"Total registros: {reviews.Count}");
        for (var i = 0; i < 3; i++)
        {
            var selection = reviews.Where(x => (int)x.Rating == i).ToList();
            Console.WriteLine($"Rating = {i}: {selection.Count} reviews; max chars = {selection.Max(x => x.Comment.Length)}");
        }

        var negative = reviews
            .Where(x => x.Rating == 0)
            .ToList();
        
        var neutral = reviews
            .Where(x => x.Rating == 1)
            .ToList();
        
        var positive = reviews
            .Where(x => x.Rating == 2)
            .ToList();

        var autoSampleSize = new[] { negative.Count, neutral.Count, positive.Count }.Min();

        var samples = new List<Review>();
        samples.AddRange(negative.Take(autoSampleSize));
        samples.AddRange(neutral.Take(autoSampleSize));
        samples.AddRange(positive.Take(autoSampleSize));

        await File.WriteAllTextAsync(@"samples.json", JsonConvert.SerializeObject(samples));
    }

    public static async Task Main(string[] args) {
        await LoadCurrentState();
        culture = CultureInfo.CreateSpecificCulture(currentState.StoreLanguage);

        await Navigate();
        await Export();
    }
}