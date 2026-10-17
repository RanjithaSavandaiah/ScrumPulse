namespace ScrumPulse.Infrastructure.Security;

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using ScrumPulse.Application.Common.Interfaces;
using ScrumPulse.Application.Common.Security;

/// <summary>
/// Signs team sessions as JWTs using the shared <see cref="SessionSigningKey"/>.
/// </summary>
public sealed class JwtSessionTokenIssuer : ISessionTokenIssuer
{
    /// <summary>Issuer and audience the validation parameters in Program.cs check against.</summary>
    public const string Issuer = "scrumpulse";
    public const string Audience = "scrumpulse-app";

    private readonly SigningCredentials _signingCredentials;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();

    public JwtSessionTokenIssuer(SessionSigningKey signingKey)
    {
        _signingCredentials = new SigningCredentials(signingKey.Key, SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public string IssueToken(TeamSession session, TimeSpan lifetime)
    {
        var claims = new[]
        {
            new Claim(ScrumPulseClaimTypes.TeamId, session.TeamId.ToString()),
            new Claim(ScrumPulseClaimTypes.TeamRole, session.Role.ToString()),
            new Claim(ScrumPulseClaimTypes.DisplayName, session.DisplayName),

            // A unique token id makes individual sessions identifiable in logs without
            // logging the token itself.
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: _signingCredentials);

        return _tokenHandler.WriteToken(token);
    }
}
