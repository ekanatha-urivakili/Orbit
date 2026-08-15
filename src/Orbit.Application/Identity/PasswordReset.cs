using FluentValidation;
using MediatR;
using Orbit.Application.Abstractions;
using Orbit.Application.Common;
using Orbit.Domain.Common;
using Orbit.Domain.Identity;
using Orbit.Domain.Messaging;

namespace Orbit.Application.Identity;

public sealed record RequestPasswordResetCommand(string Email, string FrontendBaseUrl) : ICommand<Unit>;

public sealed class RequestPasswordResetValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetValidator()
    {
        RuleFor(command => command.Email).NotEmpty().MaximumLength(320);
        RuleFor(command => command.FrontendBaseUrl)
            .Must(BeValidFrontendBaseUrl)
            .WithMessage("Frontend base URL must be an absolute HTTP or HTTPS URL without a query or fragment.");
    }

    private static bool BeValidFrontendBaseUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && string.IsNullOrEmpty(uri.Query)
        && string.IsNullOrEmpty(uri.Fragment);
}

/// <summary>
/// Always returns success whether or not the email belongs to an account with a local credential,
/// so the API response never reveals account existence (same enumeration-resistance principle as
/// <see cref="LoginHandler"/>).
/// </summary>
public sealed class RequestPasswordResetHandler(
    IAuthenticationRepository repository,
    IOutboxRepository outbox,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<RequestPasswordResetCommand, Unit>
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(1);

    public async Task<Unit> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var normalizedEmail = TryNormalizeEmail(request.Email);
        var account = normalizedEmail is null
            ? null
            : await repository.GetUserAccountByEmailAsync(normalizedEmail, cancellationToken);
        var credential = account is null
            ? null
            : await repository.GetLocalCredentialAsync(account.Id, cancellationToken);

        if (account is null || credential is null)
        {
            return Unit.Value;
        }

        var now = timeProvider.GetUtcNow();
        await repository.RevokeActivePasswordResetTokensForUserAsync(account.Id, now, cancellationToken);

        var rawToken = RefreshTokenCodec.GenerateToken();
        var token = PasswordResetToken.Create(account.Id, RefreshTokenCodec.Hash(rawToken), now, TokenLifetime);
        await repository.AddPasswordResetTokenAsync(token, cancellationToken);

        var resetLinkBuilder = new UriBuilder(request.FrontendBaseUrl)
        {
            Fragment = $"resetToken={Uri.EscapeDataString(rawToken)}"
        };
        var resetLink = System.Net.WebUtility.HtmlEncode(resetLinkBuilder.Uri.AbsoluteUri);
        var email = OutboxEmailMessage.Create(
            account.NormalizedEmail,
            "Reset your Orbit password",
            $"""
            <p>Hi {System.Net.WebUtility.HtmlEncode(account.DisplayName)},</p>
            <p>Use the link below to reset your Orbit password. It expires in one hour and can only be used once.</p>
            <p><a href="{resetLink}">Reset your password</a></p>
            <p>If you didn't request this, you can safely ignore this email.</p>
            """,
            now);
        await outbox.AddAsync(email, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }

    private static string? TryNormalizeEmail(string email)
    {
        try
        {
            return UserAccount.NormalizeEmail(email);
        }
        catch (DomainException)
        {
            return null;
        }
    }
}

public sealed record ConfirmPasswordResetCommand(string Token, string NewPassword) : ICommand<Unit>;

public sealed class ConfirmPasswordResetValidator : AbstractValidator<ConfirmPasswordResetCommand>
{
    public ConfirmPasswordResetValidator()
    {
        RuleFor(command => command.Token).NotEmpty();
        RuleFor(command => command.NewPassword)
            .NotEmpty()
            .Length(12, 128)
            .Must(password => password.Any(char.IsLower))
            .WithMessage("Password must contain a lowercase letter.")
            .Must(password => password.Any(char.IsUpper))
            .WithMessage("Password must contain an uppercase letter.")
            .Must(password => password.Any(char.IsDigit))
            .WithMessage("Password must contain a number.");
    }
}

public sealed class ConfirmPasswordResetHandler(
    IAuthenticationRepository repository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IRequestHandler<ConfirmPasswordResetCommand, Unit>
{
    private const string InvalidTokenMessage = "The reset link is invalid or has expired.";

    public async Task<Unit> Handle(ConfirmPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshTokenCodec.Hash(request.Token);
        var token = await repository.GetPasswordResetTokenByHashAsync(tokenHash, cancellationToken)
            ?? throw new AuthenticationException(InvalidTokenMessage);

        var now = timeProvider.GetUtcNow();
        if (!token.IsUsable(now))
        {
            throw new AuthenticationException(InvalidTokenMessage);
        }

        var credential = await repository.GetLocalCredentialAsync(token.UserId, cancellationToken)
            ?? throw new AuthenticationException(InvalidTokenMessage);

        var hash = await passwordHasher.HashAsync(request.NewPassword, cancellationToken);
        credential.UpdatePassword(hash.Value, hash.Algorithm, hash.ParametersVersion, now);
        await repository.UpdateLocalCredentialAsync(credential, cancellationToken);
        token.Consume(now);

        // Resetting the password logs the account out everywhere - same revoke-all sweep as
        // RevokeOtherSessionsHandler, just unconditional since there is no "current" session here.
        var sessions = await repository.ListActiveSessionsByUserAsync(token.UserId, cancellationToken);
        foreach (var session in sessions)
        {
            session.Revoke(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
