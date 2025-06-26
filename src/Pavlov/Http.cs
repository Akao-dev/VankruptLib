using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;

namespace Vankrupt.Pavlov;

public class Http : IDisposable
{
	/// <summary>
	/// Make Uri object from url string.
	/// </summary>
	/// <param name="url">URL string.</param>
	/// <returns>Uri if URL is valid.</returns>
	public static Uri? MakeUri(string url)
	{
		try
		{
			return new Uri(url);
		}
		catch (Exception)
		{
			return null;
		}
	}

	/// <summary>
	/// Combine base url with endpoint path.
	/// </summary>
	/// <param name="base_url">Base url.</param>
	/// <param name="path">Path to endpoint.</param>
	/// <returns>Combined url.</returns>
	public static string MakeUrl(string base_url, string path)
	{
		// Remove trailing separators
		while (base_url.EndsWith('/')) base_url = base_url[..^1];

		// Remove separators from beginning
		while (path.StartsWith('/')) path = path[1..];

		// Return URL
		return base_url + "/" + path;
	}

	/// <summary>
	/// Generate / Build url with parameters.
	/// </summary>
	/// <param name="base_url">Base url</param>
	/// <param name="args">Parameters</param>
	/// <returns>Generated url string.</returns>
	public static string ParamUrl(string base_url, List<KeyValuePair<string, string?>>? args)
	{
		string param_args = "";

		// Prepare url arguments
		if (args is not null)
		{
			for (int i = 0; i < args.Count; i++)
			{
				// Append ampersand separator char if not first on list.
				if (i != 0) param_args += "&";

				// Append new key or key-value pair
				if (args[i].Value is null) param_args += $"{args[i].Key}";
				else param_args += $"{args[i].Key}={args[i].Value}";
			}

		}

		// Make URL
		UriBuilder uriBuilder = new(base_url);
		if (!string.IsNullOrWhiteSpace(param_args)) uriBuilder.Query = param_args;

		// Return generated url
		return uriBuilder.ToString();
	}


	private CookieContainer _cookieContainer = new();
	private readonly List<X509Certificate> _certificates = [];
	private HttpClient _httpClient;
	//private ClientCertificateOption clientCertificateOption = ClientCertificateOption.Automatic;
	//private Func<HttpRequestMessage, X509Certificate2?, X509Chain?, System.Net.Security.SslPolicyErrors, bool>? customClientCertHandler = null;
	private readonly object _lock = new();
	public string? UserAgent { get; set; } = null;


	private HttpClientHandler NewHandler
	{
		get
		{
			// Make handler, use cookies etc
			HttpClientHandler handler = new()
			{
				CookieContainer = _cookieContainer,
				UseCookies = true
			};

			// Add client certs to handler
			//foreach (var cert in _certificates) handler.ClientCertificates.Add(cert);

			// Set options for handling custom certs
			//handler.ClientCertificateOptions = clientCertificateOption;
			//handler.ServerCertificateCustomValidationCallback = customClientCertHandler;

			// Return handler
			return handler;
		}
	}
	public Cookie[] Cookies
	{
		get
		{
			lock (_lock)
			{
				return _cookieContainer.GetAllCookies().ToArray();
			}
		}
	}


	public Http(ClientCertificateOption clientCertificateOption = ClientCertificateOption.Automatic, Func<HttpRequestMessage, X509Certificate2?, X509Chain?, System.Net.Security.SslPolicyErrors, bool>? customClientCertHandler = null, params string[] certificates)
	{
		// Load provided certificates
		LoadCertificates(certificates);

		// Build Http client
		lock (_lock)
		{
			_httpClient = new(NewHandler);
		}
	}


	public void LoadCertificates(params string[] path_to_files)
	{
		foreach (string path_to_file in path_to_files)
		{
			X509Certificate? crt = null;
			if (string.IsNullOrWhiteSpace(path_to_file)) return;
			if (path_to_file.EndsWith(".crt")) crt = X509Certificate.CreateFromCertFile(path_to_file);
			if (path_to_file.EndsWith(".pem")) crt = X509Certificate2.CreateFromPemFile(path_to_file);
			if (path_to_file.EndsWith(".pfx")) crt = new(path_to_file);

			if (crt == null) throw new Exception("Unsupported file format!");

			_certificates.Add(crt);
		}
	}

	public Cookie? GetCookie(string key)
	{
		lock (_lock)
		{
			// Search cookie with key/name
			Cookie[] cookies = _cookieContainer.GetAllCookies().Where(x => x.Name == key).ToArray();

			// Return null if none was found
			if (cookies.Length < 1) return null;

			// Return first result
			return cookies[0];
		}
	}

	public void SetCookie(string domain, string key, string? value = null, string? path = "/", bool secure = true, bool httpOnly = true, DateTime? expire = null)
	{
		Uri uri = MakeUri(domain) ?? throw new Exception("Domain Must be valid!");

		Cookie cookie = new(key, value, path, uri.Host)
		{
			Secure = secure,
			HttpOnly = httpOnly
		};

		if (expire != null) cookie.Expires = expire.Value;
		_cookieContainer.Add(cookie);
	}

