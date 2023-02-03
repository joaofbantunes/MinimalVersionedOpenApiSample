/*
 * this sample, particularly the versioning part, is heavily based on the link below, so look there for more info
 * https://github.com/dotnet/aspnet-api-versioning/blob/2292fbe6a1598d944cd5cbca918cb79da7339116/examples/AspNetCore/WebApi/MinimalOpenApiExample
 * I just wanted to test out some new stuff, as well as versioning:
 * - versioning
 * - typed results
 * - adding metadata to OpenAPI (e.g. operation and parameter descriptions)
 */

using Api;
using Asp.Versioning;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;
using Swashbuckle.AspNetCore.SwaggerGen;

var v1 = new ApiVersion(1, 0);
var v2 = new ApiVersion(2, 0);
var v3 = new ApiVersion(3, 0);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<SwaggerDefaultValues>();
    options.SupportNonNullableReferenceTypes();
    options.EnableAnnotations();
});
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddApiVersioning(options =>
    {
        options.Policies.Sunset(v1).Effective(DateTimeOffset.Now.AddDays(30));
        options.DefaultApiVersion = v3;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddApiExplorer(
        options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

var app = builder.Build();

var stuff = app.NewVersionedApi("Stuff"); // this name is used as the title of the route group

// MapGroup is new in ASP.NET Core 7
var stuffGroup = stuff
    .MapGroup("/v{version:apiVersion}/stuff");

stuffGroup
    .MapGet("/deprecated", () => "This endpoint is deprecated and you should see that in the Swagger UI")
    .WithOpenApi()
    .HasDeprecatedApiVersion(v1);

stuffGroup
    .MapGet("/", () => new[]
    {
        new Stuff(1, "Description 1"), 
        new Stuff(2, "Description 2")
    })
    .HasDeprecatedApiVersion(v1)
    .HasApiVersion(v2);

stuffGroup
    .MapGet("/", () => new[]
    {
        new StuffV3(1, "Description 1 for v3", "1v3"),
        new StuffV3(2, "Description 2 for v3", "2v3")
    })
    .HasApiVersion(v3);

stuffGroup
    .MapGet("/{id}", Results<Ok<StuffV3>, NotFound>(int id) =>
    {
        if (Random.Shared.Next() % 2 == 0)
        {
            return TypedResults.Ok(new StuffV3(id, "Description " + id, id.ToString()));
        }

        return TypedResults.NotFound();
    })
    .WithOpenApi(operation =>
    {
        // new in ASP.NET Core 7, further ability to customize the OpenAPI document
        operation.Summary = "Sometimes gets you stuff, other times, not so lucky!";
        operation.Parameters[0].Description = "The id of the stuff you want";
        return operation;
    })
    .HasApiVersion(v3);

app.UseSwagger();
app.UseSwaggerUI(
    options =>
    {
        var descriptions = app.DescribeApiVersions();

        // build a swagger endpoint for each discovered API version
        foreach (var description in descriptions)
        {
            var url = $"/swagger/{description.GroupName}/swagger.yaml";
            var name = description.GroupName.ToUpperInvariant();
            options.SwaggerEndpoint(url, name);
        }
    });


app.Run();

public record Stuff(int Id, string Description);

[SwaggerSchema(Title = "Stuff")]
public record StuffV3(int Id, string Description, string SomethingV3Specific);