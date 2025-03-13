
using System.Threading.Tasks;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;

namespace BlutTruck.Data_Access_Layer.IRepositories
{
    using BlutTruck.Application_Layer.Models;
    using System.Threading.Tasks;
    using static BlutTruck.Application_Layer.Models.PersonalDataModel;
    using static BlutTruck.Data_Access_Layer.Repositories.HealthDataRepository;

    public interface IHealthDataRepository
    {
        Task<string> GetTokenAsync();
        Task<string> VerifyIdTokenAsync(string idToken);
        Task WriteDataAsync(string userId, HealthDataInputModel data, string idToken);
        Task<object> ReadDataAsync(string userId, string idToken);
        Task<HealthDataOutputModel> GetSelectDateHealthDataAsync(string userId, string dateKey, string idToken);
        Task<FullDataOutputDTO> GetFullHealthDataAsync(string userId, string idToken);
        Task<bool> SaveUserProfileAsync(string userId, string idToken, PersonalDataModel profile);
        Task<object> GetPersonalAndLatestDayDataAsync(string userId, string idToken);
        Task<PersonalDataModel> GetPersonalDataAsync(string userId, string idToken);
        Task<bool> UpdateConnectionStatusAsync(string userId, string idToken, ConnectionModel connect);
        Task<int?> GetConnectionStatusAsync(string userId, string idToken);
        Task<string> RegisterConnectionAsync(string currentUserId, string connectedUserId, string idToken);
        Task<string> DeleteConnectionAsync(string currentUserId, string connectedUserId, string idToken);
        Task<List<ConnectedUserModel>> GetConnectedUsersAsync(string currentUserId, string idToken);
        Task<string> RegisterUserAsync(string email, string password, string name);
        Task<LoginResult> LoginUserAsync(string email, string password);
        Task<List<MonitorUserModel>> GetMonitoringUsersAsync(string currentUserId, string idToken);
        Task ChangePasswordAsync(ChangePasswordRequestInputDTO input);
        Task<byte[]> GeneratePdfAsync(string userId, string idToken);
    }
}
