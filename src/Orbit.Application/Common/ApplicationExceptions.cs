namespace Orbit.Application.Common;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class ConcurrencyException(string message) : Exception(message);

public sealed class AccessDeniedException(string message) : Exception(message);

public sealed class ConflictException(string message) : Exception(message);

/// <summary>
/// Login/refresh/session credentials could not be authenticated. Kept distinct from
/// <see cref="AccessDeniedException"/> (403, authenticated but not authorized) because this maps
/// to 401 and callers must use one enumeration-safe message regardless of the underlying reason.
/// </summary>
public sealed class AuthenticationException(string message) : Exception(message);
