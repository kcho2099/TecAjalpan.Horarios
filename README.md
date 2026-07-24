# Sistema de Horarios — Tecnológico de Ajalpan

Línea base de construcción basada en la Especificación Funcional y Diseño Técnico v2.

## Tecnología

- .NET 10 y C# 14.
- Blazor WebAssembly servido por ASP.NET Core.
- ASP.NET Core Identity con cookie segura.
- Entity Framework Core 10 y SQL Server.
- Google OR-Tools CP-SAT 9.15.

## Proyectos

| Proyecto | Responsabilidad |
|---|---|
| `TecAjalpan.Horarios.Web` | Host, API, Identity, autorización y archivos estáticos |
| `TecAjalpan.Horarios.Client` | UI WebAssembly y validación inmediata |
| `TecAjalpan.Horarios.Contracts` | DTO compartidos |
| `TecAjalpan.Horarios.Application` | Casos de uso, puertos y políticas |
| `TecAjalpan.Horarios.Domain` | Entidades, estados e invariantes |
| `TecAjalpan.Horarios.Infrastructure` | EF Core, SQL Server, Identity y auditoría |
| `TecAjalpan.Horarios.Scheduling` | Integración con OR-Tools |

## Preparación local

Requisitos:

1. .NET 10 SDK.
2. SQL Server Express, Developer o LocalDB.
3. Certificado HTTPS de desarrollo.

Desde PowerShell:

```powershell
cd TecAjalpan.Horarios
dotnet dev-certs https --trust
.\database\CrearMigracionInicial.ps1 `
  -ConnectionString "Server=.\SQLEXPRESS;Database=TecAjalpanHorarios;Trusted_Connection=True;TrustServerCertificate=True"
dotnet run --project .\src\TecAjalpan.Horarios.Web
```

La migración se genera desde el modelo EF Core incluido para conservar un `ModelSnapshot`
exacto de la versión de SDK instalada.

## Administrador inicial

La contraseña temporal no se guarda en archivos. Configurarla con User Secrets:

```powershell
dotnet user-secrets init --project .\src\TecAjalpan.Horarios.Web
dotnet user-secrets set "BootstrapAdmin:Email" "administrador@ajalpan.tecnm.mx" --project .\src\TecAjalpan.Horarios.Web
dotnet user-secrets set "BootstrapAdmin:Nombre" "Administrador del sistema" --project .\src\TecAjalpan.Horarios.Web
dotnet user-secrets set "BootstrapAdmin:ContrasenaTemporal" "CAMBIAR-POR-UNA-TEMPORAL-SEGURA" --project .\src\TecAjalpan.Horarios.Web
```

El usuario queda marcado para cambiar la contraseña en el primer acceso.

## Seguridad incluida

- Cookie `HttpOnly`, `Secure` y `SameSite=Strict`.
- Protección antifalsificación para escrituras.
- Autorización predeterminada para todos los endpoints.
- Políticas por rol.
- Preparación de alcance por carrera.
- Bloqueo por intentos fallidos.
- Rate limiting en autenticación.
- Auditoría automática y borrado lógico.
- `rowversion` para concurrencia optimista.
- Sin JWT, contraseñas ni cadenas de conexión en el cliente.

## Alcance de esta línea base

Incluye solución, modelo del dominio, DbContext, configuraciones, repositorios específicos,
Identity, roles, login, sesión, logout, antifalsificación, base visual de la SPA, preparación
de migraciones y prueba de disponibilidad de OR-Tools.

Los CRUD de catálogos, captura de disponibilidad, oferta, carga académica y generador completo
corresponden al siguiente ciclo.
