using System.Threading;
using System.Threading.Tasks;

namespace CodeDictionary.Analysis
{
    public interface IOneScriptAnalysisService
    {
        Task<AnalysisResult> AnalyzeAsync(string code, CancellationToken cancellationToken = default);
    }
}
