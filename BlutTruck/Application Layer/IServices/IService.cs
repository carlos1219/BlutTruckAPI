using System.Threading.Tasks;
using BlutTruck.Application_Layer.Models;
using static BlutTruck.Application_Layer.Models.PersonalDataModel;

namespace BlutTruck.Application_Layer.IServices
{
    public interface IHealthDataService
    {
        /// <summary>
        /// Autentica al usuario y obtiene el token de autenticación.
        /// </summary>
        /// <returns>Token de autenticación como cadena.</returns>
        Task<string> AuthenticateAndGetTokenAsync();

        /// <summary>
        /// Verifica la validez de un token de usuario.
        /// </summary>
        /// <param name="idToken">El token de usuario a verificar.</param>
        /// <returns>El UID del usuario autenticado.</returns>
        Task<string> VerifyUserTokenAsync(string idToken);

        /// <summary>
        /// Guarda los datos de salud en Firebase para un usuario específico.
        /// </summary>
        /// <param name="userId">El ID del usuario.</param>
        /// <param name="data">Modelo de datos de salud a guardar.</param>
        /// <param name="idToken">Token de autenticación válido.</param>
        /// <returns>Una tarea que representa la operación asíncrona.</returns>
        Task SaveHealthDataAsync(string userId, HealthDataInputModel data, string idToken);

        /// <summary>
        /// Obtiene los datos de salud de Firebase para un usuario específico.
        /// </summary>
        /// <param name="userId">El ID del usuario.</param>
        /// <param name="idToken">Token de autenticación válido.</param>
        /// <returns>Modelo de datos de salud recuperado.</returns>
        Task<object> GetHealthDataAsync(string userId, string idToken);


        /// <summary>
        /// Obtiene los datos de salud de una fecha específica para un usuario.
        /// </summary>
        /// <param name="userId">El ID del usuario.</param>
        /// <param name="dateKey">La clave de la fecha (yyyy-MM-dd).</param>
        /// <param name="idToken">El token de autenticación.</param>
        /// <returns>Los datos de salud para la fecha especificada.</returns>
        Task<HealthDataOutputModel> GetSelectDateHealthDataAsync(string userId, string dateKey, string idToken);

        Task<object> GetFullHealthDataAsync(string userId, string idToken);
        Task<bool> SaveUserProfileAsync(string userId, string idToken, PersonalDataModel profile);
        Task<object> GetPersonalAndLatestDayDataAsync(string userId, string idToken);
        Task<PersonalDataModel> GetPersonalDataAsync(string userId, string idToken);
        Task<bool> UpdateConnectionStatusAsync(string userId, string idToken, ConnectionModel connect);
        Task<int?> GetConnectionStatusAsync(string userId, string idToken);
        Task<string> RegisterConnectionAsync(string currentUserId, string connectedUserId, string idToken);
        Task<string> DeleteConnectionAsync(string currentUserId, string connectedUserId, string idToken);
        Task<List<ConnectedUserModel>> GetConnectedUsersAsync(string currentUserId, string idToken);
        Task<string> RegisterUserAsync(string email, string password, string name);
        Task<string> LoginUserAsync(string email, string password);
        Task<List<MonitorUserModel>> GetMonitoringUsersAsync(string currentUserId, string idToken);
    }
}
