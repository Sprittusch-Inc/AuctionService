using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Authorization;
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
    private static string? _connString;

    // Vault deployment-issues
    /* 
    private Vault vault;
    */

    public AuctionsController(ILogger<AuctionsController> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _connString = config["MongoConnection"];

        /*
        vault = new Vault(_config);
        string cons = vault.GetSecret("dbconnection", "constring").Result;
        */

        var client = new MongoClient(_connString);
        var db = client.GetDatabase("AuctionDB");
        _collection = db.GetCollection<Auction>("Auctions");

        // AuctionService
        _auctionsService = new AuctionService(_logger, _collection, _config);
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IResult> GetAuctions()
    {
        return await _auctionsService.GetAuctionsAsync();
    }

    [AllowAnonymous]
    [HttpGet("{auctionId}")]
    public async Task<IResult> GetAuctionByIdAsync(int auctionId)
    {
        return await _auctionsService.GetAuctionByIdAsync(auctionId);
    }

    // [Authorize(Roles = "Admin")]
    [AllowAnonymous]
    [HttpPost]
    public async Task<IResult> PostAuctionAsync([FromBody] Auction model)
    {
        return await _auctionsService.PostAuctionAsync(model);
    }

    // [Authorize(Roles = "User")]
    [AllowAnonymous]
    [HttpPost("{auctionId}")]
    public async Task<IResult> PostBidAsync([FromBody] Bid bid, int auctionId)
    {
        return await _auctionsService.PostBidAsync(bid, auctionId);
    }

    // [Authorize(Roles = "Admin")]
    [AllowAnonymous]
    [HttpPut("{auctionId}")]
    public async Task<IResult> UpdateAuctionAsync([FromBody] Auction model, int auctionId)
    {
        return await _auctionsService.UpdateAuctionAsync(model, auctionId);
    }

    // [Authorize(Roles = "Admin")]
    [AllowAnonymous]
    [HttpDelete("{auctionId}")]
    public async Task<IResult> DeleteAuctionAsync(int auctionId)
    {
        return await _auctionsService.DeleteAuctionAsync(auctionId);
    }





}
