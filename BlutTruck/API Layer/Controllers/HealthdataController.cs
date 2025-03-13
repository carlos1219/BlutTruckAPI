using Microsoft.AspNetCore.Mvc;
using BlutTruck.Application_Layer.IServices;
using System.Threading.Tasks;
using BlutTruck.Application_Layer.Models;
using Application.Services;
using BlutTruck.Application_Layer.Services;
using static BlutTruck.Application_Layer.Models.PersonalDataModel;
using Firebase.Database;
using Firebase.Auth.Repository;
using Microsoft.AspNetCore.Authorization;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class WriteDataController : ControllerBase
    {
        private readonly IHealthDataService _healthDataService;

        public WriteDataController(IHealthDataService healthDataService)
        {
            _healthDataService = healthDataService;
        }

        [HttpPost("write")]
        public async Task<IActionResult> WriteData([FromBody] HealthDataInputModel inputModel)
        {
            if (inputModel == null || string.IsNullOrEmpty(inputModel.UserId))
            {
                return BadRequest(new { Message = "El cuerpo de la solicitud es inválido o falta el UserId." });
            }

            try
            {
                var token = await _healthDataService.AuthenticateAndGetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
                }

                var uid = await _healthDataService.VerifyUserTokenAsync(token);
                if (uid == null)
                {
                    return Unauthorized(new { Message = "El token generado no es válido." });
                }

                await _healthDataService.SaveHealthDataAsync(uid, inputModel, token);

                return Ok(new { Message = "Datos guardados correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Ocurrió un error: {ex.Message}" });
            }
        }

        [HttpPost("registerConnection")]
        public async Task<IActionResult> RegisterConnection([FromBody] ConnectionRequestModel request)
        {
            if (request == null)
            {
                return BadRequest(new { Message = "El cuerpo de la solicitud es inválido o falta el UserId." });
            }

            try
            {
                var token = await _healthDataService.AuthenticateAndGetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
                }

                var uid = await _healthDataService.VerifyUserTokenAsync(token);
                if (uid == null)
                {
                    return Unauthorized(new { Message = "El token generado no es válido." });
                }
                await _healthDataService.RegisterConnectionAsync(request.CurrentUserId, request.ConnectedUserId, token);

                return Ok(new { Message = "Conexión registrada exitosamente" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Ocurrió un error: {ex.Message}" });
            }
        }

        [HttpGet("getMonitoringUsers")]
        public async Task<IActionResult> GetMonitoringUsers([FromQuery] string currentUserId)
        {
            if (string.IsNullOrEmpty(currentUserId))
            {
                return BadRequest(new { Message = "El UserId es obligatorio." });
            }

            try
            {
                var token = await _healthDataService.AuthenticateAndGetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
                }

                var uid = await _healthDataService.VerifyUserTokenAsync(token);
                if (uid == null)
                {
                    return Unauthorized(new { Message = "El token generado no es válido." });
                }

                var monitoringUsers = await _healthDataService.GetMonitoringUsersAsync(currentUserId, token);

                return Ok(monitoringUsers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Ocurrió un error: {ex.Message}" });
            }
        }

        [HttpDelete("deleteConnection")]
        public async Task<IActionResult> DeleteConnection([FromBody] ConnectionRequestModel request)
        {
            if (request == null)
            {
                return BadRequest(new { Message = "El cuerpo de la solicitud es inválido o falta el UserId." });
            }

            try
            {
                var token = await _healthDataService.AuthenticateAndGetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
                }

                var uid = await _healthDataService.VerifyUserTokenAsync(token);
                if (uid == null)
                {
                    return Unauthorized(new { Message = "El token generado no es válido." });
                }
                var result = await _healthDataService.DeleteConnectionAsync(request.CurrentUserId, request.ConnectedUserId, token);
                return Ok(new { Message = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Ocurrió un error: {ex.Message}" });
            }
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Name))
            {
                return BadRequest(new { Message = "Todos los campos son obligatorios." });
            }

            try
            {
                var token = await _healthDataService.RegisterUserAsync(request.Email, request.Password, request.Name);
                return Ok(new { Token = token, Message = "Registro exitoso." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = ex.Message });
            }
        }
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new { Message = "Correo y contraseña son obligatorios." });
            }

            try
            {
                // Se asume que LoginUserAsync ahora retorna un objeto con Token y UserId
                var loginResult = await _healthDataService.LoginUserAsync(request.Email, request.Password);
                return Ok(new
                {
                    Token = loginResult.Token,
                    UserId = loginResult.UserId,
                    Message = "Inicio de sesión exitoso."
                });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { Message = ex.Message });
            }
        }

        [HttpPost("change")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestInputDTO request)
        {
            if ( string.IsNullOrEmpty(request.email))
            {
                return BadRequest(new { Message = "El token y la nueva contraseña son obligatorios." });
            }

            try
            {
                await _healthDataService.ChangePasswordAsync(request);
                return Ok(new {Message = "Contraseña actualizada exitosamente." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { Message = ex.Message });
            }
        }


        [HttpPost("save-profile/{userId}")]
        public async Task<IActionResult> SaveUserProfileAsync(
        string userId,
        [FromBody] PersonalDataModel profile)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "El UserId es requerido." });
            }

            var token = await _healthDataService.AuthenticateAndGetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
            }

            var uid = await _healthDataService.VerifyUserTokenAsync(token);
            if (uid == null)
            {
                return Unauthorized(new { Message = "El token generado no es válido." });
            }

            if (profile == null)
            {
                return BadRequest(new { Message = "El perfil no puede ser nulo." });
            }

            try
            {
                var isSaved = await _healthDataService.SaveUserProfileAsync(userId, token, profile);

                if (!isSaved)
                {
                    return StatusCode(500, new { Message = "No se pudo guardar el perfil." });
                }

                return Ok(new { Message = "Perfil guardado correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Error al guardar el perfil: {ex.Message}" });
            }
        }

        [HttpPost("update-connection/{userId}")]
        public async Task<IActionResult> UpdateConnectionStatusAsync(
        string userId,
        [FromBody] ConnectionModel connection)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "El UserId es requerido." });
            }

            if (connection == null || connection.ConnectionStatus == null)
            {
                return BadRequest(new { Message = "El estado de conexión es requerido." });
            }

            var token = await _healthDataService.AuthenticateAndGetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
            }

            var uid = await _healthDataService.VerifyUserTokenAsync(token);
            if (uid == null)
            {
                return Unauthorized(new { Message = "El token generado no es válido." });
            }

            try
            {
                var isUpdated = await _healthDataService.UpdateConnectionStatusAsync(userId, token, connection);

             

                return Ok(new { Message = "Estado de conexión actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Error al actualizar el estado de conexión: {ex.Message}" });
            }
        }


    }



    [ApiController]
    [Route("api/[controller]")]
    public class ReadDataController : ControllerBase
    {
        private readonly IHealthDataService _healthDataService;

        public ReadDataController(IHealthDataService healthDataService)
        {
            _healthDataService = healthDataService;
        }

        [HttpGet("recentday/{userId}")]
        public async Task<IActionResult> ReadData(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "El UserId es requerido." });
            }

            try
            {
                var token = await _healthDataService.AuthenticateAndGetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
                }

                var uid = await _healthDataService.VerifyUserTokenAsync(token);
                if (uid == null)
                {
                    return Unauthorized(new { Message = "El token generado no es válido." });
                }

                var data = await _healthDataService.GetHealthDataAsync(userId, token);

                if (data == null)
                {
                    return NotFound(new { Message = "No se encontraron datos para el usuario especificado." });
                }

                return Ok(data);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Ocurrió un error: {ex.Message}" });
            }
        }

        [HttpGet("selectday/{userId}/{dateKey}")]
        public async Task<IActionResult> GetSelectDateHealthDataAsync(string userId, string dateKey)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(dateKey))
            {
                return BadRequest(new { Message = "El UserId y la fecha son obligatorios." });
            }

            try
            {
                var token = await _healthDataService.AuthenticateAndGetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
                }

                var uid = await _healthDataService.VerifyUserTokenAsync(token);
                if (uid == null)
                {
                    return Unauthorized(new { Message = "El token generado no es válido." });
                }

                var healthData = await _healthDataService.GetSelectDateHealthDataAsync(userId, dateKey, token);
                if (healthData == null)
                {
                    return NotFound(new { Message = $"No se encontraron datos para la fecha {dateKey}." });
                }

                return Ok(healthData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Error al obtener los datos: {ex.Message}" });
            }
        }

        [HttpGet("full/{userId}")]
        public async Task<IActionResult> GetFullHealthDataAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "El UserId es requerido." });
            }

            var token = await _healthDataService.AuthenticateAndGetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
            }

            var uid = await _healthDataService.VerifyUserTokenAsync(token);
            if (uid == null)
            {
                return Unauthorized(new { Message = "El token generado no es válido." });
            }

            try
            {
                var healthData = await _healthDataService.GetFullHealthDataAsync(userId, token);
                if (healthData == null)
                {
                    return NotFound(new { Message = "Datos no encontrados." });
                }

                return Ok(healthData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Error al obtener los datos: {ex.Message}" });
            }
        }

        [HttpGet("connected-users/{userId}")]
        public async Task<IActionResult> GetConnectedUsers(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return BadRequest(new { Message = "El UserId es requerido." });
                }

                var token = await _healthDataService.AuthenticateAndGetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
                }

                var uid = await _healthDataService.VerifyUserTokenAsync(token);
                if (uid == null)
                {
                    return Unauthorized(new { Message = "El token generado no es válido." });
                }

                // Obtener la lista de usuarios conectados
                var connectedUsers = await _healthDataService.GetConnectedUsersAsync(userId, token);
                return Ok(connectedUsers);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Ocurrió un error: {ex.Message}" });
            }
        }

        [HttpGet("latest/{userId}")]
        public async Task<IActionResult> GetPersonalAndLatestDayDataAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "El UserId es requerido." });
            }

            var token = await _healthDataService.AuthenticateAndGetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
            }

            var uid = await _healthDataService.VerifyUserTokenAsync(token);
            if (uid == null)
            {
                return Unauthorized(new { Message = "El token generado no es válido." });
            }

            try
            {
                var healthData = await _healthDataService.GetPersonalAndLatestDayDataAsync(userId, token);
                if (healthData == null)
                {
                    return NotFound(new { Message = "Datos no encontrados." });
                }

                return Ok(healthData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Error al obtener los datos: {ex.Message}" });
            }
        }

        [HttpGet("personal/{userId}")]
        public async Task<IActionResult> GetPersonalDataAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "El UserId es requerido." });
            }

            try
            {
                // Aquí pasamos el token que previamente hayas obtenido (por ejemplo, usando un método de autenticación)
                var token = await _healthDataService.AuthenticateAndGetTokenAsync();
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
                }

                // Verificación del token
                var uid = await _healthDataService.VerifyUserTokenAsync(token);
                if (uid == null)
                {
                    return Unauthorized(new { Message = "El token generado no es válido." });
                }

                // Obtener los datos personales
                var personalData = await _healthDataService.GetPersonalDataAsync(userId, token);
                if (personalData == null)
                {
                    return NotFound(new { Message = "Datos personales no encontrados para el usuario." });
                }

                return Ok(personalData);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Error al obtener los datos: {ex.Message}" });
            }
        }
        [HttpGet("get-pdf/{userId}")]
        public async Task<IActionResult> DownloadPdf(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "El UserId es requerido." });
            }

            var token = await _healthDataService.AuthenticateAndGetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
            }

            var uid = await _healthDataService.VerifyUserTokenAsync(token);
            if (uid == null)
            {
                return Unauthorized(new { Message = "El token generado no es válido." });
            }

            var pdfBytes = await _healthDataService.GeneratePdfAsync(userId, token);
            return File(pdfBytes, "application/pdf", "datosSalud.pdf");
        }

        [HttpGet("get-connection/{userId}")]
        public async Task<IActionResult> GetConnectionStatusAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                return BadRequest(new { Message = "El UserId es requerido." });
            }

            var token = await _healthDataService.AuthenticateAndGetTokenAsync();
            if (string.IsNullOrEmpty(token))
            {
                return Unauthorized(new { Message = "No se pudo generar el token de autenticación." });
            }

            var uid = await _healthDataService.VerifyUserTokenAsync(token);
            if (uid == null)
            {
                return Unauthorized(new { Message = "El token generado no es válido." });
            }

            try
            {
                var connectionStatus = await _healthDataService.GetConnectionStatusAsync(userId, token);

                if (connectionStatus == null)
                {
                    return NotFound(new { Message = "No se encontró el estado de conexión." });
                }

                return Ok(new { ConnectionStatus = connectionStatus });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = $"Error al obtener el estado de conexión: {ex.Message}" });
            }
        }
    }
}


