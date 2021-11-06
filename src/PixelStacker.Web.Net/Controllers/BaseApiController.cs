﻿using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MtCoffee.Web.Attributes;
using PixelStacker.Web.Net.AppStart;

namespace PixelStacker.Web.Net.Controllers
{
    [RequestTimeFilter]
    [ModelStateValidationFilter]
    [JsonPayloadFilter]
    [Route("api/[controller]/[action]")]
    public class BaseApiController : Controller
    {
        //protected MySqlConnection GetConnection() => new MySqlConnection(Config.Instance.SqlConnectionString.Value);

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Do a bunch of stuff here if needed. Stuff like validation.
            base.OnActionExecuting(context);
        }

        public override void OnActionExecuted(ActionExecutedContext context)
        {
            // Make sure HTML helpers get values from MODEL and not modelstate. (Except for validation errors)
            ModelState.ToList().Select(x => x.Value).ToList().ForEach(x => { x.AttemptedValue = null; x.RawValue = null; });

            // Do a bunch of stuff here if needed. Stuff like validation.
            base.OnActionExecuted(context);
        }

        //protected async Task LogAction(string action)
        //{
        //    using var conn = this.GetConnection();
        //    await conn.ExecuteAsync("INSERT INTO `edit_history` (`action`) VALUES (@action)", new { action });
        //}
    }
}