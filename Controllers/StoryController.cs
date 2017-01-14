using PagedList;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OpenDoors.Code;
using OpenDoors.Models;

namespace OpenDoors.Controllers
{
  public class StoryController : Controller
  {
    private Entities db = new Entities();
    private ArchiveImporter archive;
    private ArchiveConfig config;
    private Logging logger;

    public StoryController()
    {
      logger = new Logging(Path.Combine(HttpRuntime.AppDomainAppPath + "\\Log" + DateTime.Today.ToString("-yyyy-MM-dd") + ".log"));
      archive = new ArchiveImporter(db);
      config = archive.config;
    }

    public ActionResult Index(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      TempData["type"] = "Story";
      List<Story> stories = db.Stories.Where(item => !item.Imported && !item.DoNotImport && !item.Author.DoNotImport).ToList<Story>();
      return View(stories.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult Imported(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      TempData["type"] = "Story";
      List<Story> stories = db.Stories.Where(item => item.Imported && !item.DoNotImport).ToList<Story>();
      return View("Index", stories.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult DoNotImport(int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      TempData["type"] = "Story";
      List<Story> stories = db.Stories.Where(item => item.DoNotImport || item.Author.DoNotImport).ToList<Story>();
      return View("Index", stories.OrderBy(item => item.Title).ToPagedList(page, pageSize));
    }

    public ActionResult CheckAll(String ids, int page = 1, int pageSize = 30)
    {
      TempData["config"] = config;
      TempData["type"] = "Story";
      var storyIds = ids.Split(',').Select(i => Int32.Parse(i)).ToArray();
      var result = archive.checkMany(storyIds, Request.RequestContext);

      TempData["result"] = result;
      logger.Log(Response, string.Format("{0} [{1}]: Check all for stories {2} - response: {3}",
  config.Name, Request.UserHostAddress, ids, String.Concat("\n", result.ConcatAllMessages("\n"))));

      return Redirect(ControllerContext.HttpContext.Request.UrlReferrer.ToString());
    }

    public ActionResult Details(int id = 0)
    {
      TempData["config"] = config;
      Story story = db.Stories.Find(id);
      if (story == null)
      {
        return HttpNotFound();
      }
      return View(story);
    }

    public ActionResult Edit(int id, bool imported = false, bool doNotImport = false)
    {
      Story story = db.Stories.Find(id);
      story.Imported = imported;
      story.DoNotImport = doNotImport;
      if (!story.DoNotImport && story.Author.DoNotImport) {
        story.Author.DoNotImport = false;
      }
      if (story.DoNotImport && story.Author.Stories.All(s => s.DoNotImport))
      {
        story.Author.DoNotImport = true;
      }
      if (ModelState.IsValid)
      {
        db.Entry(story).State = EntityState.Modified;
        db.Entry(story.Author).State = EntityState.Modified;
        db.SaveChanges();
      }

      logger.Log(Response, string.Format("{0} [{1}]: Set imported={2}, doNotImport={3} all for story id [{4}]",
  config.Name, Request.UserHostAddress, imported, doNotImport, id)); 

      return Redirect(ControllerContext.HttpContext.Request.UrlReferrer.ToString());
    }

    public ActionResult Import(int id, string view = "Index")
    {
      ArchiveImporter archive = new ArchiveImporter(db);
      string baseUrl = Request.Url.GetLeftPart(UriPartial.Authority) + Request.ApplicationPath + "/Chapter/Details/";
      var result = archive.importMany(new int[] { id }, Request.RequestContext);
      
      TempData["result"] = result;
      logger.Log(Response, string.Format("{0} [{1}]: Import story id [{2}] - response: {3}",
  config.Name, Request.UserHostAddress, id, String.Concat("\n", result.ConcatAllMessages("\n"))));

      if (view == "Details")
      {
        return RedirectToAction(view, new { id = id, ViewData = ViewData });
      }
      else
        return RedirectToAction(view, ViewData);
    }

    protected override void Dispose(bool disposing)
    {
      db.Dispose();
      base.Dispose(disposing);
    }

  }
}
