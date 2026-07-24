using Google.OrTools.Sat;

namespace TecAjalpan.Horarios.Scheduling;

public static class VerificadorCpSat
{
    public static bool MotorDisponible()
    {
        var model = new CpModel();
        var bloque = model.NewBoolVar("bloque");
        model.Add(bloque == 1);

        var solver = new CpSolver();
        var status = solver.Solve(model);
        return status is CpSolverStatus.Feasible or CpSolverStatus.Optimal;
    }
}
