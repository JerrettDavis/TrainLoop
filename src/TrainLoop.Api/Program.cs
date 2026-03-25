using Microsoft.EntityFrameworkCore;
using TrainLoop.Core.Entities;
using TrainLoop.Infrastructure.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddOpenApi();

builder.Services.AddDbContext<TrainLoopDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    options.UseNpgsql(connectionString);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// --- Datasets ---

app.MapGet("/api/datasets", async (TrainLoopDbContext db) =>
    await db.Datasets.ToListAsync())
    .WithName("GetDatasets");

app.MapPost("/api/datasets", async (CreateDatasetRequest request, TrainLoopDbContext db) =>
{
    var dataset = new Dataset { Name = request.Name, Description = request.Description };
    db.Datasets.Add(dataset);
    await db.SaveChangesAsync();
    return Results.Created($"/api/datasets/{dataset.Id}", dataset);
})
.WithName("CreateDataset");

// --- Data Items ---

app.MapGet("/api/datasets/{id:guid}/items", async (Guid id, TrainLoopDbContext db) =>
{
    var exists = await db.Datasets.AnyAsync(d => d.Id == id);
    if (!exists) return Results.NotFound();

    var items = await db.DataItems.Where(i => i.DatasetId == id).ToListAsync();
    return Results.Ok(items);
})
.WithName("GetDatasetItems");

app.MapPost("/api/datasets/{id:guid}/items", async (Guid id, CreateDataItemRequest request, TrainLoopDbContext db) =>
{
    var exists = await db.Datasets.AnyAsync(d => d.Id == id);
    if (!exists) return Results.NotFound();

    var item = new DataItem
    {
        DatasetId = id,
        Content = request.Content,
        ContentType = request.ContentType
    };
    db.DataItems.Add(item);
    await db.SaveChangesAsync();
    return Results.Created($"/api/datasets/{id}/items/{item.Id}", item);
})
.WithName("CreateDatasetItem");

// --- Annotations ---

app.MapPost("/api/items/{id:guid}/annotations", async (Guid id, CreateAnnotationRequest request, TrainLoopDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(request.Rationale))
        return Results.BadRequest("Rationale is required.");

    var itemExists = await db.DataItems.AnyAsync(i => i.Id == id);
    if (!itemExists) return Results.NotFound();

    var reviewerExists = await db.Reviewers.AnyAsync(r => r.Id == request.ReviewerId);
    if (!reviewerExists) return Results.NotFound();

    var annotation = new Annotation
    {
        DataItemId = id,
        ReviewerId = request.ReviewerId,
        Label = request.Label,
        Rationale = request.Rationale,
        Confidence = request.Confidence,
        TimeToLabel = request.TimeToLabel
    };
    db.Annotations.Add(annotation);
    await db.SaveChangesAsync();
    return Results.Created($"/api/items/{id}/annotations/{annotation.Id}", annotation);
})
.WithName("CreateAnnotation");

// --- Reviewers ---

app.MapGet("/api/reviewers", async (TrainLoopDbContext db) =>
    await db.Reviewers.ToListAsync())
    .WithName("GetReviewers");

app.Run();

// --- Request records ---

record CreateDatasetRequest(string Name, string? Description);
record CreateDataItemRequest(string Content, string? ContentType);
record CreateAnnotationRequest(Guid ReviewerId, string Label, string Rationale, double Confidence, TimeSpan TimeToLabel);