	public void ClearCookies()
	{
		lock (_lock)
		{
			_cookieContainer = new();
			_httpClient = new(NewHandler);
		}
	}

	public void Update()
	{
		lock (_lock)
		{
			_httpClient = new(NewHandler);
		}
	}

	public void Dispose()
	{
		lock (_lock)
		{
			_httpClient?.Dispose();
		}
		GC.SuppressFinalize(this);
	}


	public Result<TO> GetJson<TO>(string path, List<KeyValuePair<string, string?>>? args = null, Action<HttpRequestHeaders>? f_cfg = null)
	{
		Result<TO> result = new();
		string url = ParamUrl(path, args);

		lock (_lock)
		{
			try
			{
				// Variables:
				Task<HttpResponseMessage> task_Post;
				Task<string> task_Read;
				HttpResponseMessage response;

				// Set UserAgent if provided
				if (UserAgent != null)
				{
					_httpClient.DefaultRequestHeaders.UserAgent.Clear();
					_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
				}

				// Call header configuration function
				f_cfg?.Invoke(_httpClient.DefaultRequestHeaders);

				// Perform HTTP call
				task_Post = _httpClient.GetAsync(url);
				task_Post.Wait();
				response = task_Post.Result;

				// Get status code
				result.Code = response.StatusCode;

				// Read response buffer
				task_Read = response.Content.ReadAsStringAsync();
				task_Read.Wait();
				result.DataRaw = task_Read.Result;

				// Parse json to correct data format
				result.OK = response.IsSuccessStatusCode;
				if (result.OK)
				{
					if (string.IsNullOrWhiteSpace(result.DataRaw))
						result.Data = default;
					else
						result.Data = JsonSerializer.Deserialize<TO>(result.DataRaw);
				}
				else
				{
					try
					{
						result.ApiDefaultResponse = JsonSerializer.Deserialize<DefaultResponse>(result.DataRaw);
						if (result.ApiDefaultResponse != null)
						{
							result.Info = result.ApiDefaultResponse.info;
							result.Error = new Exception(result.ApiDefaultResponse.data);
						}
					}
					catch (Exception ex)
					{
						result.OK = false;
						result.Error = ex;
					}
				}
			}
			catch (Exception ex)
			{
				result.OK = false;
				result.Data = default;
				result.Error = ex;
			}
		}

		// Return result
		return result;
	}

	public Result<TO> PostJson<TO, TI>(string path, TI? payload, List<KeyValuePair<string, string?>>? args = null, Action<HttpRequestHeaders>? f_cfg = null)
	{
		Result<TO> result = new();
		string url = ParamUrl(path, args);

		lock (_lock)
		{
			try
			{
				// Variables:
				HttpContent content;
				Task<HttpResponseMessage> task_Post;
				Task<string> task_Read;
				HttpResponseMessage response;

				// Set UserAgent if provided
				if (UserAgent != null)
				{
					_httpClient.DefaultRequestHeaders.UserAgent.Clear();
					_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
				}

				// Call header configuration function
				f_cfg?.Invoke(_httpClient.DefaultRequestHeaders);

				// Generate payload (Json obviously)
				content = new StringContent(JsonSerializer.Serialize(payload));
				content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

				// Perform HTTP call
				task_Post = _httpClient.PostAsync(url, content);
				task_Post.Wait();
				response = task_Post.Result;

				// Get status code
				result.Code = response.StatusCode;

				// Read response buffer
				task_Read = response.Content.ReadAsStringAsync();
				task_Read.Wait();
				result.DataRaw = task_Read.Result;

				// Parse json to correct data format
				result.OK = response.IsSuccessStatusCode;
				if (result.OK)
				{
					if (string.IsNullOrWhiteSpace(result.DataRaw))
						result.Data = default;
					else
						result.Data = JsonSerializer.Deserialize<TO>(result.DataRaw);
				}
				else
				{
					try
					{
						result.ApiDefaultResponse = JsonSerializer.Deserialize<DefaultResponse>(result.DataRaw);
						if (result.ApiDefaultResponse != null)
						{
							result.Info = result.ApiDefaultResponse.info;
							result.Error = new Exception(result.ApiDefaultResponse.data);
						}
					}
					catch (Exception ex)
					{
						result.OK = false;
						result.Error = ex;
					}
				}
			}
			catch (Exception ex)
			{
				result.OK = false;
				result.Data = default;
				result.Error = ex;
			}
		}

		// Return result
		return result;
	}

