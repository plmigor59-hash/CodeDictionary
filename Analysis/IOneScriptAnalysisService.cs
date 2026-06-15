namespace CodeDictionary.Analysis
{
    public interface IOneScriptAnalysisService
    {
        Task<AnalysisResult> AnalyzeAsync(string code, CancellationToken cancellationToken = default);
    }
}
