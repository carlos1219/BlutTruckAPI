using BlutTruck.Data_Access_Layer.IRepositories;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using BlutTruck.Domain_Layer.Entities;
using BlutTruck.Application_Layer.Models;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Firebase.Auth;
using Firebase.Database;
using FirebaseAdmin.Auth;
using Microsoft.Extensions.Options;
using Firebase.Auth.Providers;
using Firebase.Database.Query;
using Api.Controllers;
using static BlutTruck.Application_Layer.Models.PersonalDataModel;
using Microsoft.AspNetCore.Mvc;
using iTextSharp.text;
using iTextSharp.text.pdf;


namespace BlutTruck.Data_Access_Layer.Repositories
{


    public class HealthDataRepository : IHealthDataRepository
    {
        private const string API_KEY = "AIzaSyB03cPKHoZJ05WaIT_D-Vsmsy7bkzH4zIc"; // Tu API_KEY
        private const string AUTH_DOMAIN = "proyectocsharp-tfg.firebaseapp.com";  // Tu dominio de Firebase
        private readonly IOptions<ApiSettings> _apiSettings;
        private readonly string _databaseUrl = "https://proyectocsharp-tfg-default-rtdb.europe-west1.firebasedatabase.app/";
        private readonly FirebaseAuthClient _authClient;
        

        public HealthDataRepository(IOptions<ApiSettings> apiSettings)
        {
            _apiSettings = apiSettings ?? throw new ArgumentNullException(nameof(apiSettings));
            var config = new FirebaseAuthConfig
            {
                ApiKey = API_KEY,
                AuthDomain = AUTH_DOMAIN,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };
            _authClient = new FirebaseAuthClient(config);
        }

        public async Task<string> GetTokenAsync()
        {
            var firebaseAuthConfig = new FirebaseAuthConfig
            {
                ApiKey = API_KEY,
                AuthDomain = AUTH_DOMAIN,
                Providers = new FirebaseAuthProvider[] { new EmailProvider() }
            };

            var client = new FirebaseAuthClient(firebaseAuthConfig);
            var userCredential = await client.SignInWithEmailAndPasswordAsync(_apiSettings.Value.Email, _apiSettings.Value.Password);
            return await userCredential.User.GetIdTokenAsync();
        }

        public async Task<string> VerifyIdTokenAsync(string idToken)
        {
            var auth = FirebaseAuth.DefaultInstance;
            var decodedToken = await auth.VerifyIdTokenAsync(idToken);
            return decodedToken.Uid;
        }

