var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Memory Cache
builder.Services.AddMemoryCache();

// Distributed Cache
builder.Services.AddNanoDistributedCache(options =>
{
    options.CacheServerHost = "127.0.0.1";
    options.CacheServerPort = 5566;
    options.Username = "admin";
    options.Password = "123456";
    options.Instance = "";
});

// Singleton Objects
builder.Services.AddSingleton<AppCache>();

// Build Application
var app = builder.Build();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();