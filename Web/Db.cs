// Azure SQL access: users (hashed passwords + roles) and the device registry.
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
public class Db(IConfiguration cfg)
{
    string Cs => cfg["SqlConn"] is { Length: > 0 } s ? s : throw new InvalidOperationException("SqlConn is not set");
    public static string Hash(string pw)
    {   // PBKDF2 with a random salt: passwords are never stored in plain text
        var salt = RandomNumberGenerator.GetBytes(16);
        var h = Rfc2898DeriveBytes.Pbkdf2(pw, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(h);
    }
    public static bool Verify(string pw, string stored)
    {
        var p = stored.Split(':'); var salt = Convert.FromBase64String(p[0]);
        var h = Rfc2898DeriveBytes.Pbkdf2(pw, salt, 100_000, HashAlgorithmName.SHA256, 32);
        return CryptographicOperations.FixedTimeEquals(h, Convert.FromBase64String(p[1]));
    }
    public async Task<bool> CreateUserAsync(string u, string pw, string role)
    {
        await using var c = new SqlConnection(Cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("IF NOT EXISTS(SELECT 1 FROM Users WHERE Username=@u) INSERT INTO Users(Username,PasswordHash,Role) VALUES(@u,@h,@r)", c);
        cmd.Parameters.AddWithValue("@u", u); cmd.Parameters.AddWithValue("@h", Hash(pw)); cmd.Parameters.AddWithValue("@r", role);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }
    public async Task<AppUser?> LoginAsync(string u, string pw)
    {
        await using var c = new SqlConnection(Cs); await c.OpenAsync();
        await using var cmd = new SqlCommand("SELECT PasswordHash, Role FROM Users WHERE Username=@u", c);
        cmd.Parameters.AddWithValue("@u", u);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() && Verify(pw, r.GetString(0)) ? new AppUser(u, r.GetString(1)) : null;
    }
    public async Task<List<AppUser>> UsersAsync()
    {
        var l = new List<AppUser>(); await using var c = new SqlConnection(Cs); await c.OpenAsync();
        await using var r = await new SqlCommand("SELECT Username, Role FROM Users ORDER BY Username", c).ExecuteReaderAsync();
        while (await r.ReadAsync()) l.Add(new AppUser(r.GetString(0), r.GetString(1))); return l;
    }
    public async Task<List<Device>> DevicesAsync()
    {
        var l = new List<Device>(); await using var c = new SqlConnection(Cs); await c.OpenAsync();
        await using var r = await new SqlCommand("SELECT DeviceId, Name, Status, LastCommand FROM Devices ORDER BY DeviceId", c).ExecuteReaderAsync();
        while (await r.ReadAsync()) l.Add(new Device(r.GetString(0), r.GetString(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3))); return l;
    }
}