	public Result<TO?> PutJson<TO, TI>(string path, TI? payload, List<KeyValuePair<string, string?>>? args = null, Action<HttpRequestHeaders>? f_cfg = null)
	{
		Result<TO?> result = new();
		string url = ParamUrl(path, args);

		lock (_lock)
		{
			try
			{
				// Variables:
				HttpContent content;
				Task<HttpResponseMessage> task_Post;
				Task<string> task_Read;
				HttpResponseMessage response;

				// Set UserAgent if provided
				if (UserAgent != null)
				{
					_httpClient.DefaultRequestHeaders.UserAgent.Clear();
					_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
				}

				// Call header configuration function
				f_cfg?.Invoke(_httpClient.DefaultRequestHeaders);

				// Generate payload (Json obviously)
				content = new StringContent(JsonSerializer.Serialize(payload));
				content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json);

				// Perform HTTP call
				task_Post = _httpClient.PutAsync(url, content);
				task_Post.Wait();
				response = task_Post.Result;

				// Get status code
				result.Code = response.StatusCode;

				// Read response buffer
				task_Read = response.Content.ReadAsStringAsync();
				task_Read.Wait();
				result.DataRaw = task_Read.Result;

				// Parse json to correct data format
				result.OK = response.IsSuccessStatusCode;

				if (result.OK)
				{
					if (string.IsNullOrWhiteSpace(result.DataRaw))
						result.Data = default;
					else
						result.Data = JsonSerializer.Deserialize<TO>(result.DataRaw);
				}
				else
				{
					try
					{
						if (string.IsNullOrWhiteSpace(result.DataRaw))
						{
							result.Data = default;
						}
						else
						{
							result.ApiDefaultResponse = JsonSerializer.Deserialize<DefaultResponse>(result.DataRaw);

							if (result.ApiDefaultResponse != null)
							{
								result.Info = result.ApiDefaultResponse.info;
								result.Error = new Exception(result.ApiDefaultResponse.data);
							}
						}
					}
					catch (Exception ex)
					{
						result.OK = false;
						result.Error = ex;
					}
				}
			}
			catch (Exception ex)
			{
				result.OK = false;
				result.Data = default;
				result.Error = ex;
			}
		}

		// Return result
		return result;
	}

	public Result<TO?> DeleteJson<TO>(string path, List<KeyValuePair<string, string?>>? args = null, Action<HttpRequestHeaders>? f_cfg = null)
	{
		Result<TO?> result = new();
		string url = ParamUrl(path, args);

		lock (_lock)
		{
			try
			{
				// Variables:
				Task<HttpResponseMessage> task_Post;
				Task<string> task_Read;
				HttpResponseMessage response;

				// Set UserAgent if provided
				if (UserAgent != null)
				{
					_httpClient.DefaultRequestHeaders.UserAgent.Clear();
					_httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
				}

				// Call header configuration function
				f_cfg?.Invoke(_httpClient.DefaultRequestHeaders);

				// Perform HTTP call
				task_Post = _httpClient.DeleteAsync(url);
				task_Post.Wait();
				response = task_Post.Result;

				// Get status code
				result.Code = response.StatusCode;

				// Read response buffer
				task_Read = response.Content.ReadAsStringAsync();
				task_Read.Wait();
				result.DataRaw = task_Read.Result;

				// Parse json to correct data format
				result.OK = response.IsSuccessStatusCode;

				if (result.OK)
				{
					if (string.IsNullOrWhiteSpace(result.DataRaw))
						result.Data = default;
					else
						result.Data = JsonSerializer.Deserialize<TO>(result.DataRaw);
				}
				else
				{
					try
					{
						if (string.IsNullOrWhiteSpace(result.DataRaw))
						{
							result.Data = default;
						}
						else
						{
							result.ApiDefaultResponse = JsonSerializer.Deserialize<DefaultResponse>(result.DataRaw);

							if (result.ApiDefaultResponse != null)
							{
								result.Info = result.ApiDefaultResponse.info;
								result.Error = new Exception(result.ApiDefaultResponse.data);
							}
						}
					}
					catch (Exception ex)
					{
						result.OK = false;
						result.Error = ex;
					}
				}
			}
			catch (Exception ex)
			{
				result.OK = false;
				result.Data = default;
				result.Error = ex;
			}
		}

		// Return result
		return result;
	}


	public class Result<T>
	{
		public bool OK { get; protected internal set; } = false;
		public HttpStatusCode? Code { get; protected internal set; } = null;
		public string? Info { get; protected internal set; } = null;
		public Exception? Error { get; protected internal set; } = null;
		public T? Data { get; protected internal set; } = default;
		public string? DataRaw { get; protected internal set; } = null;
		public DefaultResponse? ApiDefaultResponse { get; protected internal set; }

		public long TimeHttp { get; protected internal set; } = 0;
		public long TimeProcessing { get; protected internal set; } = 0;
		public long TimeTotal { get; protected internal set; } = 0;

		public override string ToString()
		{
			return $"OK={OK} Code={(int?)Code}-{Code} Info='{Info}' Error={Error} Data={Data?.GetType()?.FullName}";
		}
	}
	public class DefaultResponse
	{
		public string? info { get; set; } = null;
		public string? data { get; set; } = null;
	}
}