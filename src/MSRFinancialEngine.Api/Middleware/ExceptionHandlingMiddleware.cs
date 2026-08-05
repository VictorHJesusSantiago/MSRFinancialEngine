using System.Net;
using Microsoft.AspNetCore.Mvc;
using MSRFinancialEngine.Application.Auth;
using MSRFinancialEngine.Application.Matching;
using MSRFinancialEngine.Application.Workflow;

namespace MSRFinancialEngine.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ApprovalNotAuthorizedException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.Forbidden, "Aprovação não autorizada", ex.Message);
        }
        catch (AuthenticationRequiredException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Autenticação necessária", ex.Message);
        }
        catch (InvalidCredentialsException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Credenciais inválidas", ex.Message);
        }
        catch (AccountLockedException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.Unauthorized, "Conta bloqueada", ex.Message);
        }
        catch (MatchingAlreadyRunningException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.Conflict, "Execução em andamento", ex.Message);
        }
        catch (NotSupportedException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Operação não suportada", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            await WriteProblemAsync(context, HttpStatusCode.BadRequest, "Requisição inválida", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro não tratado ao processar {Path}", context.Request.Path);
            await WriteProblemAsync(context, HttpStatusCode.InternalServerError,
                "Erro interno", "Ocorreu um erro inesperado ao processar a requisição.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, HttpStatusCode status, string title, string detail)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = (int)status;

        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = (int)status,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        });
    }
}
