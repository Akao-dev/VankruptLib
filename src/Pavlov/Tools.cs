using System.Text.RegularExpressions;

namespace Vankrupt.Pavlov;

/// <summary>
/// Tools related to Pavlov.
/// </summary>
public static class Tools
{
	/// <summary>
	/// Matches everything that is not hexadecimal character.
	/// </summary>
	public static readonly Regex Regex_NotHexadecimalCharacter = new(@"[^0-9A-Fa-f]");
	/// <summary>
	/// Matches everything that is not digit (number) character.
	/// </summary>
	public static readonly Regex Regex_NotDigitCharacter = new(@"[^0-9]");
	/// <summary>
	/// Matches if string is PlaystationNetwork ID compliant.
	/// - Must start with a letter character.
	/// - Minimum of 3 characters.
	/// - Maximum of 16 characters.
	/// - Can contain letters (uppercase and lowercase).
	/// - Can contain numbers.
	/// - Can contain hyphen/dashline "-".
	/// - Can contain underscore "_".
	/// </summary>
	public static readonly Regex Regex_PSN = new(@"^[a-zA-Z][a-zA-Z0-9\-_]{2,15}$");

	/// <summary>
	/// Test if provided id is valid Vankrupt user id.
	/// </summary>
	/// <param name="id">User id.</param>
	/// <returns>True if user id is valid.</returns>
	public static bool IsValid_VankruptId(string? id)
	{
		if (string.IsNullOrWhiteSpace(id)) return false;
		if (id.Length != 32) return false;
		if (Regex_NotHexadecimalCharacter.Match(id).Success) return false;
		if (!id.StartsWith("0002")) return false;
		return true;
	}

	/// <summary>
	/// Test if platform id is valid.
	/// </summary>
	/// <param name="id">Id.</param>
	/// <param name="type">Type of id.</param>
	/// <returns>True if platform id is valid.</returns>
	public static bool IsValid_PlatformId(string? id, int? type)
	{
		if (string.IsNullOrWhiteSpace(id)) return false;
		if (type == null) return false;
		if ((type < 0 || type > 2) && type != 99) return false;

		switch (type)
		{
			case 0:// Steam
				if (id.Length != 17) return false;
				if (Regex_NotDigitCharacter.Match(id).Success) return false;
				break;

			case 1:// PSN
				if (id.Length < 3) return false;
				if (id.Length > 16) return false;
				if (!Regex_PSN.Match(id).Success) return false;
				break;

			case 2:// Meta
				if (id.Length < 2) return false;
				if (id.Length < 20) return false;
				break;

			case 99:// Bots
				break;

			default: return false;
		}

		return true;
	}

	/// <summary>
	/// Parses Vankrupt id from user avatar.
	/// </summary>
	/// <param name="url">Avatar url of vankrupt user.</param>
	/// <returns>Vankrupt user id.</returns>
	/// <exception cref="InvalidDataException">When url or user id is invalid.</exception>
	public static string? ParseAvatarUrl(string? url)
	{
		string buffer;

		// Input validation
		if (url is null) return null;

		// Find last index of '/'
		int idx = url.LastIndexOf('/') + 1;

		// Fail check
		if (idx < 0) return null;

		// Cut the excess fat (substring after last slash)
		buffer = url[idx..];

		// Find first index of '.'
		idx = buffer.IndexOf('.');

		// Fail check
		if (idx < 0) return null;

		// Remove file extension (substring that ends before file extension dot)
		buffer = buffer[..idx];

		// Check id is as expected
		if (!IsValid_VankruptId(buffer)) throw new InvalidDataException($"Invalid player id '{buffer}'! ");

		// Return result
		return buffer;
	}
}
