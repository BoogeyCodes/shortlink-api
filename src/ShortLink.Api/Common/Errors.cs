namespace ShortLink.Api.Common;

public sealed class NotFoundException(string message) : Exception(message);

public sealed class GoneException(string message) : Exception(message);
