using System.Collections.Generic;
using Asp.Versioning.ApiExplorer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace WebApp;

public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _descriptionProvider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider descriptionProvider)
    {
        _descriptionProvider = descriptionProvider;
    }


    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _descriptionProvider.ApiVersionDescriptions)
        {
            options.SwaggerDoc(
                description.GroupName,
                new OpenApiInfo()
                {
                    Title = $"API {description.ApiVersion}",
                    Version = description.ApiVersion.ToString(),
                    // Description = , TermsOfService = , Contact = , License = 
                }
            );
        }

        // use fqn for dto descriptions
        options.CustomSchemaIds(t => t.FullName);
        
        
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
        {
            Description = "Enter JWT token only (without 'Bearer ' prefix).",
            Name = "Authorization",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            BearerFormat = "JWT"
        });

        options.OperationFilter<AuthorizeOperationFilter>();
        options.DocumentFilter<RemoveProblemDetailsDocumentFilter>();

        
    }
}

public class RemoveProblemDetailsDocumentFilter : IDocumentFilter
{
    private static readonly string ProblemDetailsSchemaId = typeof(ProblemDetails).FullName!;

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        swaggerDoc.Components?.Schemas?.Remove(ProblemDetailsSchemaId);

        if (swaggerDoc.Paths == null)
        {
            return;
        }

        foreach (var path in swaggerDoc.Paths.Values)
        {
            if (path.Operations == null)
            {
                continue;
            }

            foreach (var operation in path.Operations.Values)
            {
                if (operation.Responses == null)
                {
                    continue;
                }

                foreach (var response in operation.Responses.Values)
                {
                    RemoveProblemDetailsContent(response);
                }
            }
        }
    }

    private static void RemoveProblemDetailsContent(IOpenApiResponse response)
    {
        if (response.Content == null || response.Content.Count == 0)
        {
            return;
        }

        var contentTypesToRemove = response.Content
            .Where(content => ReferencesProblemDetails(content.Value.Schema))
            .Select(content => content.Key)
            .ToList();

        foreach (var contentType in contentTypesToRemove)
        {
            response.Content.Remove(contentType);
        }
    }

    private static bool ReferencesProblemDetails(IOpenApiSchema? schema)
    {
        return schema is OpenApiSchemaReference schemaReference
               && schemaReference.Reference.Id == ProblemDetailsSchemaId;
    }
}