        public async Task WriteDataAsync(string userId, HealthDataInputModel data, string idToken)
        {
            var currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var firebaseClient = new FirebaseClient(
                _databaseUrl,
                new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

            await firebaseClient.Child("healthData").Child(data.UserId).Child("dias").Child(currentDate).PutAsync(data);
        }

        public async Task<object> ReadDataAsync(string userId, string idToken)
        {
            string dateKey = DateTime.UtcNow.ToString("yyyy-MM-dd");

            try
            {
                // Inicializar cliente de Firebase con autenticación
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                // Intentar obtener los datos del día actual
                var todayData = await firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("dias")
                    .Child(dateKey)
                    .OnceSingleAsync<HealthDataOutputModel>();

                if (todayData != null)
                {
                    // Si hay datos para la fecha actual, devolverlos
                    return new
                    {
                        SelectedDate = dateKey,
                        Data = todayData
                    };
                }

                // Obtener todos los días disponibles si no hay datos del día actual
                var allDaysData = await firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("dias")
                    .OnceAsync<HealthDataOutputModel>();

                if (allDaysData == null || allDaysData.Count == 0)
                {
                    return "Error: No hay datos disponibles en la base de datos.";
                }

                // Buscar la fecha más reciente
                var latestDay = allDaysData
                    .OrderByDescending(entry => entry.Key) // Ordenar las claves por fecha descendente
                    .FirstOrDefault();

                if (latestDay == null)
                {
                    return "Error: No se encontró una fecha válida en los datos.";
                }

                return new
                {
                    SelectedDate = latestDay.Key,
                    Data = latestDay.Object // Los datos correspondientes a la fecha más reciente
                };
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                return $"Error en Firebase: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Error inesperado: {ex.Message}";
            }
        }

        public async Task<HealthDataOutputModel> GetSelectDateHealthDataAsync(string userId, string dateKey, string idToken)
        {
            try
            {
                // Configurar el cliente de Firebase
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                // Construir la ruta y obtener los datos
                var healthData = await firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("dias")
                    .Child(dateKey)
                    .OnceSingleAsync<HealthDataOutputModel>();

                if (healthData == null)
                {
                    throw new Exception($"Los datos para la fecha {dateKey} no existen o están mal formateados.");
                }

                return healthData;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                throw new Exception($"Error en Firebase: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error inesperado: {ex.Message}");
            }
        }

        public async Task<FullDataOutputDTO> GetFullHealthDataAsync(string userId, string idToken)
        {
            try
            {
                // Inicializar cliente de Firebase con autenticación
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                // Obtener datos personales
                var personalData = await firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("datos_personales")
                    .OnceSingleAsync<PersonalDataModel>();

                if (personalData == null)
                {
                    return null;
                }

                // Obtener datos de días
                var daysData = await firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("dias")
                    .OnceAsync<HealthDataOutputModel>();

                if (daysData == null || daysData.Count == 0)
                {
                    return null;
                }

                // Construir el objeto final utilizando el DTO
                var result = new FullDataOutputDTO
                {
                    DatosPersonales = personalData,
                    Dias = daysData.ToDictionary(entry => entry.Key, entry => entry.Object)
                };

                return result;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                // Aquí puedes registrar el error y/o lanzar la excepción según convenga
                return null;
            }
            catch (Exception ex)
            {
                // Manejo de errores inesperados
                return null;
            }
        }

        public async Task<byte[]> GeneratePdfAsync(string userId, string idToken)
        {
            // Se obtiene el objeto con los datos completos.
            FullDataOutputDTO fullData = await this.GetFullHealthDataAsync(userId, idToken);

            // Si no se obtuvieron datos, se puede manejar el error según tu lógica (por ejemplo, lanzar una excepción)
            if (fullData == null)
            {
                throw new Exception("No se encontraron datos para el usuario especificado.");
            }

            // Creamos el documento PDF en memoria
            using (var ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 50, 50, 25, 25);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                // ------------------------
                // Sección: Datos Personales
                // ------------------------
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);
                document.Add(new Paragraph("Datos Personales", headerFont));
                document.Add(new Paragraph($"Nombre: {fullData.DatosPersonales.Name}"));
                document.Add(new Paragraph($"Fecha de nacimiento: {fullData.DatosPersonales.DateOfBirth}"));
                document.Add(new Paragraph($"Altura: {fullData.DatosPersonales.Height}"));
                document.Add(new Paragraph($"Peso: {fullData.DatosPersonales.Weight}"));
                document.Add(new Paragraph($"Género: {fullData.DatosPersonales.Gender}"));
                document.Add(new Paragraph(" ")); // Línea en blanco

                // ------------------------
                // Sección: Datos de Días
                // ------------------------
                document.Add(new Paragraph("Datos de Días", headerFont));

                // Se asume que "Dias" es un diccionario donde la clave es la fecha (string)
                // y el valor es un HealthDataOutputModel
                foreach (var dia in fullData.Dias)
                {
                    string fecha = dia.Key;
                    HealthDataOutputModel diaData = dia.Value;

                    var subHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                    document.Add(new Paragraph($"Fecha: {fecha}", subHeaderFont));
                    document.Add(new Paragraph($"Pasos: {diaData.Steps}"));

                    // Datos de Ritmo Cardíaco
                    if (diaData.HeartRateData != null && diaData.HeartRateData.Any())
                    {
                        document.Add(new Paragraph("Datos de Ritmo Cardíaco:"));
                        foreach (var hr in diaData.HeartRateData)
                        {
                            // Se asume que HeartRateDataPoint tiene propiedades Time y Bpm
                            document.Add(new Paragraph($"Hora: {hr.Time} - BPM: {hr.BPM}"));
                        }
                    }

                    // Datos de Temperatura
                    if (diaData.TemperatureData != null && diaData.TemperatureData.Any())
                    {
                        document.Add(new Paragraph("Datos de Temperatura:"));
                        foreach (var temp in diaData.TemperatureData)
                        {
                            // Se asume que TemperatureDataPoint tiene propiedades Time y Temperature
                            document.Add(new Paragraph($"Hora: {temp.Time} - Temperatura: {temp.Temperature}"));
                        }
                    }

                    document.Add(new Paragraph(" ")); // Espacio entre días
                }

                document.Close();
                writer.Close();

                return ms.ToArray();
            }
        }


        public async Task<bool> SaveUserProfileAsync(string userId, string idToken, PersonalDataModel profile)
        {
            try
            {
                // Inicializar cliente de Firebase con autenticación
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                // Crear referencia al nodo de datos personales
                var profileRef = firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("datos_personales");

                // Actualizar los datos en Firebase
                await profileRef.PutAsync(profile);

                return true;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                throw new Exception($"Error en Firebase: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error inesperado: {ex.Message}");
            }
        }
        
