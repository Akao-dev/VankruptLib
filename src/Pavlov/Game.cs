using System.Diagnostics;
using static Vankrupt.Pavlov.Http;

namespace Vankrupt.Pavlov;

/// <summary>
/// API calls and control launch, runtime, closing of Pavlov TV.
/// </summary>
public static class Game
{
	/// <summary>
	/// Steam Game ID for Pavlov VR (the real game)
	/// </summary>
	public const ulong SteamGameId_PavlovVR = 555160;

	/// <summary>
	/// Steam Game ID for Pavlov TV (Replay viewer without anticheat, preferred option)
	/// </summary>
	public const ulong SteamGameId_PavlovTV = 3245770;

	/// <summary>
	/// Process names related to Pavlov.
	/// </summary>
	public static readonly string[] ProcessNames = ["Pavlov.exe", "Pavlov-Win64-Shipping.exe", "GameThread"];

	/// <summary>
	/// Check if Pavlov is running.
	/// </summary>
	/// <returns>True if process related to Pavlov is found.</returns>
	public static bool IsRunning
	{
		get
		{
			// Iterate known process names.
			foreach (string p_name in ProcessNames)
			{
				// Find processes by name.
				Process[] list = Process.GetProcessesByName(p_name);

				// Return true if any process was found.
				if (list.Length > 0) return true;
			}

			// No process was found.
			return false;
		}
	}

	/// <summary>
	/// Launch Pavlov TV if not running already. Function attempts to wait process to launch, but returns normally if not. See <seealso cref="IsRunning"/> if launch result is needed.
	/// </summary>
	/// <param name="RunFlagGetter">Function to check if function should still wait for process to start.</param>
	/// <param name="launchTimeout">How long function will wait for "game" to show in process list.</param>
	/// <param name="steam_game_id">Steam game id to launch (Pavlov TV by default).</param>
	public static void Launch(Func<bool> RunFlagGetter, long launchTimeout, ulong steam_game_id = SteamGameId_PavlovTV)
	{
		Stopwatch timeout = new();

		// Prevent double launch (We do not have ability to utilize multiple instances)
		if (IsRunning) return;

		// Launch Pavlov TV via steam
		_ = Process.Start("steam", $"steam://rungameid/{steam_game_id}");

		// Wait for game to launch.
		timeout.Restart();
		while (!IsRunning && timeout.ElapsedMilliseconds < launchTimeout && RunFlagGetter.Invoke()) { try { Thread.Sleep(250); } catch (Exception) { } }
	}

	/// <summary>
	/// Close Pavlov TV.
	/// </summary>
	/// <param name="process_names">Additional process names to shutdown.</param>
	public static void Close(params string[] process_names)
	{
		// Make list of process names
		List<string> processNames = new();

		// Add default process names & additionally provided names
		processNames.AddRange(ProcessNames);
		processNames.AddRange(process_names);

		// Send termination signal to processes with listed names
		foreach (string p_name in processNames) KillProcesses(p_name);
	}

	/// <summary>
	/// SIGTERM processes with name.
	/// </summary>
	/// <param name="name">Name of process.</param>
	private static void KillProcesses(string name)
	{
		// Input validation
		if (string.IsNullOrEmpty(name)) return;

		// Get list of processes with specific name
		Process[] list = Process.GetProcessesByName(name);

		// Send SIGTERM to all processes listed
		foreach (Process p in list)
		{
			Console.WriteLine($"TERMINATE: PID={p.Id} NAME={p.ProcessName}");
			_ = Process.Start("kill", $"-s SIGTERM {p.Id}");
		}
	}



	/// <summary>
	/// API for Pavlov TV (locally) hosted web interface.
	/// </summary>
	public static class API
	{
		/// <summary>
		/// Path to Pavlov TV API endpoint.
		/// </summary>
		public const string UrlPath_Killfeed = "Killfeed";
		/// <summary>
		/// Path to Pavlov TV API endpoint.
		/// </summary>
		public const string UrlPath_LoadReplay = "LoadReplay";
		/// <summary>
		/// Path to Pavlov TV API endpoint.
		/// </summary>
		public const string UrlPath_MatchEvents = "MatchEvents";
		/// <summary>
		/// Path to Pavlov TV API endpoint.
		/// </summary>
		public const string UrlPath_MatchStatus = "MatchStatus";
		/// <summary>
		/// Path to Pavlov TV API endpoint.
		/// </summary>
		public const string UrlPath_MatchTime = "MatchTime";
		/// <summary>
		/// Path to Pavlov TV API endpoint.
		/// </summary>
		public const string UrlPath_Pause = "Pause";
		/// <summary>
		/// Path to Pavlov TV API endpoint.
		/// </summary>
		public const string UrlPath_PlayersPos = "PlayersPos";
		/// <summary>
		/// Path to Pavlov TV API endpoint.
		/// </summary>
		public const string UrlPath_ReplayList = "ReplayList";


