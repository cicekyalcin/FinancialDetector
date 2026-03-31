using Microsoft.AspNetCore.Http;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace FinancialDetector.API.Middlewares
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        public ExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                // İstek sorunsuzsa bir sonraki adıma geçir (Normal akış)
                await _next(httpContext);
            }
            catch (Exception ex)
            {
                // Sistemde herhangi bir yerde hata patlarsa burası yakalar
                await HandleExceptionAsync(httpContext, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError; // 500 kodu

            // Front-end'e dönülecek standart hata paketi
            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Message = "Sistemde beklenmeyen bir hata oluştu. Lütfen daha sonra tekrar deneyin.",
                // Geliştirme aşamasında olduğumuz için hatanın teknik detayını da ekliyoruz. 
                // Gerçek canlı ortama (Production) çıkarken bu 'Detailed' satırı silinmelidir.
                Detailed = exception.Message
            };

            var jsonResponse = JsonSerializer.Serialize(response);
            return context.Response.WriteAsync(jsonResponse);
        }
    }
}