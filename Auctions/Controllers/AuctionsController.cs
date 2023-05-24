using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Auctions.Service;
using Auctions.Models;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Auctions.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController : ControllerBase
{
    private readonly ILogger<AuctionsController> _logger;
    private readonly IConfiguration _config;
    private readonly IMongoCollection<Auction> _collection;
    private readonly AuctionService _auctionsService;
    private static string? _hostName;


    public AuctionsController(ILogger<AuctionsController> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _hostName = config["HostName"] ?? "localhost";


        string connString = config.GetConnectionString("MongoDB")!;
        var client = new MongoClient(connString);
        var db = client.GetDatabase("AuctionDB");
        _collection = db.GetCollection<Auction>("Auctions");

        // AuctionService
        _auctionsService = new AuctionService(_logger, _collection, _config);
    }


    [HttpGet]
    public async Task<List<Auction>> GetAuctions()
    {
        return await _auctionsService.GetAuctionsAsync();
    }

    [HttpGet("{auctionId}")]
    public async Task<IResult> GetAuctionByIdAsync(int auctionId)
    {
        return await _auctionsService.GetAuctionByIdAsync(auctionId);
    }

    [HttpPost]
    public async Task<IResult> PostAuctionAsync([FromBody] Auction model)
    {
        return await _auctionsService.PostAuctionAsync(model);
    }

    [HttpPost("{auctionId}")]
    public async Task<IResult> PostBidAsync([FromBody] Bid bid, int auctionId)
    {
        return await _auctionsService.PostBidAsync(bid, auctionId);
    }


    [HttpPut("{auctionId}")]
    public async Task<IResult> UpdateAuctionAsync([FromBody] Auction model, int auctionId)
    {
        return await _auctionsService.UpdateAuctionAsync(model, auctionId);
    }

    [HttpDelete("{auctionId}")]
    public async Task<IResult> DeleteAuctionAsync(int auctionId)
    {
        return await _auctionsService.DeleteAuctionAsync(auctionId);
    }





}
