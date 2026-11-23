using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IGameStatsService
{
    Task<GameStatsViewModel> GetStatsAsync();
    Task<int> ResetAsync();
}
