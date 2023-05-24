using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Auctions.Models;

public class Bid
{
    [BsonElement("_id")]
    [BsonId]
    public ObjectId Id { get; set; }
    public int BidId { get; set; }
    public int AuctionId { get; set; }
    public string? UserId { get; set; }
    public string? Currency { get; set; } = "DKK";
    public int Amount { get; set; }
    public DateTime BidDate { get; set; } = DateTime.UtcNow;


    // Methods

}
