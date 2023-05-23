using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Auctions.Models;

public class Bid
{
    [BsonId]
    public ObjectId Id { get; set; }
    public int BidId { get; set; }
    public int AuctionId { get; set; }
    public string? UserId { get; set; }
    public string? Currency { get; set; }
    public int Amount { get; set; }
    public DateTime BidDate { get; set; }


    // Methods
    public int IncrementId(int BidId)
    {
        BidId += 1;
        return BidId;
    }

}
