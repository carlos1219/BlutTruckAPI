# BlutTruck API

API REST en **C# / .NET 8** (arquitectura por capas) del TFG *BlutTruck*, un sistema de monitorización cardíaca con IA. Actúa de intermediaria entre las apps (móvil Dart y web Vue.js), Firebase y el servidor de predicción en Python.

![CodeQL](https://img.shields.io/badge/SAST-CodeQL-blue) ![Trivy](https://img.shields.io/badge/Container-Trivy-green) ![Dependabot](https://img.shields.io/badge/SCA-Dependabot-orange)

## Arquitectura

`API Layer` (controladores) → `Application Layer` (servicios, DTOs) → `Data Access Layer` (repositorios Firebase) con `Domain` y `Transversal` como apoyo.

## Seguridad (DevSecOps)

- Sin secretos en el repositorio: credenciales por variables de entorno (`.env.example`).
- Pipeline en GitHub Actions: CodeQL (SAST), gitleaks (secretos), auditoría NuGet (SCA) y Trivy sobre la imagen.
- Dependabot para NuGet, Docker y Actions.
- Imagen Docker ejecutada como usuario no root.
- Política de divulgación en [SECURITY.md](SECURITY.md).

## Ejecución

```bash
cp .env.example .env   # rellena los valores
docker build -t bluttruck-api .
docker run --env-file .env -p 3000:3000 bluttruck-api
```
