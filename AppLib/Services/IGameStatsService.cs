using Vokabeln.AppData.Enums;
using Vokabeln.ViewModels;

namespace Vokabeln.AppLib.Services;

public interface IGameStatsService
{
    Task<GameStatsViewModel> GetStatsAsync(GameType type);
    Task<int> ResetAsync();
}
