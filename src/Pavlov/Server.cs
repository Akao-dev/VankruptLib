using System.Net;
using System.Text.Encodings.Web;
using System.Web;
using static Vankrupt.Pavlov.Http;

namespace Vankrupt.Pavlov;

/// <summary>
/// Pavlov master replay server interface.
/// </summary>
public static class Server
{
	/// <summary>
	/// Generates URL for fetching replays.
	/// </summary>
	/// <param name="host">Host URL.</param>
	/// <param name="player_name">Player name to search/filter with (must be exact).</param>
	/// <returns>Generated URL.</returns>
	/// <exception cref="InvalidDataException">If URL is invalid.</exception>
	public static string Url_ReplayList(string host, string? player_name = null)
	{
		string path = "find";

		// Add player name in to search if needed
		if (player_name != null) path += '/' + HttpUtility.UrlEncode(player_name); ;

		// Generate URL
		return UrlAppend(host, path);
	}

	/// <summary>
	/// Generates URL for replay statistics.
	/// </summary>
	/// <param name="host">Host URL.</param>
	/// <param name="replay_id">Replay id which stats to fetch.</param>
	/// <returns>Generated URL.</returns>
	/// <exception cref="InvalidDataException">If URL or replay id is invalid.</exception>
	public static string Url_Stats(string host, string replay_id)
	{
		// Test ids validity
		if (string.IsNullOrWhiteSpace(replay_id)) throw new InvalidDataException("Invalid replay id!");

		// Make player name search if needed
		string path = "replay/" + HttpUtility.UrlEncode(replay_id) + "/stats";

		// Generate URL
		return UrlAppend(host, path);
	}

	/// <summary>
	/// Get all replays listed in official Pavlov server.
	/// </summary>
	/// <param name="http_ctx">Context of special HTTP handling class.</param>
	/// <param name="player_name">Filter by player name.</param>
	/// <param name="host">Host address URL.</param>
	/// <returns>ReplayList from Pavlov server.</returns>
	/// <exception cref="InvalidDataException">If URL is invalid.</exception>
	public static Result<List<HttpResponses.ReplayList_.Replay_>> GetReplays(ref Http http_ctx, string? player_name = null, string host = "https://tv.vankrupt.net/")
	{
		List<HttpResponses.ReplayList_.Replay_> replays = [];
		UrlEncoder urlEncoder = UrlEncoder.Default;
		int offset = 0;
		int total = 0;
		HttpStatusCode code = HttpStatusCode.BadRequest;
		long TimeHttp = 0;
		long TimeProcessing = 0;
		long TimeTotal = 0;

		do
		{
			// Set offset
			List<KeyValuePair<string, string?>> urlParams = [];
			urlParams.Add(new("offset", $"{offset}"));

			// Get next replays
			var result = http_ctx.GetJson<HttpResponses.ReplayList_>(Url_ReplayList(host, player_name), urlParams, null);

			// Error handling
			if (!result.OK || result.Data?.replays == null)
			{
				return new Result<List<HttpResponses.ReplayList_.Replay_>>()
				{
					OK = result.OK,
					Code = result.Code,
					Info = result.Info,
					Error = result.Error ?? new Exception($"Unknown error occurred!"),
					Data = null,
					DataRaw = null,
					ApiDefaultResponse = result.ApiDefaultResponse,
					TimeHttp = result.TimeHttp + TimeHttp,
					TimeProcessing = result.TimeProcessing + TimeProcessing,
					TimeTotal = result.TimeTotal + TimeTotal
				};
			}

			// Break if results are empty
			if (result.Data.replays.Count <= 0) break;

			// Stats increment
			code = result.Code ?? code;
			offset += result.Data.replays?.Count ?? 100;
			total = result.Data.total ?? total;
			TimeHttp += result.TimeHttp;
			TimeProcessing += result.TimeProcessing;
			TimeTotal += result.TimeTotal;

			// Compiler is complaining too much...
			if (result.Data.replays != null)
			{
				// Process input
				foreach (var replay in result.Data.replays)
				{
					// Skip if replay is already in list
					if (replays.Where(x => x._id == replay._id).ToList().Count > 0) continue;

					// Add replay to list
					replays.Add(replay);
				}
			}

			// Sort replays
			replays.Sort((a, b) => DateTime.Compare(b.Created, a.Created));
		}
		while (replays.Count < total);

		// Return result
		return new Result<List<HttpResponses.ReplayList_.Replay_>>()
		{
			OK = true,
			Code = code,
			Info = null,
			Error = null,
			Data = replays,
			DataRaw = null,
			ApiDefaultResponse = null,
			TimeHttp = TimeHttp,
			TimeProcessing = TimeProcessing,
			TimeTotal = TimeTotal
		};
	}

