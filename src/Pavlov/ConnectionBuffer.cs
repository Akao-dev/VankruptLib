
namespace Vankrupt.Pavlov;

public class TestConnectionBuffer : IConnectionBuffer
{
	private readonly object _Print_Lock = new();

	void IConnectionBuffer.Update(Connection.ConnectionState state)
	{
		lock (_Print_Lock)
		{
			Console.WriteLine($"{DateTime.Now} ConnectionState: {state}.");
		}
	}
	void IConnectionBuffer.Update(Http.Result<Game.HttpResponses.MatchEvents_> result)
	{
		lock (_Print_Lock)
		{
			Console.WriteLine($"{DateTime.Now} Events: {(result.OK ? "OK" : "ERROR")}");
			if (!result.OK || result.Data == null) return;

			Console.WriteLine($" 🗓️  MatchTime={result.Data.MatchTime,-5:F3}s");
			Console.WriteLine($" 🗓️  TotalTime={result.Data.TotalTime,-5:F3}s");

			if (result.Data.Events != null)
			{
				foreach (var m_event in result.Data.Events)
				{
					Console.WriteLine($" 🗓️  [{m_event.Time,-5:F3}s] Type={m_event.Name} Add-Data={m_event.AdditionalData} {m_event.Planter} {m_event.Diffuser}");
				}
			}
		}
	}
	void IConnectionBuffer.Update(Http.Result<Game.HttpResponses.MatchStatus_> result)
	{
		lock (_Print_Lock)
		{
			Console.WriteLine($"{DateTime.Now} Response: Status. ReplayID={result.Data?.ReplayId}.");
			if (!result.OK || result.Data == null) return;

			Console.WriteLine($" ℹ️  MatchActive={result.Data.MatchActive}");
			Console.WriteLine($" ℹ️  MapName={result.Data.MapName}");
			Console.WriteLine($" ℹ️  RoundState={result.Data.RoundState}");
			Console.WriteLine($" ℹ️  RoundTime={result.Data.RoundTime}");
			Console.WriteLine($" ℹ️  Teams={result.Data.Teams}");
			Console.WriteLine($" ℹ️  RoundsLeft={result.Data.RoundsLeft}");
			Console.WriteLine($" ℹ️  Team0Score={result.Data.Team0Score}");
			Console.WriteLine($" ℹ️  Team1Score={result.Data.Team1Score}");
			Console.WriteLine($" ℹ️  AttackingTeamId={result.Data.AttackingTeamId}");
			Console.WriteLine($" ℹ️  Team0Cash={result.Data.Team0Cash}");
			Console.WriteLine($" ℹ️  Team1Cash={result.Data.Team1Cash}");

			if (result.Data.Team0 != null)
			{
				result.Data.Team0.Sort((a, b) => { return string.Compare(a.Name, b.Name); });
				foreach (var pl in result.Data.Team0)
				{
					string secondaries = "";

					if (pl.SecondaryWeapons != null)
					{
						for (int i = 0; i < pl.SecondaryWeapons.Count; i++)
						{
							if (i > 0) secondaries += ";";
							secondaries += $"{Tools.ParseItemUrl(pl.SecondaryWeapons[i])}";
						}
					}

					Console.WriteLine($" 👤 \tTeam=0 {(pl.Dead ?? true ? "💀" : "❤️ ")}{pl.Health,-3} {(pl.Helmet ?? false ? "🪖" : "🙄")}{pl.Armour,-3} {(pl.Bot ?? true ? "🤖" : "👤")}  Platform={pl.Platform} Id=\"{pl.Id}\" Name=\"{pl.Name}\"");
					Console.WriteLine($"\t    K={pl.Kills,-2} D={pl.Deaths,-2} ${pl.Cash,-5} Score={pl.Score,-4} PW={Tools.ParseItemUrl(pl.PrimaryWeapon)} SW={secondaries}");
					Console.WriteLine($"\t    {pl.Avatar}");
				}
			}

			if (result.Data.Team1 != null)
			{
				result.Data.Team1.Sort((a, b) => { return string.Compare(a.Name, b.Name); });
				foreach (var pl in result.Data.Team1)
				{
					string secondaries = "";

					if (pl.SecondaryWeapons != null)
					{
						for (int i = 0; i < pl.SecondaryWeapons.Count; i++)
						{
							if (i > 0) secondaries += ";";
							secondaries += $"{Tools.ParseItemUrl(pl.SecondaryWeapons[i])}";
						}
					}

					Console.WriteLine($" 👤 \tTeam=1 {(pl.Dead ?? true ? "💀" : "❤️ ")}{pl.Health,-3} {(pl.Helmet ?? false ? "🪖" : "🙄")}{pl.Armour,-3} {(pl.Bot ?? true ? "🤖" : "👤")}  Platform={pl.Platform} Id=\"{pl.Id}\" Name=\"{pl.Name}\"");
					Console.WriteLine($"\t    K={pl.Kills,-2} D={pl.Deaths,-2} ${pl.Cash,-5} Score={pl.Score,-4} PW={Tools.ParseItemUrl(pl.PrimaryWeapon)} SW={secondaries}");
					Console.WriteLine($"\t    {pl.Avatar}");
				}
			}
		}
	}
	void IConnectionBuffer.Update(Http.Result<Game.HttpResponses.Locations_> result)
	{
		lock (_Print_Lock)
		{
			Console.WriteLine($"{DateTime.Now} Locations:");
			if (!result.OK || result.Data == null) return;

			// Players:
			if (result.Data.Players != null)
			{
				result.Data.Players.Sort((a, b) =>
				{
					if ((a.TeamId ?? 0) != (b.TeamId ?? 0)) return (a.TeamId ?? 0) - (b.TeamId ?? 0);
					return string.Compare(a.Name, b.Name);
				});

				foreach (var pl_pos in result.Data.Players)
				{
					Console.WriteLine($" 🌐 X={pl_pos.X / 100,-6:F2} Y={pl_pos.Y / 100,-6:F2} E={pl_pos.Z / 100,-6:F2} H={pl_pos.Yaw,-5:F0}  {pl_pos.TeamId}:{pl_pos.Id}:{pl_pos.Name}");
				}
			}

			Console.WriteLine($" 🌐 X={result.Data.CameraX / 100,-6:F2} Y={result.Data.CameraY / 100,-6:F2} E={result.Data.CameraZ / 100,-5:F2} H={result.Data.CameraYaw,-3:F0} Camera");
			Console.WriteLine($" 🌐 X={result.Data.BombX / 100,-6:F2} Y={result.Data.BombY / 100,-6:F2} E={result.Data.BombZ / 100,-5:F2} BombState={result.Data.BombState}");
		}
	}
	void IConnectionBuffer.Update(Http.Result<Game.HttpResponses.Killfeed_> result)
	{
		lock (_Print_Lock)
		{
			Console.WriteLine($"{DateTime.Now} Killfeed:");
			if (!result.OK || result.Data?.Killfeed == null) return;
			result.Data.Killfeed.Sort((a, b) =>
			{
				var a_val = a.MatchTime ?? 0;
				var b_val = b.MatchTime ?? 0;
				if (b_val < a_val) return -1;
				if (b_val == a_val) return 0;
				return 1;
			});
			foreach (var kf in result.Data.Killfeed)
			{
				Console.WriteLine($" ☠️︎  [{kf.MatchTime:F3}] {kf.KillerTeam}:{kf.KillerId}:{kf.Killer} -> {kf.KilledBy}{(kf.Headshot ?? false ? " ⊹💀" : "")} -> {kf.KilledTeam}:{kf.KilledId}:{kf.Killed}");
			}
		}
	}
	void IConnectionBuffer.Update(Http.Result<Game.HttpResponses.MatchTime_> result)
	{
		lock (_Print_Lock)
		{
			string time = "-";
			if (result.OK && result.Data?.MatchTime != null) time = $"{result.Data?.MatchTime:F3}";
			Console.WriteLine($"{DateTime.Now} Time:  [{time}s].");
		}
	}
	void IConnectionBuffer.Update(Http.Result<Game.HttpResponses.Pause_> result)
	{
		lock (_Print_Lock)
		{
			string pause_state = (result.Data?.Paused ?? false) ? "Paused" : "Playing";
			if (result.Data?.Paused == null) pause_state = "-";
			Console.WriteLine($"{DateTime.Now} Pause: {pause_state}.");
		}
	}
}