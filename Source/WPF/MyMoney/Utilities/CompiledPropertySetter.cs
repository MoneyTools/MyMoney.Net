
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Walkabout.Utilities
{

    public class CompiledPropertySetter
    {

        public static Action<S, T> CompileSetter<S, T>(string propertyName, BindingFlags flags)
        {
            ParameterExpression contextArgument = Expression.Parameter(typeof(S), "context");
            ParameterExpression valueArgument = Expression.Parameter(typeof(T), "fontSize");

            PropertyInfo pi = typeof(S).GetProperty(propertyName, flags);
            if (pi == null)
            {
                throw new ArgumentException("property not found");
            }

            MemberExpression context = Expression.Property(contextArgument, pi);
            var assignment = Expression.Assign(context, valueArgument);

            // Create a lambda expression.
            Expression<Action<S, T>> le = Expression.Lambda<Action<S, T>>(assignment, contextArgument, valueArgument);

            // Compile the lambda expression.
            return le.Compile();
        }
    }
}