using System.Reflection;
using FluentValidation;
using Freeway.Application.Common;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Freeway.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // Register MediatR handlers
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // Register FluentValidation validators
        services.AddValidatorsFromAssembly(assembly);

        // Register validation pipeline behavior
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
