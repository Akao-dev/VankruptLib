using System.Diagnostics;
using static Vankrupt.Pavlov.Http;

namespace Vankrupt.Pavlov;

/// <summary>
/// Control launch, runtime & closing of "game".
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
	/// Http connection context.
	/// </summary>
	public static readonly Http http_ctx = new();

	/// <summary>
	/// How long we should wait for Pavlov to show up in process list in milliseconds.
	/// </summary>
	public static long LaunchTimeout { get; set; } = 30000;

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
	/// Launch Pavlov TV if not running already.
	/// </summary>
	/// <param name="RunFlagGetter">Function to check if function should still wait for process to start.</param>
	/// <param name="steam_game_id">Steam game id to launch (Pavlov TV by default).</param>
	public static void Launch(Func<bool> RunFlagGetter, ulong steam_game_id = SteamGameId_PavlovTV)
	{
		Stopwatch timeout = new();

		// Prevent double launch (We do not have ability to utilize multiple instances)
		if (IsRunning) return;

		// Launch Pavlov TV via steam
		_ = Process.Start("steam", $"steam://rungameid/{steam_game_id}");

		// Wait for game to launch.
		timeout.Restart();
		while (!IsRunning && timeout.ElapsedMilliseconds < LaunchTimeout && RunFlagGetter.Invoke()) { try { Thread.Sleep(250); } catch (Exception) { } }
	}

	/// <summary>
	/// Close Pavlov TV.
	/// </summary>
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
		public static UInt16 PortNumber { get; set; } = 1234;
		public static string Host { get { return $"http://localhost:{PortNumber}/"; } }
		public static string Url_Killfeed { get { return Host + "Killfeed"; } }
		public static string Url_LoadReplay { get { return Host + "LoadReplay"; } }
		public static string Url_MatchEvents { get { return Host + "MatchEvents"; } }
		public static string Url_MatchStatus { get { return Host + "MatchStatus"; } }
		public static string Url_MatchTime { get { return Host + "MatchTime"; } }
		public static string Url_Pause { get { return Host + "Pause"; } }
		public static string Url_PlayersPos { get { return Host + "PlayersPos"; } }
		public static string Url_ReplayList { get { return Host + "ReplayList"; } }

		/// <summary>
		/// Get List of replays from Pavlov TV.
		/// </summary>
		/// <returns>ReplayList from Pavlov TV</returns>
		[Obsolete("This function is rendered as obsolete since better list can be queried directly from server.")]
		public static Result<HttpResponses.ReplayList_> GetReplays() { return http_ctx.GetJson<HttpResponses.ReplayList_>(Url_ReplayList, null, null); }

		/// <summary>
		/// Command Pavlov TV to load specific replay.
		/// </summary>
		/// <param name="replay_id">Replay id to be loaded.</param>
		/// <returns>True, if error occurred.</returns>
		public static bool LoadReplay(string replay_id)
		{
			Result<HttpResponses.Default_> response;

			// Post command.
			response = http_ctx.PostJson<HttpResponses.Default_, HttpResponses.LoadReplay_>(Url_LoadReplay, new HttpResponses.LoadReplay_() { Id = replay_id }, null, null);

			// Return result
			return response.OK && (response.Data?.Successful ?? false);
		}

		/// <summary>
		/// Get currently available events from Pavlov TV.
		/// </summary>
		/// <returns>Match events.</returns>
		public static Result<HttpResponses.MatchEvents_> GetEvents() { return http_ctx.GetJson<HttpResponses.MatchEvents_>(Url_MatchEvents, null, null); }

		/// <summary>
		/// Get currently available status from Pavlov TV.
		/// </summary>
		/// <returns>Replay status.</returns>
		public static Result<HttpResponses.MatchStatus_> GetStatus() { return http_ctx.GetJson<HttpResponses.MatchStatus_>(Url_MatchStatus, null, null); }

		/// <summary>
		/// Get currently available locations from Pavlov TV.
		/// </summary>
		/// <returns>Locations.</returns>
		public static Result<HttpResponses.Locations_> GetLocations() { return http_ctx.GetJson<HttpResponses.Locations_>(Url_PlayersPos, null, null); }

		/// <summary>
		/// Get currently available killfeed from Pavlov TV.
		/// </summary>
		/// <returns>Killfeed.</returns>
		public static Result<HttpResponses.Killfeed_> GetKillfeed() { return http_ctx.GetJson<HttpResponses.Killfeed_>(Url_Killfeed, null, null); }

		/// <summary>
		/// Time related functions.
		/// </summary>
		public static class Time
		{
			/// <summary>
			/// Get current replay time.
			/// </summary>
			/// <returns>Match time. Null on error.</returns>
			public static double? Get()
			{
				Result<HttpResponses.MatchTime_> result;

				// Call api
				result = http_ctx.GetJson<HttpResponses.MatchTime_>(Url_MatchTime, null, null);

				// Return result
				if (!result.OK) return null;
				return result.Data?.MatchTime ?? null;
			}

			/// <summary>
			/// Set replay time.
			/// </summary>
			/// <param name="time">Time to be set in seconds.</param>
			/// <returns>True, on success.</returns>
			public static bool? Set(double time)
			{
				Result<HttpResponses.Default_> response;
				if (time < 0) return true;

				// Post command.
				response = http_ctx.PostJson<HttpResponses.Default_, HttpResponses.MatchTime_>(Url_MatchTime, new HttpResponses.MatchTime_() { MatchTime = time }, null, null);

				// Return result
				return response.OK && (response.Data?.Successful ?? false);
			}
		}

		/// <summary>
		/// Play/Pause state related functions.
		/// </summary>
		public static class Pause
		{
			/// <summary>
			/// Get current pause state.
			/// </summary>
			/// <returns>Pause state. Null on error.</returns>
			public static bool? Get()
			{
				Result<HttpResponses.Pause_> result;

				// Call api
				result = http_ctx.GetJson<HttpResponses.Pause_>(Url_Pause, null, null);

				// Return result
				if (!result.OK) return null;
				return result.Data?.Paused ?? null;
			}

			/// <summary>
			/// Set pause state.
			/// </summary>
			/// <param name="pause">Pause state.</param>
			/// <returns>True, on success.</returns>
			public static bool? Set(bool pause)
			{
				Result<HttpResponses.Default_> response;

				// Post command.
				response = http_ctx.PostJson<HttpResponses.Default_, HttpResponses.Pause_>(Url_Pause, new HttpResponses.Pause_() { Paused = pause }, null, null);

				// Return result
				return response.OK && (response.Data?.Successful ?? false);
			}
		}


		/// <summary>
		/// JSON template classes.
		/// </summary>
		public static class HttpResponses
		{
			/// <summary>
			/// Default response model.
			/// </summary>
			public class Default_
			{
				public bool Successful { get; set; } = false;
				public string? errorCode { get; set; } = null;
				public string? errorMessage { get; set; } = null;
			}

			/// <summary>
			/// Replay list queried from Pavlov TV.
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
			/// Command for Pavlov TV to load replay.
			/// </summary>
			public class LoadReplay_
			{
				public string? Id { get; set; } = null;
			}

			/// <summary>
			/// Match events queried from Pavlov TV.
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
					public Player_? Planter { get; set; } = null;
					public Player_? Diffuser { get; set; } = null;


					public class Player_
					{
						public string? Name { get; set; } = null;
						public string? Avatar { get; set; } = null;
						public bool? Tool { get; set; } = null;
					}
				}
			}

			/// <summary>
			/// Match status queried from Pavlov TV.
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
			/// Locations queried from Pavlov TV.
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
			/// Match events queried from Pavlov TV.
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
}
