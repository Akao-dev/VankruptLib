
using System.Diagnostics;

namespace Vankrupt.Pavlov;

public class Connection : IDisposable
{
	/// <summary>
	/// Minimum allowed delay between API requests per task/thread in milliseconds.
	/// </summary>
	public const long TASK_MIN_DELAY_MS = 5;



	/// <summary>
	/// Provide a function for engine to test if engine should continue running (external graceful shutdown).
	/// </summary>
	public Func<bool>? EnableGetter { get; set; } = null;

	/// <summary>
	/// Enable connection engine.
	/// </summary>
	public bool Enable
	{
		get { return _Enable; }
		set
		{
			// Set enable value
			_Enable = value;

			// Exit if enabled
			if (_Enable) return;

			// Send signal to all running threads (This wakes up threads from sleep)
			if (_T_Monitor.IsAlive) _T_Monitor.Interrupt();
			if (_TAT_Events.IsAlive) _TAT_Events.Interrupt();
			if (_TAT_Status.IsAlive) _TAT_Status.Interrupt();
			if (_TAT_Location.IsAlive) _TAT_Location.Interrupt();
			if (_TAT_Killfeed.IsAlive) _TAT_Killfeed.Interrupt();
			if (_TAT_Time.IsAlive) _TAT_Time.Interrupt();
			if (_TAT_Pause.IsAlive) _TAT_Pause.Interrupt();
		}
	}
	private bool _Enable = true;

	/// <summary>
	/// URL which API calls are made to.
	/// </summary>
	public string Url
	{
		get { lock (_Url) { return _Url; } }
		set
		{
			lock (_Url)
			{
				// Validate Url
				if (!Http.UrlRegex.Match($"{value}").Success) throw new InvalidDataException($"Invalid URL provided; '{value}'!");

				// Set parsed value
				_Url = value;
			}
		}
	}
	private string _Url = "http://localhost/";

	/// <summary>
	/// Delays, Intervals and Timeouts.
	/// </summary>
	public readonly DIT Delays;


	/// <summary>
	/// Last time Pavlov TV responded to API call.
	/// </summary>
	public DateTime? Conn_LastResponse
	{
		get { lock (_Conn_LastResponse_Lock) { return _Conn_LastResponse; } }
		set { lock (_Conn_LastResponse_Lock) { _Conn_LastResponse = value ?? DateTime.Now; } }
	}
	private DateTime? _Conn_LastResponse = null;
	private readonly object _Conn_LastResponse_Lock = new();


	/// <summary>
	/// Test if connection engine is allowed to run.
	/// </summary>
	public bool ShouldRun { get { return Enable && (EnableGetter?.Invoke() ?? true); } }


	/// <summary>
	/// Get Pavlov TV [API] connection status.
	/// </summary>
	public ConnectionState ConnectionStatus
	{
		get
		{
			// Disconnected by never being connected
			if (Conn_LastResponse == null) return ConnectionState.Disconnected;

			// Get time in milliseconds since last [responded] API call.
			double time = (DateTime.Now - Conn_LastResponse.Value).TotalMilliseconds;

			// Disconnected by Enable flag
			if (!ShouldRun) return ConnectionState.Disconnected;

			// Disconnected by Pavlov TV not running
			if (!Game.IsRunning) return ConnectionState.Disconnected;

			// Disconnected by timeout
			if (time >= Delays.ConnectionDisconnected_Timeout) return ConnectionState.Disconnected;

			// Unresponsive by timeout
			if (time >= Delays.ConnectionUnresponsive_Timeout) return ConnectionState.Unresponsive;

			// All OK -> connected
			return ConnectionState.Connected;
		}
	}
	private ConnectionState _ConnectionStatus_Last = ConnectionState.Disconnected;


	// ## Threads:

	/// <summary>
	/// Thread Monitor
	/// </summary>
	private Thread _T_Monitor;
	/// <summary>
	/// Task API Thread Events
	/// </summary>
	private Thread _TAT_Events;
	/// <summary>
	/// Task API Thread Status
	/// </summary>
	private Thread _TAT_Status;
	/// <summary>
	/// Task API Thread Location
	/// </summary>
	private Thread _TAT_Location;
	/// <summary>
	/// Task API Thread Killfeed
	/// </summary>
	private Thread _TAT_Killfeed;
	/// <summary>
	/// Task API Thread Time
	/// </summary>
	private Thread _TAT_Time;
	/// <summary>
	/// Task API Thread Pause
	/// </summary>
	private Thread _TAT_Pause;


