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
using BlutTruck.Application_Layer.Models.InputDTO;
using BlutTruck.Application_Layer.Models.OutputDTO;
using Newtonsoft.Json.Linq;


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

        public async Task WriteDataAsync(WriteDataInputDTO request)
        {
            var currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var firebaseClient = new FirebaseClient(
                _databaseUrl,
                new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });
            var dto = new GetConnectionStatusInputDTO
            {
                Credentials = new UserCredentials
                {
                    UserId = request.Credentials.UserId,
                    IdToken = request.Credentials.IdToken
                }
            };


            GetConnectionStatusOutputDTO status = await  GetConnectionStatusAsync(dto);
            if( 1 == status.ConnectionStatus)
            {
                await firebaseClient
              .Child("healthData")
              .Child(request.Credentials.UserId)
              .Child("dias")
              .Child(currentDate)
              .PutAsync(request.HealthData);
            }
        }

        public async Task writePredictionAsync(PredictionInputDTO request)
        {
            var currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var firebaseClient = new FirebaseClient(
                _databaseUrl,
                new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });
            var dto = new GetConnectionStatusInputDTO
            {
                Credentials = new UserCredentials
                {
                    UserId = request.Credentials.UserId,
                    IdToken = request.Credentials.IdToken
                }
            };


            GetConnectionStatusOutputDTO status = await GetConnectionStatusAsync(dto);
            if (1 == status.ConnectionStatus)
            {
                await firebaseClient
              .Child("healthData")
              .Child(request.Credentials.UserId)
              .Child("Prediccion")
              .PutAsync(request.Prediction);
            }
        }

        public async Task<ReadDataOutputDTO> ReadDataAsync(ReadDataInputDTO request)
        {
            var response = new ReadDataOutputDTO();
            string dateKey = DateTime.UtcNow.ToString("yyyy-MM-dd");

            try
            {
                // Inicializar cliente de Firebase con autenticación
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

                // Intentar obtener los datos del día actual
                var todayData = await firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("dias")
                    .Child(dateKey)
                    .OnceSingleAsync<HealthDataOutputModel>();

                if (todayData != null)
                {
                    // Si hay datos para la fecha actual, devolverlos
                    response.Success = true;
                    response.SelectedDate = dateKey;
                    response.Data = todayData;
                    return response;
                }

                // Obtener todos los días disponibles si no hay datos del día actual
                var allDaysData = await firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("dias")
                    .OnceAsync<HealthDataOutputModel>();

                if (allDaysData == null || allDaysData.Count == 0)
                {
                    response.Success = false;
                    response.ErrorMessage = "Error: No hay datos disponibles en la base de datos.";
                    return response;
                }

                // Buscar la fecha más reciente
                var latestDay = allDaysData
                    .OrderByDescending(entry => entry.Key) // Ordenar las claves por fecha descendente
                    .FirstOrDefault();

                if (latestDay == null)
                {
                    response.Success = false;
                    response.ErrorMessage = "Error: No se encontró una fecha válida en los datos.";
                    return response;
                }

                response.Success = true;
                response.SelectedDate = latestDay.Key;
                response.Data = latestDay.Object;
                return response;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error en Firebase: {ex.Message}";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error inesperado: {ex.Message}";
                return response;
            }
        }

        public async Task<HealthDataOutputModel> GetSelectDateHealthDataAsync(SelectDateHealthDataInputDTO request)
        {
            try
            {
                // Configurar el cliente de Firebase
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

                // Construir la ruta y obtener los datos
                var healthData = await firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("dias")
                    .Child(request.DateKey)
                    .OnceSingleAsync<HealthDataOutputModel>();

                if (healthData == null)
                {
                    throw new Exception($"Los datos para la fecha {request.DateKey} no existen o están mal formateados.");
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

        public async Task<FullDataOutputDTO> GetFullHealthDataAsync(UserCredentials credentials)
        {
            try
            {
                // Inicializar cliente de Firebase con autenticación
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(credentials.IdToken) });

                // Obtener datos personales
                var personalData = await firebaseClient
                    .Child("healthData")
                    .Child(credentials.UserId)
                    .Child("datos_personales")
                    .OnceSingleAsync<PersonalDataModel>();

                if (personalData == null)
                {
                    return null;
                }

                // Obtener datos de días
                var daysData = await firebaseClient
                    .Child("healthData")
                    .Child(credentials.UserId)
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

        public async Task<PdfOutputDTO> GeneratePdfAsync(PdfInputDTO request)
        {
            // Se obtiene el objeto con los datos completos.
            FullDataOutputDTO fullData = await this.GetFullHealthDataAsync(request.Credentials);

            if (fullData == null)
            {
                throw new Exception("No se encontraron datos para el usuario especificado.");
            }

            using (var ms = new MemoryStream())
            {
                Document document = new Document(PageSize.A4, 50, 50, 25, 25);
                PdfWriter writer = PdfWriter.GetInstance(document, ms);
                document.Open();

                // ------------------------
                // Sección: Encabezado
                // ------------------------

                // Define la fuente para el encabezado
                var headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16);

                // Cargar la imagen (ajusta la ruta según corresponda)
                Image logo = Image.GetInstance("C:\\Users\\Carlos\\Desktop\\C#\\BlutTruck - copia - copia\\BlutTruck\\Recursos\\logo.png");
                logo.ScaleAbsolute(50, 50);
                // Se omite asignar la alineación en la imagen, ya que se define en la celda

                // Crea el título de la app
                Paragraph appTitle = new Paragraph("BlutTruck", headerFont);

                // Crear una tabla de 2 columnas con ancho reducido y centrado en la página
                PdfPTable headerTable = new PdfPTable(2);
                headerTable.WidthPercentage = 50; // Reduce el ancho de la tabla para que se centre mejor
                headerTable.HorizontalAlignment = Element.ALIGN_LEFT;
                headerTable.SetWidths(new float[] { 1, 3 }); // Ajusta el ancho de cada columna según lo necesites

                // Celda para la imagen (centrada)
                PdfPCell cellLogo = new PdfPCell(logo)
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_CENTER,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    Padding = 0
                };
                headerTable.AddCell(cellLogo);

                // Celda para el título (con margen a la izquierda para separarlo un poco)
                PdfPCell cellTitle = new PdfPCell(appTitle)
                {
                    Border = Rectangle.NO_BORDER,
                    HorizontalAlignment = Element.ALIGN_LEFT,
                    VerticalAlignment = Element.ALIGN_MIDDLE,
                    PaddingLeft = 5,
                    Padding = 0
                };
                headerTable.AddCell(cellTitle);

                // Agregar la tabla de encabezado al documento
                document.Add(headerTable);
                // Salto de línea para separar el encabezado del contenido
                document.Add(new Paragraph(" "));

                // ------------------------
                // Sección: Datos Personales
                // ------------------------
                document.Add(new Paragraph("Datos Personales", headerFont));
                document.Add(new Paragraph($"Nombre: {fullData.DatosPersonales.Name}"));
                document.Add(new Paragraph($"Fecha de nacimiento: {fullData.DatosPersonales.DateOfBirth}"));
                document.Add(new Paragraph($"Altura: {fullData.DatosPersonales.Height}"));
                document.Add(new Paragraph($"Peso: {fullData.DatosPersonales.Weight}"));
                document.Add(new Paragraph($"Género: {fullData.DatosPersonales.Gender}"));
                document.Add(new Paragraph(" "));

                // ------------------------
                // Sección: Datos de Días
                // ------------------------
                document.Add(new Paragraph("Datos de Días", headerFont));

                foreach (var dia in fullData.Dias)
                {
                    string fecha = dia.Key;
                    HealthDataOutputModel diaData = dia.Value;

                    var subHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 14);
                    document.Add(new Paragraph($"Fecha: {fecha}", subHeaderFont));
                    document.Add(new Paragraph($"Pasos: {diaData.Steps}"));

                    if (diaData.HeartRateData != null && diaData.HeartRateData.Any())
                    {
                        document.Add(new Paragraph("Datos de Ritmo Cardíaco:"));
                        foreach (var hr in diaData.HeartRateData)
                        {
                            document.Add(new Paragraph($"Hora: {hr.Time} - BPM: {hr.BPM}"));
                        }
                    }

                    if (diaData.TemperatureData != null && diaData.TemperatureData.Any())
                    {
                        document.Add(new Paragraph("Datos de Temperatura:"));
                        foreach (var temp in diaData.TemperatureData)
                        {
                            document.Add(new Paragraph($"Hora: {temp.Time} - Temperatura: {temp.Temperature}"));
                        }
                    }

                    document.Add(new Paragraph(" ")); // Espacio entre días
                }

                document.Close();
                writer.Close();

                return new PdfOutputDTO { PdfBytes = ms.ToArray() };
            }
        }


        public async Task<SaveUserProfileOutputDTO> SaveUserProfileAsync(SaveUserProfileInputDTO request)
        {
            var response = new SaveUserProfileOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

                var profileRef = firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("datos_personales");

                await profileRef.PutAsync(request.Profile);

                response.Success = true;
                return response;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error en Firebase: {ex.Message}";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error inesperado: {ex.Message}";
                return response;
            }
        }

        public async Task<UpdateConnectionStatusOutputDTO> UpdateConnectionStatusAsync(UpdateConnectionStatusInputDTO request)
        {
            var response = new UpdateConnectionStatusOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

                var connectionRef = firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("datos_personales")
                    .Child("Conexion")
                    .Child("ConnectionStatus");

                await connectionRef.PutAsync(request.ConnectionStatus.ConnectionStatus);
                response.Success = true;
                return response;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error en Firebase: {ex.Message}";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error inesperado: {ex.Message}";
                return response;
            }
        }

        public async Task<PersonalAndLatestDayDataOutputDTO> GetPersonalAndLatestDayDataAsync(GetPersonalAndLatestDayDataInputDTO request)
        {
            var response = new PersonalAndLatestDayDataOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

                // Obtener datos personales
                var personalData = await firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("datos_personales")
                    .OnceSingleAsync<PersonalDataModel>();

                if (personalData == null)
                {
                    response.Success = false;
                    response.ErrorMessage = "Error: Los datos personales son nulos o no existen.";
                    return response;
                }

                // Obtener datos de días
                var daysData = await firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("dias")
                    .OnceAsync<HealthDataOutputModel>();

                if (daysData == null || daysData.Count == 0)
                {
                    response.Success = false;
                    response.ErrorMessage = "Error: No se encontraron datos de días.";
                    return response;
                }

                // Buscar el día más reciente
                var latestDayKey = daysData
                    .OrderByDescending(entry => entry.Key)
                    .FirstOrDefault()?.Key;

                if (string.IsNullOrEmpty(latestDayKey))
                {
                    response.Success = false;
                    response.ErrorMessage = "Error: No se encontró un día válido más reciente.";
                    return response;
                }

                var latestDayData = daysData.FirstOrDefault(entry => entry.Key == latestDayKey)?.Object;
                if (latestDayData == null)
                {
                    response.Success = false;
                    response.ErrorMessage = "Error: No se encontraron datos para el día más reciente.";
                    return response;
                }

                response.PersonalData = personalData;
                response.DiaMasReciente = new LatestDayData
                {
                    Fecha = latestDayKey,
                    Datos = latestDayData
                };
                response.Success = true;
                return response;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error en Firebase: {ex.Message}";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error inesperado: {ex.Message}";
                return response;
            }
        }

        public async Task<GetPersonalDataOutputDTO> GetPersonalDataAsync(GetPersonalDataInputDTO request)
        {
            var response = new GetPersonalDataOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

                var personalData = await firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("datos_personales")
                    .OnceSingleAsync<PersonalDataModel>();

                if (personalData == null)
                {
                    response.Success = false;
                    response.ErrorMessage = "No se encontraron datos personales para este usuario.";
                    return response;
                }
                response.PersonalData = personalData;
                response.Success = true;
                return response;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error en Firebase: {ex.Message}";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error inesperado: {ex.Message}";
                return response;
            }
        }

        public async Task<GetConnectionStatusOutputDTO> GetConnectionStatusAsync(GetConnectionStatusInputDTO request)
        {
            var response = new GetConnectionStatusOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

                var connectionRef = firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("datos_personales")
                    .Child("Conexion")
                    .Child("ConnectionStatus");

                var connectionStatus = await connectionRef.OnceSingleAsync<int?>();
                response.ConnectionStatus = connectionStatus;
                response.Success = true;
                return response;
            }
            catch (Firebase.Database.FirebaseException ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error en Firebase: {ex.Message}";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error inesperado: {ex.Message}";
                return response;
            }
        }

        public async Task<RegisterConnectionOutputDTO> RegisterConnectionAsync(RegisterConnectionInputDTO request)
        {
            var response = new RegisterConnectionOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.IdToken) });

                // Separar el ID real y el flag de admin
                var parts = request.ConnectedUserId.Split(";admin:");
                var extractedUserId = parts[0];
                var isAdmin = parts.Length > 1 && bool.TryParse(parts[1], out bool adminValue) ? adminValue : false;

                var connectionData = new Dictionary<string, string>
        {
            { "connectedAt", DateTime.UtcNow.ToString("o") },
            { "connectedUserId", extractedUserId },
            { "isAdmin", isAdmin.ToString() }
        };

                var monitorData = new Dictionary<string, string>
        {
            { "monitoredAt", DateTime.UtcNow.ToString("o") },
            { "monitoringUserId", request.CurrentUserId }
        };

                // Guardar la conexión en el usuario actual
                var pathConnection = $"healthData/{request.CurrentUserId}/conexiones/{extractedUserId}";
                await firebaseClient.Child(pathConnection).PutAsync(connectionData);

                // Guardar la referencia en el usuario conectado
                var pathMonitor = $"healthData/{extractedUserId}/monitores/{request.CurrentUserId}";
                await firebaseClient.Child(pathMonitor).PutAsync(monitorData);

                response.Success = true;
                response.Message = "Conexión registrada exitosamente y referencia inversa guardada";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error al registrar la conexión: {ex.Message}";
                return response;
            }
        }

        public async Task<RegisterConnectionOutputDTO> RegisterCodeConnectionAsync(RegisterCodeConnectionInputDTO request)
        {
            var response = new RegisterConnectionOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.IdToken) });

                var monitorData = new Dictionary<string, string>
        {
            { "Code", request.Code },
            { "UserId", request.CurrentUserId }
        };

                // Guardar la conexión en el usuario actual
                var pathConnection = $"/Codes/{request.Code}";
                await firebaseClient.Child(pathConnection).PutAsync(monitorData);

                response.Success = true;
                response.Message = "Conexión registrada exitosamente y referencia inversa guardada";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error al registrar la conexión: {ex.Message}";
                return response;
            }
        }

        public async Task<DeleteConnectionOutputDTO> DeleteCodeConnectionAsync(DeleteCodeConnectionInputDTO request)
        {
            var response = new DeleteConnectionOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.IdToken) });


                var pathConnection = $"/Codes/{request.Code}";
                await firebaseClient.Child(pathConnection).DeleteAsync();

                response.Success = true;
                response.Message = "Codigoborrado correctamente";
                return response;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error al registrar la conexión: {ex.Message}";
                return response;
            }
        }

        public async Task<GetConnectionOutputDTO> GetCodeConnectionAsync(DeleteCodeConnectionInputDTO request)
        {
            var response = new GetConnectionOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.IdToken) });

                var id = await firebaseClient
                    .Child("Codes")
                    .Child(request.Code)
                    .OnceAsync<object>();
                response.Id = id.ToArray()[1].Object.ToString();
                return response;
            }
            catch (Exception ex)
            {
                response.ErrorMessage = $"Error al leer el codigo";
                return response;
            }
        }

        public async Task<DeleteConnectionOutputDTO> DeleteConnectionAsync(DeleteConnectionInputDTO request)
        {
            var response = new DeleteConnectionOutputDTO();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

                var path = $"healthData/{request.Credentials.UserId}/conexiones/{request.ConnectedUserId}";
                await firebaseClient.Child(path).DeleteAsync();

                response.Success = true;
                response.Message = "Conexión eliminada correctamente";
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error al eliminar la conexión: {ex.Message}";
            }
            return response;
        }

        public async Task<RegisterUserOutputDTO> RegisterUserAsync(RegisterUserInputDTO request)
        {
            var response = new RegisterUserOutputDTO();
            try
            {
                var userCredential = await _authClient.CreateUserWithEmailAndPasswordAsync(request.Email, request.Password);
                var user = userCredential.User;
                response.Token = await user.GetIdTokenAsync();
                response.Success = true;
                var userId = userCredential.User.Uid;


                string token = await GetTokenAsync();
                var firebaseClient = new FirebaseClient(
                  _databaseUrl,
                  new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(token) });
                #region profile
                var profile = new PersonalDataModel
                {
                    Conexion = new PersonalDataModel.ConnectionModel
                    {
                        ConnectionStatus = 0
                    },
                    DateOfBirth = null, // Puedes asignar una fecha en formato string o dejarla en null
                    HasPredisposition = false,
                    Height = 0,
                    Weight = 0,
                    Gender = 0,
                    Smoke = 0,
                    Alcohol = 0,
                    Choresterol = 0,
                    PhotoURL = null,
                    Name = null,
                    Active = false
                };
                #endregion profile

                var profileRef = firebaseClient
                   .Child("healthData")
                   .Child(userId)
                   .Child("datos_personales");
                await profileRef.PutAsync(profile);
                #region healthData
                var healthData = new HealthDataInputModel
                {
                    UserId = null,
                    Steps = 0,
                    ActiveCalories = 0.0,
                    // Puedes inicializar la lista con un valor 0 o dejarla vacía según tus necesidades
                    HeartRates = new List<int?> { 0 },
                    // Para las colecciones de data, se puede crear un único objeto con valores por defecto
                    HeartRateData = new List<HeartRateDataPoint>
    {
        new HeartRateDataPoint { Time = DateTime.MinValue, BPM = 0 }
    },
                    RestingHeartRate = 0.0,
                    Weight = 0.0,
                    Height = 0.0,
                    BloodPressureData = new List<BloodPressureDataPoint>
    {
        new BloodPressureDataPoint { Time = DateTime.MinValue, Systolic = 0.0, Diastolic = 0.0 }
    },
                    OxygenSaturationData = new List<OxygenSaturationDataPoint>
    {
        new OxygenSaturationDataPoint { Time = DateTime.MinValue, Percentage = 0.0 }
    },
                    BloodGlucoseData = new List<BloodGlucoseDataPoint>
    {
        new BloodGlucoseDataPoint { Time = DateTime.MinValue, BloodGlucose = 0.0 }
    },
                    BodyTemperature = 0.0,
                    TemperatureData = new List<TemperatureDataPoint>
    {
        new TemperatureDataPoint { Time = DateTime.MinValue, Temperature = 0.0 }
    },
                    RespiratoryRateData = new List<RespiratoryRateDataPoint>
    {
        new RespiratoryRateDataPoint { Time = DateTime.MinValue, Rate = 0.0 }
    },
                    SleepData = new List<SleepSessionDataPoint>
    {
        new SleepSessionDataPoint
        {
            StartTime = DateTime.MinValue,
            EndTime = DateTime.MinValue,
            Stages = new List<SleepStageDataPoint>
            {
                new SleepStageDataPoint
                {
                    Type = null, // o string.Empty, si prefieres que sea cadena vacía
                    StartTime = DateTime.MinValue,
                    EndTime = DateTime.MinValue
                }
            }
        }
    }
                };
                #endregion healthData
                var currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                await firebaseClient
              .Child("healthData")
              .Child(userId)
              .Child("dias")
              .Child(currentDate)
              .PutAsync(healthData);

            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                response.Success = false;
                response.ErrorMessage = "Error al registrar el usuario: " + ex.Reason;
            }
            return response;
        }

        public async Task<LoginUserOutputDTO> LoginUserAsync(LoginUserInputDTO request)
        {
            var response = new LoginUserOutputDTO();
            try
            {
                var userCredential = await _authClient.SignInWithEmailAndPasswordAsync(request.Email, request.Password);
                var token = await userCredential.User.GetIdTokenAsync();
                var userId = userCredential.User.Uid; // Obtienes el UID del usuario
                response.Token = token;
                response.UserId = userId;
                response.Success = true;
            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                response.Success = false;
                response.ErrorMessage = "Credenciales incorrectas";
            }
            return response;
        }

        public async Task<DeleteUserOutputDTO> DeleteUserAsync(DeleteUserInputDTO request)
        {
            var response = new DeleteUserOutputDTO();
            try
            {
                // Obtén el token para autenticar la operación sobre la base de datos.
                string token = await GetTokenAsync();
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Token) }
                );
                await firebaseClient
                    .Child("healthData")
                    .Child(request.UserId)
                    .DeleteAsync();
                response.Success = true;

                #region profile
                var profile = new PersonalDataModel
                {
                    Conexion = new PersonalDataModel.ConnectionModel
                    {
                        ConnectionStatus = 0
                    },
                    DateOfBirth = null, // Puedes asignar una fecha en formato string o dejarla en null
                    HasPredisposition = false,
                    Height = 0,
                    Weight = 0,
                    Gender = 0,
                    Smoke = 0,
                    Alcohol = 0,
                    Choresterol = 0,
                    PhotoURL = null,
                    Name = null,
                    Active = false
                };
                #endregion profile

                var profileRef = firebaseClient
                   .Child("healthData")
                   .Child(request.UserId)
                   .Child("datos_personales");
                await profileRef.PutAsync(profile);
                #region healthData
                var healthData = new HealthDataInputModel
                {
                    UserId = null,
                    Steps = 0,
                    ActiveCalories = 0.0,
                    // Puedes inicializar la lista con un valor 0 o dejarla vacía según tus necesidades
                    HeartRates = new List<int?> { 0 },
                    // Para las colecciones de data, se puede crear un único objeto con valores por defecto
                    HeartRateData = new List<HeartRateDataPoint>
    {
        new HeartRateDataPoint { Time = DateTime.MinValue, BPM = 0 }
    },
                    RestingHeartRate = 0.0,
                    Weight = 0.0,
                    Height = 0.0,
                    BloodPressureData = new List<BloodPressureDataPoint>
    {
        new BloodPressureDataPoint { Time = DateTime.MinValue, Systolic = 0.0, Diastolic = 0.0 }
    },
                    OxygenSaturationData = new List<OxygenSaturationDataPoint>
    {
        new OxygenSaturationDataPoint { Time = DateTime.MinValue, Percentage = 0.0 }
    },
                    BloodGlucoseData = new List<BloodGlucoseDataPoint>
    {
        new BloodGlucoseDataPoint { Time = DateTime.MinValue, BloodGlucose = 0.0 }
    },
                    BodyTemperature = 0.0,
                    TemperatureData = new List<TemperatureDataPoint>
    {
        new TemperatureDataPoint { Time = DateTime.MinValue, Temperature = 0.0 }
    },
                    RespiratoryRateData = new List<RespiratoryRateDataPoint>
    {
        new RespiratoryRateDataPoint { Time = DateTime.MinValue, Rate = 0.0 }
    },
                    SleepData = new List<SleepSessionDataPoint>
    {
        new SleepSessionDataPoint
        {
            StartTime = DateTime.MinValue,
            EndTime = DateTime.MinValue,
            Stages = new List<SleepStageDataPoint>
            {
                new SleepStageDataPoint
                {
                    Type = null, // o string.Empty, si prefieres que sea cadena vacía
                    StartTime = DateTime.MinValue,
                    EndTime = DateTime.MinValue
                }
            }
        }
    }
                };
                #endregion healthData
                var currentDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
                await firebaseClient
              .Child("healthData")
              .Child(request.UserId)
              .Child("dias")
              .Child(currentDate)
              .PutAsync(healthData);
            }
            catch (Firebase.Auth.FirebaseAuthException ex)
            {
                response.Success = false;
                response.ErrorMessage = "Error al eliminar el usuario de la autenticación: " + ex.Reason;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = "Error al eliminar la información del usuario: " + ex.Message;
            }

            return response;
        }

        public async Task<GetMonitoringUsersOutputDTO> GetMonitoringUsersAsync(GetMonitoringUsersInputDTO request)
        {
            var response = new GetMonitoringUsersOutputDTO();
            response.MonitoringUsers = new List<MonitorUserModel>();
            try
            {
                var firebaseClient = new FirebaseClient(
                    _databaseUrl,
                    new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

                // Obtener lista de usuarios que monitorean al usuario actual
                var monitors = await firebaseClient
                    .Child("healthData")
                    .Child(request.Credentials.UserId)
                    .Child("monitores")
                    .OnceAsync<object>();

                if (monitors == null || !monitors.Any())
                {
                    response.Success = true;
                    return response;
                }

                foreach (var monitor in monitors)
                {
                    var monitoringUserId = monitor.Key;

                    // Obtener el correo electrónico del usuario monitor mediante el método refactorizado
                    var emailResponse = await GetUserEmailByIdAsync(new GetUserEmailByIdInputDTO { UserId = monitoringUserId });
                    string email = emailResponse.Email;

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

                    response.MonitoringUsers.Add(monitoringUser);
                }
                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error al obtener usuarios que monitorean: {ex.Message}";
            }
            return response;
        }

        public async Task<GetUserEmailByIdOutputDTO> GetUserEmailByIdAsync(GetUserEmailByIdInputDTO request)
        {
            var response = new GetUserEmailByIdOutputDTO();
            try
            {
                UserRecord user = await FirebaseAuth.DefaultInstance.GetUserAsync(request.UserId);
                response.Email = user.Email;
                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.ErrorMessage = $"Error al obtener email del usuario: {ex.Message}";
            }
            return response;
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

        public async Task<List<ConnectedUserModel>> GetConnectedUsersAsync(ConnectedUsersInputDTO request)
        {
            var firebaseClient = new FirebaseClient(
                _databaseUrl,
                new FirebaseOptions { AuthTokenAsyncFactory = () => Task.FromResult(request.Credentials.IdToken) });

            // Obtener lista de conexiones del usuario actual
            var connections = await firebaseClient
                .Child("healthData")
                .Child(request.Credentials.UserId)
                .Child("conexiones")
                .OnceAsync<object>();

            if (connections == null || !connections.Any())
                return new List<ConnectedUserModel>();

            var connectedUsers = new List<ConnectedUserModel>();

            foreach (var connection in connections)
            {
                var connectedUserId = connection.Key;

                // Obtener datos personales del usuario conectado
                var personalData = await firebaseClient
                    .Child("healthData")
                    .Child(connectedUserId)
                    .Child("datos_personales")
                    .OnceSingleAsync<PersonalDataModel>();

                // Usar ReadDataAsync (versión con DTO) para obtener la última fecha con datos
                var healthDataResult = await this.ReadDataAsync(
                    new ReadDataInputDTO
                    {
                        Credentials = new UserCredentials
                        {
                            UserId = connectedUserId,
                            IdToken = request.Credentials.IdToken
                        }
                    });

                // Extraer los datos de la respuesta
                string latestDay = "No days available";
                HealthDataOutputModel healthData = null;

                if (healthDataResult != null && healthDataResult.Success)
                {
                    latestDay = healthDataResult.SelectedDate;
                    healthData = healthDataResult.Data;
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
    }
}







