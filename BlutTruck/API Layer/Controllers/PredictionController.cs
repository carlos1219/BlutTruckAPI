using System.Net.Mail; // Agregar esta referencia
using System.Net; // Para credenciales y cliente de red
using System.Threading.Tasks;
using BlutTruck.Application_Layer.IServices;
using Microsoft.AspNetCore.Mvc;
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Auth;
using Google.Api;
using Firebase.Auth.Providers;
using BlutTruck.Application_Layer.Models;
using System.Linq;
using System;
using BlutTruck.Application_Layer.Models.InputDTO;

namespace BlutTruck.API_Layer.Controllers
{
    public class PredictionRequestDTO
    {
        public string UserId { get; set; } = string.Empty;
    }

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

        [HttpPost("PredictHealthRisk")]
        public async Task<IActionResult> PredictHealthRisk([FromBody] PredictionRequestDTO requestDto)
        {
            try
            {
                string userId = requestDto.UserId;

                // Autenticación y verificación del token
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

                var request = new GetPersonalAndLatestDayDataInputDTO
                {
                    Credentials = new UserCredentials
                    {
                        UserId = userId,
                        IdToken = token
                    }
                };
                var personalAndLatestDayData = await _healthDataService.GetPersonalAndLatestDayDataAsync(request);
                if (personalAndLatestDayData == null)
                {
                    return BadRequest(personalAndLatestDayData.ErrorMessage);
                }

                PersonalDataModel personalData = personalAndLatestDayData.PersonalData;
                HealthDataOutputModel latestDayData = personalAndLatestDayData.DiaMasReciente.Datos;

                if (!DateTime.TryParse(personalData.DateOfBirth, out DateTime dateOfBirth))
                {
                    return BadRequest("Fecha de nacimiento inválida.");
                }

                int edad = DateTime.Now.Year - dateOfBirth.Year;
                if (DateTime.Now.Month < dateOfBirth.Month ||
                    (DateTime.Now.Month == dateOfBirth.Month && DateTime.Now.Day < dateOfBirth.Day))
                {
                    edad--;
                }

                // Mapear la petición para la predicción global
                var predictionRequest = MapToPredictionRequest(personalData, latestDayData, edad);
                var predictionResult = await _prediccionService.PredecirAsync(predictionRequest);

                // Definir thresholds para los parámetros (valores de ejemplo)
                const double thresholdGlucosa = 140;          // mg/dL
                const double thresholdPresionSistolica = 110;   // mmHg
                const double thresholdPresionDiastolica = 90;    // mmHg
                const double thresholdColesterol = 240;         // mg/dL

                // Obtener las últimas lecturas de glucosa y presión arterial
                var latestGlucose = latestDayData.BloodGlucoseData?.LastOrDefault();
                var latestBloodPressure = latestDayData.BloodPressureData?.LastOrDefault();

                double bloodGlucose = latestGlucose?.BloodGlucose ?? 0;
                double systolicPressure = latestBloodPressure?.Systolic ?? 120;
                double diastolicPressure = latestBloodPressure?.Diastolic ?? 60;
                double cholesterol = Convert.ToDouble(personalData.Choresterol ?? 0);

                var requestdbprediction = new PredictionInputDTO
                {
                    Credentials = new UserCredentials
                    {
                        UserId = userId,
                        IdToken = token
                    },
                    Prediction = predictionResult
                };
                await _healthDataService.writePredictionAsync(requestdbprediction);

                // Evaluar cada parámetro y acumular alertas específicas
                var riskAlerts = new List<(string parameter, double value, string message)>();

                if (bloodGlucose > thresholdGlucosa)
                {
                    riskAlerts.Add(("Glucosa", bloodGlucose, $"Nivel de glucosa elevado: {bloodGlucose} mg/dL."));
                }
                if (systolicPressure > thresholdPresionSistolica)
                {
                    riskAlerts.Add(("Presión sistólica", systolicPressure, $"Presión sistólica elevada: {systolicPressure} mmHg."));
                }
                if (diastolicPressure > thresholdPresionDiastolica)
                {
                    riskAlerts.Add(("Presión diastólica", diastolicPressure, $"Presión diastólica elevada: {diastolicPressure} mmHg."));
                }
                if (cholesterol > thresholdColesterol)
                {
                    riskAlerts.Add(("Colesterol", cholesterol, $"Nivel de colesterol elevado: {cholesterol} mg/dL."));
                }

                var request2 = new GetMonitoringUsersInputDTO
                {
                    Credentials = new UserCredentials
                    {
                        UserId = userId,
                        IdToken = token
                    }
                };
                // Obtener la lista de usuarios que deben ser notificados
                var monitoringUsers = await _healthDataService.GetMonitoringUsersAsync(request2);

                // Se envía la alerta global si el riesgo predicho es alto...
                if (IsHighRisk(predictionResult))
                {
                    foreach (var monitor in monitoringUsers.MonitoringUsers)
                    {
                        await SendHighRiskNotificationAsync(monitor.Email, predictionResult);
                    }
                }

                // ...y se envía alerta específica si hay algún parámetro fuera de rango, sin importar la predicción global
                if (riskAlerts.Any())
                {
                    // Se genera un mensaje consolidado con todos los riesgos detectados
                    string riskDetails = string.Join("<br/>", riskAlerts.Select(r => r.message));
                    foreach (var monitor in monitoringUsers.MonitoringUsers)
                    {
                        await SendConsolidatedRiskNotificationAsync(monitor.Email, riskDetails);
                    }
                }

                return Ok(new
                {
                    Prediccion = predictionResult,
                    Fecha = personalAndLatestDayData.DiaMasReciente.Fecha,
                    DatosUtilizados = predictionRequest,
                    AlertasEspecificas = riskAlerts
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message });
            }
        }

