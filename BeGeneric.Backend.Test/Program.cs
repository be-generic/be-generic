using BeGeneric.Backend;
using BeGeneric.Backend.Settings;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


using StreamReader sr = new("be-generic.config.json");
string json = sr.ReadToEnd();
List<EntityDefinition> items = JsonSerializer.Deserialize<List<EntityDefinition>>(json);

builder.Services.AddGenericBackendServices(
    builder.Configuration.GetConnectionString("connectionString"),
    items,
    new List<ColumnMetadataDefinition>(),
    (x) => { }
);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
