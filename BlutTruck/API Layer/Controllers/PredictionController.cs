using System.Net.Mail; // Agregar esta referencia
using System.Net; // Para credenciales y cliente de red
using System.Threading.Tasks;
using BlutTruck.Application_Layer.IServices;
using Microsoft.AspNetCore.Mvc;
using Firebase.Database;
using System.Threading.Tasks;
using Firebase.Database.Query;
using Firebase.Auth;
using Google.Api;
using Firebase.Auth.Providers;
using BlutTruck.Application_Layer.Models;

namespace BlutTruck.API_Layer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrediccionController : ControllerBase
    {
        private readonly IPrediccionService _prediccionService;
        private readonly IHealthDataService _healthDataService;

        public PrediccionController(IPrediccionService prediccionService, IHealthDataService healthDataService)
        {
            _prediccionService = prediccionService;
            _healthDataService = healthDataService;
        }

        [HttpGet("PredictHealthRisk/{userId}")]
        public async Task<IActionResult> PredictHealthRisk(string userId)
        {
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
                var personalAndLatestDayData = await _healthDataService.GetPersonalAndLatestDayDataAsync(userId, token);

                if (personalAndLatestDayData is string errorMessage)
                {
                    return BadRequest(errorMessage);
                }

                dynamic data = personalAndLatestDayData;
                var personalData = data.DatosPersonales;
                var latestDayData = data.DiaMasReciente.Datos;

                if (DateTime.TryParse(personalData.DateOfBirth, out DateTime dateOfBirth))
                {
                    int edad = DateTime.Now.Year - dateOfBirth.Year;
                    if (DateTime.Now.Month < dateOfBirth.Month ||
                        (DateTime.Now.Month == dateOfBirth.Month && DateTime.Now.Day < dateOfBirth.Day))
                    {
                        edad--;
                    }

                    var predictionRequest = MapToPredictionRequest(personalData, latestDayData, edad);
                    var predictionResult = await _prediccionService.PredecirAsync(predictionRequest);

                    if (IsHighRisk(predictionResult))
                    {
                        await SendHighRiskNotificationAsync("carlosleonarjona@gmail.com", predictionResult);
                    }

                    return Ok(new
                    {
                        Prediccion = predictionResult,
                        Fecha = data.DiaMasReciente.Fecha,
                        DatosUtilizados = predictionRequest
                    });
                }
                else
                {
                    return BadRequest("Fecha de nacimiento inválida.");
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private Dictionary<string, object> MapToPredictionRequest(dynamic personalData, dynamic latestDayData, int edad)
        {
            var predictionRequest = new Dictionary<string, object>
            {
                { "edad", edad },
                { "genero", personalData.Gender ?? 1 },
                { "altura_cm", personalData.Height },
                { "peso_kg", personalData.Weight ?? 0 },
                { "presion_sistolica", latestDayData.BloodPressureSystolic ?? 120 },
                { "presion_diastolica", latestDayData.BloodPressureDiastolic ?? 60 },
                { "colesterol", personalData.Choresterol ?? 0 },
                { "glucosa", latestDayData.BloodGlucose ?? 0 },
                { "fuma", personalData.Smoke ?? false },
                { "bebe_alcohol", personalData.Alcohol ?? false },
                { "activo", personalData.Active ?? true },
                { "enfermedad_cardiaca", personalData.HasPredisposition ?? false }
            };

            return predictionRequest;
        }

        private bool IsHighRisk(dynamic predictionResult)
        {
            if (predictionResult != null)
            {
                string truncatedResult = predictionResult.Length > 4 ? predictionResult.Substring(0, 3) : predictionResult;

                // Convertir el valor truncado a número para la comparación
                if (float.TryParse(truncatedResult, out float result) && result >= 10)
                {
                    return true;
                }
            }
            return false;
        }

        private async Task SendHighRiskNotificationAsync(string email, string predictionResult)
        {
            string truncatedResult = predictionResult.Length > 4 ? predictionResult.Substring(0, 4) : predictionResult;
            try
            {
                using (var smtpClient = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new NetworkCredential("empresa.bluttruck@gmail.com", "dgpr ajeq uakc wdbf"),
                    EnableSsl = true
                })
                {
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress("empresa.bluttruck@gmail.com", "BlutTruck Health Team"),
                        Subject = "⚠️ Alerta: Alto Riesgo de Salud Detectado",
                        Body = $@"
                <html>
                <head>
                    <style>
                        body {{
                            font-family: Arial, sans-serif;
                            line-height: 1.6;
                            background-color: #f4f4f9;
                            margin: 0;
                            padding: 0;
                        }}
                        .email-container {{
                            max-width: 600px;
                            margin: 20px auto;
                            background-color: #ffffff;
                            border-radius: 8px;
                            box-shadow: 0 2px 4px rgba(0, 0, 0, 0.1);
                            padding: 20px;
                        }}
                        .header {{
                            background-color: #007bff;
                            color: #ffffff;
                            padding: 10px 20px;
                            border-radius: 8px 8px 0 0;
                            text-align: center;
                            font-size: 24px;
                            font-weight: bold;
                        }}
                        .content {{
                            padding: 20px;
                            color: #333333;
                        }}
                        .footer {{
                            text-align: center;
                            margin-top: 20px;
                            font-size: 12px;
                            color: #777777;
                        }}
                        .btn {{
                            display: inline-block;
                            margin-top: 10px;
                            padding: 10px 20px;
                            color: #ffffff;
                            background-color: #28a745;
                            text-decoration: none;
                            border-radius: 4px;
                            font-weight: bold;
                        }}
                    </style>
                </head>
                <body>
                    <div class='email-container'>
                        <div class='header'>
                            Alerta de Salud de BlutTruck
                        </div>
                        <div class='content'>
                            <p>Estimado usuario,</p>
                            <p>Los resultados de nuestro análisis indican un <strong>riesgo alto</strong> de ataque cardíaco en su salud.</p>
                            <p>Por favor, contacte a un médico lo antes posible para discutir los siguientes detalles:</p>
                            <ul>
                                <li><strong>Riesgo Detectado:</strong> {truncatedResult}</li>
                            </ul>
                            <p>Para más información, no dude en ponerse en contacto con nuestro equipo de soporte.</p>
                            <a href='mailto:support@bluttruck.com' class='btn'>Contactar Soporte</a>
                        </div>
                        <div class='footer'>
                            Este correo es generado automáticamente por nuestro sistema. Si tiene preguntas, contáctenos en support@bluttruck.com.
                            <br />
                            © 2025 BlutTruck Health Services
                        </div>
                    </div>
                </body>
                </html>",
                        IsBodyHtml = true // Habilitar formato HTML
                    };
                    mailMessage.To.Add(email);

                    await smtpClient.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar el correo electrónico: {ex.Message}");
            }
        }
    }



    


}
