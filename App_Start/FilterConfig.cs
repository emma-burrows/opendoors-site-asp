using System.Web;
using System.Web.Mvc;

namespace OpenDoors
{
  public class FilterConfig
  {
    public static void RegisterGlobalFilters(GlobalFilterCollection filters)
    {
      filters.Add(new HandleErrorAttribute());
      filters.Add(new UserAgentActionFilterAttribute());
    }
  }


  public class UserAgentActionFilterAttribute : ActionFilterAttribute
  {
    public override void OnActionExecuting(ActionExecutingContext filterContext)
    {
      if (filterContext.HttpContext.Request.UserAgent.ToLowerInvariant().Contains("ahrefsbot"))
      {
        filterContext.Result = new HttpStatusCodeResult(404);
      }

      base.OnActionExecuting(filterContext);
    }
  }
}
