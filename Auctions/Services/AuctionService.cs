using Auctions.Models;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Auctions.Service;

public class AuctionService
{
    private readonly ILogger _logger;
    private readonly IConfiguration _config;
    private readonly IMongoCollection<Auction> _collection;
    private static string? _hostName;

    public AuctionService(ILogger logger, IMongoCollection<Auction> collection, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _collection = collection;

        _hostName = config["HostName"] ?? "localhost";
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
                .Set(x => x.AuctioneerId, model.AuctioneerId)
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

    public async Task<IResult> PostBidAsync(Bid bid, int auctionId)
    {
        Auction auc = await _collection.Find(Builders<Auction>.Filter.Eq("AuctionId", auctionId)).FirstOrDefaultAsync();
        if (auc == null)
        {
            return Results.Problem($"Auction with auctionId {auctionId} not found.", statusCode: 404);
        }
        if (auc.StartDate > DateTime.Now || auc.EndDate < DateTime.Now)
        {
            return Results.Problem($"Auction with auctionId {auctionId} is not open.", statusCode: 406);
        }

        try
        {
            int counter = 0;
            if (auc != null && auc.Bids != null)
            {
                counter = auc.Bids.Count + 1;
            }
            else { counter = 1; }

            bid.BidId = counter;
            bid.AuctionId = auctionId;
            // Tilføj bid.UserId fra JWT-Token

            var message = JsonSerializer.Serialize(bid);
            var body = Encoding.UTF8.GetBytes(message);

            var factory = new ConnectionFactory { HostName = _hostName };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            channel.QueueDeclare(
                queue: "bids",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            channel.BasicPublish(
                exchange: string.Empty,
                routingKey: "bids",
                basicProperties: null,
                body: body
            );

            return Results.Ok($"A bid was sent to the queue with id: {bid.BidId}");
        }
        catch (Exception ex)
        {
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }


    public async Task<IResult> DeleteAuctionAsync(int auctionId)
    {

        try
        {
            var filter = Builders<Auction>.Filter.Eq("AuctionId", auctionId);
            Auction auction = await _collection.Find(filter).FirstOrDefaultAsync();
            if (auction == null)
            {
                throw new Exception($"Auction was not found. AuctionId: {auctionId}");
            }

            await _collection.DeleteOneAsync(filter);
            return Results.Ok($"An Auction with the AuctionId of {auctionId} was deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }
}