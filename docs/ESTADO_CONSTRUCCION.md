# Estado de construcción

Fecha de corte: 23 de julio de 2026.

## Implementado

- Solución modular de siete proyectos en .NET 10.
- Referencias entre capas sin dependencias desde Domain hacia infraestructura.
- Modelo inicial con 27 entidades de negocio y seguridad.
- Auditoría, borrado lógico y concurrencia optimista.
- Restricciones estructurales mediante índices para docente, grupo y espacio.
- Módulos sabatinos con validación 5 + 5 + 6.
- Estados y transiciones protegidas del horario.
- ASP.NET Core Identity, roles y alcance por carrera.
- Autenticación por cookie segura y antifalsificación.
- Inicio de sesión, consulta de sesión, cierre y cambio de contraseña.
- Interfaz WebAssembly, layout administrativo y panel inicial.
- Integración de paquete OR-Tools y verificación CP-SAT.
- Preparación reproducible de migración y base SQL Server.

## Siguiente ciclo

1. Crear CRUD de carreras, modalidades, retículas, materias y periodos.
2. Crear CRUD de docentes y espacios.
3. Capturar oferta y disponibilidad en operaciones agrupadas.
4. Autorizar cargas académicas.
5. Implementar el modelo CP-SAT completo.
