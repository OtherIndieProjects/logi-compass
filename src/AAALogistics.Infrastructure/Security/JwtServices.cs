using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using AAALogistics.Application.Abstractions;
using AAALogistics.Application.Authentication;
using AAALogistics.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AAALogistics.Infrastructure.Security;

public sealed class JwtSigningKeyProvider
{
    private const int KeyLength = 64;

    public JwtSigningKeyProvider(IOptions<JwtOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var keyBytes = LoadOrCreateKey(options.Value.SigningKeyPath);
        SigningKey = new SymmetricSecurityKey(keyBytes)
        {
            KeyId = "local-hs512-v1",
        };
    }

    public SymmetricSecurityKey SigningKey { get; }

    private static byte[] LoadOrCreateKey(string configuredPath)
    {
        var path = Path.GetFullPath(configuredPath);
        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("Jwt:SigningKeyPath no contiene un directorio válido.");

        Directory.CreateDirectory(directory);

        if (!File.Exists(path))
        {
            var generatedKey = RandomNumberGenerator.GetBytes(KeyLength);
            var streamOptions = new FileStreamOptions
            {
                Access = FileAccess.Write,
                Mode = FileMode.CreateNew,
                Share = FileShare.None,
            };

            if (!OperatingSystem.IsWindows())
            {
                streamOptions.UnixCreateMode =
                    UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            try
            {
                using var stream = new FileStream(path, streamOptions);
                stream.Write(generatedKey);
            }
            catch (IOException) when (File.Exists(path))
            {
                // Otro arranque concurrente creó la clave. Se valida debajo.
            }
            finally
            {
                CryptographicOperations.ZeroMemory(generatedKey);
            }
        }

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        var persistedKey = File.ReadAllBytes(path);
        if (persistedKey.Length < 32)
        {
            CryptographicOperations.ZeroMemory(persistedKey);
            throw new InvalidOperationException(
                "La clave JWT debe contener al menos 32 bytes.");
        }

        return persistedKey;
    }
}

public sealed class JwtTokenService(
    IOptions<JwtOptions> options,
    JwtSigningKeyProvider signingKeyProvider,
    IClock clock) : IJwtTokenService
{
    private readonly JwtOptions _options =
        options?.Value ?? throw new ArgumentNullException(nameof(options));

    private readonly JwtSigningKeyProvider _signingKeyProvider =
        signingKeyProvider ?? throw new ArgumentNullException(nameof(signingKeyProvider));

    private readonly IClock _clock =
        clock ?? throw new ArgumentNullException(nameof(clock));

    public AccessToken CreateAccessToken(
        AuthenticatedUser user,
        TokenAudience audience)
    {
        ArgumentNullException.ThrowIfNull(user);

        var now = _clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(_options.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(
                JwtClaimNames.Subject,
                user.Id.ToString("D", CultureInfo.InvariantCulture)),
            new(JwtClaimNames.UserName, user.UserName),
            new(JwtClaimNames.SecurityStamp, user.SecurityStamp),
            new(
                JwtRegisteredClaimNames.Jti,
                Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)),
        };

        if (user.CustomerId is { } customerId)
        {
            claims.Add(
                new Claim(
                    JwtClaimNames.CustomerId,
                    customerId.ToString("D", CultureInfo.InvariantCulture)));
        }

        if (user.CompanyId is { } companyId)
        {
            claims.Add(
                new Claim(
                    JwtClaimNames.CompanyId,
                    companyId.ToString("D", CultureInfo.InvariantCulture)));
        }

        claims.AddRange(
            user.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        claims.AddRange(
            user.Permissions.Select(
                permission => new Claim(
                    JwtClaimNames.Permission,
                    permission)));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: GetAudience(audience),
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(
                _signingKeyProvider.SigningKey,
                SecurityAlgorithms.HmacSha512));

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }

    public AccessToken CreateStepUpToken(Guid userId, string securityStamp)
    {
        if (string.IsNullOrWhiteSpace(securityStamp))
        {
            throw new ArgumentException(
                "El security stamp es obligatorio.",
                nameof(securityStamp));
        }

        var now = _clock.UtcNow;
        var expiresAtUtc = now.AddMinutes(
            Math.Clamp(_options.StepUpMinutes, 1, 30));
        var claims = new List<Claim>
        {
            new(
                JwtClaimNames.Subject,
                userId.ToString("D", CultureInfo.InvariantCulture)),
            new(JwtClaimNames.SecurityStamp, securityStamp),
            new(JwtClaimNames.StepUp, "1"),
            new(
                JwtRegisteredClaimNames.Jti,
                Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)),
        };

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.StepUpAudience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAtUtc.UtcDateTime,
            signingCredentials: new SigningCredentials(
                _signingKeyProvider.SigningKey,
                SecurityAlgorithms.HmacSha512));

        return new AccessToken(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAtUtc);
    }

    public bool TryValidateStepUpToken(
        string token,
        out Guid userId,
        out string securityStamp,
        out string jti,
        out DateTimeOffset expiresAtUtc)
    {
        userId = Guid.Empty;
        securityStamp = string.Empty;
        jti = string.Empty;
        expiresAtUtc = default;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var handler = new JwtSecurityTokenHandler
        {
            // Keep claim types as issued (sub/jti/…); matches ASP.NET JWT options.
            MapInboundClaims = false,
        };
        try
        {
            var principal = handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = _options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _options.StepUpAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = _signingKeyProvider.SigningKey,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha512],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = JwtClaimNames.UserName,
                },
                out var validated);

            if (principal.FindFirst(JwtClaimNames.StepUp)?.Value != "1")
            {
                return false;
            }

            var subject = principal.FindFirst(JwtClaimNames.Subject)?.Value
                ?? principal.FindFirst("sub")?.Value;
            var stamp = principal.FindFirst(JwtClaimNames.SecurityStamp)?.Value;
            string? tokenJti = null;
            DateTimeOffset tokenExpires = default;
            if (validated is JwtSecurityToken jwtToken)
            {
                tokenJti = jwtToken.Payload.Jti;
                if (string.IsNullOrWhiteSpace(tokenJti)
                    && jwtToken.Payload.TryGetValue("jti", out var jtiValue))
                {
                    tokenJti = jtiValue?.ToString();
                }

                if (jwtToken.ValidTo != default)
                {
                    tokenExpires = new DateTimeOffset(
                        DateTime.SpecifyKind(jwtToken.ValidTo, DateTimeKind.Utc));
                }
            }

            tokenJti ??= principal.FindFirst("jti")?.Value;
            if (!Guid.TryParse(subject, out userId)
                || string.IsNullOrWhiteSpace(stamp)
                || string.IsNullOrWhiteSpace(tokenJti))
            {
                return false;
            }

            securityStamp = stamp;
            jti = tokenJti;
            expiresAtUtc = tokenExpires == default
                ? _clock.UtcNow.AddMinutes(Math.Clamp(_options.StepUpMinutes, 1, 30))
                : tokenExpires;
            return true;
        }
        catch (SecurityTokenException)
        {
            return false;
        }
    }

    private string GetAudience(TokenAudience audience) =>
        audience switch
        {
            TokenAudience.Internal => _options.InternalAudience,
            TokenAudience.Portal => _options.PortalAudience,
            _ => throw new ArgumentOutOfRangeException(
                nameof(audience),
                audience,
                "Audiencia JWT desconocida."),
        };
}
