using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;

if (args.Length != 1)
{
    Console.WriteLine("Usage: dotnet run --project tools/PortalPasswordHash -- \"<password>\"");
    return;
}

Console.WriteLine($"PasswordHash: {new PasswordHasher<object>().HashPassword(null!, args[0])}");
Console.WriteLine($"JwtKey:       {Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))}");