        public async Task<bool> UpdateConnectionStatusAsync(string userId, string idToken, ConnectionModel connectionStatus)
        {

            try
            {
                // Inicializar cliente de Firebase con autenticación
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                // Crear referencia solo al nodo "Conexion.ConnectionStatus"
                var connectionRef = firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("datos_personales")
                    .Child("Conexion")
                    .Child("ConnectionStatus");

                // Actualizar el estado de conexión
                await connectionRef.PutAsync(connectionStatus.ConnectionStatus);

                return true;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                throw new Exception($"Error en Firebase: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error inesperado: {ex.Message}");
            }
        }
        
        public async Task<object> GetPersonalAndLatestDayDataAsync(string userId, string idToken)
        {
            try
            {
                // Inicializar cliente de Firebase con autenticación
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                // Obtener datos personales
                var personalData = await firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("datos_personales")
                    .OnceSingleAsync<PersonalDataModel>();

                if (personalData == null)
                {
                    return "Error: Los datos personales son nulos o no existen.";
                }

                // Obtener datos de días
                var daysData = await firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("dias")
                    .OnceAsync<HealthDataOutputModel>();

                if (daysData == null || daysData.Count == 0)
                {
                    return "Error: No se encontraron datos de días.";
                }

                // Buscar el día más reciente
                var latestDayKey = daysData
                    .OrderByDescending(entry => entry.Key)
                    .FirstOrDefault()?.Key;

                if (string.IsNullOrEmpty(latestDayKey))
                {
                    return "Error: No se encontró un día válido más reciente.";
                }

                // Obtener los datos del día más reciente
                var latestDayData = daysData.FirstOrDefault(entry => entry.Key == latestDayKey)?.Object;

                if (latestDayData == null)
                {
                    return "Error: No se encontraron datos para el día más reciente.";
                }

                // Construir el objeto final
                var result = new
                {
                    DatosPersonales = personalData,
                    DiaMasReciente = new
                    {
                        Fecha = latestDayKey,
                        Datos = latestDayData
                    }
                };

                return result;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                return $"Error en Firebase: {ex.Message}";
            }
            catch (Exception ex)
            {
                return $"Error inesperado: {ex.Message}";
            }
        }

        public async Task<PersonalDataModel> GetPersonalDataAsync(string userId, string idToken)
        {
            try
            {
                // Inicializar cliente de Firebase con autenticación
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                // Obtener datos personales
                var personalData = await firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("datos_personales")
                    .OnceSingleAsync<PersonalDataModel>();

                if (personalData == null)
                {
                    throw new Exception("No se encontraron datos personales para este usuario.");
                }

                return personalData;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                throw new Exception($"Error en Firebase: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error inesperado: {ex.Message}");
            }
        }

        public async Task<int?> GetConnectionStatusAsync(string userId, string idToken)
        {
            try
            {
                // Inicializar cliente de Firebase con autenticación
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                // Crear referencia al nodo "Conexion.ConnectionStatus"
                var connectionRef = firebaseClient
                    .Child("healthData")
                    .Child(userId)
                    .Child("datos_personales")
                    .Child("Conexion")
                    .Child("ConnectionStatus");

                // Obtener el estado de conexión desde Firebase
                var connectionStatus = await connectionRef.OnceSingleAsync<int?>();

                return connectionStatus;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                throw new Exception($"Error en Firebase: {ex.Message}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Error inesperado: {ex.Message}");
            }
        }

