using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartComponents;
using SmartComponents.LocalEmbeddings;

namespace SmartComponentsDemo
{
    // Entity class
    public class Document
    {
        public int DocumentId { get; set; }
        public int OwnerId { get; set; }
        public required string Title { get; set; }
        public required string Body { get; set; }

        // Embedding stored as byte[]
        public required byte[] EmbeddingI8Buffer { get; set; }
    }

    // Database context
    public class MyDbContext : DbContext
    {
        public DbSet<Document> Documents => Set<Document>();

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // Use SQLite in-memory database for simplicity
            optionsBuilder.UseSqlite("Data Source=documents.db");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Document>().HasKey(d => d.DocumentId);
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            // Initialize the database and ensure it's created
            using (var dbContext = new MyDbContext())
            {
                await dbContext.Database.EnsureCreatedAsync();
            }

            // Initialize the embedder
            using var embedder = new LocalEmbedder();

            // Seed the database with sample documents
            await SeedDatabaseAsync(embedder);

            // Simulate a search query
            Console.WriteLine("Enter search text:");
            string searchText = Console.ReadLine() ?? string.Empty;

            // Perform the similarity search
            await PerformSimilaritySearchAsync(embedder, searchText);
        }

        static async Task SeedDatabaseAsync(LocalEmbedder embedder)
        {
            using var dbContext = new MyDbContext();

            // Sample documents
            var documents = new List<Document>
            {
                new Document
                {
                    OwnerId = 1,
                    Title = "Introduction to C#",
                    Body = "C# is a modern, object-oriented programming language developed by Microsoft.",
                    EmbeddingI8Buffer = embedder.Embed<EmbeddingI8>("Introduction to C#").Buffer.ToArray()
                },
                new Document
                {
                    OwnerId = 1,
                    Title = "Entity Framework Core Guide",
                    Body = "EF Core is a lightweight, extensible, open-source, and cross-platform version of the popular Entity Framework data access technology.",
                    EmbeddingI8Buffer = embedder.Embed<EmbeddingI8>("Entity Framework Core Guide").Buffer.ToArray()
                },
                new Document
                {
                    OwnerId = 2,
                    Title = "Getting Started with ASP.NET Core",
                    Body = "ASP.NET Core is a cross-platform, high-performance, open-source framework for building modern, cloud-enabled, Internet-connected apps.",
                    EmbeddingI8Buffer = embedder.Embed<EmbeddingI8>("Getting Started with ASP.NET Core").Buffer.ToArray()
                },
            };

            // Add documents to the database
            dbContext.Documents.AddRange(documents);
            await dbContext.SaveChangesAsync();
        }

        static async Task PerformSimilaritySearchAsync(LocalEmbedder embedder, string searchText)
        {
            int currentUserId = 1; // Simulate current user ID

            using var dbContext = new MyDbContext();

            // Load documents for the current user
            var userDocuments = await dbContext.Documents
                .Where(d => d.OwnerId == currentUserId)
                .Select(d => new { d.DocumentId, d.EmbeddingI8Buffer })
                .ToListAsync();

            // Embed the search text
            var searchEmbedding = embedder.Embed<EmbeddingI8>(searchText);

            // Prepare embeddings for similarity search
            var documentEmbeddings = userDocuments.Select(d => (d.DocumentId, new EmbeddingI8(d.EmbeddingI8Buffer)));

            // Find closest documents
            int[] matchingDocIds = LocalEmbedder.FindClosest(
                searchEmbedding,
                documentEmbeddings,
                maxResults: 5);

            // Load full documents
            var matchingDocs = await dbContext.Documents
                .Where(d => matchingDocIds.Contains(d.DocumentId))
                .ToDictionaryAsync(d => d.DocumentId);

            // Display results
            Console.WriteLine("\nMatching Documents:");
            foreach (var docId in matchingDocIds)
            {
                if (matchingDocs.TryGetValue(docId, out var doc))
                {
                    Console.WriteLine($"- {doc.Title}");
                }
            }
        }
    }
}