        private Dictionary<string, object> MapToPredictionRequest(PersonalDataModel personalData, HealthDataOutputModel latestDayData, int edad)
        {
            var latestBloodPressure = latestDayData.BloodPressureData?.LastOrDefault();
            var latestGlucose = latestDayData.BloodGlucoseData?.LastOrDefault();

            var predictionRequest = new Dictionary<string, object>
            {
                { "edad", edad },
                { "genero", personalData.Gender ?? 1 },
                { "altura_cm", latestDayData.Height ?? personalData.Height ?? 0 },
                { "peso_kg", latestDayData.Weight ?? personalData.Weight ?? 0 },
                { "presion_sistolica", latestBloodPressure?.Systolic ?? 120 },
                { "presion_diastolica", latestBloodPressure?.Diastolic ?? 60 },
                { "colesterol", personalData.Choresterol ?? 0 },
                { "glucosa", latestGlucose?.BloodGlucose ?? 0 },
                { "fuma", personalData.Smoke ?? 0 },
                { "bebe_alcohol", personalData.Alcohol ?? 0 },
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
                if (float.TryParse(truncatedResult, out float result) && result >= 60)
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
            <p>Los resultados de nuestro análisis indican un <strong>alto riesgo global</strong> en su salud.</p>
            <ul>
                <li><strong>Riesgo Detectado:</strong> {truncatedResult}</li>
            </ul>
            <p>Por favor, contacte a un médico lo antes posible.</p>
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
                        IsBodyHtml = true
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

        // Método para enviar un correo consolidado con las alertas específicas (parámetros fuera de rango)
        private async Task SendConsolidatedRiskNotificationAsync(string email, string riskDetails)
        {
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
                        Subject = "⚠️ Alerta: Riesgo Específico Detectado",
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
            <p>Se han detectado las siguientes anomalías en sus parámetros de salud:</p>
            <p>{riskDetails}</p>
            <p>Le recomendamos contactar a un médico o a nuestro equipo de soporte para mayor información.</p>
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
                        IsBodyHtml = true
                    };
                    mailMessage.To.Add(email);

                    await smtpClient.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al enviar la alerta específica: {ex.Message}");
            }
        }
    }
}
