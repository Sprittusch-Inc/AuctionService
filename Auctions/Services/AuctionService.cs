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
        _logger.LogInformation($"HostName is set to: {_hostName}");
    }


    public async Task<IResult> GetAuctionsAsync()
    {
        _logger.LogInformation("Method GetAuctionsAsync() called");

        try
        {
            _logger.LogInformation("Looking for auctions...");

            // Opretter et AND-filter, som består af 2 filtre (less than & greater than) ift. tidspunktet af kaldet (UTC)
            var filter = Builders<Auction>.Filter.And(
                    Builders<Auction>.Filter.Lt(a => a.StartDate, DateTime.UtcNow),
                    Builders<Auction>.Filter.Gt(a => a.EndDate, DateTime.UtcNow)
                );

            // Henter alle auktioner asynkront ind i en liste, hvis de matcher begge filtre defineret i filter-variablen
            List<Auction> auctions = await _collection.Find(filter).ToListAsync();
            
            // Hvis listen er tom, kastes en exception som logges i catch-blokken
            if (auctions.Count < 1)
            {
                throw new Exception("No auctions found.");
            }

            _logger.LogInformation($"Found {auctions.Count} auctions.");
            return Results.Ok(auctions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Results.Problem($"{ex.Message}", statusCode:404);
        }
    }


    public async Task<IResult> GetAuctionByIdAsync(int auctionId)
    {
        _logger.LogInformation("Method GetAuctionByIdAsync() called");

        try
        {
            _logger.LogInformation($"Looking for auction with auctionId: {auctionId}");
            
            // Henter en auktion som har et "AuctionId" som matcher det id der blev kaldt efter ved endepunktet
            var auction = await _collection.Find(Builders<Auction>.Filter.Eq("AuctionId", auctionId)).FirstOrDefaultAsync();

            // hvis auktionen er null, kastes en exception
            if (auction == null)
            {
                throw new Exception($"No auction with the given ID ({auctionId}) was found.");
            }

            return Results.Ok(auction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }


    public async Task<IResult> PostAuctionAsync(Auction model)
    {
        _logger.LogInformation("Method PostAuctionAsync() called");

        try
        {
            _logger.LogInformation("Attempting to post auction...");

            // Herunder ses en liste af exceptions.
            // Hvis start- og slutdato mangler, kastes en exception som informerer om dette.
            if (model.StartDate == null || model.EndDate == null)
            {
                throw new Exception("Start- and end-dates must not be null.");
            }

            // Hvis startdato er senere end slutdato, kastes en exception som informerer om dette.
            if (model.StartDate > model.EndDate)
            {
                throw new Exception("Start-date must not be after the end-date.");
            }

            // Hvis AuctioneerId (Email) mangler, kastes en exception som informerer om dette.
            if (model.AuctioneerId == null)
            {
                throw new Exception("AuctioneerId must not be null.");
            }

            // Dokumenterne i databasen bliver talt op ved CountDocuments med et tomt filter, hvor det plusses med 1
            int highestId = ((int)_collection.CountDocuments(Builders<Auction>.Filter.Empty)) + 1;

            // Så længe det ID vi finder frem til, findes i databasen, så plusses highestId med 1
            while (_collection.Find(Builders<Auction>.Filter.Eq("AuctionId", highestId)).Any() == true)
            {
                highestId++;
            }

            // Tilgængeligt Id fundet, som nu sættes til at være AuctionId
            // Herefter er nextbid givet ved en default af 0, hvor nextbid findes ved CalcNextBid()
            model.AuctionId = highestId;
            model.NextBid = model.CalcNextBid(model.MinBid, model.NextBid);

            await _collection.InsertOneAsync(model);
            _logger.LogInformation($"Posted auction with auctionId: {model.AuctionId}");

            return Results.Ok($"A new auction was appended and given AuctionId: {model.AuctionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }


    public async Task<IResult> UpdateAuctionAsync(Auction model, int auctionId)
    {
        _logger.LogInformation("Method UpdateAuctionAsync() called");

        try
        {
            _logger.LogInformation($"Checking if auction with auctionId {auctionId} exists...");

            // Exceptions
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

            // Opretter filter som tjekker efter "AuctionId" som svarer til det kaldte auctionId
            var filter = Builders<Auction>.Filter.Eq("AuctionId", auctionId);

            // Opretter update som ændrer adskillige felter
            var update = Builders<Auction>.Update
                .Set(x => x.StartDate, model.StartDate)
                .Set(x => x.EndDate, model.EndDate)
                .Set(x => x.MinBid, model.MinBid)
                .Set(x => x.NextBid, model.CalcNextBid(model.MinBid, model.NextBid));

            // Kalder UpdateOneAsync med filter- og update-variablerne
            _logger.LogInformation($"Attempting to update auction with auctionId: {auctionId}");
            await _collection.UpdateOneAsync(filter, update);
            
            _logger.LogInformation($"Successfully updated auction with auctionId: {auctionId}");
            return Results.Ok($"An auction with the auctionId of {auctionId} was updated.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }

    public async Task<IResult> PostBidAsync(Bid bid, int auctionId)
    {
        _logger.LogInformation("Method PostBidAsync() called");

        // Tjekker om auctionen med auctionId findes i databasen
        _logger.LogInformation($"Validating auction with auctionId {auctionId}...");
        Auction auc = await _collection.Find(Builders<Auction>.Filter.Eq("AuctionId", auctionId)).FirstOrDefaultAsync();
        
        
        if (auc == null)
        {
            _logger.LogError($"Auction with auctionId {auctionId} not found.");
            return Results.Problem($"Auction with auctionId {auctionId} not found.", statusCode: 404);
        }
        if (auc.StartDate > DateTime.UtcNow || auc.EndDate < DateTime.UtcNow)
        {
            _logger.LogError($"Auction with auctionId {auctionId} is not open.");
            return Results.Problem($"Auction with auctionId {auctionId} is not open.", statusCode: 406);
        }

        try
        {
            _logger.LogInformation("Processing bid...");

            // Counter for BidId som sendes ud til queue
            int counter = 0;
            if (auc != null && auc.Bids != null)
            {
                counter = auc.Bids.Count + 1;
            }
            else { counter = 1; }

            // Opdaterer BidId og AuctionId i bid-objektet
            bid.BidId = counter;
            bid.AuctionId = auctionId;
            

            // Serialisering af bid-objektet
            var message = JsonSerializer.Serialize(bid);
            var body = Encoding.UTF8.GetBytes(message);

            // Deklarering af queue
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

            // Afsendelse af bid-objekt til queue som JSON
            _logger.LogInformation($"Publishing bid with BidId {bid.BidId} to the queue...");
            channel.BasicPublish(
                exchange: string.Empty,
                routingKey: "bids",
                basicProperties: null,
                body: body
            );

            _logger.LogInformation($"The bid with BidId {bid.BidId} was sent to the queue");
            return Results.Ok($"A bid was sent to the queue with id: {bid.BidId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }


    public async Task<IResult> DeleteAuctionAsync(int auctionId)
    {
        _logger.LogInformation("Method DeleteAuctionAsync() called");

        try
        {
            // Tjekker om auktion med givet Id findes i databasen
            _logger.LogInformation($"Checking if auction with AuctionId {auctionId} exists...");
            var filter = Builders<Auction>.Filter.Eq("AuctionId", auctionId);
            Auction auction = await _collection.Find(filter).FirstOrDefaultAsync();
            
            if (auction == null)
            {
                throw new Exception($"Auction with AuctionId {auctionId} was not found.");
            }

            // Sletter auktion som matcher givet Id
            _logger.LogInformation($"Attempting to delete auction with AuctionId {auctionId}...");
            await _collection.DeleteOneAsync(filter);
            
            _logger.LogInformation($"Successfully deleted auction with AuctionId {auctionId}.");
            return Results.Ok($"An Auction with the AuctionId of {auctionId} was deleted.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex.Message);
            return Results.Problem($"ERROR: {ex.Message}", statusCode: 500);
        }
    }
}