	// ## Thread Functions:

	/// <summary>
	/// Function Monitor.
	/// </summary>
	private void _F_Monitor()
	{
		// Send start condition
		try { Buffer?.Update(ConnectionStatus); }
		catch (Exception ex)
		{
			Console.Error.WriteLine($"{DateTime.Now} Unexpected exception! {ex}");
		}

		// Run as long as required
		while (ShouldRun)
		{
			try
			{
				// Start threads if needed
				if (!_TAT_Events.IsAlive) _TAT_Events.Start();
				if (!_TAT_Status.IsAlive) _TAT_Status.Start();
				if (!_TAT_Location.IsAlive) _TAT_Location.Start();
				if (!_TAT_Killfeed.IsAlive) _TAT_Killfeed.Start();
				if (!_TAT_Time.IsAlive) _TAT_Time.Start();
				if (!_TAT_Pause.IsAlive) _TAT_Pause.Start();

				// Check connection state change & notify if needed
				ConnectionState currentState = ConnectionStatus;
				if (currentState != _ConnectionStatus_Last)
				{
					_ConnectionStatus_Last = currentState;
					Buffer?.Update(currentState);
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine($"{DateTime.Now} Unexpected exception! {ex}");
			}

			// Skip limiter if shutting down
			if (!ShouldRun) continue;

			// CPU limiter
			try { Thread.Sleep((int)Delays.Monitor_Interval); } catch (Exception) { }
		}

		// Send last disconnect signal
		try { Buffer?.Update(ConnectionState.Disconnected); }
		catch (Exception ex)
		{
			Console.Error.WriteLine($"{DateTime.Now} Unexpected exception! {ex}");
		}
	}

	/// <summary>
	/// Task API Function Events
	/// </summary>
	private void _TAF_Events()
	{
		Stopwatch timer = new();

		// Run as long as required
		while (ShouldRun)
		{
			// Clear & Restart timer
			timer.Restart();

			// # Perform task

			// Perform HTTP request
			var result = Game.API.GetEvents(_Http_Events, Url);

			// Check if server responded & mark time
			if (result.OK) Conn_LastResponse = DateTime.Now;

			// Send data to buffer
			Buffer?.Update(result);

			// Skip limiter if shutting down
			if (!ShouldRun) continue;

			// Calculate idle time
			long time = Delays.EventsApi_Interval - timer.ElapsedMilliseconds;
			time = Math.Abs(time <= TASK_MIN_DELAY_MS ? TASK_MIN_DELAY_MS : time);

			// Limit API request rate
			try { Thread.Sleep((int)time); } catch (Exception) { }
		}
	}

	/// <summary>
	/// Task API Function Status
	/// </summary>
	private void _TAF_Status()
	{
		Stopwatch timer = new();

		// Run as long as required
		while (ShouldRun)
		{
			// Clear & Restart timer
			timer.Restart();

			// # Perform task

			// Perform HTTP request
			var result = Game.API.GetStatus(_Http_Status, Url);

			// Check if server responded & mark time
			if (result.OK) Conn_LastResponse = DateTime.Now;

			// Send data to buffer
			Buffer?.Update(result);

			// Skip limiter if shutting down
			if (!ShouldRun) continue;

			// Calculate idle time
			long time = Delays.StatusApi_Interval - timer.ElapsedMilliseconds;
			time = Math.Abs(time <= TASK_MIN_DELAY_MS ? TASK_MIN_DELAY_MS : time);

			// Limit API request rate
			try { Thread.Sleep((int)time); } catch (Exception) { }
		}
	}

	/// <summary>
	/// Task API Function Location
	/// </summary>
	private void _TAF_Location()
	{
		Stopwatch timer = new();

		// Run as long as required
		while (ShouldRun)
		{
			// Clear & Restart timer
			timer.Restart();

			// # Perform task

			// Perform HTTP request
			var result = Game.API.GetLocations(_Http_Location, Url);

			// Check if server responded & mark time
			if (result.OK) Conn_LastResponse = DateTime.Now;

			// Send data to buffer
			Buffer?.Update(result);

			// Skip limiter if shutting down
			if (!ShouldRun) continue;

			// Calculate idle time
			long time = Delays.LocationApi_Interval - timer.ElapsedMilliseconds;
			time = Math.Abs(time <= TASK_MIN_DELAY_MS ? TASK_MIN_DELAY_MS : time);

			// Limit API request rate
			try { Thread.Sleep((int)time); } catch (Exception) { }
		}
	}

	/// <summary>
	/// Task API Function Killfeed
	/// </summary>
	private void _TAF_Killfeed()
	{
		Stopwatch timer = new();

		// Run as long as required
		while (ShouldRun)
		{
			// Clear & Restart timer
			timer.Restart();

			// # Perform task

			// Perform HTTP request
			var result = Game.API.GetKillfeed(_Http_Killfeed, Url);

			// Check if server responded & mark time
			if (result.OK) Conn_LastResponse = DateTime.Now;

			// Send data to buffer
			Buffer?.Update(result);

			// Skip limiter if shutting down
			if (!ShouldRun) continue;

			// Calculate idle time
			long time = Delays.KillfeedApi_Interval - timer.ElapsedMilliseconds;
			time = Math.Abs(time <= TASK_MIN_DELAY_MS ? TASK_MIN_DELAY_MS : time);

			// Limit API request rate
			try { Thread.Sleep((int)time); } catch (Exception) { }
		}
	}

	/// <summary>
	/// Task API Function Time
	/// </summary>
	private void _TAF_Time()
	{
		Stopwatch timer = new();

		// Run as long as required
		while (ShouldRun)
		{
			// Clear & Restart timer
			timer.Restart();

			// # Perform task

			// Perform HTTP request
			var result = Game.API.GetTime(_Http_Time, Url);

			// Check if server responded & mark time
			if (result.OK) Conn_LastResponse = DateTime.Now;

			// Send data to buffer
			Buffer?.Update(result);

			// Skip limiter if shutting down
			if (!ShouldRun) continue;

			// Calculate idle time
			long time = Delays.TimeApi_Interval - timer.ElapsedMilliseconds;
			time = Math.Abs(time <= TASK_MIN_DELAY_MS ? TASK_MIN_DELAY_MS : time);

			// Limit API request rate
			try { Thread.Sleep((int)time); } catch (Exception) { }
		}
	}

	/// <summary>
	/// Task API Function Pause
	/// </summary>
	private void _TAF_Pause()
	{
		Stopwatch timer = new();

		// Run as long as required
		while (ShouldRun)
		{
			// Clear & Restart timer
			timer.Restart();

			// # Perform task

			// Perform HTTP request
			var result = Game.API.GetPause(_Http_Pause, Url);

			// Check if server responded & mark time
			if (result.OK) Conn_LastResponse = DateTime.Now;

			// Send data to buffer
			Buffer?.Update(result);

			// Skip limiter if shutting down
			if (!ShouldRun) continue;

			// Calculate idle time
			long time = Delays.PauseApi_Interval - timer.ElapsedMilliseconds;
			time = Math.Abs(time <= TASK_MIN_DELAY_MS ? TASK_MIN_DELAY_MS : time);

			// Limit API request rate
			try { Thread.Sleep((int)time); } catch (Exception) { }
		}
	}



	// ## Http contexts:

	/// <summary>
	/// HTTP connection context.
	/// </summary>
	/// <remarks>
	/// WARNING: NOT MULTITHREAD SAFE! ACCESS ONLY FROM OWNER THREAD!
	/// </remarks>
	private readonly Http _Http_Events = new();
	/// <summary>
	/// HTTP connection context.
	/// </summary>
	/// <remarks>
	/// WARNING: NOT MULTITHREAD SAFE! ACCESS ONLY FROM OWNER THREAD!
	/// </remarks>
	private readonly Http _Http_Status = new();
	/// <summary>
	/// HTTP connection context.
	/// </summary>
	/// <remarks>
	/// WARNING: NOT MULTITHREAD SAFE! ACCESS ONLY FROM OWNER THREAD!
	/// </remarks>
	private readonly Http _Http_Location = new();
	/// <summary>
	/// HTTP connection context.
	/// </summary>
	/// <remarks>
	/// WARNING: NOT MULTITHREAD SAFE! ACCESS ONLY FROM OWNER THREAD!
	/// </remarks>
	private readonly Http _Http_Killfeed = new();
	/// <summary>
	/// HTTP connection context.
	/// </summary>
	/// <remarks>
	/// WARNING: NOT MULTITHREAD SAFE! ACCESS ONLY FROM OWNER THREAD!
	/// </remarks>
	private readonly Http _Http_Time = new();
	/// <summary>
	/// HTTP connection context.
	/// </summary>
	/// <remarks>
	/// WARNING: NOT MULTITHREAD SAFE! ACCESS ONLY FROM OWNER THREAD!
	/// </remarks>
	private readonly Http _Http_Pause = new();


	// ## The Buffer

	/// <summary>
	/// Buffer/Processor for data collected from Pavlov TV API is provided to.
	/// </summary>
	public IConnectionBuffer? Buffer
	{
		get { lock (_Buffer_Lock) { return _Buffer; } }
		set { lock (_Buffer_Lock) { _Buffer = value; } }
	}
	private IConnectionBuffer? _Buffer = null;
	private readonly object _Buffer_Lock = new();


	/// <summary>
	/// Creates connection engine object.
	/// </summary>
	/// <param name="delays_intervals_and_timeouts">Uses defaults if null.</param>
	public Connection(DIT? delays_intervals_and_timeouts = null)
	{
		// Set DIT
		Delays = delays_intervals_and_timeouts ?? new();

		// Set HTTP timeouts
		_Http_Events.Timeout = new TimeSpan(0, 0, 0, 0, (int)Delays.EventsApi_Timeout);
		_Http_Status.Timeout = new TimeSpan(0, 0, 0, 0, (int)Delays.StatusApi_Timeout);
		_Http_Location.Timeout = new TimeSpan(0, 0, 0, 0, (int)Delays.LocationApi_Timeout);
		_Http_Killfeed.Timeout = new TimeSpan(0, 0, 0, 0, (int)Delays.KillfeedApi_Timeout);
		_Http_Time.Timeout = new TimeSpan(0, 0, 0, 0, (int)Delays.TimeApi_Timeout);
		_Http_Pause.Timeout = new TimeSpan(0, 0, 0, 0, (int)Delays.PauseApi_Timeout);

		// Prepare threads
		_T_Monitor = new Thread(_F_Monitor);
		_TAT_Events = new Thread(_TAF_Events);
		_TAT_Status = new Thread(_TAF_Status);
		_TAT_Location = new Thread(_TAF_Location);
		_TAT_Killfeed = new Thread(_TAF_Killfeed);
		_TAT_Time = new Thread(_TAF_Time);
		_TAT_Pause = new Thread(_TAF_Pause);
	}

	/// <summary>
	/// Start connection engine. Close/Stop by <see cref="Enable"/>.
	/// </summary>
	public void Start()
	{
		if (_T_Monitor.IsAlive) return;
		Enable = true;
		_T_Monitor.Start();
	}

	public void Dispose()
	{
		// NOTE: Setting enable to false will interrupt all running threads
		Enable = false;

		// This is here to force other threads to run so they can prepare for shutdown.
		Thread.Sleep(1);

		// Join threads
		// TODO: Better handling. This might cause issues...
		try { _T_Monitor.Join(); } catch (Exception) { }
		try { _TAT_Events.Join(); } catch (Exception) { }
		try { _TAT_Status.Join(); } catch (Exception) { }
		try { _TAT_Location.Join(); } catch (Exception) { }
		try { _TAT_Killfeed.Join(); } catch (Exception) { }
		try { _TAT_Time.Join(); } catch (Exception) { }
		try { _TAT_Pause.Join(); } catch (Exception) { }

		GC.SuppressFinalize(this);
	}



	/// <summary>
	/// Pavlov TV Connection states.
	/// </summary>
	public enum ConnectionState
	{
		/// <summary>
		/// Disconnected from Pavlov TV.
		/// </summary>
		Disconnected,

		/// <summary>
		/// Pavlov TV stopped responding to API calls, but might still be running. Occurs when loading replays/content.
		/// </summary>
		Unresponsive,

		/// <summary>
		/// Pavlov TV APIs responds in timely manner.
		/// </summary>
		Connected
	}



	/// <summary>
	/// Delays, Intervals and Timeouts.
	/// </summary>
	/// <param name="replayApiTimeout">HTTP request timeout for replay API.</param>
	/// <param name="eventsApiTimeout">HTTP request timeout for match events API.</param>
	/// <param name="statusApiTimeout">HTTP request timeout for match status API.</param>
	/// <param name="locationApiTimeout">HTTP request timeout for location API.</param>
	/// <param name="killfeedApiTimeout">HTTP request timeout for killfeed API.</param>
	/// <param name="timeApiTimeout">HTTP request timeout for replay time API.</param>
	/// <param name="pauseApiTimeout">HTTP request timeout for replay pause API.</param>
	public class DIT(uint? replayApiTimeout = null, uint? eventsApiTimeout = null, uint? statusApiTimeout = null, uint? locationApiTimeout = null, uint? killfeedApiTimeout = null, uint? timeApiTimeout = null, uint? pauseApiTimeout = null)
	{
		/// <summary>
		/// Default difference between connection timeout states.
		/// </summary>
		public const uint Default_ConnectionStateSeparation = 1000;
		public const uint Default_HttpRequestTimeout = 1000;


		/// <summary>
		/// How often <see cref="Connection"/> subtasks and connection state are checked in milliseconds.
		/// </summary>
		public uint Monitor_Interval
		{
			get { lock (_Monitor_Interval_Lock) { return _Monitor_Interval; } }
			set { lock (_Monitor_Interval_Lock) { _Monitor_Interval = value; } }
		}
		private uint _Monitor_Interval = 100;
		private readonly object _Monitor_Interval_Lock = new();


		/// <summary>
		/// How long Pavlov TV API is not responding until it is considered unresponsivce in milliseconds.
		/// </summary>
		public uint ConnectionUnresponsive_Timeout
		{
			get { lock (_ConnectionUnresponsive_Timeout_Lock) { return _ConnectionUnresponsive_Timeout; } }
			set
			{
				lock (_ConnectionUnresponsive_Timeout_Lock)
				{
					lock (_ConnectionDisconnected_Timeout_Lock)
					{
						_ConnectionUnresponsive_Timeout = value;
						if (ConnectionDisconnected_Timeout <= _ConnectionUnresponsive_Timeout) _ConnectionDisconnected_Timeout = _ConnectionUnresponsive_Timeout + Default_ConnectionStateSeparation;
					}
				}
			}
		}
		private uint _ConnectionUnresponsive_Timeout = 5000;
		private readonly object _ConnectionUnresponsive_Timeout_Lock = new();


		/// <summary>
		/// How long Pavlov TV API is not responding until it is considered disconnected in milliseconds.
		/// </summary>
		/// <remarks>
		/// NOTE: Can not be less or equal than <see cref="ConnectionUnresponsive_Timeout"/>.
		/// </remarks>
		public uint ConnectionDisconnected_Timeout
		{
			get { lock (_ConnectionDisconnected_Timeout_Lock) { return _ConnectionDisconnected_Timeout; } }
			set
			{
				lock (_ConnectionUnresponsive_Timeout_Lock)
				{
					lock (_ConnectionDisconnected_Timeout_Lock)
					{
						_ConnectionDisconnected_Timeout = (value <= _ConnectionUnresponsive_Timeout) ? _ConnectionUnresponsive_Timeout + Default_ConnectionStateSeparation : value;
					}
				}
			}
		}
		private uint _ConnectionDisconnected_Timeout = 60000;
		private readonly object _ConnectionDisconnected_Timeout_Lock = new();


		/// <summary>
		/// Maximum request flight time in milliseconds.
		/// </summary>
		public readonly uint ReplayApi_Timeout = replayApiTimeout ?? Default_HttpRequestTimeout;


		/// <summary>
		/// API polling interval in milliseconds.
		/// </summary>
		/// <remarks>
		/// WARNING: Too frequent api calls will stager Pavlov TV, causing uneven replay and lag spikes.
		/// NOTE: Match events API is not time critical. All events are given at once if replay is complete.
		/// </remarks>
		public uint EventsApi_Interval
		{
			get { lock (_EventsApi_Interval_Lock) { return _EventsApi_Interval; } }
			set { lock (_EventsApi_Interval_Lock) { _EventsApi_Interval = value; } }
		}
		private uint _EventsApi_Interval = 5000;
		private readonly object _EventsApi_Interval_Lock = new();
		/// <summary>
		/// Maximum request flight time in milliseconds.
		/// </summary>
		public readonly uint EventsApi_Timeout = eventsApiTimeout ?? Default_HttpRequestTimeout;


		/// <summary>
		/// API polling interval in milliseconds.
		/// </summary>
		/// <remarks>
		/// WARNING: Too frequent api calls will stager Pavlov TV, causing uneven replay and lag spikes.
		/// NOTE: Since status is the only API containing information of Pavlov TV state, this interval determines latch buffer delay.
		/// </remarks>
		public uint StatusApi_Interval
		{
			get { lock (_StatusApi_Interval_Lock) { return _StatusApi_Interval; } }
			set { lock (_StatusApi_Interval_Lock) { _StatusApi_Interval = value; } }
		}
		private uint _StatusApi_Interval = 1000;
		private readonly object _StatusApi_Interval_Lock = new();
		/// <summary>
		/// Maximum request flight time in milliseconds.
		/// </summary>
		public readonly uint StatusApi_Timeout = statusApiTimeout ?? Default_HttpRequestTimeout;


		/// <summary>
		/// API polling interval in milliseconds.
		/// </summary>
		/// <remarks>
		/// WARNING: Too frequent api calls will stager Pavlov TV, causing uneven replay and lag spikes.
		/// </remarks>
		public uint LocationApi_Interval
		{
			get { lock (_LocationApi_Interval_Lock) { return _LocationApi_Interval; } }
			set { lock (_LocationApi_Interval_Lock) { _LocationApi_Interval = value; } }
		}
		private uint _LocationApi_Interval = 500;
		private readonly object _LocationApi_Interval_Lock = new();
		/// <summary>
		/// Maximum request flight time in milliseconds.
		/// </summary>
		public readonly uint LocationApi_Timeout = locationApiTimeout ?? Default_HttpRequestTimeout;


		/// <summary>
		/// API polling interval in milliseconds.
		/// </summary>
		/// <remarks>
		/// WARNING: Too frequent api calls will stager Pavlov TV, causing uneven replay and lag spikes.
		/// NOTE: Killfeed lifetime is 5 seconds, using quater of it prevents missing killfeed due to stutter.
		/// </remarks>
		public uint KillfeedApi_Interval
		{
			get { lock (_KillfeedApi_Interval_Lock) { return _KillfeedApi_Interval; } }
			set { lock (_KillfeedApi_Interval_Lock) { _KillfeedApi_Interval = value; } }
		}
		private uint _KillfeedApi_Interval = 5000 / 4;
		private readonly object _KillfeedApi_Interval_Lock = new();
		/// <summary>
		/// Maximum request flight time in milliseconds.
		/// </summary>
		public readonly uint KillfeedApi_Timeout = killfeedApiTimeout ?? Default_HttpRequestTimeout;


		/// <summary>
		/// API polling interval in milliseconds.
		/// </summary>
		/// <remarks>
		/// WARNING: Too frequent api calls will stager Pavlov TV, causing uneven replay and lag spikes.
		/// </remarks>
		public uint TimeApi_Interval
		{
			get { lock (_TimeApi_Interval_Lock) { return _TimeApi_Interval; } }
			set { lock (_TimeApi_Interval_Lock) { _TimeApi_Interval = value; } }
		}
		private uint _TimeApi_Interval = 125;
		private readonly object _TimeApi_Interval_Lock = new();
		/// <summary>
		/// Maximum request flight time in milliseconds.
		/// </summary>
		public readonly uint TimeApi_Timeout = timeApiTimeout ?? Default_HttpRequestTimeout;


		/// <summary>
		/// API polling interval in milliseconds.
		/// </summary>
		/// <remarks>
		/// WARNING: Too frequent api calls will stager Pavlov TV, causing uneven replay and lag spikes.
		/// </remarks>
		public uint PauseApi_Interval
		{
			get { lock (_PauseApi_Interval_Lock) { return _PauseApi_Interval; } }
			set { lock (_PauseApi_Interval_Lock) { _PauseApi_Interval = value; } }
		}
		private uint _PauseApi_Interval = 125;
		private readonly object _PauseApi_Interval_Lock = new();
		/// <summary>
		/// Maximum request flight time in milliseconds.
		/// </summary>
		public readonly uint PauseApi_Timeout = pauseApiTimeout ?? Default_HttpRequestTimeout;
	}
}
