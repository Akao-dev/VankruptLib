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
	public static readonly Regex Regex_NonHexadecimalCharacter = new(@"[^0-9A-Fa-f]");
	/// <summary>
	/// Matches everything that is not digit (number) character.
	/// </summary>
	public static readonly Regex Regex_DigitCharacter = new(@"[^0-9]");

	/// <summary>
	/// Test if provided id is valid Vankrupt user id.
	/// </summary>
	/// <param name="id">User id.</param>
	/// <returns>True if user id is valid.</returns>
	public static bool IsValid_VankruptId(string? id)
	{
		if (string.IsNullOrWhiteSpace(id)) return false;
		if (id.Length != 32) return false;
		if (Regex_NonHexadecimalCharacter.Match(id).Success) return false;
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
				if (id.Length < 10) return false;
				if (Regex_NonHexadecimalCharacter.Match(id).Success) return false;
				break;
			case 1:// PSN
				if (id.Length < 4) return false;
				break;
			case 2:// Meta
				if (id.Length < 4) return false;
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
