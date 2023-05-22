using Auctions.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Auctions.Service;

public class AuctionService
{
    private readonly ILogger _logger;
    private readonly IMongoCollection<Auction> _collection;

    public AuctionService(ILogger logger, IMongoCollection<Auction> collection)
    {
        _logger = logger;
        _collection = collection;
    }


    public async Task<List<Auction>> GetAuctionsAsync()
    {
        try
        {
            List<Auction> auctions = await _collection.Find(Builders<Auction>.Filter.Empty).ToListAsync();
            if (auctions.Count < 1)
            {
                throw new Exception("No auctions found.");
            }

            return auctions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            throw;
        }
    }


    public async Task<IResult> GetAuctionByIdAsync(int auctionId)
    {
        try
        {
            var auction = await _collection.Find(Builders<Auction>.Filter.Eq("AuctionId", auctionId)).FirstOrDefaultAsync();
            if (auction == null)
            {
                throw new Exception($"No auction with the given ID ({auctionId}) was found.");
            }

            return Results.Ok(auction);
        }
        catch (Exception ex)
        {
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }


    public async Task<IResult> PostAuctionAsync(Auction model)
    {
        try
        {
            if (model.StartDate == null || model.EndDate == null)
            {
                throw new Exception("Start- and end-dates must not be null.");
            }
            if (model.StartDate > model.EndDate)
            {
                throw new Exception("Start-date must not be after the end-date.");
            }

            if (model.AuctioneerId == null)
            {
                throw new Exception("AuctioneerId must not be null.");
            }

            int highestId = ((int)_collection.CountDocuments(Builders<Auction>.Filter.Empty)) + 1;
            while (_collection.Find(Builders<Auction>.Filter.Eq("AuctionId", highestId)).Any() == true)
            {
                highestId++;
            }
            model.AuctionId = highestId;
            model.NextBid = model.CalcNextBid(model.MinBid, model.NextBid);

            await _collection.InsertOneAsync(model);
            return Results.Ok($"A new auction was appended and given AuctionId: {model.AuctionId}");
        }
        catch (Exception ex)
        {
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }


    public async Task<IResult> UpdateAuctionAsync(Auction model, int auctionId)
    {
        try
        {
            if (_collection.Find(Builders<Auction>.Filter.Eq("AuctionId", auctionId)).Any() == false)
            {
                throw new Exception($"No auction with the given ID ({auctionId}) exists.");
            }

            if (model.StartDate == null || model.EndDate == null)
            {
                throw new Exception("Start- and end-dates must not be null.");
            }
            if (model.StartDate > model.EndDate)
            {
                throw new Exception("Start-date must not be after the end-date.");
            }

            if (model.AuctioneerId == null)
            {
                throw new Exception("AuctioneerId must not be null.");
            }

            if (model.Bids?.Count > 0)
            {
                int counter = 1;
                foreach (var bid in model.Bids)
                {
                    bid.BidId = counter;
                    counter++;

                    bid.BidDate = DateTime.UtcNow;
                    bid.AuctionId = auctionId;
                }
            }

            var filter = Builders<Auction>.Filter.Eq("AuctionId", auctionId);
            var update = Builders<Auction>.Update
                .Set(x => x.AuctioneerId, model.AuctioneerId?.ToLower())
                .Set(x => x.StartDate, model.StartDate)
                .Set(x => x.EndDate, model.EndDate)
                .Set(x => x.MinBid, model.MinBid)
                .Set(x => x.NextBid, model.CalcNextBid(model.MinBid, model.NextBid))
                .Set(x => x.Bids, model.Bids);

            await _collection.UpdateOneAsync(filter, update);
            return Results.Ok($"An auction with the auctionId of {auctionId} was updated.");
        }
        catch (Exception ex)
        {
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }

}