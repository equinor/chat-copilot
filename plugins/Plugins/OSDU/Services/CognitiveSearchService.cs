using System.Text.Json;
using Azure;
using Azure.Core.Serialization;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using OSDU.Helpers;
using OSDU.Models;
using OSDU.Services.Interfaces;

namespace OSDU.Services;

public class CognitiveSearchService : ICognitiveSearchService
{
    private readonly ILogger<CognitiveSearchService> _logger;
    private readonly SearchClient _searchClient;

    private readonly JsonSerializerOptions _serializerOptions = new()
    {
        Converters = {new MicrosoftSpatialGeoJsonConverter()}
    };


    public CognitiveSearchService(SearchClient searchClient, ILogger<CognitiveSearchService> logger)
    {
        _searchClient = searchClient;
        _logger = logger;
    }

    public async Task<SearchData<T>> SearchAsync<T>(string searchText, SearchFilters filter,
        CancellationToken cancellationToken)
    {
        SearchData<T> data = new()
        {
            SearchText = searchText,
            Filter = filter
        };

        SearchOptions searchOptions = new() {Filter = filter.ToFilterString()};
        searchOptions.SetDefaultValues();
        searchOptions.SetAttributeFilters<T>(3);

        SearchResults<T> searchResult =
            await _searchClient.SearchAsync<T>(searchText, searchOptions, cancellationToken);
        data.SetResultValues(searchResult);

        return data;
    }

    public async Task<T?> GetDocumentInfoByKeyAsync<T>(string key) where T : SearchResult
    {
        try
        {
            Response<object> tmpResult = await _searchClient.GetDocumentAsync<object>(key);

            string tmpObject = JsonSerializer.Serialize(tmpResult.Value);

            T? searchResult = JsonSerializer.Deserialize<T>(tmpObject, _serializerOptions);

            if (searchResult == null)
                return default;

            return searchResult;
        }
        catch (UnauthorizedAccessException)
        {
            throw new UnauthorizedAccessException("No access to documents");
        }
        catch (Exception e)
        {
            _logger.LogError(e, "CognitiveSearchService: {Message}", e.Message);
        }

        return default;
    }
}