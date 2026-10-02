using System.Net.Http.Json;
using nesimokau.lt.Models;

namespace nesimokau.lt.Services;

public sealed class DictationService(HttpClient http)
{
    public async Task<IReadOnlyList<DictationQuestion>> GetQuestionsAsync()
        => await http.GetFromJsonAsync<List<DictationQuestion>>("data/dictation.json") ?? [];
}
