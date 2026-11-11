namespace Xeno.Client.Tools
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using Xeno.Client.Services;

    /// <summary>
    /// The token MCP clients send to the client's HTTP MCP server (Authorization: Bearer). Kept in ~/.xeno/mcp-token,
    /// readable only by the user on Linux and macOS, and stable across restarts so MCP client configuration keeps
    /// working.
    /// </summary>
    public static class XenoMcpToken
    {
        /// <summary>
        /// Token file path.
        /// </summary>
        public static string FilePath
        {
            get { return Path.Combine(ClientSettings.SettingsDirectory, "mcp-token"); }
        }

        /// <summary>
        /// Read the token, creating it when missing.
        /// </summary>
        /// <returns>Token.</returns>
        /// <exception cref="IOException">The token file could not be read or written.</exception>
        public static string GetOrCreate()
        {
            string path = FilePath;
            if (File.Exists(path))
            {
                string existing = ClientFileIo.ReadAllText(path).Trim();
                if (existing.Length >= 32) return existing;
            }

            string token = "xmcp_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
            ClientFileIo.WriteAllTextAtomic(path, token);
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            return token;
        }

        /// <summary>
        /// Replace the token with a new one. MCP clients using the old token must be updated.
        /// </summary>
        /// <returns>New token.</returns>
        public static string Rotate()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            return GetOrCreate();
        }

        /// <summary>
        /// Compare a presented token with the stored one in constant time.
        /// </summary>
        /// <param name="presented">Presented token.</param>
        /// <param name="expected">Stored token.</param>
        /// <returns>True when they match.</returns>
        public static bool Matches(string? presented, string expected)
        {
            if (String.IsNullOrEmpty(presented) || String.IsNullOrEmpty(expected)) return false;
            byte[] left = System.Text.Encoding.UTF8.GetBytes(presented);
            byte[] right = System.Text.Encoding.UTF8.GetBytes(expected);
            return CryptographicOperations.FixedTimeEquals(left, right);
        }
    }
}