	/// <summary>
	/// Get replay stats from Vankrypt server.
	/// </summary>
	/// <param name="replay_id">Id of replay.</param>
	/// <returns>Replay stats.</returns>
	/// <exception cref="InvalidDataException">If URL or replay id is invalid.</exception>
	public static Result<HttpResponses.ReplayStats_> GetReplayStats(ref Http http_ctx, string replay_id, string host = "https://tv.vankrupt.net/") => http_ctx.GetJson<HttpResponses.ReplayStats_>(Url_Stats(host, replay_id), null, null);



	/// <summary>
	/// JSON template classes for API calls - Pavlov TV replay hosting server.
	/// </summary>
	public static class HttpResponses
	{

		/// <summary>
		/// Replay list - Pavlov TV replay hosting server.
		/// </summary>
		public class ReplayList_
		{
			public List<Replay_>? replays { get; set; } = null;
			public int? total { get; set; } = null;

			public class Replay_
			{
				public string? _id { get; set; } = null;
				public string? workshop_id { get; set; } = null;
				public string? workshop_mods { get; set; } = null;
				public bool? shack { get; set; } = null;
				public bool? competitive { get; set; } = null;
				public string? gameMode { get; set; } = null;
				public DateTime? created { get; set; } = null;
				public DateTime? expires { get; set; } = null;
				public bool? live { get; set; } = null;
				public string? friendlyName { get; set; } = null;
				public List<string>? users { get; set; } = null;
				public int? secondsSince { get; set; } = null;
				public int? modcount { get; set; } = null;


				/// <summary>
				/// Calculated time when replay was created.
				/// </summary>
				public DateTime Created
				{
					get
					{
						if (created != null) return created.Value;
						if (secondsSince != null) return DateTime.UtcNow - new TimeSpan(0, 0, secondsSince.Value);
						return DateTime.UtcNow;
					}
				}
				private string _PrintUsers
				{
					get
					{
						string str = $"{"Users",18} :\n";
						if (users != null)
						{
							foreach (string user in users) str += $"{"",12}- {user}\n";
						}
						return str;
					}
				}
				public override string ToString()
				{
					return $"{"Id",18} : {_id}\n" +
					$"{"Workshop id",18} : {workshop_id}\n" +
					$"{"Workshop mods",18} : {workshop_mods}\n" +
					$"{"Shack",18} : {shack}\n" +
					$"{"Is competitive",18} : {competitive}\n" +
					$"{"Game mode",18} : {gameMode}\n" +
					$"{"Created",18} : {created}\n" +
					$"{"Expires",18} : {expires}\n" +
					$"{"Is live",18} : {live}\n" +
					$"{"Level name",18} : {friendlyName}\n" +
					$"{"Seconds since",18} : {secondsSince}\n" +
					$"{"Mod count",18} : {modcount}\n" + _PrintUsers;
				}
			}
		}

		/// <summary>
		/// Replay stats published by server - Pavlov TV replay hosting server.
		/// </summary>
		public class ReplayStats_
		{
			public List<PlayerStats_>? allStats { get; set; } = null;
			public string? MapLabel { get; set; } = null;
			public string? GameMode { get; set; } = null;
			public long? MatchDuration { get; set; } = null;
			public int? PlayerCount { get; set; } = null;
			public bool? bTeams { get; set; } = null;
			public int? Team0Score { get; set; } = null;
			public int? Team1Score { get; set; } = null;

			public class PlayerStats_
			{
				public string? playerName { get; set; } = null;
				public int? teamId { get; set; } = null;
				public List<Stats_>? stats { get; set; } = null;

				public class Stats_
				{
					public string? statType { get; set; } = null;
					public int? amount { get; set; } = null;
				}
			}
		}
	}
}
