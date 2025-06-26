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
	/// Urls related to operation.
	/// </summary>
	public static class Urls
	{
		private static string _Host_URL = "https://tv.vankrupt.net/";
		public static string Host
		{
			get
			{
				return _Host_URL;
			}
			set
			{
				string tmp = value;
				if (Uri.TryCreate(tmp, UriKind.RelativeOrAbsolute, out _))
					_Host_URL = tmp;
			}
		}
		public static string ReplayList
		{
			get
			{
				if (Host.EndsWith('/'))
					return Host + "find";
				return Host + "/find";
			}
		}
		public static string Stats(string replay_id)
		{
			if (!Host.EndsWith('/')) Host += "/";
			return Host + $"replay/{HttpUtility.UrlEncode(replay_id)}/stats";
		}
	}

	/// <summary>
	/// Http connection context.
	/// </summary>
	internal static readonly Http http_ctx = new();

	/// <summary>
	/// Get all replays listed in official Pavlov server.
	/// </summary>
	/// <param name="player_name">Filter by player name.</param>
	/// <returns>ReplayList from Pavlov server.</returns>
	public static Result<List<HttpResponse.ReplayList_.Replay_>> GetReplays(string? player_name = null)
	{
		List<HttpResponse.ReplayList_.Replay_> replays = [];
		UrlEncoder urlEncoder = UrlEncoder.Default;
		string url = Urls.ReplayList;
		int offset = 0;
		int total = 0;
		HttpStatusCode code = HttpStatusCode.BadRequest;
		long TimeHttp = 0;
		long TimeProcessing = 0;
		long TimeTotal = 0;

		// Add player argument into path URI
		if (player_name != null) url += "/" + urlEncoder.Encode(player_name);

		do
		{
			// Set offset
			List<KeyValuePair<string, string?>> urlParams = [];
			urlParams.Add(new("offset", $"{offset}"));

			// Get next replays
			var result = http_ctx.GetJson<HttpResponse.ReplayList_>(url, urlParams, null);

			// Error handling
			if (!result.OK || result.Data?.replays == null)
			{
				return new Result<List<HttpResponse.ReplayList_.Replay_>>()
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
		return new Result<List<HttpResponse.ReplayList_.Replay_>>()
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
	public static Result<HttpResponse.ReplayStats_> GetReplayStats(string replay_id) => http_ctx.GetJson<HttpResponse.ReplayStats_>(Urls.Stats(replay_id), null, null);

	public static class HttpResponse
	{
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
