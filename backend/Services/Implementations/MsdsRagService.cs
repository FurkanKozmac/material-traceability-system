using System.Net.Http.Json;
using System.Text.Json.Nodes;
using backend.Common.Exceptions;
using backend.DTOs;
using backend.Services.Interfaces;

namespace backend.Services.Implementations;

public class MsdsRagService(
    IWebHostEnvironment env,
    HttpClient httpClient,
    ILogger<MsdsRagService> logger) : IMsdsRagService
{
    private sealed record MsdsChunk(string ChemicalCode, string Title, string Content);

    private const string OllamaBaseUrl = "http://localhost:11434";
    private const string LlmModel = "qwen2.5:3b";
    private const string EmbeddingModel = "nomic-embed-text";

    public async Task<SafetyAnswerResponse> AskQuestionAsync(
        SafetyQuestionRequest request,
        CancellationToken ct = default)
    {
        var allChunks = await LoadAllMsdsChunksAsync(ct);
        var chemicalChunks = allChunks
            .Where(chunk => string.Equals(chunk.ChemicalCode, request.ChemicalCode, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (chemicalChunks.Count == 0)
        {
            throw new NotFoundException(
                $"'{request.ChemicalCode}' kimyasalına ait MSDS bilgisi sistemde kayıtlı değildir!");
        }

        logger.LogInformation(
            "RAG: {DocumentCount} MSDS dokümanı tarandı, {ChunkCount} chunk ile {ChemicalCode} sorgulanıyor.",
            allChunks.Select(chunk => chunk.ChemicalCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            chemicalChunks.Count,
            request.ChemicalCode);

        var bestSection = await FindMostRelevantSectionAsync(request.Question, chemicalChunks, ct);
        var answer = await GenerateAnswerWithLlmAsync(
            request.Question,
            request.ChemicalCode,
            bestSection.Title,
            bestSection.Content,
            ct);

        return new SafetyAnswerResponse(
            request.ChemicalCode,
            request.Question,
            answer,
            bestSection.Title,
            true);
    }

    private string? FindMsdsDirectory()
    {
        var candidateDirectories = new[]
        {
            Path.Combine(env.ContentRootPath, "docs", "msds"),
            Path.Combine(env.ContentRootPath, "..", "docs", "msds")
        };

        return candidateDirectories.FirstOrDefault(Directory.Exists);
    }

    private async Task<List<MsdsChunk>> LoadAllMsdsChunksAsync(CancellationToken ct)
    {
        var directory = FindMsdsDirectory();
        if (directory is null)
            return [];

        var chunks = new List<MsdsChunk>();
        foreach (var filePath in Directory.GetFiles(directory, "*.md").OrderBy(path => path))
        {
            ct.ThrowIfCancellationRequested();
            var chemicalCode = Path.GetFileNameWithoutExtension(filePath)
                .Split('_', 2, StringSplitOptions.TrimEntries)[0];
            var markdown = await File.ReadAllTextAsync(filePath, ct);

            chunks.AddRange(ExtractSections(markdown)
                .Select(section => new MsdsChunk(
                    chemicalCode,
                    section.Title,
                    $"[CHEMICAL CODE: {chemicalCode}]\n{section.Content}")));
        }

        return chunks;
    }

    private static List<(string Title, string Content)> ExtractSections(string markdown)
    {
        var result = new List<(string Title, string Content)>();
        var pattern = @"(?=(?:\r?\n|^)##\s+)";
        var parts = System.Text.RegularExpressions.Regex.Split(
                markdown,
                pattern,
                System.Text.RegularExpressions.RegexOptions.Multiline)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        foreach (var part in parts)
        {
            var trimmed = part.TrimStart('\r', '\n');
            var lines = trimmed.Split(new[] { "\r\n", "\n" }, 2, StringSplitOptions.None);
            var title = lines[0].Trim().TrimStart('#').Trim();
            var content = lines.Length > 1 ? lines[1].Trim() : trimmed;
            result.Add((title, content));
        }

        return result;
    }

    private async Task<(string Title, string Content)> FindMostRelevantSectionAsync(
        string question, List<MsdsChunk> sections, CancellationToken ct)
    {
        var q = question.ToLowerInvariant();

        // 1. KURAL BAZLI ÖNCELİK (Endüstriyel Güvenlik Poka-Yoke'si!)
        // Soru yangın veya söndürme içeriyorsa doğrudan Bölüm 5'i (Yangın) getir!
        if (q.Contains("yangın") || q.Contains("fire") || q.Contains("söndür") ||
            q.Contains("extinguish") || q.Contains("alev") || q.Contains("flame") || q.Contains("water"))
        {
            var fireSection = sections.FirstOrDefault(s => s.Title.Contains("Yangın", StringComparison.OrdinalIgnoreCase) || s.Title.Contains("BÖLÜM 5", StringComparison.OrdinalIgnoreCase));
            if (fireSection is not null)
            {
                logger.LogInformation("RAG: Yangın sorusu tespit edildi, doğrudan Bölüm 5 seçildi.");
                return (fireSection.Title, fireSection.Content);
            }
        }

        // Soru ilk yardım / temas / zehirlenme içeriyorsa doğrudan Bölüm 4'ü getir!
        if (q.Contains("göz") || q.Contains("eye") || q.Contains("cilt") || q.Contains("skin") ||
            q.Contains("yut") || q.Contains("swallow") || q.Contains("nefes") || q.Contains("breath") ||
            q.Contains("inhale") || q.Contains("inhalation") || q.Contains("oxygen") ||
            q.Contains("yardım") || q.Contains("first aid") || q.Contains("contact") || q.Contains("temas"))
        {
            var firstAidSection = sections.FirstOrDefault(s => s.Title.Contains("İlk Yardım", StringComparison.OrdinalIgnoreCase) || s.Title.Contains("BÖLÜM 4", StringComparison.OrdinalIgnoreCase));
            if (firstAidSection is not null) return (firstAidSection.Title, firstAidSection.Content);
        }

        // Soru dökülme / sızıntı / talaş içeriyorsa Bölüm 6'yı getir!
        if (q.Contains("dökül") || q.Contains("spill") || q.Contains("sızıntı") ||
            q.Contains("leak") || q.Contains("delin") || q.Contains("puncture"))
        {
            var spillSection = sections.FirstOrDefault(s => s.Title.Contains("Dökülme", StringComparison.OrdinalIgnoreCase) || s.Title.Contains("Yayılma", StringComparison.OrdinalIgnoreCase) || s.Title.Contains("BÖLÜM 6", StringComparison.OrdinalIgnoreCase));
            if (spillSection is not null) return (spillSection.Title, spillSection.Content);
        }

        // Soru depolama / sıcaklık / raf içeriyorsa Bölüm 7'yi getir!
        if (q.Contains("depo") || q.Contains("storage") || q.Contains("store") ||
            q.Contains("sıcaklık") || q.Contains("temperature") || q.Contains("raf") ||
            q.Contains("shelf") || q.Contains("güneş") || q.Contains("sunlight"))
        {
            var storageSection = sections.FirstOrDefault(s => s.Title.Contains("Depolama", StringComparison.OrdinalIgnoreCase) || s.Title.Contains("BÖLÜM 7", StringComparison.OrdinalIgnoreCase));
            if (storageSection is not null) return (storageSection.Title, storageSection.Content);
        }

        // 2. EMBEDDING VEKTÖR ARAMASI (Yukarıdakilere girmezse)
        try
        {
            var questionVector = await GetEmbeddingAsync(question, ct);
            if (questionVector != null)
            {
                double maxSimilarity = -1;
                var bestSection = sections.First();

                foreach (var section in sections)
                {
                    var sectionText = $"{section.ChemicalCode}\n{section.Title}\n{section.Content}";
                    var sectionVector = await GetEmbeddingAsync(sectionText, ct);
                    if (sectionVector != null)
                    {
                        var sim = CosineSimilarity(questionVector, sectionVector);
                        if (sim > maxSimilarity)
                        {
                            maxSimilarity = sim;
                            bestSection = section;
                        }
                    }
                }
                return (bestSection.Title, bestSection.Content);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Ollama embedding hatası: {Msg}", ex.Message);
        }

        // Varsayılan: İlk bölüm veya acil durum özeti
        var emergencySummary = sections.FirstOrDefault(s => s.Title.Contains("Acil Durum Özeti", StringComparison.OrdinalIgnoreCase));
        return emergencySummary is not null
            ? (emergencySummary.Title, emergencySummary.Content)
            : (sections[0].Title, sections[0].Content);
    }

    private static (string Title, string Content) FindByKeywords(
        string question,
        List<(string Title, string Content)> sections)
    {
        var normalizedQuestion = question.ToLowerInvariant();

        if (normalizedQuestion.Contains("yangın") ||
            normalizedQuestion.Contains("fire") ||
            normalizedQuestion.Contains("söndür") ||
            normalizedQuestion.Contains("extinguish") ||
            normalizedQuestion.Contains("alev") ||
            normalizedQuestion.Contains("flame") ||
            normalizedQuestion.Contains("water"))
        {
            return FindSectionOrFirst(sections, "Yangın");
        }

        if (normalizedQuestion.Contains("göz") ||
            normalizedQuestion.Contains("eye") ||
            normalizedQuestion.Contains("cilt") ||
            normalizedQuestion.Contains("skin") ||
            normalizedQuestion.Contains("yut") ||
            normalizedQuestion.Contains("swallow") ||
            normalizedQuestion.Contains("nefes") ||
            normalizedQuestion.Contains("breath") ||
            normalizedQuestion.Contains("inhale") ||
            normalizedQuestion.Contains("inhalation") ||
            normalizedQuestion.Contains("oxygen") ||
            normalizedQuestion.Contains("yardım") ||
            normalizedQuestion.Contains("first aid") ||
            normalizedQuestion.Contains("contact"))
        {
            return FindSectionOrFirst(sections, "İlk Yardım");
        }

        if (normalizedQuestion.Contains("dökül") ||
            normalizedQuestion.Contains("spill") ||
            normalizedQuestion.Contains("sızıntı") ||
            normalizedQuestion.Contains("leak") ||
            normalizedQuestion.Contains("delin") ||
            normalizedQuestion.Contains("puncture"))
        {
            return FindSectionOrFirst(sections, "Dökülme", "Yayılma");
        }

        if (normalizedQuestion.Contains("depo") ||
            normalizedQuestion.Contains("storage") ||
            normalizedQuestion.Contains("store") ||
            normalizedQuestion.Contains("sıcaklık") ||
            normalizedQuestion.Contains("temperature") ||
            normalizedQuestion.Contains("raf") ||
            normalizedQuestion.Contains("shelf"))
        {
            return FindSectionOrFirst(sections, "Depolama");
        }

        return FindSectionOrFirst(sections, "Özet");
    }

    private static (string Title, string Content) FindSectionOrFirst(
        List<(string Title, string Content)> sections,
        params string[] titleParts)
    {
        var section = sections.FirstOrDefault(candidate =>
            titleParts.Any(titlePart =>
                candidate.Title.Contains(titlePart, StringComparison.OrdinalIgnoreCase)));

        return section != default ? section : sections[0];
    }

    private async Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        try
        {
            var payload = new { model = EmbeddingModel, prompt = text };
            using var response = await httpClient.PostAsJsonAsync(
                $"{OllamaBaseUrl}/api/embeddings",
                payload,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
            var embeddingArray = json?["embedding"]?.AsArray();
            return embeddingArray?
                .Select(value => value is null ? 0f : value.GetValue<float>())
                .ToArray();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    private static double CosineSimilarity(float[] first, float[] second)
    {
        if (first.Length != second.Length)
        {
            return 0;
        }

        double dot = 0;
        double firstNorm = 0;
        double secondNorm = 0;

        for (var index = 0; index < first.Length; index++)
        {
            dot += first[index] * second[index];
            firstNorm += first[index] * first[index];
            secondNorm += second[index] * second[index];
        }

        return firstNorm == 0 || secondNorm == 0
            ? 0
            : dot / (Math.Sqrt(firstNorm) * Math.Sqrt(secondNorm));
    }

    private async Task<string> GenerateAnswerWithLlmAsync(
        string question,
        string chemicalCode,
        string sectionTitle,
        string sectionContent,
        CancellationToken ct)
    {
        try
        {
            var systemPrompt = $"""
    Sen endüstriyel bir tesiste görevli Acil Durum ve İSG Asistanısın.
    Görevin: Yalnızca aşağıdaki {chemicalCode} kimyasalının MSDS dokümanına dayanarak operatörün sorusuna DOĞRUDAN, KISA ve NET bir yanıt vermektir.

    KURALLAR:
    - Soru İngilizce sorulduysa yanıtı KESİNLİKLE akıcı ve profesyonel bir İNGİLİZCE ile ver. Kaynak doküman Türkçe olsa bile talimatları İngilizceye çevir.
    - Soru Türkçe sorulduysa Türkçe yanıt ver.
    - Uydurma bilgi verme, boş giriş cümleleri kurma, doğrudan adım adım talimatları ver.
    - Kullanılması gereken ekipmanları madde madde yaz.
    - Yalnızca {chemicalCode} bağlamını kullan. Başka bir kimyasalın bilgisini bu yanıta taşıma.
    - Bu kimyasala veya soruya ait bilgi verilen MSDS bağlamında yoksa tam olarak şu anlamı ver: "Bu kimyasala ait MSDS bilgisi sistemde kayıtlı değildir."

    [RESMİ MSDS METNİ - {chemicalCode} / {sectionTitle}]:
    {sectionContent}

    SORU: {question}
    """;

            var payload = new
            {
                model = LlmModel,
                prompt = systemPrompt,
                stream = false
            };

            using var response = await httpClient.PostAsJsonAsync(
                $"{OllamaBaseUrl}/api/generate",
                payload,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                return $"Ollama yanıt veremedi ({response.StatusCode}). Lütfen modelin kurulu olduğundan emin olun.";
            }

            var json = await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken: ct);
            return json?["response"]?.ToString() ?? "Cevap üretilemedi.";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(ex, "Ollama generation servisine bağlanılamadı.");
            return "Ollama servisine bağlanılamadı. Lütfen Ollama servisinin açık olduğundan emin olun.";
        }
    }
}
