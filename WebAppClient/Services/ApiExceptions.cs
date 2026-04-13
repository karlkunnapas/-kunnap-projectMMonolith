namespace WebAppClient.Services;

public class ApiException : Exception
{
    public int StatusCode { get; }

    public ApiException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }
}

public class ApiUnauthorizedException : ApiException
{
    public ApiUnauthorizedException(string message = "Authentication required.") : base(401, message)
    {
    }
}

public class ApiForbiddenException : ApiException
{
    public ApiForbiddenException(string message = "Access denied.") : base(403, message)
    {
    }
}

public class ApiNotFoundException : ApiException
{
    public ApiNotFoundException(string message = "Not found.") : base(404, message)
    {
    }
}

public class ApiBadRequestException : ApiException
{
    public ApiBadRequestException(string message) : base(400, message)
    {
    }
}