		/// <summary>
		/// Get List of replays from Pavlov TV.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>ReplayList from Pavlov TV</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		[Obsolete("This function is rendered as obsolete since better list can be queried directly from server, but left here since it might be useful in some cenarios.")]
		public static Result<HttpResponses.ReplayList_> GetReplays(Http http_ctx, string host = "http://localhost/")
		{
			// Append path to url
			string url = UrlAppend(host, UrlPath_ReplayList);

			// Call API
			return http_ctx.GetJson<HttpResponses.ReplayList_>(url, null, null);
		}

		/// <summary>
		/// Command Pavlov TV to load specific replay.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="replay_id">Replay id to be loaded.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>True, on success.</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		public static bool LoadReplay(Http http_ctx, string replay_id, string host = "http://localhost/")
		{
			Result<HttpResponses.Default_> response;

			// Append path to url
			string url = UrlAppend(host, UrlPath_LoadReplay);

			// Call API
			response = http_ctx.PostJson<HttpResponses.Default_, HttpResponses.LoadReplay_>(url, new HttpResponses.LoadReplay_() { Id = replay_id }, null, null);

			// Return result
			return response.OK && (response.Data?.Successful ?? false);
		}

		/// <summary>
		/// Get currently available events from Pavlov TV.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>Result of API call. Possibly containing match events.</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		public static Result<HttpResponses.MatchEvents_> GetEvents(Http http_ctx, string host = "http://localhost/")
		{
			// Append path to url
			string url = UrlAppend(host, UrlPath_MatchEvents);

			// Call API
			return http_ctx.GetJson<HttpResponses.MatchEvents_>(url, null, null);
		}

		/// <summary>
		/// Get currently available status from Pavlov TV.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>Result of API call. Possibly containing status of replay.</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		public static Result<HttpResponses.MatchStatus_> GetStatus(Http http_ctx, string host = "http://localhost/")
		{
			// Append path to url
			string url = UrlAppend(host, UrlPath_MatchStatus);

			// Call API
			return http_ctx.GetJson<HttpResponses.MatchStatus_>(url, null, null);
		}

		/// <summary>
		/// Get currently available locations from Pavlov TV.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>Result of API call. Possibly containing player, bomb and camera locations.</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		public static Result<HttpResponses.Locations_> GetLocations(Http http_ctx, string host = "http://localhost/")
		{
			// Append path to url
			string url = UrlAppend(host, UrlPath_PlayersPos);

			// Call API
			return http_ctx.GetJson<HttpResponses.Locations_>(url, null, null);
		}

		/// <summary>
		/// Get currently available killfeed from Pavlov TV.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>Result of API call. Possibly containing killfeed.</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		public static Result<HttpResponses.Killfeed_> GetKillfeed(Http http_ctx, string host = "http://localhost/")
		{
			// Append path to url
			string url = UrlAppend(host, UrlPath_Killfeed);

			// Call API
			return http_ctx.GetJson<HttpResponses.Killfeed_>(url, null, null);
		}

		/// <summary>
		/// Get current replay time.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>Result of API call. Possibly containing seconds passed since replay started.</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		public static Result<HttpResponses.MatchTime_> GetTime(Http http_ctx, string host = "http://localhost/")
		{
			// Append path to url
			string url = UrlAppend(host, UrlPath_MatchTime);

			// Call API
			return http_ctx.GetJson<HttpResponses.MatchTime_>(url, null, null);
		}

		/// <summary>
		/// Set replay time.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="time">Time to be set in seconds.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>True, on success.</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		public static bool? SetTime(Http http_ctx, double time, string host = "http://localhost/")
		{
			Result<HttpResponses.Default_> response;

			// Append path to url
			string url = UrlAppend(host, UrlPath_MatchTime);

			// Do not allow negative replay time
			if (time < 0) time = 0;

			// Call API
			response = http_ctx.PostJson<HttpResponses.Default_, HttpResponses.MatchTime_>(url, new HttpResponses.MatchTime_() { MatchTime = time }, null, null);

			// Return result
			return response.OK && (response.Data?.Successful ?? false);
		}

		/// <summary>
		/// Get current pause state.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>Pause state. Null on error.</returns>
		/// <returns>Result of API call. Possibly containing pause state.</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		public static Result<HttpResponses.Pause_> GetPause(Http http_ctx, string host = "http://localhost/")
		{
			// Append path to url
			string url = UrlAppend(host, UrlPath_Pause);

			// Call API
			return http_ctx.GetJson<HttpResponses.Pause_>(url, null, null);
		}

		/// <summary>
		/// Set pause state.
		/// </summary>
		/// <param name="http_ctx">Context of special HTTP handling class.</param>
		/// <param name="pause">Pause state.</param>
		/// <param name="host">Host address URL.</param>
		/// <returns>True, on success.</returns>
		/// <exception cref="InvalidDataException">If invalid host URL is provided.</exception>
		public static bool? SetPause(Http http_ctx, bool pause, string host = "http://localhost/")
		{
			Result<HttpResponses.Default_> response;

			// Append path to url
			string url = UrlAppend(host, UrlPath_Pause);

			// Call API
			response = http_ctx.PostJson<HttpResponses.Default_, HttpResponses.Pause_>(url, new HttpResponses.Pause_() { Paused = pause }, null, null);

			// Return result
			return response.OK && (response.Data?.Successful ?? false);
		}
	}



