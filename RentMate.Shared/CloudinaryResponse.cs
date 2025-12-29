namespace RentMate.Shared
{
    public class CloudinaryResponse
    {
    
    public string? public_id { get; set; }
    public string? secure_url { get; set; } // To je URL, ki ga shraniš v bazo
    public string? format { get; set; }
    public int width { get; set; }
    public int height { get; set; }
    }
}