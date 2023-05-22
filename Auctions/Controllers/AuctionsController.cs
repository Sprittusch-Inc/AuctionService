using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Auctions.Service;
using Auctions.Models;
using MongoDB.Driver;

namespace Auctions.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuctionsController : ControllerBase
{
    private readonly ILogger<AuctionsController> _logger;
    private readonly IConfiguration _config;
    private readonly IMongoCollection<Auction> _collection;
    private readonly AuctionService _auctionsService;


    public AuctionsController(ILogger<AuctionsController> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;

        string connString = config.GetConnectionString("MongoDB")!;
        var client = new MongoClient(connString);
        var db = client.GetDatabase("AuctionDB");
        _collection = db.GetCollection<Auction>("Auctions");

        _auctionsService = new AuctionService(_logger, _collection);
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

    [HttpPut("{auctionId}")]
    public async Task<IResult> UpdateAuctionAsync([FromBody] Auction model, int auctionId)
    {
        return await _auctionsService.UpdateAuctionAsync(model, auctionId);
    }





}