	/// <summary>
	/// JSON template classes for API calls - Pavlov TV.
	/// </summary>
	public static class HttpResponses
	{
		/// <summary>
		/// Default response model - Pavlov TV.
		/// </summary>
		public class Default_
		{
			public bool Successful { get; set; } = false;
			public string? errorCode { get; set; } = null;
			public string? errorMessage { get; set; } = null;
		}

		/// <summary>
		/// Replay list - Pavlov TV.
		/// </summary>
		public class ReplayList_
		{
			public List<Replay_>? Replays { get; set; } = null;

			public class Replay_
			{
				public string? Id { get; set; } = null;
				public string? Name { get; set; } = null;
				public string? GameMode { get; set; } = null;
				public DateTime? Timestamp { get; set; } = null;
				public bool? Shack { get; set; } = null;
				public bool? LocalReplay { get; set; } = null;
				public bool? Live { get; set; } = null;
			}
		}

		/// <summary>
		/// Command for Pavlov TV to load replay - Pavlov TV.
		/// </summary>
		public class LoadReplay_
		{
			public string? Id { get; set; } = null;
		}

		/// <summary>
		/// Match events queried - Pavlov TV.
		/// </summary>
		public class MatchEvents_
		{
			public double? MatchTime { get; set; } = null;
			public double? TotalTime { get; set; } = null;
			public List<MatchEvent_>? Events { get; set; } = null;

			public class MatchEvent_
			{
				public string? Name { get; set; } = null;
				public double? Time { get; set; } = null;
				public int? AdditionalData { get; set; } = null;
			}
		}

		/// <summary>
		/// Match status queried - Pavlov TV.
		/// </summary>
		public class MatchStatus_
		{
			public string? ReplayId { get; set; } = null;
			public bool? MatchActive { get; set; } = null;
			public string? MapName { get; set; } = null;
			public string? RoundState { get; set; } = null;
			public int? RoundTime { get; set; } = null;
			public bool? Teams { get; set; } = null;
			public int? RoundsLeft { get; set; } = null;
			public int? Team0Score { get; set; } = null;
			public int? Team1Score { get; set; } = null;
			public int? AttackingTeamId { get; set; } = null;
			public List<Player_>? Team0 { get; set; } = null;
			public int? Team0Cash { get; set; } = null;
			public List<Player_>? Team1 { get; set; } = null;
			public int? Team1Cash { get; set; } = null;

			public class Player_
			{
				public string? Name { get; set; } = null;
				public string? Id { get; set; } = null;
				public int? Platform { get; set; } = null;
				public int? Cash { get; set; } = null;
				public int? Score { get; set; } = null;
				public int? Kills { get; set; } = null;
				public int? Deaths { get; set; } = null;
				public bool? Dead { get; set; } = null;
				public int? Health { get; set; } = null;
				public int? Armour { get; set; } = null;
				public bool? Helmet { get; set; } = null;
				public string? PrimaryWeapon { get; set; } = null;
				public List<string>? SecondaryWeapons { get; set; } = null;
				public string? Avatar { get; set; } = null;
				public bool? Bot { get; set; } = null;
			}
		}

		/// <summary>
		/// Locations queried - Pavlov TV.
		/// </summary>
		public class Locations_
		{
			public double? CameraX { get; set; } = null;
			public double? CameraY { get; set; } = null;
			public double? CameraZ { get; set; } = null;
			public double? CameraYaw { get; set; } = null;
			public double? BombX { get; set; } = null;
			public double? BombY { get; set; } = null;
			public double? BombZ { get; set; } = null;
			public int? BombState { get; set; } = null;
			public List<Player_>? Players { get; set; } = null;

			public class Player_
			{
				public string? Name { get; set; } = null;
				public string? Id { get; set; } = null;
				public int? TeamId { get; set; } = null;
				public double? X { get; set; } = null;
				public double? Y { get; set; } = null;
				public double? Z { get; set; } = null;
				public double? Yaw { get; set; } = null;
			}
		}

		/// <summary>
		/// Match events queried - Pavlov TV.
		/// </summary>
		public class Killfeed_
		{
			public List<Kill_>? Killfeed { get; set; } = null;


			public class Kill_
			{
				public string? Killer { get; set; } = null;
				public string? KillerId { get; set; } = null;
				public string? Killed { get; set; } = null;
				public string? KilledId { get; set; } = null;
				public bool? Headshot { get; set; } = null;
				public double? MatchTime { get; set; } = null;
				public double? EntryLifespan { get; set; } = null;
				public string? KilledBy { get; set; } = null;
				public int? KillerTeam { get; set; } = null;
				public int? KilledTeam { get; set; } = null;
			}
		}

		/// <summary>
		/// Replay time commanded or queried - Pavlov TV.
		/// </summary>
		public class MatchTime_
		{
			public double? MatchTime { get; set; } = null;
		}

		/// <summary>
		/// Pause commanded or queried - Pavlov TV.
		/// </summary>
		public class Pause_
		{
			public bool? Paused { get; set; } = null;
		}
	}
}
