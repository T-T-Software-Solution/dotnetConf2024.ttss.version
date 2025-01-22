public class ProductManualService
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IVectorStore _store;
    private readonly IVectorStoreRecordCollection<ulong, ManualChunk> _collection;
    private readonly string _collectionName = "ProductManuals";
    private readonly int _dimensions;

    public ProductManualService(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, IVectorStore store, bool useOpenAIEmbeddings)
    {
        _embeddingGenerator = embeddingGenerator;
        _store = store;
        _collection = store.GetCollection<ulong, ManualChunk>(_collectionName);
        _dimensions = useOpenAIEmbeddings ? EmbeddingDimensions.OpenAIEmbeddingSize : EmbeddingDimensions.OllamaEmbeddingSize;
    }

    public async Task InsertManualChunksAsync(IEnumerable<ManualChunk> manualChunks)
    {
        // Create the collection if it doesn't exist yet.
        await _collection.CreateCollectionIfNotExistsAsync();

        foreach (var chunk in manualChunks)
        {
            await _collection.UpsertAsync(chunk);
        }
    }

    public async Task<VectorSearchResults<ManualChunk>> GetManualChunksAsync(string query, int? employeeId, int? limit = 5)
    {

        var queryEmbedding = await _embeddingGenerator.GenerateEmbeddingVectorAsync(query);

        var filter =
            new VectorSearchFilter()
                .EqualTo(nameof(ManualChunk.EmployeeId), employeeId);

        var searchOptions = new VectorSearchOptions
        {
            Top = limit ?? 1,
            Filter = filter,
            IncludeVectors = true
        };

        return await _collection.VectorizedSearchAsync(queryEmbedding, searchOptions);
    }

    private VectorStoreRecordDefinition GetRecordDefinition()
    {
        return new VectorStoreRecordDefinition
        {
            Properties = new List<VectorStoreRecordProperty>
            {
                new VectorStoreRecordKeyProperty(nameof(ManualChunk.ChunkId), typeof(ulong)),
                new VectorStoreRecordDataProperty(nameof(ManualChunk.EmployeeId), typeof(int)) { IsFilterable = true },
                new VectorStoreRecordDataProperty(nameof(ManualChunk.PageNumber), typeof(int)) { IsFilterable = true },
                new VectorStoreRecordVectorProperty(nameof(ManualChunk.Embedding), typeof(ReadOnlyMemory<float>)) { Dimensions = _dimensions, DistanceFunction = DistanceFunction.CosineDistance },
                new VectorStoreRecordDataProperty(nameof(ManualChunk.Text), typeof(string)) { IsFilterable = true },
            }
        };
    }
}

internal class EmbeddingDimensions
{
    public const int OllamaEmbeddingSize = 384;
    public const int OpenAIEmbeddingSize = 1536;
}
