using HashidsNet;
using LiteDB;
using Microsoft.OpenApi.Models;
using UrlShortener.Models;

// Create the WebApplicationBuilder
WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Set correct port
builder.WebHost.UseUrls(new[] { "http://0.0.0.0:5097", "https://0.0.0.0:5098" });

// Add services to the container
builder.Services.AddSingleton<ILiteDatabase, LiteDatabase>(_ => new LiteDatabase("url.db"));

// Register Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(setup => setup.SwaggerDoc("v1", new OpenApiInfo()
{
    Description = "Simple API to shorten URLs",
    Title = "Url Shorter",
    Version = "v1",
    Contact = new OpenApiContact()
    {
        Name = "Thomas Sebastian Jensen",
        Url = new Uri("https://www.tsjdev-apps.de")
    }
}));

// Build WebApplication.
WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Configure HashIds.NET
Hashids _hashIds = new("URLShortener", 5);

// Configure routes
app.MapPost("/add", (UrlInfoDto urlInfoDto, ILiteDatabase database, HttpContext httpContext) =>
{
    // check if an URL is provided
    if (urlInfoDto is null || string.IsNullOrEmpty(urlInfoDto.Url))
    {
        return Results.BadRequest("Please provide a valid UrlInfo object.");
    }

    // get the collection from the database
    ILiteCollection<UrlInfo> collection = database.GetCollection<UrlInfo>(BsonAutoId.Int32);

    // check if an entry with the corresponding url is already part of the database
    UrlInfo entry = collection.Query().Where(x => x.Url.Equals(urlInfoDto.Url)).FirstOrDefault();

    // if there is already an entry in the database, just return the hashed valued
    if (entry is not null)
    {
        return Results.Ok(_hashIds.Encode(entry.Id));
    }

    // otherwise just insert the url info into the database and return the hashed valued
    BsonValue documentId = collection.Insert(new UrlInfo(urlInfoDto.Url, 0));

    string encodedId = _hashIds.Encode(documentId);
    string url = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{encodedId}";

    return Results.Created(url, encodedId);
})
    .Produces<string>(StatusCodes.Status200OK)
    .Produces<string>(StatusCodes.Status201Created)
    .Produces(StatusCodes.Status400BadRequest);

app.MapGet("/{shortUrl}", (string shortUrl, ILiteDatabase context) =>
{
    // decode the short url into the corresponding id
    int[] ids = _hashIds.Decode(shortUrl);
    int tempraryId = ids[0];

    // get the collection from the database
    ILiteCollection<UrlInfo> collection = context.GetCollection<UrlInfo>();

    // try to get the entry with the corresponding id from the database
    UrlInfo entry = collection.Query().Where(x => x.Id.Equals(tempraryId)).FirstOrDefault();

    // if the url info is present in the database, just return the url
    if (entry is not null)
    {
        return Results.Ok(entry.Url);
    }

    // otherwise return the status code 'not found'
    return Results.NotFound();
})
    .Produces<string>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.Run();