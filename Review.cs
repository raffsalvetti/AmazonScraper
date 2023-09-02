namespace AmazonScraper;
public class Review
{
    /// <summary>
    /// review unique id
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// product unique id
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// review title
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// review rating 1 to 5 stars
    /// </summary>
    public decimal Rating { get; set; }

    /// <summary>
    /// review body
    /// </summary>
    public string Comment { get; set; }
}