        public async Task<string> RegisterConnectionAsync(string currentUserId, string connectedUserId, string idToken)
        {
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                // Separar el ID del usuario y el flag de admin
                var parts = connectedUserId.Split(";admin:");
                var extractedUserId = parts[0];  // El ID real del usuario
                var isAdmin = parts.Length > 1 && bool.TryParse(parts[1], out bool adminValue) ? adminValue : false;  // Extraer true/false

                var connectionData = new Dictionary<string, string>
        {
            { "connectedAt", DateTime.UtcNow.ToString("o") },
            { "connectedUserId", extractedUserId },
            { "isAdmin", isAdmin.ToString() } // Guardamos el flag como string
        };

                var monitorData = new Dictionary<string, string>
        {
            { "monitoredAt", DateTime.UtcNow.ToString("o") },
            { "monitoringUserId", currentUserId }
        };

                // Guardar la conexión en el usuario actual
                var pathConnection = $"healthData/{currentUserId}/conexiones/{extractedUserId}";
                await firebaseClient.Child(pathConnection).PutAsync(connectionData);

                // Guardar la referencia en el usuario conectado
                var pathMonitor = $"healthData/{extractedUserId}/monitores/{currentUserId}";
                await firebaseClient.Child(pathMonitor).PutAsync(monitorData);

                return "Conexión registrada exitosamente y referencia inversa guardada";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al registrar la conexión: {ex.Message}");
            }
        }

        public async Task<string> DeleteConnectionAsync(string currentUserId, string connectedUserId, string idToken)
        {
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

                var path = $"healthData/{currentUserId}/conexiones/{connectedUserId}";
                await firebaseClient.Child(path).DeleteAsync();

                return "Conexión eliminada correctamente";
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al eliminar la conexión: {ex.Message}");
            }
        }
        
        public async Task<List<ConnectedUserModel>> GetConnectedUsersAsync(string currentUserId, string idToken)
        {
            var firebaseClient = new FirebaseClient(
                _databaseUrl,
                new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

            // Obtener lista de conexiones del usuario actual
            var connections = await firebaseClient
                .Child("healthData")
                .Child(currentUserId)
                .Child("conexiones")
                .OnceAsync<object>();

            if (connections == null || !connections.Any())
                return new List<ConnectedUserModel>();

            var connectedUsers = new List<ConnectedUserModel>();

            foreach (var connection in connections)
            {
                var connectedUserId = connection.Key;

                // Obtener datos personales
                var personalData = await firebaseClient
                    .Child("healthData")
                    .Child(connectedUserId)
                    .Child("datos_personales")
                    .OnceSingleAsync<PersonalDataModel>();

                // **Usar ReadDataAsync para obtener la última fecha con datos**
                var healthDataResult = await this.ReadDataAsync(connectedUserId, idToken);

                // Extraer los datos de la respuesta
                string latestDay = "No days available";
                HealthDataOutputModel healthData = null;

                if (healthDataResult is not string && healthDataResult is not null)
                {
                    latestDay = ((dynamic)healthDataResult).SelectedDate;
                    healthData = ((dynamic)healthDataResult).Data;
                }

                var connectedUser = new ConnectedUserModel
                {
                    ConnectedUserId = connectedUserId,
                    Name = personalData?.Name ?? "No Name",
                    PhotoURL = personalData?.PhotoURL ?? "No photo",
                    LatestDay = latestDay,
                    MaxHeartRate = healthData?.MaxHeartRate?.ToString() ?? "N/A",
                    MinHeartRate = healthData?.MinHeartRate?.ToString() ?? "N/A",
                    AvgHeartRate = healthData?.AvgHeartRate?.ToString() ?? "N/A"
                };


                connectedUsers.Add(connectedUser);
            }

            return connectedUsers;
        }

        public async Task ChangePasswordAsync(ChangePasswordRequestInputDTO input)
        {
            try
            {
                await _authClient.ResetEmailPasswordAsync(input.email);
            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                throw new Exception("Error al cambiar la contraseña: " + ex.Reason);
            }
        }

        public async Task<string> RegisterUserAsync(string email, string password, string name)
        {
            try
            {
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(email, password);
                var user = userCredential.User;
                return await user.GetIdTokenAsync(); // Devuelve solo el token seguro
            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                throw new Exception("Error al registrar el usuario: " + ex.Reason);
            }
        }

        public async Task<LoginResult> LoginUserAsync(string email, string password)
        {
            try
            {
                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(email, password);
                var token = await userCredential.User.GetIdTokenAsync();
                var userId = userCredential.User.Uid; // Obtienes el UID del usuario
                return new LoginResult
                {
                    Token = token,
                    UserId = userId
                };
            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                throw new Exception("Credenciales incorrectas");
            }
        }
        


        public async Task<List<MonitorUserModel>> GetMonitoringUsersAsync(string currentUserId, string idToken)
        {
            var firebaseClient = new FirebaseClient(
                _databaseUrl,
                new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(idToken) });

            // Obtener lista de usuarios que monitorean al usuario actual
            var monitors = await firebaseClient
                .Child("healthData")
                .Child(currentUserId)
                .Child("monitores")
                .OnceAsync<object>();

            if (monitors == null || !monitors.Any())
                return new List<MonitorUserModel>();

            var monitoringUsers = new List<MonitorUserModel>();

            foreach (var monitor in monitors)
            {
                var monitoringUserId = monitor.Key;

                // Obtener el correo electrónico del usuario monitor
                string email = await GetUserEmailByIdAsync(monitoringUserId);

                // Obtener datos personales del usuario monitor
                var personalData = await firebaseClient
                    .Child("healthData")
                    .Child(monitoringUserId)
                    .Child("datos_personales")
                    .OnceSingleAsync<PersonalDataModel>();

                var monitoringUser = new MonitorUserModel
                {
                    MonitoringUserId = monitoringUserId,
                    Name = personalData?.Name ?? "No Name",
                    PhotoURL = personalData?.PhotoURL ?? "No photo",
                    Email = email
                };

                monitoringUsers.Add(monitoringUser);
            }

            return monitoringUsers;
        }

        public async Task<string> GetUserEmailByIdAsync(string userId)
        {
            UserRecord user = await FirebaseAuth.DefaultInstance.GetUserAsync(userId);
            return user.Email;
        }

    }
}







