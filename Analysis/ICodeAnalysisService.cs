namespace CodeDictionary.Analysis
{
    public interface ICodeAnalysisService
    {
        Task<AnalysisResult> AnalyzeAsync(string code, CancellationToken cancellationToken = default);
    }
}
