#nullable enable

namespace Myra.Utility.Search
{
	/// <summary>
	/// Matches a single candidate string against a query. Multi-field weighting
	/// across several candidate strings is the caller's job (see <see cref="SearchScoring"/>).
	/// </summary>
	public interface ISearchStrategy
	{
		SearchMatch Match(string candidate, string query);

		bool IsQueryValid(string query) => true;
	}
}
