﻿// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;

namespace Memory;

/// <summary>
/// A simple example showing how to ingest data into a vector store and then use vector search to find related records to a given string.
///
/// The example shows the following steps:
/// 1. Create an embedding generator.
/// 2. Create an InMemory Vector Store.
/// 3. Ingest some data into the vector store.
/// 4. Search the vector store with various text and filtering options.
/// </summary>
public class VectorStore_VectorSearch_Simple(ITestOutputHelper output) : BaseTest(output)
{
    [Fact]
    public async Task ExampleAsync()
    {
        // Create an embedding generation service.
        var embeddingGenerator = new AzureOpenAIClient(new Uri(TestConfiguration.AzureOpenAIEmbeddings.Endpoint), new AzureCliCredential())
            .GetEmbeddingClient(TestConfiguration.AzureOpenAIEmbeddings.DeploymentName)
            .AsIEmbeddingGenerator(1536);

        // Construct an InMemory vector store.
        var vectorStore = new InMemoryVectorStore();

        // Get and create collection if it doesn't exist.
        var collection = vectorStore.GetCollection<ulong, Glossary>("skglossary");
        await collection.EnsureCollectionExistsAsync();

        // Create glossary entries and generate embeddings for them.
        var glossaryEntries = CreateGlossaryEntries().ToList();
        var tasks = glossaryEntries.Select(entry => Task.Run(async () =>
        {
            entry.DefinitionEmbedding = (await embeddingGenerator.GenerateAsync(entry.Definition)).Vector;
        }));
        await Task.WhenAll(tasks);

        // Upsert the glossary entries into the collection and return their keys.
        await collection.UpsertAsync(glossaryEntries);

        // Search the collection using a vector search.
        var searchString = "What is an Application Programming Interface";
        var searchVector = (await embeddingGenerator.GenerateAsync(searchString)).Vector;
        var resultRecords = await collection.SearchAsync(searchVector, top: 1).ToListAsync();

        Console.WriteLine("Search string: " + searchString);
        Console.WriteLine("Result: " + resultRecords.First().Record.Definition);
        Console.WriteLine();

        // Search the collection using a vector search.
        searchString = "What is Retrieval Augmented Generation";
        searchVector = (await embeddingGenerator.GenerateAsync(searchString)).Vector;
        resultRecords = await collection.SearchAsync(searchVector, top: 1).ToListAsync();

        Console.WriteLine("Search string: " + searchString);
        Console.WriteLine("Result: " + resultRecords.First().Record.Definition);
        Console.WriteLine();

        // Search the collection using a vector search with pre-filtering.
        searchString = "What is Retrieval Augmented Generation";
        searchVector = (await embeddingGenerator.GenerateAsync(searchString)).Vector;
        resultRecords = await collection.SearchAsync(searchVector, top: 3, new() { Filter = g => g.Category == "External Definitions" }).ToListAsync();

        Console.WriteLine("Search string: " + searchString);
        Console.WriteLine("Number of results: " + resultRecords.Count);
        Console.WriteLine("Result 1 Score: " + resultRecords[0].Score);
        Console.WriteLine("Result 1: " + resultRecords[0].Record.Definition);
        Console.WriteLine("Result 2 Score: " + resultRecords[1].Score);
        Console.WriteLine("Result 2: " + resultRecords[1].Record.Definition);
    }

    /// <summary>
    /// Sample model class that represents a glossary entry.
    /// </summary>
    /// <remarks>
    /// Note that each property is decorated with an attribute that specifies how the property should be treated by the vector store.
    /// This allows us to create a collection in the vector store and upsert and retrieve instances of this class without any further configuration.
    /// </remarks>
    private sealed class Glossary
    {
        [VectorStoreKey]
        public ulong Key { get; set; }

        [VectorStoreData(IsIndexed = true)]
        public string Category { get; set; }

        [VectorStoreData]
        public string Term { get; set; }

        [VectorStoreData]
        public string Definition { get; set; }

        [VectorStoreVector(1536)]
        public ReadOnlyMemory<float> DefinitionEmbedding { get; set; }
    }

    /// <summary>
    /// Create some sample glossary entries.
    /// </summary>
    /// <returns>A list of sample glossary entries.</returns>
    private static IEnumerable<Glossary> CreateGlossaryEntries()
    {
        yield return new Glossary
        {
            Key = 1,
            Category = "External Definitions",
            Term = "API",
            Definition = "Application Programming Interface. A set of rules and specifications that allow software components to communicate and exchange data."
        };

        yield return new Glossary
        {
            Key = 2,
            Category = "Core Definitions",
            Term = "Connectors",
            Definition = "Connectors allow you to integrate with various services provide AI capabilities, including LLM, AudioToText, TextToAudio, Embedding generation, etc."
        };

        yield return new Glossary
        {
            Key = 3,
            Category = "External Definitions",
            Term = "RAG",
            Definition = "Retrieval Augmented Generation - a term that refers to the process of retrieving additional data to provide as context to an LLM to use when generating a response (completion) to a user’s question (prompt)."
        };
    }
}
