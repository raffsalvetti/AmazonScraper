namespace AmazonScraper;

using System.Collections.Generic;

public class CurrentState
{
    /// <summary>
    /// current product url
    /// </summary>
    public string ProductsUrl { get; set; }

    /// <summary>
    /// all product ids on the current page
    /// </summary>
    public List<string> ProductList { get; set; }

    /// <summary>
    /// the product id that is currently being processed
    /// </summary>
    public string CurrentProduct { get; set; }

    /// <summary>
    /// the next product page url
    /// </summary>
    public string NextUrl { get; set; }

    /// <summary>
    /// the maximum number of pages to be read
    /// </summary>
    public int MaxPages { get; set; }

    /// <summary>
    /// delay time in seconds between url navigations
    /// </summary>
    public int Delay { get; set; }

    /// <summary>
    /// the number of the page that is currently being processed
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// the review list
    /// </summary>
    public List<Review> Reviews { get; set; }

    /// <summary>
    /// amazon store language (pt-BR, en-GB, en-US...)
    /// </summary>
    public string StoreLanguage { get; set; }

    /// <summary>
    /// amazon product review base url
    /// </summary>
    public string ProductReviewBaseUrl { get; set; }

    /// <summary>
    /// amazon product base url
    /// </summary>
    public string AmazonBaseUrl { get; set; }

    public override string ToString()
    {
        return $"\t{nameof(ProductsUrl)}: {ProductsUrl}\n\t{nameof(CurrentProduct)}: {CurrentProduct}\n\t{nameof(NextUrl)}: {NextUrl}\n\t{nameof(MaxPages)}: {MaxPages}\n\t{nameof(Delay)}: {Delay}\n\t{nameof(CurrentPage)}: {CurrentPage}";
    }
}
