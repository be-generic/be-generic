using BeGeneric.Backend;
using BeGeneric.Backend.Database.MySql.Extensions;
//For MSSQL
//using BeGeneric.Backend.Database.MsSql.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddBeGeneric<Guid>()
    // For MS SQL:
    //.WithMsSqlDatabase(
    //    builder.Configuration.GetConnectionString("connectionString"),
    //    "project_59_development")
    // For MySQL:
    .WithMySqlDatabase(
        builder.Configuration.GetConnectionString("mySqlConnectionString"),
        "database/namespace")
    .WithControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
