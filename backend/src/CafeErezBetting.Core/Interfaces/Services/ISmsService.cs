namespace CafeErezBetting.Core.Interfaces.Services;

public interface ISmsService
{
    Task SendAsync(string phone, string message);
}
