using System.Security.Cryptography;
using System.Text;

namespace CodeTurtleEngine.Web.Services;

public static class JobAccess
{
    public static string Identity(HttpContext context)
    {
        var value = context.Request.Headers.Authorization.ToString();
        if (value.Length == 0) value = context.Request.Cookies["turtle-session"] ?? "";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
    public static bool Allows(HttpContext context, ReviewJobContext job) =>
        job.AccessOwner is null || job.AccessOwner == Identity(context);
}
