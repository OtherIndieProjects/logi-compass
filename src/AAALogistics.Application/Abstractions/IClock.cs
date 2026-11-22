namespace AAALogistics.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
