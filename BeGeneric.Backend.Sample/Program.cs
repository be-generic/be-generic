using BeGeneric.Backend;
using BeGeneric.Backend.Database.MsSql.Extensions;
using BeGeneric.Backend.Database.MySql.Extensions;
using BeGeneric.Backend.Sample;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddBeGeneric<Guid>()
    //.WithMsSqlDatabase(
    //    builder.Configuration.GetConnectionString("connectionString"),
    //    "project_59_development")
    .WithMySqlDatabase(
        builder.Configuration.GetConnectionString("mySqlConnectionString"),
        "anchansi_be_gen_test")
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
app.UseMiddleware<BeGenericAuthenticationMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();
