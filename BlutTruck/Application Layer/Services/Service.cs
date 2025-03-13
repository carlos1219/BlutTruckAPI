namespace Application.Services
{
    using BlutTruck.Application_Layer.IServices;
    using BlutTruck.Data_Access_Layer.IRepositories;
    using BlutTruck.Domain_Layer.Entities;
    using BlutTruck.Transversal_Layer.IHelper;
    using System.Threading.Tasks;
    using BlutTruck.Application_Layer.Models;
    using static System.Runtime.InteropServices.JavaScript.JSType;
    using static BlutTruck.Application_Layer.Models.PersonalDataModel;
    using Firebase.Auth;
    using Google.Type;
    using static BlutTruck.Data_Access_Layer.Repositories.HealthDataRepository;

    public class HealthDataService : IHealthDataService
    {
        private readonly IHealthDataRepository _healthDataRepository;

        public HealthDataService(IHealthDataRepository healthDataRepository)
        {
            _healthDataRepository = healthDataRepository;
        }

        public async Task<string> AuthenticateAndGetTokenAsync()
        {
            return await _healthDataRepository.GetTokenAsync();
        }

        public async Task<string> VerifyUserTokenAsync(string idToken)
        {
            return await _healthDataRepository.VerifyIdTokenAsync(idToken);
        }

        public async Task SaveHealthDataAsync(string userId, HealthDataInputModel data, string idToken)
        {
            await _healthDataRepository.WriteDataAsync(userId, data, idToken);
        }

        public async Task<string> RegisterConnectionAsync(string currentUserId, string connectedUserId, string idToken)
        {
            return await _healthDataRepository.RegisterConnectionAsync(currentUserId, connectedUserId, idToken);
        }

        public async Task<string> DeleteConnectionAsync(string currentUserId, string connectedUserId, string idToken)
        {
            return await _healthDataRepository.DeleteConnectionAsync(currentUserId, connectedUserId, idToken);
        }

        public async Task<List<ConnectedUserModel>> GetConnectedUsersAsync(string currentUserId, string idToken)
        {
            return await _healthDataRepository.GetConnectedUsersAsync(currentUserId, idToken);
        }

        public async Task<object> GetHealthDataAsync(string userId, string idToken)
        {
            return await _healthDataRepository.ReadDataAsync(userId, idToken);
        }

        public async Task<HealthDataOutputModel> GetSelectDateHealthDataAsync(string userId, string dateKey, string idToken)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(dateKey))
            {
                throw new ArgumentException("El UserId y la fecha no pueden estar vacíos.");
            }

            return await _healthDataRepository.GetSelectDateHealthDataAsync(userId, dateKey, idToken);
        }
        public async Task<object> GetFullHealthDataAsync(string userId, string idToken)
        {
            return await _healthDataRepository.GetFullHealthDataAsync(userId, idToken);
        }
        public async Task<bool> SaveUserProfileAsync(string userId, string idToken, PersonalDataModel profile)
        {
            return await _healthDataRepository.SaveUserProfileAsync(userId, idToken, profile);
        }

        public async Task<bool> UpdateConnectionStatusAsync(string userId, string idToken, ConnectionModel connect)
        {
            return await _healthDataRepository.UpdateConnectionStatusAsync(userId, idToken, connect);
        }

        public async Task<object> GetPersonalAndLatestDayDataAsync(string userId, string idToken)
        {
            return await _healthDataRepository.GetPersonalAndLatestDayDataAsync(userId, idToken);
        }

        public async Task<PersonalDataModel> GetPersonalDataAsync(string userId, string idToken)
        {
            return await _healthDataRepository.GetPersonalDataAsync(userId, idToken);
        }

        public async Task<int?> GetConnectionStatusAsync(string userId, string idToken)
        {
            return await _healthDataRepository.GetConnectionStatusAsync(userId, idToken);
        }

        public async Task<string> RegisterUserAsync(string email, string password, string name)
        {
            return await _healthDataRepository.RegisterUserAsync(email, password, name);
        }

        public async Task<LoginResult> LoginUserAsync(string email, string password)
        {
            return await _healthDataRepository.LoginUserAsync(email, password);
        }
        public async Task<List<MonitorUserModel>> GetMonitoringUsersAsync(string currentUserId, string idToken)
        {
            return await _healthDataRepository.GetMonitoringUsersAsync(currentUserId, idToken);
        }

        public async Task ChangePasswordAsync(ChangePasswordRequestInputDTO input)
        {
             await _healthDataRepository.ChangePasswordAsync(input);

        }
        public async Task<byte[]> GeneratePdfAsync(string userId, string idToken)
        {
            return await _healthDataRepository.GeneratePdfAsync(userId, idToken);
        }
    }
}


