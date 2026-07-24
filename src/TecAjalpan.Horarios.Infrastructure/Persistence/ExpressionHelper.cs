using System.Linq.Expressions;
using TecAjalpan.Horarios.Domain.Common;

namespace TecAjalpan.Horarios.Infrastructure.Persistence;

internal static class ExpressionHelper
{
    public static LambdaExpression CrearFiltroNoEliminado(Type entityType)
    {
        var parameter = Expression.Parameter(entityType, "entidad");
        var property = Expression.Property(parameter, nameof(EntidadAuditable.Eliminado));
        var body = Expression.Equal(property, Expression.Constant(false));
        return Expression.Lambda(body, parameter);
    }
}
