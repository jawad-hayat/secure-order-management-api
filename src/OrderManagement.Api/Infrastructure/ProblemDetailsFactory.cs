using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace OrderManagement.Api.Infrastructure
{
    public static class ProblemDetailsFactory
    {
        public const string ValidationType = "https://example.com/probs/validation";

        public static ValidationProblemDetails CreateValidationProblemDetails(ModelStateDictionary modelState, HttpContext? httpContext)
        {
            var details = new ValidationProblemDetails(modelState)
            {
                Type = ValidationType,
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Detail = "See the errors property for details."
            };

            if (httpContext is not null)
            {
                details.Extensions["traceId"] = httpContext.TraceIdentifier;
            }

            return details;
        }
    }
}
