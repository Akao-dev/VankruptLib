namespace Vankrupt.Pavlov;

public interface IConnectionBuffer
{
	/// <summary>
	/// Connection current state update.
	/// </summary>
	/// <param name="state">New connection state.</param>
	protected internal void Update(Connection.ConnectionState state);

	/// <summary>
	/// Match events update.
	/// </summary>
	/// <param name="result">API request results.</param>
	protected internal void Update(Http.Result<Game.HttpResponses.MatchEvents_> result) { }

	/// <summary>
	/// Match current status update.
	/// </summary>
	/// <param name="result">API request results.</param>
	protected internal void Update(Http.Result<Game.HttpResponses.MatchStatus_> result) { }

	/// <summary>
	/// Players, bombs & cameras current position update.
	/// </summary>
	/// <param name="result">API request results.</param>
	protected internal void Update(Http.Result<Game.HttpResponses.Locations_> result) { }

	/// <summary>
	/// Killfeed notifications.
	/// </summary>
	/// <param name="result">API request results.</param>
	protected internal void Update(Http.Result<Game.HttpResponses.Killfeed_> result) { }

	/// <summary>
	/// Replays current time update.
	/// </summary>
	/// <param name="result">API request results.</param>
	protected internal void Update(Http.Result<Game.HttpResponses.MatchTime_> result) { }

	/// <summary>
	/// Replay current pause state update.
	/// </summary>
	/// <param name="result">API request results.</param>
	protected internal void Update(Http.Result<Game.HttpResponses.Pause_> result) { }
}
