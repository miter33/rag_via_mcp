using System.Text;
using FluentAssertions;
using RagDemo.Core;

namespace RagDemo.Tests;

public class FileProcessingServiceTests : IDisposable
{
    // ── Fixtures ────────────────────────────────────────────────────────────

    private readonly string _tempDir;

    public FileProcessingServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private string CreateTestFile(string filename, string content)
    {
        var path = Path.Combine(_tempDir, filename);
        File.WriteAllText(path, content);
        return path;
    }

    private void SetCoherApiKey(string key)
    {
        Environment.SetEnvironmentVariable("COHERE_API_KEY", key);
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadDocumentAsync_ValidFile_ReturnsFileContent()
    {
        SetCoherApiKey("test-key");
        const string expectedContent = "This is test content";
        CreateTestFile("test.txt", expectedContent);
        var service = new FileProcessingService(_tempDir);

        var result = await service.ReadDocumentAsync("test.txt");

        result.Should().Be(expectedContent);
    }

    [Fact]
    public async Task ReadDocumentAsync_SecondCall_ReturnsCachedValue()
    {
        SetCoherApiKey("test-key");
        const string content = "Original content";
        CreateTestFile("cache-test.txt", content);
        var service = new FileProcessingService(_tempDir);

        var first = await service.ReadDocumentAsync("cache-test.txt");
        var second = await service.ReadDocumentAsync("cache-test.txt");

        second.Should().Be(first);
        second.Should().Be(content);
    }

    [Fact]
    public async Task ReadDocumentAsync_EmptyFile_ReturnsEmptyString()
    {
        SetCoherApiKey("test-key");
        CreateTestFile("empty.txt", string.Empty);
        var service = new FileProcessingService(_tempDir);

        var result = await service.ReadDocumentAsync("empty.txt");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ReadDocumentAsync_FileWithSpecialCharacters_ReturnsContent()
    {
        SetCoherApiKey("test-key");
        const string content = "Content with special chars: äöü €©®";
        CreateTestFile("special.txt", content);
        var service = new FileProcessingService(_tempDir);

        var result = await service.ReadDocumentAsync("special.txt");

        result.Should().Be(content);
    }

    [Fact]
    public async Task ReadDocumentAsync_NonexistentFile_ThrowsFileNotFoundException()
    {
        SetCoherApiKey("test-key");
        var service = new FileProcessingService(_tempDir);

        Func<Task> act = () => service.ReadDocumentAsync("nonexistent.txt");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task GetEmbeddingAsync_AnyText_ReturnsFixedVector()
    {
        SetCoherApiKey("test-key");
        var service = new FileProcessingService(_tempDir);
        const string text = "test document";

        var result = await service.GetEmbeddingAsync(text);

        result.Should().BeEquivalentTo(new float[] { 0.1f, 0.2f, 0.3f },
            o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task GetEmbeddingAsync_MultipleTexts_ReturnsSameVector()
    {
        SetCoherApiKey("test-key");
        var service = new FileProcessingService(_tempDir);

        var result1 = await service.GetEmbeddingAsync("text one");
        var result2 = await service.GetEmbeddingAsync("different text");

        result1.Should().BeEquivalentTo(result2, o => o.WithStrictOrdering());
    }

    [Fact]
    public async Task GetEmbeddingAsync_EmptyText_ReturnsFixedVector()
    {
        SetCoherApiKey("test-key");
        var service = new FileProcessingService(_tempDir);

        var result = await service.GetEmbeddingAsync(string.Empty);

        result.Should().HaveCount(3);
        result[0].Should().Be(0.1f);
    }

    [Fact]
    public async Task ProcessBatchAsync_MultipleFiles_ProcessesAll()
    {
        SetCoherApiKey("test-key");
        CreateTestFile("file1.txt", "content1");
        CreateTestFile("file2.txt", "content2");
        CreateTestFile("file3.txt", "content3");
        var service = new FileProcessingService(_tempDir);

        Func<Task> act = () => service.ProcessBatchAsync(new[] { "file1.txt", "file2.txt", "file3.txt" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessBatchAsync_EmptyList_CompletesWithoutError()
    {
        SetCoherApiKey("test-key");
        var service = new FileProcessingService(_tempDir);

        Func<Task> act = () => service.ProcessBatchAsync(Array.Empty<string>());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessBatchAsync_WithInvalidFile_SuppressesError()
    {
        SetCoherApiKey("test-key");
        CreateTestFile("valid.txt", "content");
        var service = new FileProcessingService(_tempDir);

        Func<Task> act = () => service.ProcessBatchAsync(
            new[] { "valid.txt", "nonexistent.txt", "another.txt" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ProcessBatchAsync_SingleFile_Processes()
    {
        SetCoherApiKey("test-key");
        CreateTestFile("single.txt", "test content");
        var service = new FileProcessingService(_tempDir);

        Func<Task> act = () => service.ProcessBatchAsync(new[] { "single.txt" });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Constructor_ValidDirectory_CreatesInstance()
    {
        SetCoherApiKey("test-key");

        var service = new FileProcessingService(_tempDir);

        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_MissingApiKey_ThrowsInvalidOperationException()
    {
        Environment.SetEnvironmentVariable("COHERE_API_KEY", null);

        Func<FileProcessingService> act = () => new FileProcessingService(_tempDir);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*COHERE_API_KEY*");
    }
}
