using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Auctions.Models;

public class Auction
{
    [BsonElement("_id")]
    [BsonId]
    public ObjectId Id { get; set; }
    public int AuctionId { get; set; }
    public string? AuctioneerId { get; set; }
    public int ItemId { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int MinBid { get; set; }
    public int NextBid { get; set; } = 0;
    public List<Bid>? Bids { get; set; } = new List<Bid>();

    // Methods
    public int CalcNextBid(int MinBid, int NextBid)
    {
        NextBid += MinBid;
        return NextBid;
    }
}